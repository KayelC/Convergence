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
    RecalculateResources,
    ValidateStartupSnapshot,
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
    int ActiveSkillCount,
    int PassiveSkillCount,
    bool ResourceRecalculationApplied,
    bool StartupSnapshotValidated,
    int StartupSnapshotDiagnosticCount,
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
        GrowthRulesetServices growthServices = new RuntimeRulesetBindingResolver()
            .BindGrowthServices(catalog, TrainingAnnexHostSupport.Qualified("standard_growth"))
            .RequireService();
        var commands = new List<CleanTrainingAnnexPlayCommand>();
        bool resourceRecalculationApplied = false;
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

        while (true)
        {
            HostCommandReadResult<CleanTrainingAnnexPlayCommand> result =
                await _commandSource.ReadAsync(CreateMenu(), cancellationToken).ConfigureAwait(false);
            if (!result.IsSelected || result.Command == CleanTrainingAnnexPlayCommand.Exit)
            {
                commands.Add(CleanTrainingAnnexPlayCommand.Exit);
                LastSummary = CreateSummary(
                    request,
                    roster,
                    resourceRecalculationApplied,
                    snapshotValidated,
                    snapshotDiagnosticCount,
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
                    await PrintSessionAsync(catalog, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.InspectActor:
                    await PrintActorsAsync(roster, cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.RecalculateResources:
                    resourceRecalculationApplied = await RecalculatePlayerResourcesAsync(
                        roster.Player,
                        growthServices.ResourceGrowthPolicy,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot:
                    RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(
                        TrainingAnnexHostSupport.BuildStartupSaveSnapshot(roster),
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
                            resourceRecalculationApplied,
                            snapshotValidated,
                            snapshotDiagnosticCount,
                            commands);
                        return 5;
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Training Annex command '{command}'.");
            }
        }
    }

    private static HostCommandRequest<CleanTrainingAnnexPlayCommand> CreateMenu() =>
        new(
            "Training Annex Clean Session",
            [
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.InspectSession,
                    "Inspect Session"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.InspectActor,
                    "Inspect Actors"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.RecalculateResources,
                    "Recalculate Resources"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot,
                    "Validate Startup Snapshot"),
                new HostCommandOption<CleanTrainingAnnexPlayCommand>(
                    CleanTrainingAnnexPlayCommand.Exit,
                    "Exit")
            ]);

    private ValueTask PrintSessionAsync(GameDataCatalog catalog, CancellationToken cancellationToken) =>
        _eventSink.PublishAsync(
            $"Session: {TrainingAnnexHostSupport.PackId}; {catalog.Entities.Count} entities, {catalog.Skills.Count} skills, {catalog.Items.Count} items, {catalog.Encounters.Count} encounters, {catalog.Dungeons.Count} dungeons.",
            cancellationToken);

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
        string stats = string.Join(
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
        await _eventSink.PublishAsync($"Stats: {stats}.", cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync($"Active skills: {activeSkills}.", cancellationToken).ConfigureAwait(false);
        await _eventSink.PublishAsync(
            $"Passive skills: {(string.IsNullOrWhiteSpace(passiveSkills) ? "none" : passiveSkills)}.",
            cancellationToken).ConfigureAwait(false);
    }

    private static CleanTrainingAnnexPlaySummary CreateSummary(
        ContentPackTextRequest request,
        TrainingAnnexActorRoster roster,
        bool resourceRecalculationApplied,
        bool snapshotValidated,
        int snapshotDiagnosticCount,
        IReadOnlyList<CleanTrainingAnnexPlayCommand> commands)
    {
        CatalogBattleActor player = roster.Player.Actor;
        return new(
            [request.ManifestPath],
            request.DocumentPaths,
            player.Entity.Id,
            roster.Player.Level,
            roster.AllActors.Count,
            roster.Enemies.Count,
            roster.AllActors.Select(actor => actor.Actor.Entity.Id).ToArray(),
            roster.AllActors.Select(actor => ContentId.Parse(actor.RuntimeState.InstanceId.ToString())).ToArray(),
            roster.Player.RuntimeState.ToSnapshot().Resources,
            player.ActiveSkills.Count,
            player.SkillLoadout.Count(skill => skill.Activation == SkillActivation.Passive),
            resourceRecalculationApplied,
            snapshotValidated,
            snapshotDiagnosticCount,
            commands.ToArray());
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
