using Convergence.Content;
using Convergence.Catalog;
using Convergence.DemoHost.TrainingAnnex;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Fusion;
using Convergence.Runtime;

using static Convergence.DemoHost.TrainingAnnex.TrainingAnnexHostSupport;

namespace Convergence.DemoHost;

internal sealed record CleanTrainingAnnexDemoSummary(
    IReadOnlyList<string> RequestedManifestPaths,
    IReadOnlyList<string> RequestedDocumentPaths,
    IReadOnlyList<RuntimeDungeonTraversalEventKind> DungeonEventKinds,
    ContentId EncounterId,
    ContentId EnemyEntityId,
    int InventoryRemaining,
    BattleActionExecutionStatus ItemStatus,
    ItemConsumptionDecision ItemConsumption,
    bool ItemConsumptionCommitted,
    AutomatedBattleOutcome BattleOutcome,
    ContentId? WinningTeamId,
    int RewardExperience,
    int RewardCredits,
    long ExperienceAfter,
    long LifetimeExperienceAfter,
    int LevelAfter,
    bool SaveValid,
    int SaveDiagnosticCount);

internal sealed class CleanTrainingAnnexDemoHost
{
    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _eventSink;

    public CleanTrainingAnnexDemoHost(TextWriter output, string? contentRoot = null)
        : this(
            new FileContentPackSource(contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Content")),
            new TextWriterEventSink(output))
    {
    }

    internal CleanTrainingAnnexDemoHost(
        IContentPackTextSource contentSource,
        IHostEventSink<string> eventSink)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    internal CleanTrainingAnnexDemoSummary? LastSummary { get; private set; }

    public int Run() => RunAsync().GetAwaiter().GetResult();

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        LastSummary = null;
        ContentPackTextRequest request = CreateContentRequest();
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
                cancellationToken)
                .ConfigureAwait(false);
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

        int sequence = 1;
        GameDataCatalog catalog = load.Catalog;
        await PrintAsync(sequence++, "catalog", "Loaded Training Annex slice.", cancellationToken)
            .ConfigureAwait(false);
        await PrintAsync(
            sequence++,
            "catalog",
            $"Sample counts: {catalog.Races.Count} races, {catalog.Entities.Count} entities, {catalog.Skills.Count} skills, {catalog.Items.Count} items, {catalog.Encounters.Count} encounters.",
            cancellationToken)
            .ConfigureAwait(false);

        var resolver = new RuntimeRulesetBindingResolver(
            RuntimeRulesetPolicyFactoryRegistry.CreateStandard());
        var random = new TrainingAnnexMinimumRandomSource();
        StatRulesetServices statServices = resolver
            .BindStatServices(catalog, Qualified("standard_stat"))
            .RequireService();
        IStatModifierPolicyService statModifiers = resolver
            .BindStatModifierPolicy(catalog, Qualified("standard_stat_modifiers"))
            .RequireService();
        CombatExecutionPolicySet combatPolicies = resolver.BindCombatPolicies(
            catalog,
            Qualified("standard_damage"),
            random,
            statServices.StageScalingPolicy).RequireService();
        IBattleRewardService rewardService = resolver.BindBattleRewardService(
            catalog,
            Qualified("standard_reward"),
            random).RequireService();
        GrowthRulesetServices growthServices = resolver.BindGrowthServices(
            catalog,
            Qualified("standard_growth")).RequireService();
        IStatResolutionPolicy statPolicy = statServices.StatResolutionPolicy;
        BattleTurnEconomyRuleset turnEconomy = resolver.BindTurnEconomy(
            catalog,
            Qualified("standard_action_token")).RequireService();
        IRosterCapacityPolicy rosterCapacityPolicy = resolver
            .BindRosterCapacityPolicy(catalog, Qualified("standard_roster_capacity"))
            .RequireService();
        resolver.BindResourceManagementServices(catalog, Qualified("standard_economy")).RequireService();
        await PrintAsync(sequence++, "ruleset", "Bound standard Training Annex rulesets.", cancellationToken)
            .ConfigureAwait(false);

        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new TrainingAnnexResourceInitializationPolicy(growthServices.ResourceGrowthPolicy));
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
        RuntimePartyRosterSnapshot partyRoster = new TrainingAnnexPartyController()
            .CreateInitialParty(roster)
            .Snapshot;
        CatalogBattleActor echo = roster.Player.Actor;
        await PrintAsync(sequence++, "actor", $"Hydrated {echo.Entity.DisplayName}.", cancellationToken)
            .ConfigureAwait(false);

        DungeonDefinition dungeon = catalog.GetRequiredDungeon(Qualified("training_annex"));
        var dungeonService = new RuntimeDungeonTraversalService(new TrainingAnnexDungeonPolicy());
        var dungeonStart = new RuntimeDungeonTraversalSnapshot(
            dungeon.Id,
            TrainingAnnexEntrance);
        RuntimeDungeonTraversalResult traversed = dungeonService.Traverse(
            dungeonStart,
            EnterReviewHallTransition);
        IReadOnlyList<RuntimeDungeonTraversalEvent> dungeonEvents = traversed.Events;
        foreach (RuntimeDungeonTraversalEvent dungeonEvent in dungeonEvents)
        {
            await PrintAsync(
                sequence++,
                "dungeon",
                $"{dungeonEvent.Kind}: {dungeonEvent.SourceNodeId} -> {dungeonEvent.DestinationNodeId}.",
                cancellationToken)
                .ConfigureAwait(false);
        }

        RuntimeInstanceId hostTriggerId = RuntimeInstanceId.Parse("annex_scene_trigger");
        EncounterStartPlanResult encounterStart = new CatalogEncounterStartPlanner(catalog).Plan(
            new EncounterStartRequest(
                Qualified("ashling_drill"),
                EnemyTeam,
                ContentId.Parse("training_annex_ai"),
                hostTriggerId));
        if (!encounterStart.IsSuccess)
        {
            await PrintEncounterDiagnosticsAsync(encounterStart.Diagnostics, cancellationToken).ConfigureAwait(false);
            return 4;
        }

        EncounterStartPlan encounterPlan = encounterStart.RequirePlan();
        EncounterDefinition encounter = encounterPlan.Encounter;
        await PrintAsync(
            sequence++,
            "encounter",
            $"Host trigger {hostTriggerId} selected {encounter.DisplayName}.",
            cancellationToken).ConfigureAwait(false);
        CatalogBattleActorCreationRequest ashlingRequest = AssertSingleActorRequest(encounterPlan);
        CatalogBattleActorCreationResult ashlingResult = actorFactory.Create(ashlingRequest);
        if (!ashlingResult.IsSuccess)
        {
            await PrintActorDiagnosticsAsync(ashlingResult.Diagnostics, cancellationToken).ConfigureAwait(false);
            return 4;
        }

        CatalogBattleActor ashling = ashlingResult.RequireActor();
        await PrintAsync(sequence++, "encounter", $"Resolved {encounter.DisplayName}: {ashling.Entity.DisplayName}.", cancellationToken)
            .ConfigureAwait(false);

        BattleExecutionServices executionServices =
            TrainingAnnexHostSupport.CreateExecutionServices(catalog, combatPolicies, statModifiers);
        var actionExecutor = new BattleActionExecutor(
            new SkillExecutor(executionServices),
            new ItemExecutor(executionServices),
            executionServices,
            new CatalogBattleActionAuthorizationPolicy(
                catalog,
                catalog,
                NoBattleBasicAttackProfileSource.Instance));
        Dictionary<ContentId, int> inventory = new() { [Qualified("annex_tonic")] = 1 };
        echo.State.AddResource(Hp, -20);
        decimal damagedHp = echo.State.GetRequiredResource(Hp).Current;
        ItemDefinition tonic = catalog.GetRequiredItem(Qualified("annex_tonic"));
        BattleActionExecutionResult itemUse = await actionExecutor.ExecuteAsync(
            new BattleActionExecutionRequest(
                new ItemBattleActionCommand(tonic, [echo.State.InstanceId]),
                echo.State,
                [echo.State, ashling.State],
                new EffectExecutionEnvironment(TrainingAnnexHostSupport.Battle, NormalBattle),
                new DemoItemActionInventory(inventory)),
            cancellationToken).ConfigureAwait(false);
        await PrintAsync(
            sequence++,
            "item",
            $"{tonic.DisplayName}: HP {damagedHp} -> {echo.State.GetRequiredResource(Hp).Current}; consume={itemUse.ItemConsumption}; remaining={inventory.GetValueOrDefault(tonic.Id)}.",
            cancellationToken).ConfigureAwait(false);

        var skillExecutor = new SkillExecutor(executionServices);
        var lifecycle = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(random),
            executionServices,
            ContentId.Parse("battle_start"),
            ContentId.Parse("owner_turn_end"),
            new ExplicitBattleEncounterLifecycleClockPolicy(
                [
                    new BattleTeamPhaseClockDefinition(
                        PlayerTeam,
                        ContentId.Parse("player_phase"),
                        ContentId.Parse("player_phase_end")),
                    new BattleTeamPhaseClockDefinition(
                        EnemyTeam,
                        ContentId.Parse("enemy_phase"),
                        ContentId.Parse("enemy_phase_end"))
                ],
                ContentId.Parse("round_end")));
        AutomatedBattleResult battle = await new AutomatedBattleRunner(
                skillExecutor,
                new DeterministicBattleActionSelector(skillExecutor),
                lifecycle,
                turnEconomy,
                new AutomatedBattleTurnRestrictionResolver(),
                new BattleEncounterProgressPolicy(4096))
            .RunAsync(
                new AutomatedBattleRequest(
                    [echo, ashling],
                    TrainingAnnexHostSupport.Battle,
                    NormalBattle,
                    null,
                    10),
                cancellationToken);
        foreach (BattleEncounterEvent battleEvent in battle.Events)
        {
            await _eventSink.PublishAsync(
                $"{sequence++:D3} [battle] {battleEvent.Kind}: " +
                $"{battleEvent.DebugText ?? battleEvent.Kind.ToString()}",
                cancellationToken).ConfigureAwait(false);
        }

        await PrintAsync(
            sequence++,
            "battle",
            battle.WinningTeamId is ContentId winner
                ? $"Outcome {battle.Outcome}; winner {winner}."
                : $"Outcome {battle.Outcome}.",
            cancellationToken).ConfigureAwait(false);
        if (battle.Outcome == AutomatedBattleOutcome.Faulted)
        {
            return 5;
        }

        BattleRewardResult reward = rewardService.Calculate(new BattleRewardRequest(
            [EnemyRewardSnapshot(ashling.Entity, ashlingRequest.Level)],
            [new BattleRewardRecipientSnapshot(echo.Entity.Id, IsAlive: !echo.State.IsDefeated, HasActiveHostedEntity: true)]));
        RuntimeActorReferenceSnapshot activeReference = partyRoster.ActiveHostedEntity ??
            throw new InvalidOperationException(
                "Training Annex demo requires an active Hosted Entity.");
        TrainingAnnexRuntimeActor growthActor = roster.AllActors.Single(actor =>
            actor.Actor.State.InstanceId == activeReference.InstanceId);
        RuntimeActorSnapshot beforeGrowth = growthActor.Actor.State.ToSnapshot();
        LevelGrowthResult growth = growthServices.LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
            beforeGrowth.Progression,
            beforeGrowth.Stats,
            StandardLevelGrowthProfiles.OwnedEntity,
            reward.TotalExperience,
            random,
            resources: beforeGrowth.Resources,
            baseResourceValues: beforeGrowth.BaseResourceValues));
        var compositionService = new RuntimeActorCombatProfileCompositionService(
            statPolicy,
            growthServices.ResourceGrowthPolicy,
            catalog,
            rosterCapacityPolicy);
        RuntimeActorGrowthCompositionResult growthTransaction =
            new RuntimeActorGrowthCompositionService(
                compositionService,
                catalog).Apply(new RuntimeActorGrowthCompositionRequest(
                    growthActor.Actor.State,
                    growthActor.Actor.Entity,
                    growth,
                    new SharedRuntimeMoveListCapacityPolicy(),
                    TrainingAnnexHostSupport.CreatePlayerCombatProfileCompositionRequest(
                        roster,
                        partyRoster,
                        new RuntimeEquipmentProfile())));
        if (!growthTransaction.Applied)
        {
            foreach (RuntimeActorGrowthCompositionDiagnostic diagnostic in
                     growthTransaction.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[growth:{diagnostic.Code}] {diagnostic.Message}",
                    cancellationToken).ConfigureAwait(false);
            }

            return 5;
        }
        await PrintAsync(
            sequence++,
            "reward",
            $"Awarded {reward.TotalExperience} EXP and {reward.TotalCurrency} Credits; " +
            $"{growthActor.Actor.Entity.DisplayName} level {growth.Progression.Level}.",
            cancellationToken).ConfigureAwait(false);

        RuntimeSaveGameSnapshot save = BuildSaveSnapshot(
            catalog,
            roster,
            partyRoster,
            ashling,
            inventory,
            reward,
            new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(Qualified("training_annex_floor_2")),
                new RuntimeDungeonTraversalSnapshot(
                    dungeon.Id,
                    Qualified("annex_floor_2"),
                    visitedNodeIds: [Qualified("annex_entrance"), Qualified("annex_floor_2")],
                    unlockedCheckpointIds: [Qualified("annex_lobby_checkpoint")])));
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator(
            rulesetBindings: resolver,
            chargePolicies: ChargePolicyRegistry.CreateStandard()).Validate(save, catalog);
        await PrintAsync(
            sequence++,
            "save",
            $"Validated Training Annex save snapshot with {validation.Diagnostics.Count} diagnostic(s).",
            cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            LastSummary = CreateSummary(request, dungeonEvents, encounter.Id, ashling.Entity.Id, inventory, itemUse, battle, reward, growth, validation);
            return 6;
        }

        LastSummary = CreateSummary(request, dungeonEvents, encounter.Id, ashling.Entity.Id, inventory, itemUse, battle, reward, growth, validation);
        await PrintAsync(sequence, "outcome", "Training Annex runtime slice completed successfully.", cancellationToken)
            .ConfigureAwait(false);
        return 0;
    }

    private static CatalogBattleActorCreationRequest AssertSingleActorRequest(EncounterStartPlan plan) =>
        plan.ActorRequests.Single();

    private static BattleRewardEnemySnapshot EnemyRewardSnapshot(EntityDefinition entity, int level) =>
        new(
            entity.Id,
            level,
            entity.Stats.GetValueOrDefault(ContentId.Parse("strength")),
            entity.Stats.GetValueOrDefault(ContentId.Parse("magic")),
            entity.Stats.GetValueOrDefault(ContentId.Parse("vitality")),
            entity.Stats.GetValueOrDefault(ContentId.Parse("agility")),
            entity.Stats.GetValueOrDefault(ContentId.Parse("luck")));

    private static RuntimeSaveGameSnapshot BuildSaveSnapshot(
        GameDataCatalog catalog,
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        CatalogBattleActor ashling,
        IReadOnlyDictionary<ContentId, int> inventory,
        BattleRewardResult reward,
        RuntimeFieldSnapshot field)
    {
        RuntimeActorSnapshot echoSnapshot = roster.Player.Actor.State.ToSnapshot();
        RuntimeActorSnapshot[] actorSnapshots =
        [
            .. roster.AllActors.Select(member => member.Actor.State.ToSnapshot()),
            ashling.State.ToSnapshot()
        ];
        return new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse("0.8.0"),
            [TrainingAnnexHostSupport.PackIdentity],
            actorSnapshots,
            partyRoster,
            new RuntimeInventorySnapshot(inventory),
            new RuntimeEquipmentSnapshot(),
            new RuntimeWalletSnapshot(reward.TotalCurrency),
            field,
            new CompendiumStateSnapshot(),
            new RuntimeKnowledgeSnapshot(),
            new RuntimeSessionProgressSnapshot(
                counters:
                [
                    new KeyValuePair<ContentId, long>(ContentId.Parse("training_annex_runs"), 1),
                    new KeyValuePair<ContentId, long>(ContentId.Parse("training_annex_exp"), reward.TotalExperience)
                ],
                flags: [ContentId.Parse("training_annex_complete")]),
            new RuntimeCheckpointLogSnapshot(
            [
                new RuntimeCheckpointEntrySnapshot(1, RuntimeCheckpointKind.ContentLoaded, "Training Annex content loaded.", contentId: Qualified("training_annex")),
                new RuntimeCheckpointEntrySnapshot(2, RuntimeCheckpointKind.BattleCompleted, "Ashling drill completed.", echoSnapshot.Identity.InstanceId, ashling.Entity.Id),
                new RuntimeCheckpointEntrySnapshot(3, RuntimeCheckpointKind.SaveCreated, "Training Annex snapshot validated.", echoSnapshot.Identity.InstanceId, catalog.GetRequiredItem(Qualified("annex_tonic")).Id)
            ]),
            hostContext: [new KeyValuePair<ContentId, string>(ContentId.Parse("host_demo"), "clean_training_annex")]);
    }

    private static CleanTrainingAnnexDemoSummary CreateSummary(
        ContentPackTextRequest request,
        IReadOnlyList<RuntimeDungeonTraversalEvent> dungeonEvents,
        ContentId encounterId,
        ContentId enemyEntityId,
        IReadOnlyDictionary<ContentId, int> inventory,
        BattleActionExecutionResult itemUse,
        AutomatedBattleResult battle,
        BattleRewardResult reward,
        LevelGrowthResult growth,
        RuntimeSaveValidationResult validation) =>
        new(
            [request.ManifestPath],
            request.DocumentPaths,
            dungeonEvents.Select(dungeonEvent => dungeonEvent.Kind).ToArray(),
            encounterId,
            enemyEntityId,
            inventory.GetValueOrDefault(Qualified("annex_tonic")),
            itemUse.Status,
            itemUse.ItemConsumption,
            itemUse.ItemConsumptionCommitted,
            battle.Outcome,
            battle.WinningTeamId,
            reward.TotalExperience,
            reward.TotalCurrency,
            growth.Progression.Experience,
            growth.Progression.LifetimeExperience,
            growth.Progression.Level,
            validation.IsValid,
            validation.Diagnostics.Count);

    private async ValueTask PrintActorDiagnosticsAsync(
        IEnumerable<CatalogBattleActorDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (CatalogBattleActorDiagnostic diagnostic in diagnostics)
        {
            await _eventSink.PublishAsync($"[{diagnostic.Code}] {diagnostic.Message}", cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask PrintEncounterDiagnosticsAsync(
        IEnumerable<EncounterStartDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (EncounterStartDiagnostic diagnostic in diagnostics)
        {
            await _eventSink.PublishAsync($"[{diagnostic.Code}] {diagnostic.Message}", cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private ValueTask PrintAsync(
        int sequence,
        string kind,
        string message,
        CancellationToken cancellationToken) =>
        _eventSink.PublishAsync($"{sequence:D3} [{kind}] {message}", cancellationToken);

    private sealed class DemoItemActionInventory(IDictionary<ContentId, int> quantities) : IItemActionInventory
    {
        public bool HasAvailable(ContentId itemId, int quantity) =>
            quantities.TryGetValue(itemId, out int available) && available >= quantity;

        public IItemActionReservation Reserve(ContentId itemId, int quantity)
        {
            if (!HasAvailable(itemId, quantity))
            {
                throw new InvalidOperationException($"Item '{itemId}' is not available.");
            }

            return new Reservation(quantities, itemId, quantity);
        }

        private sealed class Reservation(
            IDictionary<ContentId, int> quantities,
            ContentId itemId,
            int quantity) : IItemActionReservation
        {
            public ContentId ItemId { get; } = itemId;
            public int Quantity { get; } = quantity;
            public bool IsCommitted { get; private set; }
            public bool IsRolledBack { get; private set; }

            public ItemActionReservationTransitionResult Commit()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        "Item reservation has already been completed.");
                }

                quantities[ItemId] -= Quantity;
                IsCommitted = true;
                return ItemActionReservationTransitionResult.Success;
            }

            public ItemActionReservationTransitionResult Rollback()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        "Item reservation has already been completed.");
                }

                IsRolledBack = true;
                return ItemActionReservationTransitionResult.Success;
            }
        }
    }
}
