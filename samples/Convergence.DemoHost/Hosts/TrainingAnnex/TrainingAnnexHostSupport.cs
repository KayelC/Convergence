using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Fusion;
using Convergence.Runtime;

namespace Convergence.DemoHost.TrainingAnnex;

internal sealed record TrainingAnnexRuntimeActor(
    string Role,
    CatalogBattleActor Actor)
{
    public int Level => Actor.State.Progression.Level;
}

internal sealed record TrainingAnnexActorRoster
{
    public TrainingAnnexActorRoster(
        TrainingAnnexRuntimeActor player,
        IEnumerable<TrainingAnnexRuntimeActor> supportMembers,
        IEnumerable<TrainingAnnexRuntimeActor> ownedActors,
        IEnumerable<TrainingAnnexRuntimeActor> enemies,
        IEnumerable<TrainingAnnexRuntimeActor>? dynamicMembers = null)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        SupportMembers = Array.AsReadOnly(
            (supportMembers ?? throw new ArgumentNullException(nameof(supportMembers))).ToArray());
        OwnedActors = Array.AsReadOnly(
            (ownedActors ?? throw new ArgumentNullException(nameof(ownedActors))).ToArray());
        Enemies = Array.AsReadOnly((enemies ?? throw new ArgumentNullException(nameof(enemies))).ToArray());
        DynamicMembers = Array.AsReadOnly((dynamicMembers ?? []).ToArray());
        AllActors = Array.AsReadOnly([Player, .. SupportMembers, .. OwnedActors, .. DynamicMembers, .. Enemies]);
    }

    public TrainingAnnexRuntimeActor Player { get; }
    public IReadOnlyList<TrainingAnnexRuntimeActor> SupportMembers { get; }
    public IReadOnlyList<TrainingAnnexRuntimeActor> OwnedActors { get; }
    public IReadOnlyList<TrainingAnnexRuntimeActor> DynamicMembers { get; }
    public IReadOnlyList<TrainingAnnexRuntimeActor> Enemies { get; }
    public IReadOnlyList<TrainingAnnexRuntimeActor> AllActors { get; }

    public TrainingAnnexActorRoster WithDynamicMember(TrainingAnnexRuntimeActor dynamicMember) =>
        new(
            Player,
            SupportMembers,
            OwnedActors,
            Enemies,
            [.. DynamicMembers, dynamicMember ?? throw new ArgumentNullException(nameof(dynamicMember))]);
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
    public static readonly ContentPackIdentity PackIdentity =
        new(PackId, SemanticVersion.Parse("0.3.0"));

    public static readonly ContentId Battle = ContentId.Parse("battle");
    public static readonly ContentId AshlingDrillClearedFlag = ContentId.Parse("ashling_drill_cleared");
    public static readonly ContentId FieldMenuSaveContext = ContentId.Parse("field_menu");
    public static readonly ContentId DungeonMenuSaveContext = ContentId.Parse("dungeon_menu");
    public static readonly ContentId BattleSaveContext = ContentId.Parse("battle");
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
    public static readonly ContentId TrainingSupply = Qualified("training_supply");
    public static readonly ContentId AnnexTonic = Qualified("annex_tonic");
    public static readonly ContentId PracticeBlade = Qualified("practice_blade");
    public static readonly ContentId FocusCharm = Qualified("focus_charm");
    public static readonly ContentId SteadySampleNegotiation = Qualified("steady_sample");
    public static readonly ContentId SampleCreditsDemand = ContentId.Parse("sample_credits");
    public static readonly ContentId NegotiationAcquisitionSource = ContentId.Parse("negotiation");
    public static readonly ContentId FusionAcquisitionSource = ContentId.Parse("fusion");
    public static readonly ContentId FrostTip = Qualified("frost_tip");
    public static readonly ContentId EchoStrike = Qualified("echo_strike");
    public static readonly ContentId Mend = Qualified("mend");
    public static readonly ContentId ToxinTouch = Qualified("toxin_touch");
    public static readonly ContentId ClearToxin = Qualified("clear_toxin");
    public static readonly RuntimeInstanceId EchoAdeptInstance = RuntimeInstanceId.Parse("echo_adept");
    public static readonly RuntimeInstanceId SupportAnnexMentorInstance = RuntimeInstanceId.Parse("support_annex_mentor");
    public static readonly RuntimeInstanceId HostedAnnexMentorInstance = RuntimeInstanceId.Parse("hosted_annex_mentor");
    public static readonly RuntimeInstanceId HostedBrambleRunnerInstance = RuntimeInstanceId.Parse("hosted_bramble_runner");
    public static readonly RuntimeInstanceId CompanionAshlingInstance = RuntimeInstanceId.Parse("companion_ashling");
    public static readonly RuntimeInstanceId CompanionWardShellInstance = RuntimeInstanceId.Parse("companion_ward_shell");
    public static readonly RuntimeInstanceId ReplacementBrambleRunnerInstance = RuntimeInstanceId.Parse("replacement_bramble_runner");
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
        ContentId.Parse("clean_training_annex_ai"),
        RuntimeInstanceId.Parse("review_hall_trigger"));

    public static ContentPackTextRequest CreateContentRequest() =>
        new(
            "original/training-annex/training_annex_slice.manifest.json",
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
            .RegisterEntityKind("vessel", "companion")
            .RegisterAlignment("neutral")
            .RegisterNegotiationPersonality("steady_sample")
            .RegisterAilmentGroup("major_ailment", "toxin", "rest", "immobilize")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterBattleKind("normal_battle")
            .RegisterShopCategory("training_supply")
            .RegisterNegotiationDemand("sample_credits")
            .RegisterEncounterEnvironment("training_annex")
            .RegisterPolicy(
                "standard_damage",
                "standard_reward",
                "standard_growth",
                "standard_stat",
                "standard_action_token",
                "standard_roster_capacity",
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
            new TrainingAnnexFirstTargetSelectionPolicy(),
            new OrderedRuntimeTargetSelectionPolicy());

    public static TrainingAnnexActorRosterResult CreateActorRoster(GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var diagnostics = new List<string>();
        GrowthRulesetServices? growthServices = BindGrowthServices(catalog, diagnostics);
        StatRulesetServices? statServices = BindStatServices(catalog, diagnostics);
        if (growthServices is null || statServices is null)
        {
            return new TrainingAnnexActorRosterResult(null, diagnostics);
        }

        var actorFactory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new TrainingAnnexResourceInitializationPolicy(growthServices.ResourceGrowthPolicy));

        CatalogBattleActorCreationResult playerResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("echo_adept"),
            EchoAdeptInstance,
            PlayerTeam,
            3,
            IsDeployed: true,
            ContentId.Parse("clean_training_annex"),
            new RuntimeProgressionSnapshot(3, 0, 0, 0)));
        if (!playerResult.IsSuccess)
        {
            AddActorDiagnostics("player", playerResult.Diagnostics, diagnostics);
        }

        CatalogBattleActorCreationResult mentorResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("annex_mentor"),
            SupportAnnexMentorInstance,
            PlayerTeam,
            5,
            IsDeployed: false,
            ContentId.Parse("clean_training_annex"),
            new RuntimeProgressionSnapshot(5, 0, 0, 0)));
        if (!mentorResult.IsSuccess)
        {
            AddActorDiagnostics("support", mentorResult.Diagnostics, diagnostics);
        }

        CatalogBattleActorCreationResult activeHostedEntityResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("annex_mentor"),
            HostedAnnexMentorInstance,
            PlayerTeam,
            5,
            IsDeployed: false,
            ContentId.Parse("clean_training_annex"),
            new RuntimeProgressionSnapshot(5, 0, 0, 0)));
        if (!activeHostedEntityResult.IsSuccess)
        {
            AddActorDiagnostics("active_hosted_entity", activeHostedEntityResult.Diagnostics, diagnostics);
        }

        CatalogBattleActorCreationResult hostedEntityRosterResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("bramble_runner"),
            HostedBrambleRunnerInstance,
            PlayerTeam,
            3,
            IsDeployed: false,
            ContentId.Parse("clean_training_annex"),
            new RuntimeProgressionSnapshot(3, 0, 0, 0)));
        if (!hostedEntityRosterResult.IsSuccess)
        {
            AddActorDiagnostics("hosted_entity_roster", hostedEntityRosterResult.Diagnostics, diagnostics);
        }

        CatalogBattleActorCreationResult companionAshlingResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("ashling"),
            CompanionAshlingInstance,
            PlayerTeam,
            2,
            IsDeployed: false,
            ContentId.Parse("clean_training_annex"),
            new RuntimeProgressionSnapshot(2, 0, 0, 0)));
        if (!companionAshlingResult.IsSuccess)
        {
            AddActorDiagnostics("companion_roster", companionAshlingResult.Diagnostics, diagnostics);
        }

        CatalogBattleActorCreationResult companionWardShellResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("ward_shell"),
            CompanionWardShellInstance,
            PlayerTeam,
            4,
            IsDeployed: false,
            ContentId.Parse("clean_training_annex"),
            new RuntimeProgressionSnapshot(4, 0, 0, 0)));
        if (!companionWardShellResult.IsSuccess)
        {
            AddActorDiagnostics("companion_roster", companionWardShellResult.Diagnostics, diagnostics);
        }

        CatalogBattleActorCreationResult replacementBrambleResult = actorFactory.Create(new CatalogBattleActorCreationRequest(
            Qualified("bramble_runner"),
            ReplacementBrambleRunnerInstance,
            PlayerTeam,
            3,
            IsDeployed: false,
            ContentId.Parse("clean_training_annex"),
            new RuntimeProgressionSnapshot(3, 0, 0, 0)));
        if (!replacementBrambleResult.IsSuccess)
        {
            AddActorDiagnostics("companion_replacement_candidate", replacementBrambleResult.Diagnostics, diagnostics);
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

            enemies.Add(new TrainingAnnexRuntimeActor("Enemy", enemyResult.RequireActor()));
        }

        if (diagnostics.Count > 0 ||
            playerResult.Actor is null ||
            mentorResult.Actor is null ||
            activeHostedEntityResult.Actor is null ||
            hostedEntityRosterResult.Actor is null ||
            companionAshlingResult.Actor is null ||
            companionWardShellResult.Actor is null ||
            replacementBrambleResult.Actor is null)
        {
            return new TrainingAnnexActorRosterResult(null, diagnostics);
        }

        CatalogBattleActor player = playerResult.RequireActor();
        CatalogBattleActor activeHostedEntity = activeHostedEntityResult.RequireActor();
        RuntimeActorReferenceSnapshot playerReference = Reference(player.State.ToSnapshot());
        RuntimeActorReferenceSnapshot activeHostedEntityReference =
            Reference(activeHostedEntity.State.ToSnapshot());
        var partyRoster = new RuntimePartyRosterSnapshot(
            playerReference,
            player.State.Progression.Level,
            activeParty: [playerReference],
            reserveMembers: [Reference(mentorResult.RequireActor().State.ToSnapshot())],
            activeHostedEntity: activeHostedEntityReference,
            hostedEntityRoster:
            [
                activeHostedEntityReference,
                Reference(hostedEntityRosterResult.RequireActor().State.ToSnapshot())
            ],
            companionRoster:
            [
                Reference(companionAshlingResult.RequireActor().State.ToSnapshot()),
                Reference(companionWardShellResult.RequireActor().State.ToSnapshot()),
                Reference(replacementBrambleResult.RequireActor().State.ToSnapshot())
            ],
            maxActivePartySize: 2);
        RuntimeActorCombatProfileCompositionResult composition = new RuntimeActorCombatProfileCompositionService(
                statServices.StatResolutionPolicy,
                growthServices.ResourceGrowthPolicy,
                catalog)
            .Compose(new RuntimeActorCombatProfileCompositionRequest(
                player.State,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster,
                [activeHostedEntity.State]));
        if (!composition.Applied)
        {
            foreach (RuntimeActorCombatProfileCompositionDiagnostic diagnostic in composition.Diagnostics)
            {
                diagnostics.Add($"[combat_profile_composition:{diagnostic.Code}] {diagnostic.Message}");
            }

            return new TrainingAnnexActorRosterResult(null, diagnostics);
        }

        foreach (BattleResourceState resource in player.State.Resources.Values)
        {
            player.State.SetResource(resource.Id, resource.Maximum);
        }

        return new TrainingAnnexActorRosterResult(
            new TrainingAnnexActorRoster(
                new TrainingAnnexRuntimeActor("Player", player),
                [new TrainingAnnexRuntimeActor("Reserve", mentorResult.RequireActor())],
                [
                    new TrainingAnnexRuntimeActor("Active Hosted Entity", activeHostedEntityResult.RequireActor()),
                    new TrainingAnnexRuntimeActor("Hosted Entity roster", hostedEntityRosterResult.RequireActor()),
                    new TrainingAnnexRuntimeActor("Companion roster", companionAshlingResult.RequireActor()),
                    new TrainingAnnexRuntimeActor("Companion roster", companionWardShellResult.RequireActor()),
                    new TrainingAnnexRuntimeActor("Companion Replacement Candidate", replacementBrambleResult.RequireActor())
                ],
                enemies));
    }

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
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("clean_training_annex"), actor.State.TeamId),
            new RuntimeEncounterPresenceSnapshot(actor.State.IsDeployed),
            resolvedProgression,
            RuntimeResources(actor.State),
            ActorStats(actor.Entity),
            new RuntimeSkillStateSnapshot(
                actor.SkillLoadout.Select(skill => skill.Id),
                actor.ActiveSkills.Select(skill => skill.Id)),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            baseResourceValues ?? BaseResourceValues(actor.State),
            actor.State.VitalResourceId);
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        TrainingAnnexActorRoster roster,
        RuntimeFieldSnapshot? field = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeWalletSnapshot? wallet = null,
        RuntimeSessionProgressSnapshot? session = null,
        IEnumerable<KeyValuePair<ContentId, string>>? hostContext = null,
        CompendiumStateSnapshot? compendium = null) =>
        BuildStartupSaveSnapshot(
            roster,
            null,
            field,
            knowledge,
            inventory,
            wallet,
            session,
            hostContext,
            compendium);

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot? partyRoster,
        RuntimeFieldSnapshot? field = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeWalletSnapshot? wallet = null,
        RuntimeSessionProgressSnapshot? session = null,
        IEnumerable<KeyValuePair<ContentId, string>>? hostContext = null,
        CompendiumStateSnapshot? compendium = null)
    {
        ArgumentNullException.ThrowIfNull(roster);

        RuntimeActorSnapshot playerSnapshot = roster.Player.Actor.State.ToSnapshot();
        IReadOnlyList<RuntimeActorSnapshot> actorSnapshots = roster.AllActors
            .Select(actor => actor.Actor.State.ToSnapshot())
            .ToArray();
        RuntimeActorReferenceSnapshot playerReference = Reference(playerSnapshot);
        return BuildStartupSaveSnapshot(
            actorSnapshots,
            playerReference,
            partyRoster,
            field,
            knowledge,
            inventory,
            wallet,
            session,
            hostContext,
            compendium);
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        CatalogBattleActor actor,
        RuntimeFieldSnapshot? field = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeWalletSnapshot? wallet = null,
        RuntimeSessionProgressSnapshot? session = null,
        IEnumerable<KeyValuePair<ContentId, string>>? hostContext = null,
        CompendiumStateSnapshot? compendium = null)
    {
        RuntimeActorSnapshot actorSnapshot = CreateActorSnapshot(
            actor,
            RuntimeInstanceId.Parse("echo_adept"),
            new RuntimeProgressionSnapshot(actor.Entity.BaseLevel, 0, 0, actor.Entity.BaseLevel - 1));
        RuntimeActorReferenceSnapshot actorReference = Reference(actorSnapshot);
        return BuildStartupSaveSnapshot(
            [actorSnapshot],
            actorReference,
            null,
            field,
            knowledge,
            inventory,
            wallet,
            session,
            hostContext,
            compendium);
    }

    public static RuntimeSaveGameSnapshot BuildStartupSaveSnapshot(
        IReadOnlyList<RuntimeActorSnapshot> actors,
        RuntimeActorReferenceSnapshot playerReference,
        RuntimePartyRosterSnapshot? partyRoster = null,
        RuntimeFieldSnapshot? field = null,
        RuntimeKnowledgeSnapshot? knowledge = null,
        RuntimeInventorySnapshot? inventory = null,
        RuntimeWalletSnapshot? wallet = null,
        RuntimeSessionProgressSnapshot? session = null,
        IEnumerable<KeyValuePair<ContentId, string>>? hostContext = null,
        CompendiumStateSnapshot? compendium = null)
    {
        RuntimeActorSnapshot playerSnapshot = actors.First(actor =>
            actor.Identity.InstanceId == playerReference.InstanceId);
        var hostContextEntries = new List<KeyValuePair<ContentId, string>>
        {
            new(ContentId.Parse("host_mode"), "clean_training_annex_play")
        };
        if (hostContext is not null)
        {
            hostContextEntries.AddRange(hostContext);
        }

        return new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse("0.3.0"),
            [PackIdentity],
            actors,
            partyRoster ?? new RuntimePartyRosterSnapshot(
                playerReference,
                playerSnapshot.Progression.Level,
                activeParty: [playerReference]),
            inventory ?? new RuntimeInventorySnapshot(),
            new RuntimeEquipmentSnapshot(),
            wallet ?? new RuntimeWalletSnapshot(0),
            field,
            compendium ?? new CompendiumStateSnapshot(),
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
            hostContext: hostContextEntries);
    }

    public static ContentId Qualified(string localId) => ContentId.Parse($"{PackId}:{localId}");

    public static RuntimeProgressionSnapshot InitialProgression(EntityDefinition entity, int level) =>
        new(
            level,
            0,
            0,
            entity.EntityKindId == StandardProgressionIds.IndependentActor ? level - 1 : 0);

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

    public static RuntimeActorReferenceSnapshot Reference(TrainingAnnexRuntimeActor actor) =>
        Reference(actor.Actor.State.ToSnapshot());

    public static RuntimeActorCombatProfileCompositionResult ComposePlayerCombatProfile(
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        IRuntimeActorCombatProfileCompositionService compositionService,
        RuntimeEquipmentProfile equipmentProfile)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(partyRoster);
        ArgumentNullException.ThrowIfNull(compositionService);
        ArgumentNullException.ThrowIfNull(equipmentProfile);

        return compositionService.Compose(CreatePlayerCombatProfileCompositionRequest(
            roster,
            partyRoster,
            equipmentProfile));
    }

    public static RuntimeActorCombatProfileCompositionRequest CreatePlayerCombatProfileCompositionRequest(
        TrainingAnnexActorRoster roster,
        RuntimePartyRosterSnapshot partyRoster,
        RuntimeEquipmentProfile equipmentProfile)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(partyRoster);
        ArgumentNullException.ThrowIfNull(equipmentProfile);

        RuntimeActorReferenceSnapshot? activeReference = partyRoster.ActiveHostedEntity;
        RuntimeActorState? activeState = activeReference is null
            ? null
            : roster.AllActors
                .Select(member => member.Actor.State)
                .FirstOrDefault(state =>
                    state.InstanceId == activeReference.InstanceId &&
                    state.EntityId == activeReference.EntityDefinitionId);
        return new RuntimeActorCombatProfileCompositionRequest(
            roster.Player.Actor.State,
            RuntimeStatSourceKind.ActiveHostedEntity,
            MissingHostedEntityBehavior.RejectStatResolution,
            partyRoster,
            activeState is null ? [] : [activeState],
            equipmentProfile.StatModifiers);
    }

    public static IReadOnlyDictionary<ContentId, decimal> InitialBaseResourceValues(int level) =>
        new Dictionary<ContentId, decimal>
        {
            [Hp] = 40 + level * 5,
            [Sp] = 10 + level * 2
        };

    private static GrowthRulesetServices? BindGrowthServices(GameDataCatalog catalog, List<string> diagnostics)
    {
        RulesetBindingResult<GrowthRulesetServices> growth = new RuntimeRulesetBindingResolver(
            RuntimeRulesetPolicyFactoryRegistry.CreateStandard())
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

    private static StatRulesetServices? BindStatServices(
        GameDataCatalog catalog,
        List<string> diagnostics)
    {
        RulesetBindingResult<StatRulesetServices> stats = new RuntimeRulesetBindingResolver(
            RuntimeRulesetPolicyFactoryRegistry.CreateStandard())
            .BindStatServices(catalog, Qualified("standard_stat"));
        if (stats.IsSuccess)
        {
            return stats.RequireService();
        }

        foreach (RulesetBindingDiagnostic diagnostic in stats.Diagnostics)
        {
            diagnostics.Add($"[stat:{diagnostic.Code}] {diagnostic.Message}");
        }

        return null;
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
                ContentId.Parse("clean_training_annex_ai"),
                RuntimeInstanceId.Parse($"roster_{LocalId(encounterId)}")));
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
                    request with { InstanceId = RuntimeInstanceId.Parse($"enemy_{LocalId(request.EntityId)}") });
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
                resource.Maximum)),
            TrainingAnnexHostSupport.InitialBaseResourceValues(level));
    }
}

internal sealed class TrainingAnnexMinimumRandomSource : IRandomSource
{
    public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

    public decimal NextUnitDecimal() => 0m;
}

internal sealed class TrainingAnnexFirstTargetSelectionPolicy : IRandomTargetSelectionPolicy
{
    public IReadOnlyList<RuntimeActorState> Select(
        IReadOnlyList<RuntimeActorState> candidates,
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
