using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;

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
    private const string Pack = "convergence.training_annex_slice";

    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId NewMoon = ContentId.Parse("new_moon");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly ContentId Hp = ContentId.Parse("hp");

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
        ContentPackTextRequest request = TrainingAnnexRequest();
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
            new SkillSystemCatalogLoadRequest(BuildRegistrations(), [bundle]));
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
        var random = new MinimumRandomSource();
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
        resolver.BindPressTurnFactory(catalog, Qualified("standard_press_turn")).RequireService();
        resolver.BindStockCapacityPolicy(catalog, Qualified("standard_stock_capacity")).RequireService();
        resolver.BindResourceManagementServices(catalog, Qualified("standard_economy")).RequireService();
        resolver.BindMoonPhaseRuleset(catalog, Qualified("standard_moon_phase")).RequireService();
        await PrintAsync(sequence++, "ruleset", "Bound standard Training Annex rulesets.", cancellationToken)
            .ConfigureAwait(false);

        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new DemoBattleActorInitializationPolicy());
        CatalogBattleActorCreationResult echoResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("echo_adept"),
            ContentId.Parse("echo_adept"),
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
        RuntimeDungeonContentSnapshot runtimeDungeon = ToRuntimeDungeonContent(dungeon);
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

        ContentId hostTriggerId = ContentId.Parse("annex_scene_trigger");
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

        BattleExecutionServices executionServices = CreateExecutionServices(catalog);
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
                new EffectExecutionEnvironment(Battle, NormalBattle, NewMoon),
                new DemoItemActionInventory(inventory)),
            cancellationToken).ConfigureAwait(false);
        await PrintAsync(
            sequence++,
            "item",
            $"{tonic.DisplayName}: HP {damagedHp} -> {echo.State.GetRequiredResource(Hp).Current}; consume={itemUse.ItemConsumption}; remaining={inventory.GetValueOrDefault(tonic.Id)}.",
            cancellationToken).ConfigureAwait(false);

        var skillExecutor = new SkillExecutor(executionServices);
        AutomatedBattleResult battle = new AutomatedBattleRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            executionServices)
            .Run(new AutomatedBattleRequest(
                [echo, ashling],
                Battle,
                NormalBattle,
                NewMoon,
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
            $"Awarded {reward.TotalExperience} EXP and {reward.TotalMacca} Macca; level {growth.Progression.Level}.",
            cancellationToken).ConfigureAwait(false);

        RuntimeSaveGameSnapshot save = BuildSaveSnapshot(
            catalog,
            echo,
            ashling,
            growth,
            inventory,
            reward,
            new RuntimeFieldSnapshot(RuntimeFieldLocation.Dungeon, ascended.After));
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

    private static ContentPackTextRequest TrainingAnnexRequest() =>
        new(
            "training_annex_slice.manifest.json",
            [
                "training_annex_slice.races.json",
                "training_annex_slice.ailments.json",
                "training_annex_slice.skills.json",
                "training_annex_slice.entities.json",
                "training_annex_slice.items.json",
                "training_annex_slice.equipment.json",
                "training_annex_slice.shops.json",
                "training_annex_slice.negotiations.json",
                "training_annex_slice.encounters.json",
                "training_annex_slice.dungeons.json",
                "training_annex_slice.fusion.json",
                "training_annex_slice.rulesets.json"
            ]);

    private static SkillSystemRegistrationSnapshot BuildRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterModifierTrack("attack", "defense")
            .RegisterEntityKind("demon")
            .RegisterAlignment("neutral")
            .RegisterNegotiationPersonality("steady_sample")
            .RegisterAilmentGroup("major_ailment", "toxin", "rest", "immobilize")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
            .RegisterShopCategory("training_supply")
            .RegisterNegotiationDemand("sample_macca")
            .RegisterEncounterEnvironment("training_annex")
            .RegisterPolicy(
                "standard_damage",
                "standard_reward",
                "standard_growth",
                "standard_stat",
                "standard_press_turn",
                "standard_stock_capacity",
                "standard_economy",
                "standard_moon_phase",
                "return_to_lobby",
                "training_barrier",
                "standard_accident",
                "standard_mutation")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportEffect<ReduceResourceEffectDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportEffect<ReviveEffectDefinition>()
            .SupportEffect<ApplyAilmentEffectDefinition>()
            .SupportEffect<ModifyStatStageEffectDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<SkipAilmentTurnBehaviorDefinition>()
            .Build();

    private static BattleExecutionServices CreateExecutionServices(GameDataCatalog catalog) =>
        new(
            catalog,
            new DemoDamageExecutionPolicy(),
            new DemoInstantDeathPolicy(),
            new DemoAilmentPolicy(),
            new DemoChancePolicy(),
            new DemoPowerAmountPolicy(),
            new DemoRandomTargetPolicy());

    private static RuntimeDungeonContentSnapshot ToRuntimeDungeonContent(DungeonDefinition dungeon) =>
        new(
            dungeon.Id,
            dungeon.DisplayName,
            dungeon.Blocks.Select(block => new RuntimeDungeonBlockSnapshot(
                block.Id,
                block.DisplayName,
                block.StartFloor,
                block.EndFloor,
                block.EncounterPoolIds,
                block.FixedFloors.Select(ToRuntimeFixedFloor))));

    private static RuntimeDungeonFixedFloorSnapshot ToRuntimeFixedFloor(DungeonFixedFloorDefinition fixedFloor) =>
        new(
            fixedFloor.Floor,
            fixedFloor.Kind switch
            {
                DungeonFixedFloorKind.Battle => RuntimeDungeonFloorKind.Battle,
                DungeonFixedFloorKind.Boss => RuntimeDungeonFloorKind.Boss,
                DungeonFixedFloorKind.BlockEnd or DungeonFixedFloorKind.Barrier => RuntimeDungeonFloorKind.BlockEnd,
                DungeonFixedFloorKind.SafeRoom or DungeonFixedFloorKind.Terminal => RuntimeDungeonFloorKind.SafeRoom,
                _ => RuntimeDungeonFloorKind.Empty
            },
            fixedFloor.EncounterId ?? fixedFloor.TransitionRuleId ?? fixedFloor.BarrierRuleId,
            fixedFloor.HasTerminal,
            fixedFloor.Description);

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

    private static RuntimeProgressionSnapshot InitialProgression(EntityDefinition entity, int level) =>
        new(level, 0, 0, entity.EntityKindId == StandardProgressionIds.Demon ? 0 : level - 1);

    private static RuntimeStatBlockSnapshot ActorStats(EntityDefinition entity) =>
        new(entity.Stats.Select(pair => new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
            entity.Stats.Select(pair => new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)));

    private static IReadOnlyList<RuntimeResourceSnapshot> RuntimeResources(RuntimeActorState state) =>
        state.Resources.Values
            .Select(resource => new RuntimeResourceSnapshot(resource.Id, resource.Current, resource.Maximum))
            .ToArray();

    private static IReadOnlyDictionary<ContentId, decimal> BaseResourceValues(RuntimeActorState state) =>
        state.Resources.Values.ToDictionary(resource => resource.Id, resource => resource.Maximum);

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
            [echoSnapshot, ashlingSnapshot],
            new RuntimePartyStockSnapshot(
                echoReference,
                echoSnapshot.Progression.Level,
                activeParty: [echoReference]),
            new RuntimeInventorySnapshot(inventory),
            new RuntimeEquipmentSnapshot(),
            new RuntimeWalletSnapshot(reward.TotalMacca),
            field,
            new CompendiumStateSnapshot(),
            new RuntimeKnowledgeSnapshot(),
            new RuntimeSessionProgressSnapshot(
                NewMoon,
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
            baseResources);
    }

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorSnapshot actor) =>
        new(actor.Identity.InstanceId, actor.Identity.EntityDefinitionId, actor.Identity.DisplayName);

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
            reward.TotalMacca,
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

    private static ContentId Qualified(string localId) => ContentId.Parse($"{Pack}:{localId}");

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => 0m;
    }

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

            public void Commit()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return;
                }

                quantities[ItemId] -= Quantity;
                IsCommitted = true;
            }

            public void Rollback()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return;
                }

                IsRolledBack = true;
            }
        }
    }
}
