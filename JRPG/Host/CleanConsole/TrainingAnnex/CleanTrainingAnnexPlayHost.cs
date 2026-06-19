using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using JRPGPrototype.Services;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal enum CleanTrainingAnnexPlayCommand
{
    InspectSession,
    InspectActor,
    ResolveStats,
    RecalculateResources,
    ApplyVictoryExperience,
    ValidateStartupSnapshot,
    EnterTrainingAnnex,
    ReturnToStagingArea,
    Exit
}

internal sealed record CleanTrainingAnnexPlaySummary(
    IReadOnlyList<string> RequestedManifestPaths,
    IReadOnlyList<string> RequestedDocumentPaths,
    ContentId PlayerEntityId,
    int PlayerLevel,
    int ActorCount,
    int EnemyActorCount,
    IReadOnlyList<ContentId> ActorEntityIds,
    IReadOnlyList<ContentId> ActorInstanceIds,
    IReadOnlyList<RuntimeResourceSnapshot> PlayerResources,
    RuntimeProgressionSnapshot PlayerProgression,
    IReadOnlyList<StatResolutionResult> PlayerResolvedStats,
    int ActiveSkillCount,
    int PassiveSkillCount,
    bool StatResolutionPreviewed,
    bool ResourceRecalculationApplied,
    bool GrowthApplied,
    int LevelUpCount,
    bool StartupSnapshotValidated,
    int StartupSnapshotDiagnosticCount,
    ContentId FinalLocationId,
    IReadOnlyList<ContentId> LocationHistory,
    IReadOnlyList<CleanTrainingAnnexPlayCommand> Commands);

internal sealed class CleanTrainingAnnexPlayHost
{
    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _eventSink;
    private readonly IHostCommandSource<CleanTrainingAnnexPlayCommand> _commandSource;

    public CleanTrainingAnnexPlayHost(IGameIO io, string? contentRoot = null)
        : this(
            new FileContentPackSource(contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Data", "Jsons")),
            new GameIoEventSink(io),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io))
    {
    }

    internal CleanTrainingAnnexPlayHost(
        IContentPackTextSource contentSource,
        IHostEventSink<string> eventSink,
        IHostCommandSource<CleanTrainingAnnexPlayCommand> commandSource)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        _commandSource = commandSource ?? throw new ArgumentNullException(nameof(commandSource));
    }

    internal CleanTrainingAnnexPlaySummary? LastSummary { get; private set; }

    public int Run() => RunAsync().GetAwaiter().GetResult();

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        LastSummary = null;
        ContentPackTextRequest request = TrainingAnnexHostSupport.CreateContentRequest();
        ContentPackTextBundle bundle;
        try
        {
            bundle = await _contentSource.ReadAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _eventSink.PublishAsync(
                $"Content read failed for {request.ManifestPath}: {exception.Message}",
                cancellationToken).ConfigureAwait(false);
            return 2;
        }

        CatalogLoadResult load = new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(TrainingAnnexHostSupport.BuildRegistrations(), [bundle]));
        if (!load.IsSuccess || load.Catalog is null)
        {
            foreach (CatalogLoadDiagnostic diagnostic in load.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.SourceName} {diagnostic.JsonPath}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return 3;
        }

        GameDataCatalog catalog = load.Catalog;
        TrainingAnnexActorRosterResult rosterResult = TrainingAnnexHostSupport.CreateActorRoster(catalog);
        if (!rosterResult.IsSuccess)
        {
            foreach (string diagnostic in rosterResult.Diagnostics)
            {
                await _eventSink.PublishAsync(diagnostic, cancellationToken).ConfigureAwait(false);
            }

            return 4;
        }

        TrainingAnnexActorRoster roster = rosterResult.RequireRoster();
        CatalogBattleActor player = roster.Player.Actor;
        var rulesetResolver = new RuntimeRulesetBindingResolver();
        GrowthRulesetServices growthServices = rulesetResolver
            .BindGrowthServices(catalog, TrainingAnnexHostSupport.Qualified("standard_growth"))
            .RequireService();
        IStatResolutionPolicy statPolicy = rulesetResolver
            .BindStatResolutionPolicy(catalog, TrainingAnnexHostSupport.Qualified("standard_stat"))
            .RequireService();
        var navigation = new RuntimeNavigationService(new TrainingAnnexNavigationPolicy());
        RuntimeFieldSnapshot field = new(
            new RuntimeNavigationSnapshot(TrainingAnnexHostSupport.StagingArea));
        var locationHistory = new List<ContentId> { field.Navigation.CurrentLocationId };
        var commands = new List<CleanTrainingAnnexPlayCommand>();
        IReadOnlyList<StatResolutionResult> statPreview = [];
        bool statResolutionPreviewed = false;
        bool resourceRecalculationApplied = false;
        bool growthApplied = false;
        int levelUpCount = 0;
        bool snapshotValidated = false;
        int snapshotDiagnosticCount = -1;

        await _eventSink.PublishAsync("Clean Training Annex session booted.", cancellationToken)
            .ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Loaded {TrainingAnnexHostSupport.PackId} without legacy Database startup.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Hydrated {player.Entity.DisplayName} at level {player.Entity.BaseLevel}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Hydrated clean actor roster with {roster.AllActors.Count} actor(s): {roster.Enemies.Count} enemy model(s).",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync("Field location: Staging Area.", cancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> result =
                await _commandSource.ReadAsync(
                    CreateMenu(field.Navigation.CurrentLocationId),
                    cancellationToken).ConfigureAwait(false);
            if (!result.IsSelected || result.Command == CleanTrainingAnnexPlayCommand.Exit)
            {
                commands.Add(CleanTrainingAnnexPlayCommand.Exit);
                LastSummary = CreateSummary(
                    request,
                    roster,
                    statPreview,
                    statResolutionPreviewed,
                    resourceRecalculationApplied,
                    growthApplied,
                    levelUpCount,
                    snapshotValidated,
                    snapshotDiagnosticCount,
                    field,
                    locationHistory,
                    commands);
                await _eventSink.PublishAsync("Clean Training Annex session exited.", cancellationToken)
                    .ConfigureAwait(false);
                return 0;
            }

            CleanTrainingAnnexPlayCommand command = result.Command;
            commands.Add(command);
            switch (command)
            {
                case CleanTrainingAnnexPlayCommand.InspectSession:
                    await PrintSessionAsync(catalog, field, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectActor:
                    await PrintActorsAsync(roster, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ResolveStats:
                    statPreview = await ResolvePlayerStatsAsync(
                        roster.Player,
                        statPolicy,
                        cancellationToken).ConfigureAwait(false);
                    statResolutionPreviewed = true;
                    break;
                case CleanTrainingAnnexPlayCommand.RecalculateResources:
                    resourceRecalculationApplied = await RecalculatePlayerResourcesAsync(
                        roster.Player,
                        growthServices.ResourceGrowthPolicy,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ApplyVictoryExperience:
                    LevelGrowthResult growth = await ApplyVictoryExperienceAsync(
                        roster.Player,
                        growthServices,
                        cancellationToken).ConfigureAwait(false);
                    growthApplied = growth.Applied;
                    levelUpCount = growth.LevelUps.Count;
                    break;
                case CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot:
                    RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
                        TrainingAnnexHostSupport.BuildStartupSaveSnapshot(roster, field),
                        catalog);
                    snapshotValidated = validation.IsValid;
                    snapshotDiagnosticCount = validation.Diagnostics.Count;
                    await _eventSink.PublishAsync(
                        $"Startup snapshot validation: {validation.Diagnostics.Count} diagnostic(s).",
                        cancellationToken).ConfigureAwait(false);
                    if (!validation.IsValid)
                    {
                        foreach (RuntimeSaveValidationDiagnostic diagnostic in validation.Diagnostics)
                        {
                            await _eventSink.PublishAsync(
                                $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                                cancellationToken).ConfigureAwait(false);
                        }

                        LastSummary = CreateSummary(
                            request,
                            roster,
                            statPreview,
                            statResolutionPreviewed,
                            resourceRecalculationApplied,
                            growthApplied,
                            levelUpCount,
                            snapshotValidated,
                            snapshotDiagnosticCount,
                            field,
                            locationHistory,
                            commands);
                        return 5;
                    }
                    break;
                case CleanTrainingAnnexPlayCommand.EnterTrainingAnnex:
                {
                    RuntimeNavigationResult navigationResult = navigation.Navigate(
                        field.Navigation,
                        TrainingAnnexHostSupport.EnterTrainingAnnexTransition);
                    field = await ApplyNavigationAsync(
                        field,
                        navigationResult,
                        "entered Training Annex",
                        cancellationToken).ConfigureAwait(false);
                    if (navigationResult.Applied)
                    {
                        locationHistory.Add(field.Navigation.CurrentLocationId);
                    }
                    break;
                }
                case CleanTrainingAnnexPlayCommand.ReturnToStagingArea:
                {
                    RuntimeNavigationResult navigationResult = navigation.Navigate(
                        field.Navigation,
                        TrainingAnnexHostSupport.LeaveTrainingAnnexTransition);
                    field = await ApplyNavigationAsync(
                        field,
                        navigationResult,
                        "returned to Staging Area",
                        cancellationToken).ConfigureAwait(false);
                    if (navigationResult.Applied)
                    {
                        locationHistory.Add(field.Navigation.CurrentLocationId);
                    }
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unknown Training Annex command '{command}'.");
            }
        }
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateMenu(
        ContentId locationId)
    {
        var options = new List<HostCommandOption<CleanTrainingAnnexPlayCommand>>
        {
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.InspectSession,
                    "Inspect Session"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.InspectActor,
                    "Inspect Actors"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ResolveStats,
                    "Resolve Stats"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.RecalculateResources,
                    "Recalculate Resources"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ApplyVictoryExperience,
                    "Apply Victory EXP"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot,
                    "Validate Startup Snapshot")
        };

        options.Add(locationId == TrainingAnnexHostSupport.StagingArea
            ? new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.EnterTrainingAnnex,
                "Enter Training Annex")
            : new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                CleanTrainingAnnexPlayCommand.ReturnToStagingArea,
                "Return to Staging Area"));
        options.Add(new HostCommandOption<CleanTrainingAnnexPlayCommand>(
            CleanTrainingAnnexPlayCommand.Exit,
            "Exit"));

        string locationLabel = FieldLabel(locationId);
        return new HostCommandRequest<CleanTrainingAnnexPlayCommand>(
            $"Training Annex Clean Session - {locationLabel}",
            options);
    }

    private ValueTask PrintSessionAsync(
        GameDataCatalog catalog,
        RuntimeFieldSnapshot field,
        CancellationToken cancellationToken) =>
        _eventSink.PublishAsync(
            $"Session: {TrainingAnnexHostSupport.PackId}; {catalog.Entities.Count} entities, {catalog.Skills.Count} skills, {catalog.Items.Count} items, {catalog.Encounters.Count} encounters, {catalog.Dungeons.Count} dungeons. Location: {FieldLabel(field.Navigation.CurrentLocationId)} ({field.Navigation.CurrentLocationId}); dungeon state: {(field.DungeonProgress is null ? "not active" : field.DungeonProgress.DungeonId.ToString())}.",
            cancellationToken);

    private async ValueTask<RuntimeFieldSnapshot> ApplyNavigationAsync(
        RuntimeFieldSnapshot field,
        RuntimeNavigationResult navigation,
        string appliedDescription,
        CancellationToken cancellationToken)
    {
        if (!navigation.Applied)
        {
            await _eventSink.PublishAsync(
                $"Field navigation rejected: {navigation.Message}",
                cancellationToken).ConfigureAwait(false);
            return field;
        }

        await _eventSink.PublishAsync(
            $"Field navigation: {appliedDescription}; location {FieldLabel(navigation.After.CurrentLocationId)} ({navigation.After.CurrentLocationId}).",
            cancellationToken).ConfigureAwait(false);
        return new RuntimeFieldSnapshot(navigation.After, field.DungeonProgress);
    }

    private static string FieldLabel(ContentId locationId) =>
        locationId == TrainingAnnexHostSupport.StagingArea
            ? "Staging Area"
            : locationId == TrainingAnnexHostSupport.TrainingAnnexEntrance
                ? "Training Annex Entrance"
                : locationId.ToString();

    private async ValueTask PrintActorsAsync(TrainingAnnexActorRoster roster, CancellationToken cancellationToken)
    {
        await _eventSink.PublishAsync(
            $"Actor roster: {roster.AllActors.Count} actor(s).",
            cancellationToken).ConfigureAwait(false);
        foreach (TrainingAnnexRuntimeActor actor in roster.AllActors)
        {
            await PrintActorAsync(actor, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask PrintActorAsync(TrainingAnnexRuntimeActor runtimeActor, CancellationToken cancellationToken)
    {
        CatalogBattleActor actor = runtimeActor.Actor;
        RuntimeActorSnapshot snapshot = runtimeActor.RuntimeState.ToSnapshot();
        string resources = string.Join(
            ", ",
            snapshot.Resources.Select(resource => $"{resource.ResourceId} {resource.Current}/{resource.Maximum}"));
        string baseStats = string.Join(
            ", ",
            snapshot.Stats.BaseStats.Select(pair => $"{pair.Key} {pair.Value}"));
        string effectiveStats = string.Join(
            ", ",
            snapshot.Stats.EffectiveStats.Select(pair => $"{pair.Key} {pair.Value}"));
        string activeSkills = string.Join(", ", actor.ActiveSkills.Select(skill => skill.DisplayName));
        string passiveSkills = string.Join(
            ", ",
            actor.SkillLoadout
                .Where(skill => skill.Activation == SkillActivation.Passive)
                .Select(skill => skill.DisplayName));

        await _eventSink.PublishAsync(
            $"{runtimeActor.Role}: {actor.Entity.DisplayName}; instance {snapshot.Identity.InstanceId}; level {runtimeActor.Level}; resources: {resources}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync($"Base stats: {baseStats}.", cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync($"Effective stats: {effectiveStats}.", cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync($"Active skills: {activeSkills}.", cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Passive skills: {(string.IsNullOrWhiteSpace(passiveSkills) ? "none" : passiveSkills)}.",
            cancellationToken).ConfigureAwait(false);
    }

    private static CleanTrainingAnnexPlaySummary CreateSummary(
        ContentPackTextRequest request,
        TrainingAnnexActorRoster roster,
        IReadOnlyList<StatResolutionResult> statPreview,
        bool statResolutionPreviewed,
        bool resourceRecalculationApplied,
        bool growthApplied,
        int levelUpCount,
        bool snapshotValidated,
        int snapshotDiagnosticCount,
        RuntimeFieldSnapshot field,
        IReadOnlyList<ContentId> locationHistory,
        IReadOnlyList<CleanTrainingAnnexPlayCommand> commands)
    {
        CatalogBattleActor player = roster.Player.Actor;
        RuntimeActorSnapshot playerSnapshot = roster.Player.RuntimeState.ToSnapshot();
        return new(
            [request.ManifestPath],
            request.DocumentPaths,
            player.Entity.Id,
            roster.Player.Level,
            roster.AllActors.Count,
            roster.Enemies.Count,
            roster.AllActors.Select(actor => actor.Actor.Entity.Id).ToArray(),
            roster.AllActors.Select(actor => ContentId.Parse(actor.RuntimeState.InstanceId.ToString())).ToArray(),
            playerSnapshot.Resources,
            playerSnapshot.Progression,
            statPreview.ToArray(),
            player.ActiveSkills.Count,
            player.SkillLoadout.Count(skill => skill.Activation == SkillActivation.Passive),
            statResolutionPreviewed,
            resourceRecalculationApplied,
            growthApplied,
            levelUpCount,
            snapshotValidated,
            snapshotDiagnosticCount,
            field.Navigation.CurrentLocationId,
            locationHistory.ToArray(),
            commands.ToArray());
    }

    private async ValueTask<IReadOnlyList<StatResolutionResult>> ResolvePlayerStatsAsync(
        TrainingAnnexRuntimeActor player,
        IStatResolutionPolicy statPolicy,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot snapshot = player.RuntimeState.ToSnapshot();
        RuntimeStatStageSnapshot attackStage = new(StandardProgressionIds.Attack, 1);
        IEnumerable<KeyValuePair<ContentId, decimal>> activeFormStats =
            snapshot.Identity.ActorKindId == StandardProgressionIds.Demon
                ? snapshot.Stats.BaseStats
                : [];
        var results = new List<StatResolutionResult>();
        var messages = new List<string>();

        foreach (ContentId statId in StandardProgressionIds.CoreStats)
        {
            StatResolutionResult unmodified = statPolicy.Resolve(new StatResolutionRequest(
                snapshot.Identity.ActorKindId,
                statId,
                snapshot.Stats.BaseStats,
                activeFormStats));
            StatResolutionResult boosted = statPolicy.Resolve(new StatResolutionRequest(
                snapshot.Identity.ActorKindId,
                statId,
                snapshot.Stats.BaseStats,
                activeFormStats,
                statStages: [attackStage]));
            results.Add(boosted);
            messages.Add($"{statId} {unmodified.FinalValue}->{boosted.FinalValue}");
        }

        await _eventSink.PublishAsync(
            $"Stat policy: standard_stat resolved {player.Actor.Entity.DisplayName} with attack stage +1.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Resolved stats: {string.Join(", ", messages)}.",
            cancellationToken).ConfigureAwait(false);

        return results.ToArray();
    }

    private async ValueTask<LevelGrowthResult> ApplyVictoryExperienceAsync(
        TrainingAnnexRuntimeActor player,
        GrowthRulesetServices growthServices,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot before = player.RuntimeState.ToSnapshot();
        long requiredExperience = growthServices.ExperienceCurve.GetRequiredExperience(before.Progression.Level);
        long award = Math.Max(0, requiredExperience - before.Progression.Experience);
        LevelGrowthResult growth = growthServices.LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
            before.Progression,
            before.Stats,
            before.Identity.ActorKindId,
            award,
            new TrainingAnnexMinimumRandomSource(),
            resources: before.Resources,
            baseResourceValues: before.BaseResourceValues));
        RuntimeMutationResult mutation = new RuntimeProgressionTransactionService().ApplyLevelGrowth(
            player.RuntimeState,
            growth);
        if (!mutation.Applied)
        {
            foreach (RuntimeMutationDiagnostic diagnostic in mutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return growth;
        }

        RuntimeActorSnapshot after = mutation.After;
        await _eventSink.PublishAsync(
            $"Victory EXP: awarded {award} EXP through standard_growth.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Growth result: {player.Actor.Entity.DisplayName} level {before.Progression.Level}->{after.Progression.Level}; exp {before.Progression.Experience}->{after.Progression.Experience}; lifetime {before.Progression.LifetimeExperience}->{after.Progression.LifetimeExperience}; stat points {before.Progression.UnspentStatPoints}->{after.Progression.UnspentStatPoints}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Level-up events: {(growth.LevelUps.Count == 0 ? "none" : string.Join(", ", growth.LevelUps.Select(levelUp => levelUp.Level.ToString())))}.",
            cancellationToken).ConfigureAwait(false);

        return growth;
    }

    private async ValueTask<bool> RecalculatePlayerResourcesAsync(
        TrainingAnnexRuntimeActor player,
        IResourceGrowthPolicy resourceGrowthPolicy,
        CancellationToken cancellationToken)
    {
        RuntimeActorSnapshot before = player.RuntimeState.ToSnapshot();
        RuntimeResourceSnapshot beforeHp = before.Resources.Single(resource =>
            resource.ResourceId == TrainingAnnexHostSupport.Hp);
        RuntimeMutationResult mutation = new RuntimeResourceTransactionService().AddResource(
            player.RuntimeState,
            TrainingAnnexHostSupport.Hp,
            -10);
        if (!mutation.Applied)
        {
            foreach (RuntimeMutationDiagnostic diagnostic in mutation.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.Path}: {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        ResourceRecalculationResult recalculated = resourceGrowthPolicy.Recalculate(
            new ResourceRecalculationRequest(
                mutation.After.Resources,
                mutation.After.BaseResourceValues,
                mutation.After.Stats.EffectiveStats,
                ResourceCurrentAdjustmentMode.PreserveCurrent));
        RuntimeResourceSnapshot afterHp = recalculated.GetRequired(TrainingAnnexHostSupport.Hp);
        await _eventSink.PublishAsync(
            $"Resource recalculation: {player.Actor.Entity.DisplayName} hp {beforeHp.Current}/{beforeHp.Maximum} -> {afterHp.Current}/{afterHp.Maximum}.",
            cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Resource policy: standard_growth preserved current hp and recalculated maximum {afterHp.Maximum}.",
            cancellationToken).ConfigureAwait(false);
        return true;
    }
}
