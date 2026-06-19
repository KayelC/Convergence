using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed record TrainingAnnexRuntimeActor(string Role, int Level, CatalogBattleActor Actor);

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
    public static readonly ContentId NewMoon = ContentId.Parse("new_moon");
    public static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    public static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    public static readonly ContentId Hp = ContentId.Parse("hp");
    public static readonly ContentId Sp = ContentId.Parse("sp");

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

    public static BattleExecutionServices CreateExecutionServices(GameDataCatalog catalog) =>
        new(
            catalog,
            new DemoDamageExecutionPolicy(),
            new DemoInstantDeathPolicy(),
            new DemoAilmentPolicy(),
            new DemoChancePolicy(),
            new DemoPowerAmountPolicy(),
            new DemoRandomTargetPolicy());

    public static TrainingAnnexActorRosterResult CreateActorRoster(GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var diagnostics = new List<string>();
        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new DemoBattleActorInitializationPolicy());

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

            enemies.Add(new TrainingAnnexRuntimeActor("Enemy", request.Level, enemyResult.RequireActor()));
        }

        if (diagnostics.Count > 0 || playerResult.Actor is null)
        {
            return new TrainingAnnexActorRosterResult(null, diagnostics);
        }

        return new TrainingAnnexActorRosterResult(
            new TrainingAnnexActorRoster(
                new TrainingAnnexRuntimeActor("Player", 3, playerResult.RequireActor()),
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
        RuntimeProgressionSnapshot? progression = null)
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
            BaseResourceValues(actor.State));
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(TrainingAnnexActorRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        RuntimeActorSnapshot playerSnapshot = CreateActorSnapshot(
            roster.Player.Actor,
            RuntimeInstanceId.Parse(roster.Player.Actor.State.InstanceId.ToString()),
            new RuntimeProgressionSnapshot(roster.Player.Level, 0, 0, roster.Player.Level - 1));
        IReadOnlyList<RuntimeActorSnapshot> enemySnapshots = roster.Enemies
            .Select(enemy => CreateActorSnapshot(
                enemy.Actor,
                RuntimeInstanceId.Parse(enemy.Actor.State.InstanceId.ToString()),
                new RuntimeProgressionSnapshot(enemy.Level, 0, 0, 0)))
            .ToArray();
        RuntimeActorReferenceSnapshot playerReference = Reference(playerSnapshot);
        return BuildStartupSaveSnapshot([playerSnapshot, .. enemySnapshots], playerReference);
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        CatalogBattleActor actor,
        RuntimeFieldSnapshot? field = null)
    {
        RuntimeActorSnapshot actorSnapshot = CreateActorSnapshot(
            actor,
            RuntimeInstanceId.Parse("echo_adept"),
            new RuntimeProgressionSnapshot(actor.Entity.BaseLevel, 0, 0, actor.Entity.BaseLevel - 1));
        RuntimeActorReferenceSnapshot actorReference = Reference(actorSnapshot);
        return BuildStartupSaveSnapshot([actorSnapshot], actorReference, field);
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        IReadOnlyList<RuntimeActorSnapshot> actors,
        RuntimeActorReferenceSnapshot playerReference,
        RuntimeFieldSnapshot? field = null)
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
            new RuntimeInventorySnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeWalletSnapshot(0),
            field ?? new RuntimeFieldSnapshot(
                RuntimeFieldLocation.City,
                new RuntimeDungeonProgressSnapshot(Qualified("training_annex"))),
            new CompendiumStateSnapshot(),
            new RuntimeKnowledgeSnapshot(),
            new RuntimeSessionProgressSnapshot(NewMoon),
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

internal sealed class TrainingAnnexMinimumRandomSource : IRandomSource
{
    public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

    public decimal NextUnitDecimal() => 0m;
}
