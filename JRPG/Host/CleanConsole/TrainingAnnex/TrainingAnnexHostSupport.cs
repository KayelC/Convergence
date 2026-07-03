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

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed record TrainingAnnexRuntimeActor(
    string Role,
    int Level,
    CatalogBattleActor Actor,
    RuntimeActorStateSet RuntimeState);

internal sealed record TrainingAnnexActorRoster
{
    public TrainingAnnexActorRoster(TrainingAnnexRuntimeActor player, IEnumerable<TrainingAnnexRuntimeActor> enemies)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Enemies = Array.AsReadOnly((enemies ?? throw new ArgumentNullException(nameof(enemies))).ToArray());
        AllActors = Array.AsReadOnly([Player, .. Enemies]);
    }

    public TrainingAnnexRuntimeActor Player { get; }
    public IReadOnlyList<TrainingAnnexRuntimeActor> Enemies { get; }
    public IReadOnlyList<TrainingAnnexRuntimeActor> AllActors { get; }
}

internal sealed record TrainingAnnexActorRosterResult
{
    public TrainingAnnexActorRosterResult(
        TrainingAnnexActorRoster? roster,
        IEnumerable<string>? diagnostics = null)
    {
        Roster = roster;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public TrainingAnnexActorRoster? Roster { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public bool IsSuccess => Roster is not null && Diagnostics.Count == 0;

    public TrainingAnnexActorRoster RequireRoster() =>
        Roster ?? throw new InvalidOperationException(
            $"Training Annex actor roster creation failed with {Diagnostics.Count} diagnostic(s).");
}

internal static class TrainingAnnexHostSupport
{
    public const string PackId = "convergence.training_annex_slice";

    public static readonly ContentId Battle = ContentId.Parse("battle");
    public static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    public static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    public static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    public static readonly ContentId Hp = ContentId.Parse("hp");
    public static readonly ContentId Sp = ContentId.Parse("sp");
    public static readonly ContentId StagingArea = Qualified("staging_area");
    public static readonly ContentId TrainingAnnexEntrance = Qualified("training_annex_entrance");
    public static readonly ContentId TrainingAnnexDungeon = Qualified("training_annex");
    public static readonly ContentId ReviewHall = Qualified("review_hall");
    public static readonly ContentId ReviewAlcove = Qualified("review_alcove");
    public static readonly ContentId SealedWing = Qualified("sealed_wing");
    public static readonly ContentId ReviewCheckpoint = Qualified("review_checkpoint");
    public static readonly ContentId AnnexTonic = Qualified("annex_tonic");
    public static readonly ContentId PracticeBlade = Qualified("practice_blade");
    public static readonly ContentId FrostTip = Qualified("frost_tip");
    public static readonly ContentId EchoStrike = Qualified("echo_strike");
    public static readonly ContentId Mend = Qualified("mend");
    public static readonly ContentId ToxinTouch = Qualified("toxin_touch");
    public static readonly ContentId ClearToxin = Qualified("clear_toxin");
    public static readonly RuntimeNavigationTransition EnterTrainingAnnexTransition = new(
        ContentId.Parse("enter_training_annex"),
        StagingArea,
        TrainingAnnexEntrance);
    public static readonly RuntimeNavigationTransition LeaveTrainingAnnexTransition = new(
        ContentId.Parse("leave_training_annex"),
        TrainingAnnexEntrance,
        StagingArea);
    public static readonly RuntimeDungeonTraversalTransition EnterReviewHallTransition = new(
        ContentId.Parse("enter_review_hall"),
        TrainingAnnexDungeon,
        TrainingAnnexEntrance,
        ReviewHall);
    public static readonly RuntimeDungeonTraversalTransition ReturnToEntranceTransition = new(
        ContentId.Parse("return_to_annex_entrance"),
        TrainingAnnexDungeon,
        ReviewHall,
        TrainingAnnexEntrance);
    public static readonly RuntimeDungeonTraversalTransition EnterReviewAlcoveTransition = new(
        ContentId.Parse("enter_review_alcove"),
        TrainingAnnexDungeon,
        ReviewHall,
        ReviewAlcove);
    public static readonly RuntimeDungeonTraversalTransition ReturnToReviewHallTransition = new(
        ContentId.Parse("return_to_review_hall"),
        TrainingAnnexDungeon,
        ReviewAlcove,
        ReviewHall);
    public static readonly RuntimeDungeonTraversalTransition InspectBarrierTransition = new(
        ContentId.Parse("pass_training_barrier"),
        TrainingAnnexDungeon,
        ReviewHall,
        SealedWing);
    public static readonly RuntimeEncounterTriggerRequest ReviewHallAshlingTrigger = new(
        ContentId.Parse("review_hall_ashling_trigger"),
        Qualified("ashling_drill"),
        EnemyTeam,
        ContentId.Parse("review_hall_trigger"));

    public static ContentPackTextRequest CreateContentRequest() =>
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

    public static SkillSystemRegistrationSnapshot BuildRegistrations() =>
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
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<SkipAilmentTurnBehaviorDefinition>()
            .Build();

    public static BattleExecutionServices CreateExecutionServices(
        GameDataCatalog catalog,
        ProductionCombatRuleset combatRuleset) =>
        new(
            catalog,
            combatRuleset,
            combatRuleset,
            combatRuleset,
            combatRuleset,
            combatRuleset,
            new TrainingAnnexFirstTargetSelectionPolicy());

    public static TrainingAnnexActorRosterResult CreateActorRoster(GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var diagnostics = new List<string>();
        GrowthRulesetServices? growthServices = BindGrowthServices(catalog, diagnostics);
        if (growthServices is null)
        {
            return new TrainingAnnexActorRosterResult(null, diagnostics);
        }

        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new TrainingAnnexResourceInitializationPolicy(growthServices.ResourceGrowthPolicy));

        CatalogBattleActorCreationResult playerResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("echo_adept"),
            ContentId.Parse("echo_adept"),
            PlayerTeam,
            3));
        if (!playerResult.IsSuccess)
        {
            AddActorDiagnostics("player", playerResult.Diagnostics, diagnostics);
        }

        IReadOnlyList<CatalogBattleActorCreationRequest> enemyRequests = CreateEnemyActorRequests(catalog, diagnostics);
        var enemies = new List<TrainingAnnexRuntimeActor>();
        foreach (CatalogBattleActorCreationRequest request in enemyRequests)
        {
            CatalogBattleActorCreationResult enemyResult = actorFactory.Create(request);
            if (!enemyResult.IsSuccess)
            {
                AddActorDiagnostics(request.EntityId.ToString(), enemyResult.Diagnostics, diagnostics);
                continue;
            }

            enemies.Add(CreateRuntimeActor("Enemy", request.Level, enemyResult.RequireActor()));
        }

        if (diagnostics.Count > 0 || playerResult.Actor is null)
        {
            return new TrainingAnnexActorRosterResult(null, diagnostics);
        }

        return new TrainingAnnexActorRosterResult(
            new TrainingAnnexActorRoster(
                CreateRuntimeActor("Player", 3, playerResult.RequireActor()),
                enemies));
    }

    public static RuntimeDungeonContentSnapshot ToRuntimeDungeonContent(DungeonDefinition dungeon) =>
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

    public static RuntimeActorSnapshot CreateActorSnapshot(
        CatalogBattleActor actor,
        RuntimeInstanceId instanceId,
        RuntimeProgressionSnapshot? progression = null,
        IReadOnlyDictionary<ContentId, decimal>? baseResourceValues = null)
    {
        RuntimeProgressionSnapshot resolvedProgression =
            progression ?? InitialProgression(actor.Entity, actor.Entity.BaseLevel);
        return new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                instanceId,
                actor.Entity.Id,
                actor.Entity.EntityKindId,
                actor.Entity.DisplayName),
            new RuntimeActorOwnershipSnapshot(ContentId.Parse("clean_training_annex"), actor.State.TeamId),
            new RuntimeActorDeploymentSnapshot(
                actor.State.TeamId == PlayerTeam ? RuntimeActorDeployment.Active : RuntimeActorDeployment.Deployed,
                actor.State.IsActive),
            resolvedProgression,
            RuntimeResources(actor.State),
            ActorStats(actor.Entity),
            new RuntimeSkillStateSnapshot(
                actor.SkillLoadout.Select(skill => skill.Id),
                actor.ActiveSkills.Select(skill => skill.Id)),
            new RuntimeFormStockSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            baseResourceValues ?? BaseResourceValues(actor.State));
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        TrainingAnnexActorRoster roster,
        RuntimeFieldSnapshot? field = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeWalletSnapshot? wallet = null,
        RuntimeSessionProgressSnapshot? session = null)
    {
        ArgumentNullException.ThrowIfNull(roster);

        RuntimeActorSnapshot playerSnapshot = roster.Player.RuntimeState.ToSnapshot();
        IReadOnlyList<RuntimeActorSnapshot> enemySnapshots = roster.Enemies
            .Select(enemy => enemy.RuntimeState.ToSnapshot())
            .ToArray();
        RuntimeActorReferenceSnapshot playerReference = Reference(playerSnapshot);
        return BuildStartupSaveSnapshot(
            [playerSnapshot, .. enemySnapshots],
            playerReference,
            field,
            knowledge,
            inventory,
            wallet,
            session);
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        CatalogBattleActor actor,
        RuntimeFieldSnapshot? field = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeWalletSnapshot? wallet = null,
        RuntimeSessionProgressSnapshot? session = null)
    {
        RuntimeActorSnapshot actorSnapshot = CreateActorSnapshot(
            actor,
            RuntimeInstanceId.Parse("echo_adept"),
            new RuntimeProgressionSnapshot(actor.Entity.BaseLevel, 0, 0, actor.Entity.BaseLevel - 1));
        RuntimeActorReferenceSnapshot actorReference = Reference(actorSnapshot);
        return BuildStartupSaveSnapshot([actorSnapshot], actorReference, field, knowledge, inventory, wallet, session);
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        IReadOnlyList<RuntimeActorSnapshot> actors,
        RuntimeActorReferenceSnapshot playerReference,
        RuntimeFieldSnapshot? field = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeWalletSnapshot? wallet = null,
        RuntimeSessionProgressSnapshot? session = null)
    {
        RuntimeActorSnapshot playerSnapshot = actors.First(actor =>
            actor.Identity.InstanceId == playerReference.InstanceId);
        return new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse("0.1.0"),
            actors,
            new RuntimePartyStockSnapshot(
                playerReference,
                playerSnapshot.Progression.Level,
                activeParty: [playerReference]),
            inventory ?? new RuntimeInventorySnapshot(),
            new RuntimeEquipmentSnapshot(),
            wallet ?? new RuntimeWalletSnapshot(0),
            field,
            new CompendiumStateSnapshot(),
            knowledge ?? new RuntimeKnowledgeSnapshot(),
            session ?? new RuntimeSessionProgressSnapshot(),
            new RuntimeCheckpointLogSnapshot(
            [
                new RuntimeCheckpointEntrySnapshot(
                    1,
                    RuntimeCheckpointKind.ContentLoaded,
                    "Training Annex clean session booted.",
                    playerSnapshot.Identity.InstanceId,
                    Qualified("training_annex"))
            ]),
            hostContext: [new KeyValuePair<ContentId, string>(ContentId.Parse("host_mode"), "clean_training_annex_play")]);
    }

    public static ContentId Qualified(string localId) => ContentId.Parse($"{PackId}:{localId}");

    public static RuntimeProgressionSnapshot InitialProgression(EntityDefinition entity, int level) =>
        new(level, 0, 0, entity.EntityKindId == StandardProgressionIds.Demon ? 0 : level - 1);

    public static RuntimeStatBlockSnapshot ActorStats(EntityDefinition entity) =>
        new(
            entity.Stats.Select(pair => new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
            entity.Stats.Select(pair => new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)));

    public static IReadOnlyList<RuntimeResourceSnapshot> RuntimeResources(RuntimeActorState state) =>
        state.Resources.Values
            .Select(resource => new RuntimeResourceSnapshot(resource.Id, resource.Current, resource.Maximum))
            .ToArray();

    public static IReadOnlyDictionary<ContentId, decimal> BaseResourceValues(RuntimeActorState state) =>
        state.Resources.Values.ToDictionary(resource => resource.Id, resource => resource.Maximum);

    public static RuntimeActorReferenceSnapshot Reference(RuntimeActorSnapshot actor) =>
        new(actor.Identity.InstanceId, actor.Identity.EntityDefinitionId, actor.Identity.DisplayName);

    public static IReadOnlyDictionary<ContentId, decimal> InitialBaseResourceValues(int level) =>
        new Dictionary<ContentId, decimal>
        {
            [Hp] = 40 + level * 5,
            [Sp] = 10 + level * 2
        };

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

    private static GrowthRulesetServices? BindGrowthServices(GameDataCatalog catalog, List<string> diagnostics)
    {
        RulesetBindingResult<GrowthRulesetServices> growth = new RuntimeRulesetBindingResolver()
            .BindGrowthServices(catalog, Qualified("standard_growth"));
        if (growth.IsSuccess)
        {
            return growth.RequireService();
        }

        foreach (RulesetBindingDiagnostic diagnostic in growth.Diagnostics)
        {
            diagnostics.Add($"[growth:{diagnostic.Code}] {diagnostic.Message}");
        }

        return null;
    }

    private static TrainingAnnexRuntimeActor CreateRuntimeActor(
        string role,
        int level,
        CatalogBattleActor actor)
    {
        RuntimeActorSnapshot snapshot = CreateActorSnapshot(
            actor,
            RuntimeInstanceId.Parse(actor.State.InstanceId.ToString()),
            new RuntimeProgressionSnapshot(level, 0, 0, role == "Player" ? level - 1 : 0),
            InitialBaseResourceValues(level));
        return new TrainingAnnexRuntimeActor(role, level, actor, RuntimeActorStateSet.FromSnapshot(snapshot));
    }

    private static IReadOnlyList<CatalogBattleActorCreationRequest> CreateEnemyActorRequests(
        GameDataCatalog catalog,
        List<string> diagnostics)
    {
        var planner = new CatalogEncounterStartPlanner(catalog);
        ContentId[] encounterIds =
        [
            Qualified("ashling_drill"),
            Qualified("mixed_drill"),
            Qualified("shell_check")
        ];
        var requestsByEntity = new Dictionary<ContentId, CatalogBattleActorCreationRequest>();
        foreach (ContentId encounterId in encounterIds)
        {
            EncounterStartPlanResult planResult = planner.Plan(new EncounterStartRequest(
                encounterId,
                EnemyTeam,
                ContentId.Parse($"roster_{LocalId(encounterId)}")));
            if (!planResult.IsSuccess)
            {
                foreach (EncounterStartDiagnostic diagnostic in planResult.Diagnostics)
                {
                    diagnostics.Add($"[{diagnostic.Code}] {diagnostic.Message}");
                }

                continue;
            }

            foreach (CatalogBattleActorCreationRequest request in planResult.RequirePlan().ActorRequests)
            {
                requestsByEntity.TryAdd(
                    request.EntityId,
                    request with { InstanceId = ContentId.Parse($"enemy_{LocalId(request.EntityId)}") });
            }
        }

        return requestsByEntity.Values.ToArray();
    }

    private static void AddActorDiagnostics(
        string label,
        IEnumerable<CatalogBattleActorDiagnostic> actorDiagnostics,
        List<string> diagnostics)
    {
        foreach (CatalogBattleActorDiagnostic diagnostic in actorDiagnostics)
        {
            diagnostics.Add($"[{label}:{diagnostic.Code}] {diagnostic.Message}");
        }
    }

    private static string LocalId(ContentId id)
    {
        string value = id.ToString();
        int separator = value.LastIndexOf(':');
        return separator >= 0 ? value[(separator + 1)..] : value;
    }
}

internal sealed class TrainingAnnexResourceInitializationPolicy(IResourceGrowthPolicy resourceGrowthPolicy)
    : IBattleActorInitializationPolicy
{
    public BattleActorInitialization Initialize(EntityDefinition entity, int level)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ResourceRecalculationResult resources = resourceGrowthPolicy.Recalculate(new ResourceRecalculationRequest(
            [
                new RuntimeResourceSnapshot(TrainingAnnexHostSupport.Hp, 0, 0),
                new RuntimeResourceSnapshot(TrainingAnnexHostSupport.Sp, 0, 0)
            ],
            TrainingAnnexHostSupport.InitialBaseResourceValues(level),
            entity.Stats.Select(pair => new KeyValuePair<ContentId, decimal>(pair.Key, pair.Value)),
            ResourceCurrentAdjustmentMode.LevelUpDelta));

        return new BattleActorInitialization(
            TrainingAnnexHostSupport.Hp,
            resources.Resources.Select(resource => new BattleResourceState(
                resource.ResourceId,
                resource.Current,
                resource.Maximum)));
    }
}

internal sealed class TrainingAnnexMinimumRandomSource : IRandomSource
{
    public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

    public decimal NextUnitDecimal() => 0m;
}

internal sealed class TrainingAnnexFirstTargetSelectionPolicy : IRandomTargetSelectionPolicy
{
    public IReadOnlyList<BattleActorState> Select(
        IReadOnlyList<BattleActorState> candidates,
        TargetCountDefinition count,
        SkillExecutionRequest request) =>
        Array.AsReadOnly(candidates.Take(count.Minimum).ToArray());
}

internal sealed class TrainingAnnexNavigationPolicy : IRuntimeNavigationPolicy
{
    public RuntimeNavigationPolicyDecision Evaluate(RuntimeNavigationPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool isKnownTransition =
            request.Transition == TrainingAnnexHostSupport.EnterTrainingAnnexTransition ||
            request.Transition == TrainingAnnexHostSupport.LeaveTrainingAnnexTransition;
        return isKnownTransition
            ? new RuntimeNavigationPolicyDecision(true)
            : new RuntimeNavigationPolicyDecision(
                false,
                ContentId.Parse("unsupported_transition"),
                $"Transition '{request.Transition.Id}' is not available in the Training Annex host.");
    }
}

internal sealed class TrainingAnnexDungeonPolicy : IRuntimeDungeonTraversalPolicy
{
    public RuntimeDungeonTraversalPolicyDecision Evaluate(RuntimeDungeonTraversalPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Transition == TrainingAnnexHostSupport.InspectBarrierTransition)
        {
            return new RuntimeDungeonTraversalPolicyDecision(
                false,
                ContentId.Parse("training_barrier"),
                "The sample barrier is sealed.");
        }

        bool isKnownTransition =
            request.Transition == TrainingAnnexHostSupport.EnterReviewHallTransition ||
            request.Transition == TrainingAnnexHostSupport.ReturnToEntranceTransition ||
            request.Transition == TrainingAnnexHostSupport.EnterReviewAlcoveTransition ||
            request.Transition == TrainingAnnexHostSupport.ReturnToReviewHallTransition;
        return isKnownTransition
            ? new RuntimeDungeonTraversalPolicyDecision(true)
            : new RuntimeDungeonTraversalPolicyDecision(
                false,
                ContentId.Parse("unsupported_transition"),
                $"Transition '{request.Transition.Id}' is not available in the Training Annex host.");
    }
}
