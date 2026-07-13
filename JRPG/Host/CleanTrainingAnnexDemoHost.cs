using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Host.CleanConsole.TrainingAnnex;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;

using static JRPGPrototype.Host.CleanConsole.TrainingAnnex.TrainingAnnexHostSupport;

namespace JRPGPrototype.Host;

internal sealed record CleanTrainingAnnexDemoSummary(
    IReadOnlyList<string> RequestedManifestPaths,
    IReadOnlyList<string> RequestedDocumentPaths,
    IReadOnlyList<RuntimeDungeonEventKind> DungeonEventKinds,
    ContentId EncounterId,
    ContentId EnemyEntityId,
    int InventoryRemaining,
    BattleActionExecutionStatus ItemStatus,
    ItemConsumptionDecision ItemConsumption,
    bool ItemConsumptionCommitted,
    AutomatedBattleOutcome BattleOutcome,
    ContentId? WinningTeamId,
    int RewardExperience,
    int RewardMacca,
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
            new FileContentPackSource(contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Data", "Jsons")),
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

        var resolver = new RuntimeRulesetBindingResolver();
        var random = new TrainingAnnexMinimumRandomSource();
        ProductionCombatRuleset damageRuleset = resolver.BindProductionCombatRuleset(
            catalog,
            Qualified("standard_damage"),
            random).RequireService();
        IBattleRewardService rewardService = resolver.BindBattleRewardService(
            catalog,
            Qualified("standard_reward"),
            damageRuleset).RequireService();
        GrowthRulesetServices growthServices = resolver.BindGrowthServices(
            catalog,
            Qualified("standard_growth")).RequireService();
        resolver.BindStatResolutionPolicy(catalog, Qualified("standard_stat")).RequireService();
        BattleTurnEconomyRuleset turnEconomy = resolver.BindTurnEconomy(
            catalog,
            Qualified("standard_press_turn")).RequireService();
        resolver.BindStockCapacityPolicy(catalog, Qualified("standard_stock_capacity")).RequireService();
        resolver.BindResourceManagementServices(catalog, Qualified("standard_economy")).RequireService();
        await PrintAsync(sequence++, "ruleset", "Bound standard Training Annex rulesets.", cancellationToken)
            .ConfigureAwait(false);

        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new DemoBattleActorInitializationPolicy());
        CatalogBattleActorCreationResult echoResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("echo_adept"),
            RuntimeInstanceId.Parse("echo_adept"),
            PlayerTeam,
            3));
        if (!echoResult.IsSuccess)
        {
            await PrintActorDiagnosticsAsync(echoResult.Diagnostics, cancellationToken).ConfigureAwait(false);
            return 4;
        }

        CatalogBattleActor echo = echoResult.RequireActor();
        await PrintAsync(sequence++, "actor", $"Hydrated {echo.Entity.DisplayName}.", cancellationToken)
            .ConfigureAwait(false);

        var dungeonService = new RuntimeFieldDungeonService(random);
        DungeonDefinition dungeon = catalog.GetRequiredDungeon(Qualified("training_annex"));
        RuntimeDungeonContentSnapshot runtimeDungeon = TrainingAnnexHostSupport.ToRuntimeDungeonContent(dungeon);
        RuntimeDungeonProgressSnapshot progress = new(dungeon.Id);
        RuntimeDungeonTransitionResult entered = dungeonService.EnterDungeon(runtimeDungeon, progress);
        RuntimeDungeonTransitionResult ascended = dungeonService.Ascend(runtimeDungeon, entered.After);
        IReadOnlyList<RuntimeDungeonEvent> dungeonEvents = entered.Events.Concat(ascended.Events).ToArray();
        foreach (RuntimeDungeonEvent dungeonEvent in dungeonEvents)
        {
            string floor = dungeonEvent.Floor is int value ? $" floor {value}" : string.Empty;
            await PrintAsync(sequence++, "dungeon", $"{dungeonEvent.Kind}{floor}.", cancellationToken)
                .ConfigureAwait(false);
        }

        RuntimeInstanceId hostTriggerId = RuntimeInstanceId.Parse("annex_scene_trigger");
        EncounterStartPlanResult encounterStart = new CatalogEncounterStartPlanner(catalog).Plan(
            new EncounterStartRequest(
                Qualified("ashling_drill"),
                EnemyTeam,
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
            TrainingAnnexHostSupport.CreateExecutionServices(catalog, damageRuleset);
        var actionExecutor = new BattleActionExecutor(
            new SkillExecutor(executionServices),
            new ItemExecutor(executionServices),
            executionServices);
        Dictionary<ContentId, int> inventory = new() { [Qualified("annex_tonic")] = 1 };
        echo.State.AddResource(Hp, -20);
        decimal damagedHp = echo.State.GetRequiredResource(Hp).Current;
        ItemDefinition tonic = catalog.GetRequiredItem(Qualified("annex_tonic"));
        BattleActionExecutionResult itemUse = await actionExecutor.ExecuteAsync(
            new BattleActionExecutionRequest(
                new ItemBattleActionCommand(tonic, [echo.State.InstanceId]),
                echo.State,
                [echo.State, ashling.State],
                new EffectExecutionEnvironment(Battle, NormalBattle),
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
            ContentId.Parse("owner_turn_end"));
        AutomatedBattleResult battle = new AutomatedBattleRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            executionServices,
            lifecycle,
            turnEconomy)
            .Run(new AutomatedBattleRequest(
                [echo, ashling],
                Battle,
                NormalBattle,
                null,
                10));
        foreach (BattleRuntimeEvent battleEvent in battle.Events)
        {
            await _eventSink.PublishAsync(
                $"{sequence++:D3} [battle] {battleEvent.Kind}: {battleEvent.Message}",
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
            [new BattleRewardRecipientSnapshot(echo.Entity.Id, IsAlive: !echo.State.IsDefeated, HasActiveForm: false)]));
        LevelGrowthResult growth = growthServices.LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
            InitialProgression(echo.Entity, 3),
            ActorStats(echo.Entity),
            echo.Entity.EntityKindId,
            reward.TotalExperience,
            random,
            resources: RuntimeResources(echo.State),
            baseResourceValues: BaseResourceValues(echo.State)));
        await PrintAsync(
            sequence++,
            "reward",
            $"Awarded {reward.TotalExperience} EXP and {reward.TotalCurrency} Macca; level {growth.Progression.Level}.",
            cancellationToken).ConfigureAwait(false);

        RuntimeSaveGameSnapshot save = BuildSaveSnapshot(
            catalog,
            echo,
            ashling,
            growth,
            inventory,
            reward,
            new RuntimeFieldSnapshot(
                new RuntimeNavigationSnapshot(Qualified("training_annex_floor_2")),
                new RuntimeDungeonTraversalSnapshot(
                    dungeon.Id,
                    Qualified("annex_floor_2"),
                    visitedNodeIds: [Qualified("annex_entrance"), Qualified("annex_floor_2")],
                    unlockedCheckpointIds: [Qualified("annex_lobby_checkpoint")])));
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(save, catalog);
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
        CatalogBattleActor echo,
        CatalogBattleActor ashling,
        LevelGrowthResult growth,
        IReadOnlyDictionary<ContentId, int> inventory,
        BattleRewardResult reward,
        RuntimeFieldSnapshot field)
    {
        RuntimeActorSnapshot echoSnapshot = ActorSnapshot(echo, RuntimeInstanceId.Parse("echo_adept"), growth);
        RuntimeActorSnapshot ashlingSnapshot = ActorSnapshot(ashling, RuntimeInstanceId.Parse("ashling"), null);
        RuntimeActorReferenceSnapshot echoReference = Reference(echoSnapshot);
        return new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse("0.1.0"),
            [TrainingAnnexHostSupport.PackIdentity],
            [echoSnapshot, ashlingSnapshot],
            new RuntimePartyStockSnapshot(
                echoReference,
                echoSnapshot.Progression.Level,
                activeParty: [echoReference]),
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

    private static RuntimeActorSnapshot ActorSnapshot(
        CatalogBattleActor actor,
        RuntimeInstanceId instanceId,
        LevelGrowthResult? growth)
    {
        RuntimeProgressionSnapshot progression = growth?.Progression ?? InitialProgression(actor.Entity, actor.Entity.BaseLevel);
        RuntimeStatBlockSnapshot stats = growth?.Stats ?? ActorStats(actor.Entity);
        IReadOnlyList<RuntimeResourceSnapshot> resources = growth?.Resources ?? RuntimeResources(actor.State);
        IReadOnlyDictionary<ContentId, decimal> baseResources = growth?.BaseResourceValues ?? BaseResourceValues(actor.State);
        return new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(instanceId, actor.Entity.Id, actor.Entity.EntityKindId, actor.Entity.DisplayName),
            new RuntimeActorOwnershipSnapshot(ContentId.Parse("clean_training_annex"), actor.State.TeamId),
            new RuntimeActorDeploymentSnapshot(actor.State.TeamId == PlayerTeam ? RuntimeActorDeployment.Active : RuntimeActorDeployment.Deployed, actor.State.IsActive),
            progression,
            resources,
            stats,
            new RuntimeSkillStateSnapshot(actor.SkillLoadout.Select(skill => skill.Id), actor.ActiveSkills.Select(skill => skill.Id)),
            new RuntimeFormStockSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            baseResources,
            actor.State.VitalResourceId);
    }

    private static CleanTrainingAnnexDemoSummary CreateSummary(
        ContentPackTextRequest request,
        IReadOnlyList<RuntimeDungeonEvent> dungeonEvents,
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
