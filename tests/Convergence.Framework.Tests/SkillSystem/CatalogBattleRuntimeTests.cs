using System.Collections.ObjectModel;
using System.Reflection;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Battle;
using Convergence.Hosting;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class CatalogBattleRuntimeTests
{
    private static readonly ContentId Battle = Id("battle");
    private static readonly ContentId NormalBattle = Id("normal_battle");
    private static readonly ContentId NewMoon = Id("new_moon");
    private static readonly ContentId PlayerTeam = Id("player_team");
    private static readonly ContentId EnemyTeam = Id("enemy_team");

    [Fact]
    public void DemoPacks_LoadAndHydrateQualifiedOrderedActors()
    {
        GameDataCatalog catalog = LoadDemoCatalog();

        Assert.Contains(Id("convergence.skill_system_redesign_sample:ice_boost_sample"), catalog.Skills.Keys);
        Assert.Contains(Id("convergence.clean_battle_demo:frost_lance_demo"), catalog.Skills.Keys);
        Assert.Contains(Id("convergence.clean_battle_demo:frost_duelist_demo"), catalog.Entities.Keys);

        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);

        Assert.Equal(
        [
            Id("convergence.clean_battle_demo:ember_bolt_demo"),
            Id("convergence.clean_battle_demo:frost_lance_demo"),
            Id("convergence.skill_system_redesign_sample:ice_boost_sample")
        ], frost.SkillLoadout.Select(skill => skill.Id));
        Assert.Equal(2, frost.ActiveSkills.Count);
        Assert.Equal(Id("convergence.skill_system_redesign_sample:ice_boost_sample"),
            Assert.Single(frost.State.Passives.Entries).Skill.Id);
        Assert.Equal(ElementalAffinity.Resist, frost.State.DefenseProfile.GetElementalAffinity(DamageElement.Ice));
        Assert.Equal(80, frost.State.GetRequiredResource(Id("hp")).Maximum);
        Assert.Equal(36, frost.State.GetRequiredResource(Id("sp")).Maximum);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SkillDefinition>)frost.SkillLoadout).Add(frost.SkillLoadout[0]));
    }

    [Fact]
    public void CatalogActor_SkillViewsFollowTheCurrentRuntimeCombatProfile()
    {
        SkillDefinition first = Active("test.pack:first_profile_skill", DamageElement.Ice);
        SkillDefinition second = Active("test.pack:second_profile_skill", DamageElement.Fire);
        CatalogBattleActor actor = RuntimeCatalogActor(
            "profile_actor",
            "profile_actor",
            PlayerTeam,
            [first],
            catalogSkills: [first, second]);
        RuntimeResourceSnapshot[] resources = actor.State.Resources.Values
            .Select(resource => new RuntimeResourceSnapshot(
                resource.Id,
                resource.Current,
                resource.Maximum))
            .ToArray();

        actor.State.ApplyCombatProfile(
            actor.State.Stats,
            resources,
            actor.State.DefenseProfile,
            new RuntimeSkillStateSnapshot(
                [first.Id, second.Id],
                [second.Id]),
            [second],
            actor.State.InstanceId,
            actor.State.EntityId);

        Assert.Equal([second.Id], actor.SkillLoadout.Select(skill => skill.Id));
        Assert.Equal([second.Id], actor.ActiveSkills.Select(skill => skill.Id));
        Assert.True(actor.State.HasSkill(second.Id));
        Assert.False(actor.State.HasSkill(first.Id));
    }

    [Fact]
    public void Order7R4_FactoryActorAndAutomatedSelectionUseTheLiveEquipmentProfile()
    {
        SkillDefinition grantedSkill = Active("test.pack:equipment_skill", DamageElement.Ice);
        EntityDefinition entity = Entity("test.pack:equipment_actor", []);
        var skillRepository = new SkillRepository(grantedSkill);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            skillRepository,
            new TestInitializationPolicy());
        CatalogBattleActor actor = factory.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("equipment_actor"),
            PlayerTeam,
            1,
            IsDeployed: true,
            Id("test_host"))).RequireActor();
        CatalogBattleActor target = RuntimeCatalogActor(
            "equipment_target",
            "equipment_target",
            EnemyTeam);
        ContentId armorId = Id("test.pack:skill_armor");
        RuntimeInstanceId armorInstanceId = RuntimeInstanceId.Parse("skill-armor-001");
        var armor = new EquipmentDefinition(
            armorId,
            "Skill Armor",
            "Grants one active skill.",
            StandardEquipmentSlotIds.Armor,
            10,
            grantedSkillIds: [grantedSkill.Id],
            armor: new EquipmentArmorProfileDefinition(2, 1));
        var inventory = new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    StandardEquipmentSlotIds.Armor,
                    [new RuntimeEquipmentInstanceSnapshot(armorInstanceId, armorId)])
            ]);
        var equipped = new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<ContentId, RuntimeInstanceId>(
                StandardEquipmentSlotIds.Armor,
                armorInstanceId)
        ]);
        var equipmentRepository = new EquipmentRepository(armor);
        var equipmentProfiles = new RuntimeActorEquipmentProfileSource(
            inventory,
            equipmentRepository);
        var equipmentApplication = new RuntimeActorEquipmentApplicationService(
            new RuntimeActorCombatProfileCompositionService(skillRepository));
        ApplyEquipment(equipped);
        var executor = new SkillExecutor(Services(LoadDemoCatalog()));
        var selector = new DeterministicBattleActionSelector(executor);

        BattleActionSelection selected = selector.Select(new BattleActionSelectionRequest(
            actor,
            [actor, target],
            Battle,
            NormalBattle,
            NewMoon,
            KnowledgeView(),
            activeStatModifierBoundaries: null,
            equipmentProfiles));
        ApplyEquipment(new RuntimeEquipmentSnapshot());
        BattleActionSelection afterUnequip = selector.Select(new BattleActionSelectionRequest(
            actor,
            [actor, target],
            Battle,
            NormalBattle,
            NewMoon,
            KnowledgeView(),
            activeStatModifierBoundaries: null,
            equipmentProfiles));

        Assert.Empty(actor.SkillLoadout);
        Assert.Equal(BattleActionSelectionStatus.Selected, selected.Status);
        Assert.NotNull(selected.Skill);
        Assert.Equal(grantedSkill.Id, selected.Skill.Id);
        Assert.True(selected.Assessment?.CanExecute);
        Assert.Equal(BattleActionSelectionStatus.Pass, afterUnequip.Status);
        Assert.Empty(actor.State.Skills.LearnedSkillIds);
        Assert.Empty(actor.State.Skills.EquippedSkillIds);

        void ApplyEquipment(RuntimeEquipmentSnapshot candidate)
        {
            RuntimeActorEquipmentApplicationResult result = equipmentApplication.Apply(
                new RuntimeActorEquipmentApplicationRequest(
                    actor.State,
                    inventory,
                    candidate,
                    equipmentRepository,
                    RuntimeStatSourceKind.Actor,
                    MissingHostedEntityBehavior.UseActorBaseStats,
                    runtimeActors: [actor.State, target.State]));
            Assert.True(
                result.Applied,
                string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        }
    }

    [Fact]
    public void ActorFactory_PreservesSameLevelUnlockOrderAndSuppressesFirstOccurrenceDuplicates()
    {
        SkillDefinition first = Active("test.pack:first", DamageElement.Fire);
        SkillDefinition second = Active("test.pack:second", DamageElement.Ice);
        SkillDefinition third = Active("test.pack:third", DamageElement.Wind);
        EntityDefinition entity = Entity(
            "test.pack:entity",
            [first.Id],
            [
                new SkillUnlockDefinition(5, first.Id),
                new SkillUnlockDefinition(5, second.Id),
                new SkillUnlockDefinition(5, third.Id)
            ]);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(first, second, third),
            new TestInitializationPolicy());

        CatalogBattleActor actor = factory.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            5,
            IsDeployed: true,
            Id("test_host"))).RequireActor();

        Assert.Equal([first.Id, second.Id, third.Id], actor.SkillLoadout.Select(skill => skill.Id));
    }

    [Fact]
    public void ActorFactory_AppliesMoveListCapacityToBaseSkillsAndStartingLevelUnlocks()
    {
        SkillDefinition first = Active("test.pack:first", DamageElement.Fire);
        SkillDefinition second = Active("test.pack:second", DamageElement.Ice);
        SkillDefinition third = Active("test.pack:third", DamageElement.Wind);
        EntityDefinition entity = Entity(
            "test.pack:entity",
            [first.Id],
            [
                new SkillUnlockDefinition(2, second.Id),
                new SkillUnlockDefinition(3, third.Id)
            ]);
        var constrained = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(first, second, third),
            new TestInitializationPolicy(),
            moveListCapacityPolicy: new SharedRuntimeMoveListCapacityPolicy(2));

        CatalogBattleActor actor = constrained.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            3,
            IsDeployed: true,
            Id("test_host"))).RequireActor();

        Assert.Equal([first.Id, second.Id], actor.State.Skills.EquippedSkillIds);
        RuntimePendingSkillChoiceSnapshot pending = Assert.Single(actor.State.Skills.PendingChoices);
        Assert.Equal(third.Id, pending.SkillId);
        Assert.Equal(3, pending.UnlockLevel);

        var insufficientForBase = new CatalogBattleActorFactory(
            new EntityRepository(Entity("test.pack:base_overflow", [first.Id, second.Id])),
            new SkillRepository(first, second),
            new TestInitializationPolicy(),
            moveListCapacityPolicy: new SharedRuntimeMoveListCapacityPolicy(1));
        CatalogBattleActorCreationResult rejected = insufficientForBase.Create(
            new CatalogBattleActorCreationRequest(
                Id("test.pack:base_overflow"),
                RuntimeInstanceId.Parse("overflow"),
                PlayerTeam,
                1,
                IsDeployed: true,
                Id("test_host")));

        Assert.False(rejected.IsSuccess);
        Assert.Equal(
            CatalogBattleActorDiagnosticCode.MoveListCapacityRejected,
            Assert.Single(rejected.Diagnostics).Code);
    }

    [Fact]
    public void ActorFactory_RestoreRejectsEquippedMovesBeyondItsSelectedCapacity()
    {
        SkillDefinition first = Active("test.pack:first", DamageElement.Fire);
        SkillDefinition second = Active("test.pack:second", DamageElement.Ice);
        EntityDefinition entity = Entity("test.pack:entity", [first.Id, second.Id]);
        var permissive = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(first, second),
            new TestInitializationPolicy(),
            moveListCapacityPolicy: new SharedRuntimeMoveListCapacityPolicy(2));
        RuntimeActorSnapshot snapshot = permissive.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            1,
            IsDeployed: true,
            Id("test_host"))).RequireActor().State.ToSnapshot();
        var constrained = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(first, second),
            new TestInitializationPolicy(),
            moveListCapacityPolicy: new SharedRuntimeMoveListCapacityPolicy(1));

        CatalogBattleActorCreationResult result = constrained.Restore(
            new CatalogBattleActorRestoreRequest(
                snapshot,
                RuntimeStatSourceKind.Actor,
                MissingHostedEntityBehavior.UseActorBaseStats));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CatalogBattleActorDiagnosticCode.SnapshotMoveListCapacityRejected,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ActorInitialization_DetachesAndProtectsBaseResourceValues()
    {
        var source = new Dictionary<ContentId, decimal>
        {
            [Id("hp")] = 24m
        };
        var initialization = new BattleActorInitialization(
            Id("hp"),
            [new BattleResourceState(Id("hp"), 24m, 24m)],
            source);

        source[Id("hp")] = 99m;

        Assert.Equal(24m, initialization.BaseResourceValues[Id("hp")]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ContentId, decimal>)initialization.BaseResourceValues).Add(Id("sp"), 12m));
    }

    [Fact]
    public void ActorFactory_ReturnsTypedDiagnosticsForInvalidRequestsAndMissingSkills()
    {
        EntityDefinition entity = Entity("test.pack:entity", [Id("test.pack:missing")]);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(),
            new TestInitializationPolicy());

        CatalogBattleActorCreationResult invalid = factory.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            0,
            IsDeployed: true,
            Id("test_host")));
        CatalogBattleActorCreationResult missingEntity = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:unknown"),
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            1,
            IsDeployed: true,
            Id("test_host")));

        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.InvalidLevel);
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.SkillMissing);
        Assert.Contains(missingEntity.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.EntityMissing);
        Assert.False(invalid.IsSuccess);
        Assert.Throws<CatalogBattleActorCreationException>(() => invalid.RequireActor());
    }

    [Fact]
    public void ActorFactory_RejectsDefaultIdentifiersBeforeRepositoryOrInitializationAccess()
    {
        EntityDefinition entity = Entity("test.pack:entity", []);
        var initialization = new RecordingInitializationPolicy();
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(),
            initialization);

        CatalogBattleActorCreationResult result = factory.Create(new CatalogBattleActorCreationRequest(
            default,
            default,
            default,
            1,
            IsDeployed: true,
            CommandAuthorityId: default(ContentId)));

        Assert.False(result.IsSuccess);
        Assert.Equal(4, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, diagnostic =>
            Assert.Equal(CatalogBattleActorDiagnosticCode.IdentifierInvalid, diagnostic.Code));
        Assert.Equal(0, initialization.CallCount);
    }

    [Fact]
    public void ActorFactory_RejectsMismatchedRequestAndProgressionLevelsBeforeInitialization()
    {
        EntityDefinition entity = Entity("test.pack:entity", []);
        var initialization = new RecordingInitializationPolicy();
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(),
            initialization);

        CatalogBattleActorCreationResult result = factory.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            5,
            IsDeployed: true,
            Id("test_host"),
            new RuntimeProgressionSnapshot(6, 0, 0, 0)));

        CatalogBattleActorDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CatalogBattleActorDiagnosticCode.ProgressionLevelMismatch, diagnostic.Code);
        Assert.Contains("'5'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'6'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(0, initialization.CallCount);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ActorFactory_UsesOneLevelForUnlocksInitializationAndRuntimeProgression()
    {
        SkillDefinition unlocked = Active("test.pack:unlocked", DamageElement.Ice);
        EntityDefinition entity = Entity(
            "test.pack:entity",
            [],
            [new SkillUnlockDefinition(5, unlocked.Id)]);
        var initialization = new RecordingInitializationPolicy();
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(unlocked),
            initialization);
        var progression = new RuntimeProgressionSnapshot(5, 12, 40, 2);

        CatalogBattleActor actor = factory.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            5,
            IsDeployed: true,
            Id("player_one"),
            progression)).RequireActor();

        Assert.Equal(1, initialization.CallCount);
        Assert.Equal(5, initialization.LastLevel);
        Assert.Same(progression, actor.State.Progression);
        Assert.Equal([unlocked.Id], actor.SkillLoadout.Select(skill => skill.Id));
        Assert.Equal(Id("player_one"), actor.State.Affiliation.CommandAuthorityId);
        Assert.Equal(PlayerTeam, actor.State.Affiliation.TeamId);
    }

    [Fact]
    public void ActorFactory_RejectsDuplicateInitializationResourcesWithoutThrowing()
    {
        EntityDefinition entity = Entity("test.pack:entity", []);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(),
            new DuplicateResourceInitializationPolicy());

        CatalogBattleActorCreationResult result = factory.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            1,
            IsDeployed: true,
            Id("test_host")));

        CatalogBattleActorDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CatalogBattleActorDiagnosticCode.InitializationResourceDuplicate, diagnostic.Code);
        Assert.Equal(Id("hp"), diagnostic.ResourceId);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ActorFactory_RejectsNullInitializationWithoutEscapingItsDiagnosticBoundary()
    {
        EntityDefinition entity = Entity("test.pack:entity", []);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(),
            new NullInitializationPolicy());

        CatalogBattleActorCreationResult result = factory.Create(new CatalogBattleActorCreationRequest(
            entity.Id,
            RuntimeInstanceId.Parse("instance"),
            PlayerTeam,
            1,
            IsDeployed: true,
            Id("test_host")));

        Assert.Equal(
            CatalogBattleActorDiagnosticCode.InitializationReturnedNull,
            Assert.Single(result.Diagnostics).Code);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ActorFactory_RestoreUsesCompleteSnapshotWithoutReinitializingRuntimeState()
    {
        EntityDefinition entity = Entity("test.pack:entity", []);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(),
            new ThrowingInitializationPolicy());
        var snapshot = new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse("saved:actor_1"),
                entity.Id,
                entity.EntityKindId,
                "Saved Actor"),
            new RuntimeActorAffiliationSnapshot(Id("host"), PlayerTeam),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false, HasSwappedThisTurn: true),
            new RuntimeProgressionSnapshot(9, 12, 100, 3),
            [
                new RuntimeResourceSnapshot(Id("life"), 7, 25),
                new RuntimeResourceSnapshot(Id("sp"), 4, 11)
            ],
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 5)],
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 8)]),
            new RuntimeSkillStateSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(
                statModifiers: PersistentModifiers(Id("attack"), 2),
                affinityOverrides:
                [
                    new RuntimeAffinityOverrideSnapshot(
                        DamageElement.Ice,
                        ElementalAffinity.Resist,
                        EncounterLifetime(new BattleDurationDefinition()))
                ],
                isGuarding: true),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(Id("life"), 20)],
            Id("life"));

        CatalogBattleActorCreationResult result = factory.Restore(ActorRestore(snapshot));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        RuntimeActorState state = result.RequireActor().State;
        Assert.Equal(Id("life"), state.VitalResourceId);
        Assert.Equal(7, state.GetRequiredResource(Id("life")).Current);
        Assert.Equal(9, state.Progression.Level);
        Assert.False(state.IsDeployed);
        Assert.True(state.EncounterPresence.HasSwappedThisTurn);
        Assert.True(state.IsGuarding);
        Assert.Equal(2, state.StatStages[Id("attack")].Stage);
        Assert.IsType<BattleDurationDefinition>(state.AffinityOverrides[DamageElement.Ice].Duration);
    }

    [Fact]
    public void ActorFactory_RestoreContainsDefaultSkillAndAilmentIdsInsideTypedDiagnostics()
    {
        EntityDefinition entity = Entity("test.pack:entity", []);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(entity),
            new SkillRepository(),
            new ThrowingInitializationPolicy());
        var snapshot = new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse("saved_actor"),
                entity.Id,
                entity.EntityKindId,
                "Saved Actor"),
            new RuntimeActorAffiliationSnapshot(Id("host"), PlayerTeam),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            new RuntimeProgressionSnapshot(1, 0, 0, 0),
            [new RuntimeResourceSnapshot(Id("hp"), 1, 1)],
            new RuntimeStatBlockSnapshot(),
            new RuntimeSkillStateSnapshot([default], [default]),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(
                ailments: [new RuntimeTimedStateSnapshot(
                    default,
                    EncounterLifetime(new BattleDurationDefinition()))]),
            new RuntimeBattleActivationSnapshot(),
            baseResourceValues: null,
            Id("hp"));

        CatalogBattleActorCreationResult result = factory.Restore(ActorRestore(snapshot));

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, diagnostic =>
            Assert.Equal(CatalogBattleActorDiagnosticCode.SnapshotInvalid, diagnostic.Code));
    }

    [Fact]
    public void ActorFactory_DirectRestoreValidatesPendingSkillCatalogAndUnlockProvenance()
    {
        SkillDefinition pendingSkill = Active("test.pack:pending", DamageElement.Ice);
        EntityDefinition validEntity = Entity(
            "test.pack:valid_entity",
            [],
            [new SkillUnlockDefinition(1, pendingSkill.Id)]);
        RuntimeActorSnapshot validSnapshot = WithPendingSkill(
            RestorableActorSnapshot("valid_actor", validEntity, CoreStats(5m)),
            unlockLevel: 1,
            pendingSkill.Id);
        var validFactory = new CatalogBattleActorFactory(
            new EntityRepository(validEntity),
            new SkillRepository(pendingSkill),
            new ThrowingInitializationPolicy());

        CatalogBattleActorCreationResult valid = validFactory.Restore(ActorRestore(validSnapshot));

        Assert.True(valid.IsSuccess);
        Assert.Equal(
            pendingSkill.Id,
            Assert.Single(valid.RequireActor().State.Skills.PendingChoices).SkillId);

        ContentId missingSkillId = Id("test.pack:missing_pending");
        EntityDefinition missingEntity = Entity(
            "test.pack:missing_entity",
            [],
            [new SkillUnlockDefinition(1, missingSkillId)]);
        CatalogBattleActorCreationResult missing = new CatalogBattleActorFactory(
            new EntityRepository(missingEntity),
            new SkillRepository(),
            new ThrowingInitializationPolicy()).Restore(ActorRestore(WithPendingSkill(
                RestorableActorSnapshot("missing_actor", missingEntity, CoreStats(5m)),
                unlockLevel: 1,
                missingSkillId)));
        Assert.Equal(
            CatalogBattleActorDiagnosticCode.SnapshotSkillMissing,
            Assert.Single(missing.Diagnostics).Code);

        EntityDefinition unauthoredEntity = Entity("test.pack:unauthored_entity", []);
        CatalogBattleActorCreationResult unauthored = new CatalogBattleActorFactory(
            new EntityRepository(unauthoredEntity),
            new SkillRepository(pendingSkill),
            new ThrowingInitializationPolicy()).Restore(ActorRestore(WithPendingSkill(
                RestorableActorSnapshot("unauthored_actor", unauthoredEntity, CoreStats(5m)),
                unlockLevel: 1,
                pendingSkill.Id)));
        Assert.Equal(
            CatalogBattleActorDiagnosticCode.SnapshotPendingSkillUnlockMismatch,
            Assert.Single(unauthored.Diagnostics).Code);

        EntityDefinition futureEntity = Entity(
            "test.pack:future_entity",
            [],
            [new SkillUnlockDefinition(2, pendingSkill.Id)]);
        CatalogBattleActorCreationResult future = new CatalogBattleActorFactory(
            new EntityRepository(futureEntity),
            new SkillRepository(pendingSkill),
            new ThrowingInitializationPolicy()).Restore(ActorRestore(WithPendingSkill(
                RestorableActorSnapshot("future_actor", futureEntity, CoreStats(5m)),
                unlockLevel: 2,
                pendingSkill.Id)));
        Assert.Equal(
            CatalogBattleActorDiagnosticCode.SnapshotPendingSkillLevelUnavailable,
            Assert.Single(future.Diagnostics).Code);
    }

    [Fact]
    public void ActorFactory_RestoreRecomposesVesselStatsInsteadOfTrustingSavedEffectiveValues()
    {
        EntityDefinition vesselEntity = Entity("test.pack:vessel", []);
        EntityDefinition hostedEntity = Entity("test.pack:hosted", []);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(vesselEntity, hostedEntity),
            new SkillRepository(),
            new ThrowingInitializationPolicy());
        RuntimeActorSnapshot hostedSnapshot = RestorableActorSnapshot(
            "saved_hosted",
            hostedEntity,
            CoreStats(20m));
        CatalogBattleActorCreationResult hostedRestore = factory.Restore(ActorRestore(hostedSnapshot));
        RuntimeActorState hostedState = hostedRestore.RequireActor().State;
        RuntimeActorReferenceSnapshot hostedReference = new(
            hostedState.InstanceId,
            hostedState.EntityId,
            hostedState.Identity.DisplayName);
        RuntimeActorSnapshot vesselSnapshot = RestorableActorSnapshot(
            "saved_vessel",
            vesselEntity,
            CoreStats(5m),
            effectiveStats: CoreStats(999m),
            combatProfileIdentity: new RuntimeCombatProfileIdentitySnapshot(
                hostedSnapshot.Identity.InstanceId,
                hostedSnapshot.Identity.EntityDefinitionId,
                revision: 4));
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vesselSnapshot, hostedReference);

        CatalogBattleActorCreationResult vesselRestore = factory.Restore(
            new CatalogBattleActorRestoreRequest(
                vesselSnapshot,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster,
                [hostedState]));

        Assert.True(
            vesselRestore.IsSuccess,
            string.Join(Environment.NewLine, vesselRestore.Diagnostics.Select(item => item.Message)));
        RuntimeActorState vesselState = vesselRestore.RequireActor().State;
        Assert.Equal(20m, vesselState.Stats[StandardProgressionIds.Strength]);
        Assert.Equal(20m, vesselState.Stats[StandardProgressionIds.Magic]);
        Assert.DoesNotContain(999m, vesselState.Stats.Values);
    }

    [Fact]
    public void ActorFactory_PublicRestoreSurfaceCannotBypassStatComposition()
    {
        Assembly assembly = typeof(CatalogBattleActorRestoreRequest).Assembly;
        Type requestType = typeof(CatalogBattleActorRestoreRequest);

        Assert.DoesNotContain(
            assembly.GetExportedTypes(),
            type => type.Name == "CatalogBattleActorRestoreMode");
        Assert.DoesNotContain(
            requestType.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => property.Name is "Mode" or "PreserveValidatedSnapshot");
        Assert.All(
            requestType.GetConstructors(BindingFlags.Instance | BindingFlags.Public),
            constructor => Assert.DoesNotContain(
                constructor.GetParameters(),
                parameter =>
                    parameter.Name?.Contains("mode", StringComparison.OrdinalIgnoreCase) == true ||
                    parameter.Name?.Contains("preserve", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public void ActorFactory_RestoreReportsTypedDiagnosticWhenHostedStateIsRequiredButMissing()
    {
        EntityDefinition vesselEntity = Entity("test.pack:vessel", []);
        EntityDefinition hostedEntity = Entity("test.pack:hosted", []);
        var factory = new CatalogBattleActorFactory(
            new EntityRepository(vesselEntity, hostedEntity),
            new SkillRepository(),
            new ThrowingInitializationPolicy());
        RuntimeActorReferenceSnapshot hostedReference = new(
            RuntimeInstanceId.Parse("saved_hosted"),
            hostedEntity.Id,
            "Hosted");
        RuntimeActorSnapshot vesselSnapshot = RestorableActorSnapshot(
            "saved_vessel",
            vesselEntity,
            CoreStats(5m));
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vesselSnapshot, hostedReference);

        CatalogBattleActorCreationResult result = factory.Restore(
            new CatalogBattleActorRestoreRequest(
                vesselSnapshot,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                partyRoster: partyRoster));

        Assert.False(result.IsSuccess);
        CatalogBattleActorDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CatalogBattleActorDiagnosticCode.SnapshotCombatProfileCompositionFailed, diagnostic.Code);
        Assert.Contains("has no runtime state", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Selector_UsesSharedAssessmentKnowledgeAndAuthoredTieOrder()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var selector = new DeterministicBattleActionSelector(executor);
        BattleActionSelectionRequest Request(IBattleKnowledgeView knowledge) => new(
            frost, [frost, ember], Battle, NormalBattle, NewMoon, knowledge);

        BattleActionSelection first = selector.Select(Request(KnowledgeView()));
        BattleActionSelection afterResistance = selector.Select(Request(KnowledgeView(
            (ember, DamageElement.Fire, ElementalAffinity.Resist))));
        BattleActionSelection afterNull = selector.Select(Request(KnowledgeView(
            (ember, DamageElement.Fire, ElementalAffinity.Resist),
            (ember, DamageElement.Ice, ElementalAffinity.Null))));

        Assert.Equal(Id("convergence.clean_battle_demo:ember_bolt_demo"), first.Skill!.Id);
        Assert.Equal(Id("convergence.clean_battle_demo:frost_lance_demo"), afterResistance.Skill!.Id);
        Assert.True(afterResistance.Assessment!.CanExecute);
        Assert.Equal(Id("convergence.clean_battle_demo:ember_bolt_demo"), afterNull.Skill!.Id);
    }

    [Fact]
    public void Selector_UsesStableEncounterFactsButDoesNotTreatTemporaryObservationsAsTimeless()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var selector = new DeterministicBattleActionSelector(new SkillExecutor(services));
        BattleActionSelection Select(BattleDefenseInfluence influences) => selector.Select(
            new BattleActionSelectionRequest(
                frost,
                [frost, ember],
                Battle,
                NormalBattle,
                NewMoon,
                new BattleKnowledgeView(
                    new RuntimeKnowledgeSnapshot(),
                    new RuntimeEncounterKnowledgeSnapshot(
                        elemental:
                        [
                            new EncounterElementalKnowledgeEntry(
                                ember.State.InstanceId,
                                ember.State.CombatProfileIdentity,
                                DamageElement.Fire,
                                ElementalAffinity.Resist,
                                influences)
                        ]))));

        BattleActionSelection stable = Select(BattleDefenseInfluence.None);
        BattleActionSelection temporary = Select(BattleDefenseInfluence.AffinityOverride);

        Assert.Equal(Id("convergence.clean_battle_demo:frost_lance_demo"), stable.Skill!.Id);
        Assert.Equal(Id("convergence.clean_battle_demo:ember_bolt_demo"), temporary.Skill!.Id);
    }

    [Theory]
    [InlineData(TargetSelection.All)]
    [InlineData(TargetSelection.Random)]
    public void Selector_RejectsMultiTargetSkillWhenAnyResolvedTargetHasKnownBlockingAffinity(
        TargetSelection selection)
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        TargetCountDefinition? count = selection == TargetSelection.Random
            ? new TargetCountDefinition(2, 2)
            : null;
        SkillDefinition unsafeFire = Active(
            "unsafe_fire",
            DamageElement.Fire,
            new TargetingDefinition(
                TargetRelation.Enemy,
                selection,
                TargetLifeState.Alive,
                false,
                count));
        SkillDefinition safeIce = Active("safe_ice", DamageElement.Ice);
        CatalogBattleActor actor = RuntimeCatalogActor(
            "selector_actor",
            "selector_actor",
            PlayerTeam,
            [unsafeFire, safeIce]);
        CatalogBattleActor firstTarget = RuntimeCatalogActor(
            "first_target",
            "first_target",
            EnemyTeam);
        CatalogBattleActor secondSafeTarget = RuntimeCatalogActor(
            "second_safe_target",
            "second_safe_target",
            EnemyTeam);
        CatalogBattleActor blockingTarget = RuntimeCatalogActor(
            "blocking_target",
            "blocking_target",
            EnemyTeam);
        var executor = new SkillExecutor(Services(catalog));
        var selector = new DeterministicBattleActionSelector(executor);

        BattleActionSelection result = selector.Select(new BattleActionSelectionRequest(
            actor,
            [actor, firstTarget, secondSafeTarget, blockingTarget],
            Battle,
            NormalBattle,
            NewMoon,
            KnowledgeView(
                (firstTarget, DamageElement.Fire, ElementalAffinity.Weak),
                (blockingTarget, DamageElement.Fire, ElementalAffinity.Null))));

        Assert.Equal(BattleActionSelectionStatus.Selected, result.Status);
        Assert.Equal(safeIce.Id, result.Skill!.Id);
    }

    [Fact]
    public void Runner_ExecutesDeterministicKnowledgePassiveAndActionTokenLifecycle()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var runner = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services);

        AutomatedBattleResult result = runner.Run(new AutomatedBattleRequest(
            [frost, ember], Battle, NormalBattle, NewMoon, 10));

        Assert.Equal(AutomatedBattleOutcome.Victory, result.Outcome);
        Assert.Equal(PlayerTeam, result.WinningTeamId);
        Assert.Equal(
            Id("convergence.clean_battle_demo:ember_bolt_demo"),
            result.Events.First(battleEvent => battleEvent.Kind == BattleEncounterEventKind.CommandSelected).SourceId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected &&
            battleEvent.SourceId == Id("convergence.clean_battle_demo:frost_lance_demo"));
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.PassiveActivated &&
            battleEvent.SourceId == Id("convergence.clean_battle_demo:regenerate_demo"));
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.EffectResolved &&
            battleEvent.DebugText?.Contains("Weakness", StringComparison.Ordinal) == true);
        Assert.True(result.Events.Select(battleEvent => battleEvent.Sequence).SequenceEqual(
            Enumerable.Range(1, result.Events.Count)));
        Assert.True(result.FinalActors.Single(actor => actor.TeamId == EnemyTeam).IsDefeated);
    }

    [Fact]
    public async Task RunnerAsync_ReturnsTheCompleteCanonicalEncounterEventStream()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        AutomatedBattleRunner runner = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services);

        AutomatedBattleResult result = await runner.RunAsync(new AutomatedBattleRequest(
            [frost, ember], Battle, NormalBattle, NewMoon, 10));

        Assert.Equal(AutomatedBattleOutcome.Victory, result.Outcome);
        Assert.True(result.Events.Select(battleEvent => battleEvent.Sequence).SequenceEqual(
            Enumerable.Range(1, result.Events.Count)));
        BattleEncounterEvent turnStarted = result.Events.First(
            battleEvent => battleEvent.Kind == BattleEncounterEventKind.TurnStarted &&
                           battleEvent.ActorId == frost.State.InstanceId);
        Assert.IsType<BattleTurnStartedEventPayload>(turnStarted.Payload);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnEnded &&
            battleEvent.ActorId == frost.State.InstanceId &&
            battleEvent.Payload is BattleTurnEndedEventPayload
            {
                Reason: BattleEncounterTurnEndReason.CommandCommitted
            });
        Assert.IsType<BattleEndedEventPayload>(
            result.Events.Last(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.BattleEnded).Payload);
    }

    [Fact]
    public void Runner_ExecutesUntargetedCustomSkillsWithoutFabricatingATarget()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        ContentId handlerId = Id("untargeted_resource_change");
        SkillDefinition skill = UntargetedActive(
            "test.pack:untargeted_resource_skill",
            new CustomEffectDefinition(handlerId));
        CatalogBattleActor player = RuntimeCatalogActor(
            "untargeted_player",
            "untargeted_player",
            PlayerTeam,
            [skill]);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "untargeted_enemy",
            "untargeted_enemy",
            EnemyTeam);
        BattleExecutionServices services = Services(
            catalog,
            customEffects:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(
                    handlerId,
                    new ResourceChangingCustomEffectHandler(Id("sp"), -1m))
            ]);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                null,
                1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Equal(19m, player.State.GetRequiredResource(Id("sp")).Current);
        BattleCommandSelectedEventPayload selected = Assert.IsType<BattleCommandSelectedEventPayload>(
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.CommandSelected).Payload);
        Assert.Null(selected.TargetId);
        BattleEffectResolvedEventPayload resolved = Assert.IsType<BattleEffectResolvedEventPayload>(
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.EffectResolved).Payload);
        Assert.Null(resolved.Result.TargetId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ResourceChanged &&
            battleEvent.ActorId == player.State.InstanceId &&
            battleEvent.TargetId == player.State.InstanceId &&
            battleEvent.SourceId == skill.Id &&
            battleEvent.Value == -1m);
    }

    [Fact]
    public void Runner_PublishesUntargetedSkillHostActionRequests()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        ContentId handlerId = Id("host_request_handler");
        ContentId hostActionId = Id("open_host_sequence");
        SkillDefinition skill = UntargetedActive(
            "test.pack:host_request_skill",
            new CustomEffectDefinition(handlerId));
        CatalogBattleActor player = RuntimeCatalogActor(
            "host_request_player",
            "host_request_player",
            PlayerTeam,
            [skill]);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "host_request_enemy",
            "host_request_enemy",
            EnemyTeam);
        BattleExecutionServices services = Services(
            catalog,
            customEffects:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(
                    handlerId,
                    new HostRequestCustomEffectHandler(hostActionId))
            ]);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                null,
                1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        BattleEncounterEvent hostRequest = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.HostActionRequested);
        var payload = Assert.IsType<BattleHostActionRequestedEventPayload>(hostRequest.Payload);
        Assert.Equal(player.State.InstanceId, payload.ActorId);
        Assert.Equal(hostActionId, payload.ActionId);
        Assert.Null(payload.TargetId);
        Assert.True(
            result.Events.Single(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.EffectResolved).Sequence <
            hostRequest.Sequence);
    }

    [Fact]
    public void Runner_MapsSuccessfulUntargetedEscapeToTheEncounterOutcome()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        ContentId escapeRuleId = Id("always_escape");
        SkillDefinition skill = UntargetedActive(
            "test.pack:escape_skill",
            new EscapeEffectDefinition(escapeRuleId, 100));
        CatalogBattleActor player = RuntimeCatalogActor(
            "escape_skill_player",
            "escape_skill_player",
            PlayerTeam,
            [skill]);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "escape_skill_enemy",
            "escape_skill_enemy",
            EnemyTeam);
        BattleExecutionServices services = Services(
            catalog,
            escapeRules:
            [
                new KeyValuePair<ContentId, IEscapeRuleHandler>(
                    escapeRuleId,
                    new AlwaysEscapeRuleHandler())
            ]);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                null,
                5));

        Assert.Equal(AutomatedBattleOutcome.Escape, result.Outcome);
        BattleCommandSelectedEventPayload selected = Assert.IsType<BattleCommandSelectedEventPayload>(
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.CommandSelected).Payload);
        Assert.Null(selected.TargetId);
        BattleEffectResolvedEventPayload resolved = Assert.IsType<BattleEffectResolvedEventPayload>(
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.EffectResolved).Payload);
        Assert.True(resolved.Result.EscapeRequested);
        Assert.Equal(
            BattleEncounterOutcome.Escape,
            Assert.IsType<BattleEndedEventPayload>(
                Assert.Single(result.Events, battleEvent =>
                    battleEvent.Kind == BattleEncounterEventKind.BattleEnded).Payload).Outcome);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.RoundEnded);
    }

    [Fact]
    public async Task RunnerAsync_PreCancelledTokenDoesNotMutateParticipants()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        RuntimeActorSnapshot frostBefore = frost.State.ToSnapshot();
        RuntimeActorSnapshot emberBefore = ember.State.ToSnapshot();
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        AutomatedBattleRunner runner = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunAsync(
                new AutomatedBattleRequest(
                    [frost, ember], Battle, NormalBattle, NewMoon, 10),
                cancellation.Token).AsTask());

        AssertActorStateUnchanged(frostBefore, frost.State.ToSnapshot());
        AssertActorStateUnchanged(emberBefore, ember.State.ToSnapshot());

        static void AssertActorStateUnchanged(
            RuntimeActorSnapshot before,
            RuntimeActorSnapshot after)
        {
            Assert.Equal(before.Identity, after.Identity);
            Assert.Equal(before.Affiliation, after.Affiliation);
            Assert.Equal(before.EncounterPresence, after.EncounterPresence);
            Assert.Equal(before.Progression, after.Progression);
            Assert.Equal(before.Resources, after.Resources);
            Assert.Equal(before.Stats.BaseStats, after.Stats.BaseStats);
            Assert.Equal(before.Stats.EffectiveStats, after.Stats.EffectiveStats);
            Assert.Equal(before.Skills.LearnedSkillIds, after.Skills.LearnedSkillIds);
            Assert.Equal(before.Skills.EquippedSkillIds, after.Skills.EquippedSkillIds);
            Assert.Equal(before.Skills.PendingChoices, after.Skills.PendingChoices);
            Assert.Equal(before.BattleStatus.Ailments, after.BattleStatus.Ailments);
            Assert.Equal(before.BattleStatus.Statuses, after.BattleStatus.Statuses);
            Assert.Equal(before.BattleStatus.Charges, after.BattleStatus.Charges);
            Assert.Equal(before.BattleStatus.Shields, after.BattleStatus.Shields);
            Assert.Equal(before.BattleStatus.AffinityOverrides, after.BattleStatus.AffinityOverrides);
            Assert.Equal(before.BattleStatus.AffinityBreaks, after.BattleStatus.AffinityBreaks);
            Assert.Equal(before.BattleStatus.IsGuarding, after.BattleStatus.IsGuarding);
            Assert.Equal(
                before.BattleActivations.PassiveActivations,
                after.BattleActivations.PassiveActivations);
        }
    }

    [Fact]
    public void Runner_SynchronousCompatibilityPathRestoresTheCallerContext()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        AutomatedBattleRunner runner = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services);
        var context = new NonPumpingSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;

        try
        {
            SynchronizationContext.SetSynchronizationContext(context);

            AutomatedBattleResult result = runner.Run(new AutomatedBattleRequest(
                [frost, ember], Battle, NormalBattle, NewMoon, 10));

            Assert.Equal(AutomatedBattleOutcome.Victory, result.Outcome);
            Assert.Same(context, SynchronizationContext.Current);
            Assert.Equal(0, context.PostCount);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void Runner_SharesEncounterKnowledgeWithinTeamAndReturnsImmutableEvidence()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition openingFire = Active("test.pack:opening_fire", DamageElement.Fire);
        SkillDefinition followupFire = Active("test.pack:followup_fire", DamageElement.Fire);
        SkillDefinition followupIce = Active("test.pack:followup_ice", DamageElement.Ice);
        CatalogBattleActor scout = RuntimeCatalogActor(
            "knowledge_scout",
            "knowledge_scout",
            PlayerTeam,
            [openingFire]);
        CatalogBattleActor teammate = RuntimeCatalogActor(
            "knowledge_teammate",
            "knowledge_teammate",
            PlayerTeam,
            [followupFire, followupIce]);
        CatalogBattleActor target = RuntimeCatalogActor(
            "knowledge_target",
            "knowledge_target",
            EnemyTeam,
            defense: new CombatDefenseProfile(
            [
                new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Fire,
                    ElementalAffinity.Resist)
            ]));
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [scout, teammate, target], Battle, NormalBattle, null, 1));

        ContentId[] playerSelections = result.Events
            .Where(battleEvent => battleEvent.Kind == BattleEncounterEventKind.CommandSelected &&
                                  battleEvent.ActorId is RuntimeInstanceId actorId &&
                                  actorId != target.State.InstanceId)
            .Select(battleEvent => battleEvent.SourceId!.Value)
            .ToArray();
        Assert.Equal([openingFire.Id, followupIce.Id], playerSelections);
        EncounterElementalKnowledgeEntry learned = Assert.Single(
            result.TeamKnowledge[PlayerTeam].Elemental,
            entry => entry.Element == DamageElement.Fire);
        Assert.Equal(target.State.InstanceId, learned.TargetInstanceId);
        Assert.Equal(target.Entity.Id, learned.TargetEntityId);
        Assert.Equal(DamageElement.Fire, learned.Element);
        Assert.Equal(ElementalAffinity.Resist, learned.Affinity);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ContentId, RuntimeEncounterKnowledgeSnapshot>)result.TeamKnowledge).Add(
                Id("test.pack:other_team"),
                RuntimeEncounterKnowledgeSnapshot.Empty));
    }

    [Fact]
    public void Runner_StartsFreshUnlessTheHostExplicitlySeedsTeamKnowledge()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition fire = Active("test.pack:fresh_fire", DamageElement.Fire);
        SkillDefinition ice = Active("test.pack:fresh_ice", DamageElement.Ice);

        (CatalogBattleActor Actor, CatalogBattleActor Target) Actors() =>
        (
            RuntimeCatalogActor("fresh_actor", "fresh_actor", PlayerTeam, [fire, ice]),
            RuntimeCatalogActor(
                "fresh_target",
                "fresh_target",
                EnemyTeam,
                defense: new CombatDefenseProfile(
                [
                    new KeyValuePair<DamageElement, ElementalAffinity>(
                        DamageElement.Fire,
                        ElementalAffinity.Resist)
                ]))
        );

        AutomatedBattleResult Run(
            CatalogBattleActor actor,
            CatalogBattleActor target,
            RuntimeEncounterKnowledgeSnapshot? seed = null)
        {
            BattleExecutionServices services = Services(catalog);
            var executor = new SkillExecutor(services);
            var runner = CreateAutomatedRunner(
                executor,
                new DeterministicBattleActionSelector(executor),
                services);
            return seed is null
                ? runner.Run(new AutomatedBattleRequest([actor, target], Battle, NormalBattle, null, 1))
                : runner.Run(new AutomatedBattleRequest(
                    [actor, target],
                    Battle,
                    NormalBattle,
                    null,
                    1,
                    [new KeyValuePair<ContentId, RuntimeEncounterKnowledgeSnapshot>(PlayerTeam, seed)]));
        }

        (CatalogBattleActor firstActor, CatalogBattleActor firstTarget) = Actors();
        AutomatedBattleResult first = Run(firstActor, firstTarget);
        (CatalogBattleActor secondActor, CatalogBattleActor secondTarget) = Actors();
        AutomatedBattleResult second = Run(secondActor, secondTarget);
        (CatalogBattleActor seededActor, CatalogBattleActor seededTarget) = Actors();
        AutomatedBattleResult seeded = Run(
            seededActor,
            seededTarget,
            first.TeamKnowledge[PlayerTeam]);

        Assert.Equal(fire.Id, FirstSelectedSkill(first, firstActor));
        Assert.Equal(fire.Id, FirstSelectedSkill(second, secondActor));
        Assert.Equal(ice.Id, FirstSelectedSkill(seeded, seededActor));
    }

    [Fact]
    public void Runner_MissedDamageDoesNotBecomeEncounterKnowledge()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition attack = Active("test.pack:missed_fire", DamageElement.Fire);
        CatalogBattleActor actor = RuntimeCatalogActor("miss_actor", "miss_actor", PlayerTeam, [attack]);
        CatalogBattleActor target = RuntimeCatalogActor(
            "miss_target",
            "miss_target",
            EnemyTeam,
            defense: new CombatDefenseProfile(
            [
                new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Fire,
                    ElementalAffinity.Weak)
            ]));
        BattleExecutionServices services = Services(catalog, damagePolicy: new AlwaysMissDamagePolicy());
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest([actor, target], Battle, NormalBattle, null, 1));

        Assert.Empty(result.TeamKnowledge[PlayerTeam].Elemental);
    }

    [Fact]
    public void Runner_SuppliesAllSeededKnowledgeDomainsToAReadOnlyStrategyView()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        ContentId ailmentId = Id("test.pack:seeded_ailment");
        CatalogBattleActor actor = RuntimeCatalogActor("seed_actor", "seed_actor", PlayerTeam);
        CatalogBattleActor target = RuntimeCatalogActor("seed_target", "seed_target", EnemyTeam);
        RuntimeEncounterKnowledgeSnapshot seed = new(
            ailments:
            [
                new EncounterAilmentKnowledgeEntry(
                    target.State.InstanceId,
                    target.State.CombatProfileIdentity,
                    ailmentId,
                    ResistanceLevel.Resistant)
            ],
            instantDeath:
            [
                new EncounterInstantDeathKnowledgeEntry(
                    target.State.InstanceId,
                    target.State.CombatProfileIdentity,
                    InstantDeathChannel.Light,
                    ResistanceLevel.Immune)
            ]);
        var selector = new RecordingAggregateKnowledgeSelector(
            actor.State.InstanceId,
            target.State.InstanceId,
            target.State.CombatProfileIdentity,
            ailmentId);
        BattleExecutionServices services = Services(catalog);

        AutomatedBattleResult result = CreateAutomatedRunner(
            new SkillExecutor(services),
            selector,
            services).Run(new AutomatedBattleRequest(
                [actor, target],
                Battle,
                NormalBattle,
                null,
                1,
                [new KeyValuePair<ContentId, RuntimeEncounterKnowledgeSnapshot>(PlayerTeam, seed)]));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.True(selector.ObservedAilment);
        Assert.True(selector.ObservedInstantDeath);
    }

    [Fact]
    public void AutomatedBattleRequest_RejectsKnowledgeSeedsForUnknownTeamsOrTargets()
    {
        CatalogBattleActor actor = RuntimeCatalogActor("seed_guard_actor", "seed_guard_actor", PlayerTeam);
        CatalogBattleActor target = RuntimeCatalogActor("seed_guard_target", "seed_guard_target", EnemyTeam);
        RuntimeEncounterKnowledgeSnapshot mismatchedTarget = new(
            elemental:
            [
                new EncounterElementalKnowledgeEntry(
                    RuntimeInstanceId.Parse("missing_target"),
                    target.State.CombatProfileIdentity,
                    DamageElement.Fire,
                    ElementalAffinity.Weak)
            ]);

        Assert.Throws<ArgumentException>(() => new AutomatedBattleRequest(
            [actor, target],
            Battle,
            NormalBattle,
            null,
            1,
            [new KeyValuePair<ContentId, RuntimeEncounterKnowledgeSnapshot>(PlayerTeam, mismatchedTarget)]));
        Assert.Throws<ArgumentException>(() => new AutomatedBattleRequest(
            [actor, target],
            Battle,
            NormalBattle,
            null,
            1,
            [new KeyValuePair<ContentId, RuntimeEncounterKnowledgeSnapshot>(
                Id("test.pack:unknown_team"),
                RuntimeEncounterKnowledgeSnapshot.Empty)]));
    }

    [Fact]
    public void AutomatedBattleRequest_RejectsMissingOrNullParticipantsAtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => new AutomatedBattleRequest(
            null!,
            Battle,
            NormalBattle,
            null,
            1));
        Assert.Throws<ArgumentException>(() => new AutomatedBattleRequest(
            [],
            Battle,
            NormalBattle,
            null,
            1));
        Assert.Throws<ArgumentException>(() => new AutomatedBattleRequest(
            [null!],
            Battle,
            NormalBattle,
            null,
            1));
    }

    [Fact]
    public void AutomatedBattleRequest_RejectsInvalidEncounterMetadataAtConstruction()
    {
        CatalogBattleActor actor = RuntimeCatalogActor(
            "invalid_request_actor",
            "invalid_request_actor",
            PlayerTeam);

        Assert.Throws<ArgumentException>(() => new AutomatedBattleRequest(
            [actor],
            default,
            NormalBattle,
            null,
            1));
        Assert.Throws<ArgumentException>(() => new AutomatedBattleRequest(
            [actor],
            Battle,
            default,
            null,
            1));
        Assert.Throws<ArgumentException>(() => new AutomatedBattleRequest(
            [actor],
            Battle,
            NormalBattle,
            (ContentId?)default(ContentId),
            1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutomatedBattleRequest(
            [actor],
            Battle,
            NormalBattle,
            null,
            0));
    }

    [Fact]
    public void AutomatedBattleRequest_RejectsMalformedIntrinsicElementSeedBeforeSelection()
    {
        CatalogBattleActor actor = RuntimeCatalogActor("almighty_seed_actor", "almighty_seed_actor", PlayerTeam);
        CatalogBattleActor target = RuntimeCatalogActor("almighty_seed_target", "almighty_seed_target", EnemyTeam);
        var validEntry = new EncounterElementalKnowledgeEntry(
            target.State.InstanceId,
            target.State.CombatProfileIdentity,
            DamageElement.Fire,
            ElementalAffinity.Weak);
        EncounterElementalKnowledgeEntry malformedEntry = CloneWithProperty(
            validEntry,
            nameof(EncounterElementalKnowledgeEntry.Element),
            DamageElement.Almighty);
        var validSnapshot = new RuntimeEncounterKnowledgeSnapshot([validEntry]);
        RuntimeEncounterKnowledgeSnapshot malformedSnapshot = CloneWithProperty(
            validSnapshot,
            nameof(RuntimeEncounterKnowledgeSnapshot.Elemental),
            (IReadOnlyList<EncounterElementalKnowledgeEntry>)Array.AsReadOnly([malformedEntry]));

        Assert.Throws<ArgumentException>(() => new AutomatedBattleRequest(
            [actor, target],
            Battle,
            NormalBattle,
            null,
            1,
            [new KeyValuePair<ContentId, RuntimeEncounterKnowledgeSnapshot>(
                PlayerTeam,
                malformedSnapshot)]));
    }

    [Fact]
    public void Runner_ReportsSignedResourceChangesForCostsDamageAndReflection()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition attack = Active(
            "test.pack:reflectable_attack",
            DamageElement.Fire,
            costs: [new SkillCostDefinition(Id("sp"), new FlatAmountDefinition(3))]);
        CatalogBattleActor attacker = RuntimeCatalogActor(
            "resource_attacker",
            "resource_attacker",
            PlayerTeam,
            [attack]);
        CatalogBattleActor reflector = RuntimeCatalogActor(
            "resource_reflector",
            "resource_reflector",
            EnemyTeam,
            defense: new CombatDefenseProfile(
                [new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Fire,
                    ElementalAffinity.Repel)]));
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var runner = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services);

        AutomatedBattleResult result = runner.Run(new AutomatedBattleRequest(
            [attacker, reflector], Battle, NormalBattle, null, 1));

        BattleEncounterEvent[] resourceChanges = result.Events.Where(battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ResourceChanged).ToArray();
        Assert.Equal(2, resourceChanges.Length);
        BattleEncounterEvent cost = resourceChanges[0];
        Assert.Equal(attacker.State.InstanceId, cost.ActorId);
        Assert.Equal(attacker.State.InstanceId, cost.TargetId);
        Assert.Equal(attack.Id, cost.SourceId);
        Assert.Equal(-3, cost.Value);
        BattleEncounterEvent reflected = resourceChanges[1];
        Assert.Equal(attacker.State.InstanceId, reflected.ActorId);
        Assert.Equal(attacker.State.InstanceId, reflected.TargetId);
        Assert.Equal(attack.Id, reflected.SourceId);
        Assert.Equal(-1, reflected.Value);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ResourceChanged &&
            battleEvent.TargetId == reflector.State.InstanceId);
    }

    [Fact]
    public void Runner_AllowsMissingMoonPhaseWhenContentDoesNotUseMoonConditions()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var runner = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services);

        AutomatedBattleResult result = runner.Run(new AutomatedBattleRequest(
            [frost, ember], Battle, NormalBattle, null, 10));

        Assert.Equal(AutomatedBattleOutcome.Victory, result.Outcome);
        Assert.Equal(PlayerTeam, result.WinningTeamId);
    }

    [Fact]
    public void Runner_HonorsRoundLimitWithDraw()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor, new DeterministicBattleActionSelector(executor), services).Run(
            new AutomatedBattleRequest([frost, ember], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Null(result.WinningTeamId);
        Assert.Null(result.FaultMessage);
        Assert.Null(result.FaultCode);
        Assert.Equal(
            "Battle ended in a draw after 1 round(s).",
            Assert.Single(result.Events, battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.BattleEnded).DebugText);
    }

    [Theory]
    [InlineData(BattleEncounterOutcome.Defeat, AutomatedBattleOutcome.Defeat)]
    [InlineData(BattleEncounterOutcome.Escape, AutomatedBattleOutcome.Escape)]
    [InlineData(BattleEncounterOutcome.Cancelled, AutomatedBattleOutcome.Cancelled)]
    public void Runner_PreservesEveryCanonicalTerminalOutcome(
        BattleEncounterOutcome encounterOutcome,
        AutomatedBattleOutcome expectedOutcome)
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor(
            "terminal_outcome_player",
            "terminal_outcome_player",
            PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "terminal_outcome_enemy",
            "terminal_outcome_enemy",
            EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.Skip));
        BattleEncounterCommandResult terminalResult = encounterOutcome switch
        {
            BattleEncounterOutcome.Defeat => BattleEncounterCommandResult.Executed(
                ActionTurnConsumption.Normal,
                requestedOutcome: BattleEncounterOutcome.Defeat,
                winningTeamId: EnemyTeam),
            BattleEncounterOutcome.Escape => BattleEncounterCommandResult.Executed(
                ActionTurnConsumption.Normal,
                requestedOutcome: BattleEncounterOutcome.Escape),
            BattleEncounterOutcome.Cancelled => BattleEncounterCommandResult.Cancelled(),
            _ => throw new InvalidOperationException(
                $"Unsupported test outcome '{encounterOutcome}'.")
        };

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services,
            lifecycle,
            restrictionResolver: new FixedRestrictionResultResolver(terminalResult)).Run(
            new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                NewMoon,
                1));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(
            encounterOutcome == BattleEncounterOutcome.Defeat ? EnemyTeam : null,
            result.WinningTeamId);
        Assert.Equal(
            encounterOutcome,
            Assert.IsType<BattleEndedEventPayload>(
                Assert.Single(result.Events, battleEvent =>
                    battleEvent.Kind == BattleEncounterEventKind.BattleEnded).Payload).Outcome);
    }

    [Fact]
    public void AutomatedBattleOutcome_PreservesPublishedNumericValues()
    {
        Assert.Equal(0, (int)AutomatedBattleOutcome.Victory);
        Assert.Equal(1, (int)AutomatedBattleOutcome.Draw);
        Assert.Equal(2, (int)AutomatedBattleOutcome.Faulted);
        Assert.Equal(3, (int)AutomatedBattleOutcome.Defeat);
        Assert.Equal(4, (int)AutomatedBattleOutcome.Escape);
        Assert.Equal(5, (int)AutomatedBattleOutcome.Cancelled);
    }

    [Fact]
    public void Runner_PropagatesDuplicateParticipantFaultBeforeAutomatedActorLookup()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor(
            "duplicate_player",
            "shared_automated_instance",
            PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "duplicate_enemy",
            "shared_automated_instance",
            EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                NewMoon,
                1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.DuplicateParticipantInstanceId, result.FaultCode);
        Assert.Contains("shared_automated_instance", result.FaultMessage, StringComparison.Ordinal);
        Assert.Equal(
            [BattleEncounterEventKind.BattleFaulted, BattleEncounterEventKind.BattleEnded],
            result.Events.Select(battleEvent => battleEvent.Kind));
        Assert.Equal(
            BattleEncounterFaultCode.DuplicateParticipantInstanceId,
            result.Events[0].FaultCode);
        Assert.Equal(BattleEncounterFaultCode.DuplicateParticipantInstanceId, result.Events[1].FaultCode);
        Assert.Equal(100, player.State.GetRequiredResource(Id("hp")).Current);
        Assert.Equal(100, enemy.State.GetRequiredResource(Id("hp")).Current);
    }

    [Fact]
    public void Runner_ExecutesTheSelectorsPreparedRandomAssessmentWithoutRerolling()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition randomAttack = Active(
            "test.pack:random_attack",
            DamageElement.Fire,
            new TargetingDefinition(
                TargetRelation.Enemy,
                TargetSelection.Random,
                TargetLifeState.Alive,
                false,
                new TargetCountDefinition(1, 1)));
        CatalogBattleActor player = RuntimeCatalogActor(
            "random_player",
            "random_player",
            PlayerTeam,
            [randomAttack]);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "random_enemy",
            "random_enemy",
            EnemyTeam,
            [randomAttack]);
        var randomTargets = new CountingRandomTargetPolicy();
        BattleExecutionServices services = Services(catalog, randomTargets);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                NewMoon,
                1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Equal(2, randomTargets.CallCount);
        Assert.Equal(2, result.Events.Count(battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.EffectResolved));
    }

    [Fact]
    public void Runner_ConsumesPhaseAndBattleDurationBoundariesWithoutClearingPermanentState()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        ContentId phaseStatus = Id("phase_status");
        ContentId battleStatus = Id("battle_status");
        ContentId permanentStatus = Id("permanent_status");
        frost.State.AddOtherStatus(phaseStatus, EncounterLifetime(new PhaseDurationDefinition(PlayerTeam)));
        frost.State.AddOtherStatus(battleStatus, EncounterLifetime(new BattleDurationDefinition()));
        frost.State.AddOtherStatus(permanentStatus, StandardStatusLifetimes.Persistent);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);

        CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [frost, ember],
                Battle,
                NormalBattle,
                NewMoon,
                1));

        Assert.DoesNotContain(phaseStatus, frost.State.OtherStatuses);
        Assert.DoesNotContain(battleStatus, frost.State.OtherStatuses);
        Assert.Contains(permanentStatus, frost.State.OtherStatuses);
    }

    [Fact]
    public void Runner_ExposesOnlyTheAuthoritiesItUses()
    {
        ConstructorInfo constructor = Assert.Single(typeof(AutomatedBattleRunner).GetConstructors());

        Assert.Equal(
        [
            typeof(ISkillExecutor),
            typeof(IBattleActionSelector),
            typeof(IBattleEncounterLifecyclePort),
            typeof(BattleTurnEconomyRuleset),
            typeof(IAutomatedBattleTurnRestrictionResolver),
            typeof(BattleEncounterProgressPolicy)
        ], constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void Runner_AutomatedSkillSelectionPreservesANewTimedModifierForItsApplicationTurn()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition focus = TimedModifierSkill("test.pack:automated_timed_focus");
        CatalogBattleActor player = RuntimeCatalogActor(
            "automated_timed_player",
            "automated_timed_player",
            PlayerTeam,
            [focus]);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "automated_timed_enemy",
            "automated_timed_enemy",
            EnemyTeam);
        var statModifiers = new RecordingStatModifierPolicyService(
            new StatModifierPolicyService(new TimedContributionStatModifierPolicy(
                Id("test.pack:automated_timed_contribution"))));
        BattleExecutionServices services = Services(catalog, statModifiers: statModifiers);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        AssertSameBoundaryTickPreservedOneTurnDuration(statModifiers);
    }

    [Fact]
    public void Runner_RestrictedSkillExecutionPreservesANewTimedModifierForItsApplicationTurn()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition focus = TimedModifierSkill("test.pack:restricted_timed_focus");
        CatalogBattleActor player = RuntimeCatalogActor(
            "restricted_timed_player",
            "restricted_timed_player",
            PlayerTeam,
            [focus]);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "restricted_timed_enemy",
            "restricted_timed_enemy",
            EnemyTeam);
        var statModifiers = new RecordingStatModifierPolicyService(
            new StatModifierPolicyService(new TimedContributionStatModifierPolicy(
                Id("test.pack:restricted_timed_contribution"))));
        BattleExecutionServices services = Services(catalog, statModifiers: statModifiers);
        var executor = new SkillExecutor(services);
        var source = new RecordingRestrictedActionSource(request =>
            AutomatedRestrictedActionSelection.Selected(
                focus.Id,
                new SkillBattleActionCommand(focus, [request.Actor.State.InstanceId])));
        var innerLifecycle = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            Id("battle_start"),
            Id("owner_turn_end"),
            TestEncounterClocks.Standard(PlayerTeam, EnemyTeam));
        var lifecycle = new RestrictedBattleStatusLifecyclePort(
            innerLifecycle,
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.LimitedAction, [focus.Id]));

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services,
            lifecycle,
            restrictionResolver: RestrictionResolver(executor, services, source)).Run(
            new AutomatedBattleRequest([player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Equal(1, source.CallCount);
        StatModifierLifecycleBoundary restrictedBoundary = Assert.Single(
            source.LastRequest!.Turn.ActiveStatModifierBoundaries);
        StatModifierLifecycleBoundary applicationBoundary = Assert.Single(
            statModifiers.ApplicationBoundaries);
        Assert.Equal(restrictedBoundary.EventId, applicationBoundary.EventId);
        Assert.Equal(restrictedBoundary.Sequence, applicationBoundary.Sequence);
        AssertSameBoundaryTickPreservedOneTurnDuration(statModifiers);
    }

    [Fact]
    public void Runner_SkipRestrictionConsumesTheTurnWithoutSelectingANormalAction()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition attack = Active("test.pack:skip_attack", DamageElement.Fire);
        CatalogBattleActor player = RuntimeCatalogActor("skip_player", "skip_player", PlayerTeam, [attack]);
        CatalogBattleActor enemy = RuntimeCatalogActor("skip_enemy", "skip_enemy", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.Skip));

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services,
            lifecycle).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Equal(100m, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected &&
            battleEvent.ActorId == player.State.InstanceId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandPassed &&
            battleEvent.ActorId == player.State.InstanceId);
    }

    [Fact]
    public void RestrictedActionSelection_RequiresCanonicalIdentityForEverySupportedCommandKind()
    {
        SkillDefinition skill = Active("test.pack:restricted_identity_skill", DamageElement.Physical);
        var item = new ItemDefinition(
            Id("test.pack:restricted_identity_item"),
            "Restricted Identity Item",
            "Identity-only test item.",
            ItemKind.Consumable,
            1,
            1m);
        RuntimeInstanceId targetId = RuntimeInstanceId.Parse("restricted_identity_target");
        ContentId basicAttackId = Id("test.pack:restricted_identity_attack");
        ContentId escapeRuleId = Id("test.pack:restricted_identity_escape_rule");
        var targeting = new TargetingDefinition(
            TargetRelation.Enemy,
            TargetSelection.Single,
            TargetLifeState.Alive,
            false);
        (ContentId ActionId, BattleActionCommand Command)[] cases =
        [
            (
                basicAttackId,
                new BasicAttackBattleActionCommand(
                    new EquipmentBasicAttackDefinition(
                        DamageElement.Physical,
                        1,
                        100,
                        new NeverCriticalDefinition(),
                        false),
                    targeting,
                    [targetId],
                    basicAttackId)),
            (skill.Id, new SkillBattleActionCommand(skill, [targetId])),
            (item.Id, new ItemBattleActionCommand(item, [targetId])),
            (Id("guard"), new GuardBattleActionCommand()),
            (Id("pass"), new PassBattleActionCommand()),
            (Id("analyze"), new AnalyzeBattleActionCommand(targetId, [AnalysisLayer.Affinities])),
            (Id("escape"), new EscapeAttemptBattleActionCommand(escapeRuleId, 100))
        ];

        foreach ((ContentId actionId, BattleActionCommand command) in cases)
        {
            AutomatedRestrictedActionSelection selected =
                AutomatedRestrictedActionSelection.Selected(actionId, command);

            Assert.Equal(actionId, selected.ActionId);
            Assert.Same(command, selected.Command);
            ArgumentException mismatch = Assert.Throws<ArgumentException>(() =>
                AutomatedRestrictedActionSelection.Selected(Id("test.pack:mismatched_action"), command));
            Assert.Equal("actionId", mismatch.ParamName);
            Assert.Contains("does not match typed command action ID", mismatch.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Runner_LimitedActionExecutesAnExplicitlyAllowedTypedCommand()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition attack = Active("test.pack:limited_strike", DamageElement.Physical);
        CatalogBattleActor player = RuntimeCatalogActor("limited_player", "limited_player", PlayerTeam, [attack]);
        CatalogBattleActor enemy = RuntimeCatalogActor("limited_enemy", "limited_enemy", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var source = new RecordingRestrictedActionSource(request =>
            AutomatedRestrictedActionSelection.Selected(
                attack.Id,
                new SkillBattleActionCommand(
                    attack,
                    [request.Participants.Single(actor => actor.State.TeamId == EnemyTeam).State.InstanceId])));
        var resolver = RestrictionResolver(skillExecutor, services, source);
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.LimitedAction, [attack.Id]));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle,
            restrictionResolver: resolver).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Equal(1, source.CallCount);
        Assert.Equal(BattleTurnStartOutcome.LimitedAction, source.LastRequest!.Turn.TurnStartOutcome);
        Assert.Equal(99m, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected &&
            battleEvent.ActorId == player.State.InstanceId &&
            battleEvent.SourceId == attack.Id);
    }

    [Fact]
    public void Runner_LimitedActionRejectsADisallowedCommandBeforeMutation()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition attack = Active("test.pack:forbidden_strike", DamageElement.Physical);
        CatalogBattleActor player = RuntimeCatalogActor("forbidden_player", "forbidden_player", PlayerTeam, [attack]);
        CatalogBattleActor enemy = RuntimeCatalogActor("forbidden_enemy", "forbidden_enemy", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var source = new RecordingRestrictedActionSource(request =>
            AutomatedRestrictedActionSelection.Selected(
                attack.Id,
                new SkillBattleActionCommand(
                    attack,
                    [request.Participants.Single(actor => actor.State.TeamId == EnemyTeam).State.InstanceId])));
        var resolver = RestrictionResolver(skillExecutor, services, source);
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.LimitedAction, [Id("guard")]));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle,
            restrictionResolver: resolver).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Contains("not allowed", result.FaultMessage, StringComparison.Ordinal);
        Assert.Equal(100m, enemy.State.GetRequiredResource(Id("hp")).Current);
    }

    [Fact]
    public void Runner_LimitedActionContainsAMismatchedTypedCommandAsAFaultBeforeExecution()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor(
            "mismatched_limited_player",
            "mismatched_limited_player",
            PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "mismatched_limited_enemy",
            "mismatched_limited_enemy",
            EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var source = new RecordingRestrictedActionSource(request =>
            AutomatedRestrictedActionSelection.Selected(
                Id("guard"),
                new AnalyzeBattleActionCommand(
                    request.Participants.Single(actor => actor.State.TeamId == EnemyTeam).State.InstanceId,
                    [AnalysisLayer.Affinities])));
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.LimitedAction, [Id("guard")]));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle,
            restrictionResolver: RestrictionResolver(skillExecutor, services, source)).Run(
            new AutomatedBattleRequest([player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Contains("does not match typed command action ID", result.FaultMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind is BattleEncounterEventKind.CommandSelected or
                BattleEncounterEventKind.EffectResolved);
        Assert.Equal(100m, player.State.GetRequiredResource(Id("hp")).Current);
        Assert.Equal(100m, enemy.State.GetRequiredResource(Id("hp")).Current);
    }

    [Fact]
    public void Runner_ForcedBasicAttackExecutesTheTypedBasicAttackSelectedByTheHostPolicy()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor("forced_player", "forced_player", PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor("forced_enemy", "forced_enemy", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var source = new RecordingRestrictedActionSource(request =>
            AutomatedRestrictedActionSelection.Selected(
                Id("basic_attack"),
                new BasicAttackBattleActionCommand(
                    new EquipmentBasicAttackDefinition(DamageElement.Physical, 10, 100, new NeverCriticalDefinition(), false),
                    new TargetingDefinition(
                        TargetRelation.Enemy,
                        TargetSelection.Single,
                        TargetLifeState.Alive,
                        false),
                    [request.Participants.Single(actor => actor.State.TeamId == EnemyTeam).State.InstanceId])));
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.ForcedBasicAttack));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle,
            restrictionResolver: RestrictionResolver(skillExecutor, services, source)).Run(
            new AutomatedBattleRequest([player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Equal(90m, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected &&
            battleEvent.ActorId == player.State.InstanceId &&
            battleEvent.SourceId == Id("basic_attack"));
    }

    [Fact]
    public void Runner_RestrictedActionEvidenceUpdatesTheActingTeamsEncounterKnowledge()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor("forced_learner", "forced_learner", PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "forced_observed",
            "forced_observed",
            EnemyTeam,
            defense: new CombatDefenseProfile(
            [
                new KeyValuePair<DamageElement, ElementalAffinity>(
                    DamageElement.Physical,
                    ElementalAffinity.Resist)
            ]));
        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var source = new RecordingRestrictedActionSource(request =>
            AutomatedRestrictedActionSelection.Selected(
                Id("forced_observation"),
                new BasicAttackBattleActionCommand(
                    new EquipmentBasicAttackDefinition(
                        DamageElement.Physical,
                        10,
                        100,
                        new NeverCriticalDefinition(),
                        false),
                    new TargetingDefinition(
                        TargetRelation.Enemy,
                        TargetSelection.Single,
                        TargetLifeState.Alive,
                        false),
                    [enemy.State.InstanceId],
                    Id("forced_observation"))));
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.ForcedBasicAttack));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle,
            restrictionResolver: RestrictionResolver(skillExecutor, services, source)).Run(
            new AutomatedBattleRequest([player, enemy], Battle, NormalBattle, null, 1));

        EncounterElementalKnowledgeEntry learned = Assert.Single(result.TeamKnowledge[PlayerTeam].Elemental);
        Assert.Equal(enemy.State.InstanceId, learned.TargetInstanceId);
        Assert.Equal(DamageElement.Physical, learned.Element);
        Assert.Equal(ElementalAffinity.Resist, learned.Affinity);
    }

    [Fact]
    public void Runner_ForcedConfusionExecutesTheTypedTargetChosenByTheHostPolicy()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor("confused_player", "confused_player", PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor("confused_enemy", "confused_enemy", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var source = new RecordingRestrictedActionSource(request =>
            AutomatedRestrictedActionSelection.Selected(
                Id("confused_attack"),
                new BasicAttackBattleActionCommand(
                    new EquipmentBasicAttackDefinition(DamageElement.Physical, 7, 100, new NeverCriticalDefinition(), false),
                    new TargetingDefinition(
                        TargetRelation.Any,
                        TargetSelection.Single,
                        TargetLifeState.Alive,
                        true),
                    [request.Actor.State.InstanceId],
                    Id("confused_attack"))));
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.ForcedConfusion));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle,
            restrictionResolver: RestrictionResolver(skillExecutor, services, source)).Run(
            new AutomatedBattleRequest([player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Equal(93m, player.State.GetRequiredResource(Id("hp")).Current);
        Assert.Equal(100m, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.EffectResolved &&
            battleEvent.TargetId == player.State.InstanceId);
    }

    [Theory]
    [InlineData(BattleTurnStartOutcome.FleeBattle, "fled the battle")]
    [InlineData(BattleTurnStartOutcome.RecallToRoster, "recalled to its roster")]
    public void Runner_ExitRestrictionsRemoveTheActorWithoutChangingPartyPlacement(
        BattleTurnStartOutcome outcome,
        string expectedMessage)
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor("leaving_player", "leaving_player", PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor("remaining_enemy", "remaining_enemy", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(outcome));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Victory, result.Outcome);
        Assert.Equal(EnemyTeam, result.WinningTeamId);
        BattleActorFinalSnapshot finalPlayer = result.FinalActors.Single(actor =>
            actor.InstanceId == player.State.InstanceId);
        Assert.False(finalPlayer.IsDeployed);
        BattleEncounterEvent presenceChanged = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.EncounterPresenceChanged &&
            battleEvent.ActorId == player.State.InstanceId &&
            battleEvent.DebugText?.Contains(expectedMessage, StringComparison.Ordinal) == true);
        Assert.Equal(player.State.InstanceId, presenceChanged.ActorId);
        Assert.False(
            Assert.IsType<BattleEncounterPresenceChangedEventPayload>(presenceChanged.Payload)
                .IsDeployed);
    }

    [Theory]
    [InlineData(false, StatusRemovalCause.Flee, "Flee")]
    [InlineData(true, StatusRemovalCause.RosterRecall, "RosterRecall")]
    public void Runner_CanonicalLifecycleCleansStatusesForTypedActorExit(
        bool canRecallToRoster,
        StatusRemovalCause removalCause,
        string expectedCleanupDetail)
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor(
            "canonical_exit_player",
            "canonical_exit_player",
            PlayerTeam,
            capabilityIds: canRecallToRoster ? [Id("recall_to_roster")] : []);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "canonical_exit_enemy",
            "canonical_exit_enemy",
            EnemyTeam);
        ContentId departureStatusId = Id("test.pack:departure_only_status");
        player.State.AddOtherStatus(
            departureStatusId,
            new StatusLifetimeDefinition(
                new PermanentDurationDefinition(),
                new StatusRemovalProfileDefinition([removalCause])));
        StatusLifetimeDefinition fearLifetime = new(
            new PermanentDurationDefinition(),
            StatusRemovalProfiles.Standard);
        var fear = new AilmentDefinition(
            Id("test.pack:forced_departure"),
            "Forced Departure",
            "Forces an encounter departure for lifecycle integration coverage.",
            fearLifetime,
            new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(
                SkipChance: 0,
                FleeChance: 100,
                CompanionFleeOutcome.RecallToRoster),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition());
        player.State.ApplyAilment(fear, fearLifetime);

        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                NewMoon,
                1));

        Assert.Equal(AutomatedBattleOutcome.Victory, result.Outcome);
        Assert.Equal(EnemyTeam, result.WinningTeamId);
        Assert.False(player.State.IsDeployed);
        Assert.DoesNotContain(departureStatusId, player.State.OtherStatuses);
        BattleEncounterEvent presence = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.EncounterPresenceChanged &&
            battleEvent.ActorId == player.State.InstanceId);
        BattleEncounterEvent[] departureEvents = result.Events.Where(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.StatusChanged &&
                battleEvent.ActorId == player.State.InstanceId &&
                battleEvent.DebugText == expectedCleanupDetail)
            .ToArray();
        Assert.Equal(2, departureEvents.Length);
        BattleEncounterEvent cleanup = departureEvents[^1];
        BattleEncounterEvent battleEnd = result.Events.Single(battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.BattleEnded);
        Assert.True(presence.Sequence < cleanup.Sequence);
        Assert.True(cleanup.Sequence < battleEnd.Sequence);
    }

    [Theory]
    [InlineData(BattleTurnStartOutcome.FleeBattle, BattleStatusDepartureReason.Flee)]
    [InlineData(BattleTurnStartOutcome.RecallToRoster, BattleStatusDepartureReason.RosterRecall)]
    public void Runner_ExplicitDepartureOwnsTheActorsCurrentDefeatPeriod(
        BattleTurnStartOutcome outcome,
        BattleStatusDepartureReason expectedReason)
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor(
            $"explicit_{outcome}_player",
            $"explicit_{outcome}_player",
            PlayerTeam,
            capabilityIds: outcome == BattleTurnStartOutcome.RecallToRoster
                ? [Id("recall_to_roster")]
                : []);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            $"explicit_{outcome}_enemy",
            $"explicit_{outcome}_enemy",
            EnemyTeam);
        ContentId defeatOnlyStatusId = Id($"test.pack:explicit_{outcome}_defeat_only");
        player.State.AddOtherStatus(
            defeatOnlyStatusId,
            new StatusLifetimeDefinition(
                new PermanentDurationDefinition(),
                new StatusRemovalProfileDefinition([StatusRemovalCause.Defeat])));

        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var lifecycle = new RestrictedBattleStatusLifecyclePort(
            new BattleStatusEncounterLifecyclePort(
                new BattleStatusLifecycleService(new MinimumRandomSource()),
                services,
                Id("battle_start"),
                Id("owner_turn_end"),
                TestEncounterClocks.Standard(PlayerTeam, EnemyTeam)),
            player.State.InstanceId,
            new BattleTurnStartRestriction(outcome));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle,
            restrictionResolver: new DefeatingExitRestrictionResolver()).Run(
            new AutomatedBattleRequest([player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Victory, result.Outcome);
        Assert.Equal(EnemyTeam, result.WinningTeamId);
        Assert.True(player.State.IsDefeated);
        Assert.False(player.State.IsDeployed);
        Assert.Contains(defeatOnlyStatusId, player.State.OtherStatuses);
        Assert.Equal([expectedReason], lifecycle.DepartureReasons);
        Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ActorDefeated &&
            battleEvent.ActorId == player.State.InstanceId);
    }

    [Fact]
    public void Runner_CanonicalLifecycleCleansDefeatStatusesBeforeDefeatAnnouncement()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition attack = Active("test.pack:defeat_cleanup_attack", DamageElement.Physical);
        CatalogBattleActor player = RuntimeCatalogActor(
            "defeat_cleanup_player",
            "defeat_cleanup_player",
            PlayerTeam,
            [attack]);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            "defeat_cleanup_enemy",
            "defeat_cleanup_enemy",
            EnemyTeam);
        enemy.State.SetResource(Id("hp"), 1m);
        ContentId defeatStatusId = Id("test.pack:defeat_only_status");
        enemy.State.AddOtherStatus(
            defeatStatusId,
            new StatusLifetimeDefinition(
                new PermanentDurationDefinition(),
                new StatusRemovalProfileDefinition([StatusRemovalCause.Defeat])));

        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services).Run(new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                NewMoon,
                1));

        Assert.Equal(AutomatedBattleOutcome.Victory, result.Outcome);
        Assert.Equal(PlayerTeam, result.WinningTeamId);
        Assert.DoesNotContain(defeatStatusId, enemy.State.OtherStatuses);
        BattleEncounterEvent[] defeatLifecycleEvents = result.Events.Where(battleEvent =>
                battleEvent.Kind == BattleEncounterEventKind.StatusChanged &&
                battleEvent.ActorId == enemy.State.InstanceId &&
                battleEvent.DebugText == "Defeat")
            .ToArray();
        Assert.Equal(2, defeatLifecycleEvents.Length);
        BattleEncounterEvent cleanup = defeatLifecycleEvents[^1];
        BattleEncounterEvent defeat = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ActorDefeated &&
            battleEvent.ActorId == enemy.State.InstanceId);
        Assert.True(cleanup.Sequence < defeat.Sequence);
    }

    [Fact]
    public async Task CanonicalLifecycle_DepartureCancellationPrecedesCleanupMutation()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor actor = RuntimeCatalogActor(
            "cancelled_departure_actor",
            "cancelled_departure_actor",
            PlayerTeam);
        ContentId statusId = Id("test.pack:cancelled_departure_status");
        actor.State.AddOtherStatus(
            statusId,
            new StatusLifetimeDefinition(
                new PermanentDurationDefinition(),
                new StatusRemovalProfileDefinition([StatusRemovalCause.Flee])));
        BattleExecutionServices services = Services(catalog);
        var lifecycle = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            Id("battle_start"),
            Id("owner_turn_end"),
            TestEncounterClocks.Standard(PlayerTeam, EnemyTeam));
        var participant = new BattleEncounterParticipant(actor.State, actor.Entity.DisplayName);
        var encounter = new BattleEncounterRequest(
            [participant],
            Battle,
            NormalBattle,
            NewMoon,
            1);
        var request = new BattleEncounterDepartureLifecycleRequest(
            encounter,
            participant,
            encounter.Participants,
            BattleStatusDepartureReason.Flee);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => lifecycle.ProcessActorDepartureAsync(request, cancellation.Token).AsTask());

        Assert.Contains(statusId, actor.State.OtherStatuses);
    }

    [Fact]
    public void Runner_FaultsRatherThanDiscardingAForcedCommandWithoutAConfiguredPolicy()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor("unresolved_player", "unresolved_player", PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor("unresolved_enemy", "unresolved_enemy", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var skillExecutor = new SkillExecutor(services);
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.ForcedBasicAttack));

        AutomatedBattleResult result = CreateAutomatedRunner(
            skillExecutor,
            new DeterministicBattleActionSelector(skillExecutor),
            services,
            lifecycle).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Contains("explicit action source", result.FaultMessage, StringComparison.Ordinal);
        Assert.Equal(100m, player.State.GetRequiredResource(Id("hp")).Current);
        Assert.Equal(100m, enemy.State.GetRequiredResource(Id("hp")).Current);
    }

    [Fact]
    public void Runner_UsesCanonicalLifecycleAndInjectedTurnEconomyFactory()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        LifecycleScenario scenario = CreateLifecycleScenario(catalog, "automated");
        var economyCreations = 0;
        var turnEconomy = new BattleTurnEconomyRuleset(
            () =>
            {
                economyCreations++;
                return new ActionTokenTurnEconomy();
            },
            new BattlePhaseProgressPolicy(256, 32));
        var executor = new SkillExecutor(scenario.Services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            scenario.Services,
            scenario.Lifecycle,
            turnEconomy).Run(new AutomatedBattleRequest(
                [scenario.Player, scenario.Enemy],
                Battle,
                NormalBattle,
                NewMoon,
                1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Equal(2, economyCreations);
        Assert.False(scenario.Player.State.IsGuarding);
        Assert.Equal(90m, scenario.Player.State.GetRequiredResource(Id("hp")).Current);
        Assert.Equal(
            1,
            Assert.IsType<TurnDurationDefinition>(
                scenario.Player.State.Ailments[scenario.AilmentId].Duration).Value);
        Assert.DoesNotContain(scenario.BattleStatusId, scenario.Player.State.OtherStatuses);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.TurnRestricted &&
            battleEvent.ActorId == scenario.Player.State.InstanceId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.ResourceChanged &&
            battleEvent.ActorId == scenario.Player.State.InstanceId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.StatusChanged &&
            battleEvent.ActorId == scenario.Player.State.InstanceId);
        Assert.All(
            result.Events.Where(battleEvent => battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged),
            battleEvent => Assert.IsType<ActionTokenTurnEconomySnapshot>(battleEvent.TurnEconomyState));
    }

    [Fact]
    public void Runner_PreservesTypedEventsForANonActionTokenEconomy()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = RuntimeCatalogActor("standard_player", "standard_player", PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor("standard_enemy", "standard_enemy", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var turnEconomy = new BattleTurnEconomyRuleset(
            () => new StandardActionTurnEconomy(),
            new BattlePhaseProgressPolicy(256, 32));

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services,
            turnEconomy: turnEconomy).Run(new AutomatedBattleRequest(
                [player, enemy],
                Battle,
                NormalBattle,
                NewMoon,
                1));

        BattleEncounterEvent[] economyEvents = result.Events
            .Where(battleEvent => battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged)
            .ToArray();
        Assert.NotEmpty(economyEvents);
        Assert.All(economyEvents, battleEvent =>
        {
            Assert.NotNull(battleEvent.TurnEconomyState);
            Assert.Equal(StandardActionTurnEconomy.EconomyId, battleEvent.TurnEconomyState!.EconomyId);
        });
    }

    [Fact]
    public void Runner_AndDirectEncounterProduceEquivalentLifecycleAndTurnEconomyState()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        LifecycleScenario automated = CreateLifecycleScenario(catalog, "automated_parity");
        LifecycleScenario direct = CreateLifecycleScenario(catalog, "direct_parity");
        var automatedEconomyCreations = 0;
        var directEconomyCreations = 0;
        BattleTurnEconomyRuleset automatedEconomy = CountingTurnEconomy(
            () => automatedEconomyCreations++);
        BattleTurnEconomyRuleset directEconomy = CountingTurnEconomy(
            () => directEconomyCreations++);
        var automatedExecutor = new SkillExecutor(automated.Services);

        AutomatedBattleResult automatedResult = CreateAutomatedRunner(
            automatedExecutor,
            new DeterministicBattleActionSelector(automatedExecutor),
            automated.Services,
            automated.Lifecycle,
            automatedEconomy).Run(new AutomatedBattleRequest(
                [automated.Player, automated.Enemy],
                Battle,
                NormalBattle,
                NewMoon,
                1));
        BattleEncounterResult directResult = new BattleEncounterRunner().Run(
            new BattleEncounterRequest(
                [
                    new BattleEncounterParticipant(direct.Player.State, direct.Player.Entity.DisplayName),
                    new BattleEncounterParticipant(direct.Enemy.State, direct.Enemy.Entity.DisplayName)
                ],
                Battle,
                NormalBattle,
                NewMoon,
                1),
            new BattleEncounterServices(
                new ParticipantOrderInitiativePolicy(),
                new TeamPhaseRoundRobinBattleEncounterSchedulePolicy(),
                direct.Lifecycle,
                new RestrictedPassTurnHandler(),
                new LastTeamStandingCompletionPolicy(),
                directEconomy.CreateEconomy,
                directEconomy.PhaseProgress,
                new BattleEncounterProgressPolicy(4096)));

        Assert.Equal(AutomatedBattleOutcome.Draw, automatedResult.Outcome);
        Assert.Equal(BattleEncounterOutcome.Draw, directResult.Outcome);
        Assert.Equal(directEconomyCreations, automatedEconomyCreations);
        Assert.Equal(
            direct.Player.State.GetRequiredResource(Id("hp")).Current,
            automated.Player.State.GetRequiredResource(Id("hp")).Current);
        Assert.Equal(direct.Player.State.IsGuarding, automated.Player.State.IsGuarding);
        Assert.Equal(
            Assert.IsType<TurnDurationDefinition>(direct.Player.State.Ailments[direct.AilmentId].Duration).Value,
            Assert.IsType<TurnDurationDefinition>(automated.Player.State.Ailments[automated.AilmentId].Duration).Value);
        Assert.Equal(
            direct.Player.State.OtherStatuses.Contains(direct.BattleStatusId),
            automated.Player.State.OtherStatuses.Contains(automated.BattleStatusId));

        ActionTokenTurnEconomySnapshot[] automatedEconomyEvents = automatedResult.Events
            .Where(battleEvent => battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged)
            .Select(battleEvent => Assert.IsType<ActionTokenTurnEconomySnapshot>(battleEvent.TurnEconomyState))
            .ToArray();
        ActionTokenTurnEconomySnapshot[] directEconomyEvents = directResult.Events
            .Where(battleEvent => battleEvent.Kind == BattleEncounterEventKind.TurnEconomyChanged)
            .Select(battleEvent => Assert.IsType<ActionTokenTurnEconomySnapshot>(battleEvent.TurnEconomyState))
            .ToArray();
        Assert.Equal(
            directEconomyEvents.Select(TurnEconomyValues),
            automatedEconomyEvents.Select(TurnEconomyValues));
    }

    [Fact]
    public void Runner_DispatchesBattleStartBeforeTheFirstRound()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        SkillDefinition attack = Active("test.pack:attack", DamageElement.Fire);
        SkillDefinition openingPassive = new(
            Id("test.pack:opening_passive"), "Opening Passive", "Opening Passive",
            SkillActivation.Passive, null, InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            triggers:
            [
                new PassiveTriggerDefinition(
                    Id("battle_start"),
                    [new RestoreResourceEffectDefinition(Id("hp"), new FlatAmountDefinition(1))])
            ]);
        var skills = new SkillRepository(attack, openingPassive);
        var entities = new EntityRepository(
            Entity("test.pack:player", [attack.Id, openingPassive.Id]),
            Entity("test.pack:enemy", [attack.Id]));
        var factory = new CatalogBattleActorFactory(entities, skills, new TestInitializationPolicy());
        CatalogBattleActor player = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:player"),
            RuntimeInstanceId.Parse("player"),
            PlayerTeam,
            1,
            IsDeployed: true,
            Id("test_host"))).RequireActor();
        CatalogBattleActor enemy = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:enemy"),
            RuntimeInstanceId.Parse("enemy"),
            EnemyTeam,
            1,
            IsDeployed: true,
            Id("test_host"))).RequireActor();
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor, new DeterministicBattleActionSelector(executor), services).Run(
            new AutomatedBattleRequest([player, enemy], Battle, NormalBattle, NewMoon, 1));

        BattleEncounterEvent activation = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.PassiveActivated &&
            battleEvent.SourceId == openingPassive.Id);
        Assert.True(activation.Sequence < result.Events.First(battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.RoundStarted).Sequence);
    }

    [Fact]
    public void Runner_FaultsWhenASelectedActionIsUnexpectedlyRejected()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor, new InvalidTargetSelector(), services).Run(
            new AutomatedBattleRequest([frost, ember], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.NotNull(result.FaultMessage);
        Assert.Contains(result.Events, battleEvent => battleEvent.Kind == BattleEncounterEventKind.BattleFaulted);
    }

    [Fact]
    public void Runner_RejectsAnUnequippedPreparedSkillBeforeMutationOrCommandPublication()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor enemy = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        SkillDefinition unequipped = Active("test.pack:unequipped_attack", DamageElement.Ice);
        decimal playerSp = player.State.GetRequiredResource(Id("sp")).Current;
        decimal enemyHp = enemy.State.GetRequiredResource(Id("hp")).Current;
        var selector = new DelegatingBattleActionSelector(request =>
            PrepareSelection(executor, request, unequipped, unequipped));

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            selector,
            services).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Contains("not authorized", result.FaultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(playerSp, player.State.GetRequiredResource(Id("sp")).Current);
        Assert.Equal(enemyHp, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected);
    }

    [Fact]
    public void Runner_RejectsASubstitutedEquippedSkillDefinitionBeforeMutation()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor enemy = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        SkillDefinition substituted = player.ActiveSkills[0] with { };
        decimal playerSp = player.State.GetRequiredResource(Id("sp")).Current;
        decimal enemyHp = enemy.State.GetRequiredResource(Id("hp")).Current;
        var selector = new DelegatingBattleActionSelector(request =>
            PrepareSelection(executor, request, substituted, substituted));

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            selector,
            services).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Contains("canonical catalog definition", result.FaultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(playerSp, player.State.GetRequiredResource(Id("sp")).Current);
        Assert.Equal(enemyHp, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected);
    }

    [Fact]
    public void Runner_RejectsASelectionWhoseSkillDoesNotMatchItsPreparedAssessment()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor enemy = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        SkillDefinition advertised = player.ActiveSkills[0];
        SkillDefinition preparedSkill = player.ActiveSkills[1];
        decimal playerSp = player.State.GetRequiredResource(Id("sp")).Current;
        decimal enemyHp = enemy.State.GetRequiredResource(Id("hp")).Current;
        var selector = new DelegatingBattleActionSelector(request =>
            PrepareSelection(executor, request, preparedSkill, advertised));

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            selector,
            services).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Contains("does not match", result.FaultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(playerSp, player.State.GetRequiredResource(Id("sp")).Current);
        Assert.Equal(enemyHp, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected);
    }

    [Fact]
    public void Runner_RejectsPreparedAssessmentFromAnotherExecutorAuthority()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor enemy = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executionAuthority = new SkillExecutor(services);
        var foreignAssessmentAuthority = new SkillExecutor(Services(catalog));
        SkillDefinition skill = player.ActiveSkills[0];
        decimal playerSp = player.State.GetRequiredResource(Id("sp")).Current;
        decimal enemyHp = enemy.State.GetRequiredResource(Id("hp")).Current;
        var selector = new DelegatingBattleActionSelector(request =>
        {
            RuntimeInstanceId targetId = request.Participants.Single(participant =>
                participant.State.TeamId != request.Actor.State.TeamId).State.InstanceId;
            var executionRequest = new SkillExecutionRequest(
                skill,
                request.Actor.State,
                request.Participants.Select(participant => participant.State),
                new EffectExecutionEnvironment(
                    request.ContextId,
                    request.BattleKindId,
                    request.MoonPhaseId,
                    request.ActiveStatModifierBoundaries),
                [targetId]);
            SkillExecutionAssessment assessment = foreignAssessmentAuthority.Assess(executionRequest);
            Assert.True(
                assessment.CanExecute,
                string.Join("; ", assessment.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return new BattleActionSelection(
                BattleActionSelectionStatus.Selected,
                skill,
                assessment.TargetIds,
                assessment);
        });

        AutomatedBattleResult result = CreateAutomatedRunner(
            executionAuthority,
            selector,
            services).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Equal(BattleEncounterFaultCode.CommandExecutionFaulted, result.FaultCode);
        Assert.Contains("another executor", result.FaultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(playerSp, player.State.GetRequiredResource(Id("sp")).Current);
        Assert.Equal(enemyHp, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind is BattleEncounterEventKind.ResourceChanged
                or BattleEncounterEventKind.EffectResolved
                or BattleEncounterEventKind.TurnEconomyChanged);
    }

    [Theory]
    [InlineData(PreparedSelectionMismatch.Actor, "another actor")]
    [InlineData(PreparedSelectionMismatch.Participants, "another participant set")]
    [InlineData(PreparedSelectionMismatch.Environment, "another encounter environment")]
    [InlineData(PreparedSelectionMismatch.Targets, "targets do not match")]
    public void Runner_RejectsPreparedSelectionMetadataFromAnotherActionBoundary(
        PreparedSelectionMismatch mismatch,
        string expectedDiagnostic)
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor player = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor enemy = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        SkillDefinition skill = player.ActiveSkills[0];
        decimal playerSp = player.State.GetRequiredResource(Id("sp")).Current;
        decimal enemyHp = enemy.State.GetRequiredResource(Id("hp")).Current;
        var selector = new DelegatingBattleActionSelector(request =>
        {
            RuntimeActorState preparedActor = mismatch == PreparedSelectionMismatch.Actor
                ? enemy.State
                : request.Actor.State;
            RuntimeActorState[] preparedParticipants = request.Participants
                .Select(participant => participant.State)
                .ToArray();
            if (mismatch == PreparedSelectionMismatch.Participants)
            {
                preparedParticipants = preparedParticipants.Reverse().ToArray();
            }

            RuntimeInstanceId targetId = preparedParticipants.Single(participant =>
                participant.TeamId != preparedActor.TeamId).InstanceId;
            var executionRequest = new SkillExecutionRequest(
                skill,
                preparedActor,
                preparedParticipants,
                request.ContextId,
                mismatch == PreparedSelectionMismatch.Environment
                    ? Id("alternate_battle")
                    : request.BattleKindId,
                request.MoonPhaseId,
                [targetId]);
            SkillExecutionAssessment assessment = executor.Assess(executionRequest);
            Assert.True(
                assessment.CanExecute,
                string.Join("; ", assessment.Diagnostics.Select(diagnostic => diagnostic.Message)));
            IReadOnlyList<RuntimeInstanceId> advertisedTargets =
                mismatch == PreparedSelectionMismatch.Targets
                    ? [preparedActor.InstanceId]
                    : assessment.TargetIds;
            return new BattleActionSelection(
                BattleActionSelectionStatus.Selected,
                skill,
                advertisedTargets,
                assessment);
        });

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor,
            selector,
            services).Run(new AutomatedBattleRequest(
                [player, enemy], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.Contains(expectedDiagnostic, result.FaultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(playerSp, player.State.GetRequiredResource(Id("sp")).Current);
        Assert.Equal(enemyHp, enemy.State.GetRequiredResource(Id("hp")).Current);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected);
    }

    [Theory]
    [InlineData(TurnEconomyOutcome.Normal, false, false, 1, 0)]
    [InlineData(TurnEconomyOutcome.Normal, true, false, 1, 0)]
    [InlineData(TurnEconomyOutcome.Weakness, false, false, 1, 1)]
    [InlineData(TurnEconomyOutcome.Critical, true, false, 1, 1)]
    [InlineData(TurnEconomyOutcome.Miss, false, false, 0, 0)]
    [InlineData(TurnEconomyOutcome.Null, false, false, 0, 0)]
    [InlineData(TurnEconomyOutcome.Repel, false, true, 0, 0)]
    [InlineData(TurnEconomyOutcome.Absorb, false, true, 0, 0)]
    public void CleanActionTokenOverload_ConsumesEveryTypedOutcome(
        TurnEconomyOutcome outcome,
        bool critical,
        bool terminates,
        int expectedFull,
        int expectedBlinking)
    {
        var engine = new ActionTokenTurnEconomy();
        engine.StartPhase(2);

        engine.Apply(ActionTurnConsumption.FromTurnEconomy(
            new TurnEconomyResolution(outcome, critical, terminates)));

        Assert.Equal(expectedFull, engine.FullTokens);
        Assert.Equal(expectedBlinking, engine.PartialTokens);
    }

    [Fact]
    public void ActionTokenPass_ConsumesAnExistingPartialTokenBeforeAFullToken()
    {
        var engine = new ActionTokenTurnEconomy();
        engine.StartPhase(2);
        engine.Apply(ActionTurnConsumption.FromTurnEconomy(new TurnEconomyResolution(
            TurnEconomyOutcome.Weakness,
            AnyCritical: false,
            TerminatesPhase: false)));

        Assert.Equal(1, engine.FullTokens);
        Assert.Equal(1, engine.PartialTokens);

        engine.Apply(ActionTurnConsumption.Pass);

        Assert.Equal(1, engine.FullTokens);
        Assert.Equal(0, engine.PartialTokens);
    }

    [Fact]
    public void RuntimePublicApi_DoesNotExposeHostSerializerFilesystemOrLegacyTypes()
    {
        Type[] publicTypes = typeof(CatalogBattleActorFactory).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "Convergence.Encounters")
            .ToArray();
        string[] forbidden =
        [
            "Newtonsoft", "System.Text.Json", "Godot", "System.IO.File", "Database",
            "Combatant", "SkillData", string.Concat("Per", "sona", "Data")
        ];

        IEnumerable<Type> signatures = publicTypes.SelectMany(PublicSignatureTypes);

        Assert.DoesNotContain(signatures, type =>
            forbidden.Any(token => (type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal)));
    }

    private static GameDataCatalog LoadDemoCatalog()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Content");
        ContentPackTextBundle reference = Bundle(root,
            "skill_system_redesign.manifest.sample.json",
            "skill_system_redesign.races.sample.json",
            "skill_system_redesign.skills.sample.json",
            "skill_system_redesign.entities.sample.json");
        ContentPackTextBundle demo = Bundle(root,
            "clean_battle_demo.manifest.json",
            "clean_battle_demo.races.json",
            "clean_battle_demo.skills.json",
            "clean_battle_demo.entities.json");

        return new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(Registrations(), [reference, demo])).RequireCatalog();
    }

    private static ContentPackTextBundle Bundle(string root, string manifest, params string[] documents) =>
        new(
            manifest,
            File.ReadAllText(TestContentPath.Resolve(root, manifest)),
            documents.Select(path => new ContentDocumentText(
                path,
                path,
                File.ReadAllText(TestContentPath.Resolve(root, path)))));

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterEntityKind("companion")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .Build();

    private static CatalogBattleActor CreateDemoActor(
        GameDataCatalog catalog,
        string entityId,
        string instanceId,
        ContentId teamId) =>
        new CatalogBattleActorFactory(catalog, catalog, new TestInitializationPolicy()).Create(
            new CatalogBattleActorCreationRequest(
                Id($"convergence.clean_battle_demo:{entityId}"),
                RuntimeInstanceId.Parse(instanceId),
                teamId,
                5,
                IsDeployed: true,
                Id("test_host"))).RequireActor();

    private static BattleExecutionServices Services(
        GameDataCatalog catalog,
        IRandomTargetSelectionPolicy? randomTargetPolicy = null,
        IStatModifierPolicyService? statModifiers = null,
        IDamageExecutionPolicy? damagePolicy = null,
        IEnumerable<KeyValuePair<ContentId, IEscapeRuleHandler>>? escapeRules = null,
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffects = null) => new(
        catalog,
        damagePolicy ?? new TestDamagePolicy(),
        new NeverInstantDeathPolicy(),
        new TestAilmentPolicy(),
        new AlwaysChancePolicy(),
        new TestPowerPolicy(),
        randomTargetPolicy ?? new FirstRandomTargetPolicy(),
        new OrderedRuntimeTargetSelectionPolicy(),
        statModifiers ?? TestStatModifierPolicy.CreatePersistent(),
        new SplitChargePolicy(),
        escapeRuleHandlers: escapeRules,
        customEffectHandlers: customEffects);

    private static IBattleKnowledgeView KnowledgeView(
        params (CatalogBattleActor Target, DamageElement Element, ElementalAffinity Affinity)[] facts) =>
        new BattleKnowledgeView(
            new RuntimeKnowledgeSnapshot(
                facts.Select(fact => new RuntimeElementalAffinityKnowledgeSnapshot(
                    fact.Target.Entity.Id,
                    fact.Element,
                    fact.Affinity))),
            RuntimeEncounterKnowledgeSnapshot.Empty);

    private static ContentId FirstSelectedSkill(
        AutomatedBattleResult result,
        CatalogBattleActor actor) =>
        result.Events.First(battleEvent =>
            battleEvent.Kind == BattleEncounterEventKind.CommandSelected &&
            battleEvent.ActorId == actor.State.InstanceId).SourceId!.Value;

    private static AutomatedBattleRunner CreateAutomatedRunner(
        ISkillExecutor executor,
        IBattleActionSelector selector,
        BattleExecutionServices services,
        IBattleEncounterLifecyclePort? lifecycle = null,
        BattleTurnEconomyRuleset? turnEconomy = null,
        IAutomatedBattleTurnRestrictionResolver? restrictionResolver = null) =>
        new(
            executor,
            selector,
            lifecycle ?? new BattleStatusEncounterLifecyclePort(
                new BattleStatusLifecycleService(new MinimumRandomSource()),
                services,
                Id("battle_start"),
                Id("owner_turn_end"),
                TestEncounterClocks.Standard(PlayerTeam, EnemyTeam)),
            turnEconomy ?? StandardTurnEconomy(),
            restrictionResolver ?? new AutomatedBattleTurnRestrictionResolver(),
            new BattleEncounterProgressPolicy(4096));

    private static BattleActionSelection PrepareSelection(
        ISkillExecutor executor,
        BattleActionSelectionRequest request,
        SkillDefinition preparedSkill,
        SkillDefinition advertisedSkill)
    {
        RuntimeInstanceId targetId = request.Participants.Single(participant =>
            participant.State.TeamId != request.Actor.State.TeamId).State.InstanceId;
        var executionRequest = new SkillExecutionRequest(
            preparedSkill,
            request.Actor.State,
            request.Participants.Select(participant => participant.State),
            request.ContextId,
            request.BattleKindId,
            request.MoonPhaseId,
            [targetId]);
        SkillExecutionAssessment assessment = executor.Assess(executionRequest);
        Assert.True(
            assessment.CanExecute,
            string.Join("; ", assessment.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return new BattleActionSelection(
            BattleActionSelectionStatus.Selected,
            advertisedSkill,
            assessment.TargetIds,
            assessment);
    }

    private static AutomatedBattleTurnRestrictionResolver RestrictionResolver(
        ISkillExecutor skillExecutor,
        BattleExecutionServices services,
        IAutomatedBattleRestrictionActionSource source) =>
        new(
            new BattleActionExecutor(
                skillExecutor,
                new ItemExecutor(services),
                services,
                AllowAllBattleActionAuthorizationPolicy.Instance),
            source);

    private static BattleTurnEconomyRuleset StandardTurnEconomy() =>
        new(
            () => new ActionTokenTurnEconomy(),
            new BattlePhaseProgressPolicy(
                maximumCommands: 256,
                maximumConsecutiveFreeActions: 32));

    private static BattleTurnEconomyRuleset CountingTurnEconomy(Action onCreated) =>
        new(
            () =>
            {
                onCreated();
                return new ActionTokenTurnEconomy();
            },
            new BattlePhaseProgressPolicy(
                maximumCommands: 256,
                maximumConsecutiveFreeActions: 32));

    private static LifecycleScenario CreateLifecycleScenario(GameDataCatalog catalog, string prefix)
    {
        CatalogBattleActor player = RuntimeCatalogActor(
            $"{prefix}_player",
            $"{prefix}_player",
            PlayerTeam);
        CatalogBattleActor enemy = RuntimeCatalogActor(
            $"{prefix}_enemy",
            $"{prefix}_enemy",
            EnemyTeam);
        ContentId ailmentId = Id($"test.pack:{prefix}_fatigue");
        ContentId battleStatusId = Id($"test.pack:{prefix}_battle_status");
        StatusLifetimeDefinition duration =
            FieldLifetime(new TurnDurationDefinition(2, Id("owner_turn_end"), false));
        var ailment = new AilmentDefinition(
            ailmentId,
            "Fatigue",
            "Lifecycle parity fixture.",
            duration,
            new SkipAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition(),
            triggers:
            [
                new PassiveTriggerDefinition(
                    Id("owner_turn_end"),
                    [new ReduceResourceEffectDefinition(Id("hp"), new FlatAmountDefinition(10), true)])
            ]);
        player.State.SetGuarding(true);
        player.State.ApplyAilment(ailment, duration);
        player.State.AddOtherStatus(battleStatusId, EncounterLifetime(new BattleDurationDefinition()));

        BattleExecutionServices services = Services(catalog);
        var lifecycle = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            Id("battle_start"),
            Id("owner_turn_end"),
            TestEncounterClocks.Standard(PlayerTeam, EnemyTeam));
        return new LifecycleScenario(
            player,
            enemy,
            services,
            lifecycle,
            ailmentId,
            battleStatusId);
    }

    private static (int FullTokens, int PartialTokens) TurnEconomyValues(
        ActionTokenTurnEconomySnapshot snapshot) =>
        (snapshot.FullTokens, snapshot.PartialTokens);

    private static SkillDefinition Active(
        string id,
        DamageElement element,
        TargetingDefinition? targeting = null,
        IEnumerable<SkillCostDefinition>? costs = null) => new(
        Id(id), id, id, SkillActivation.Active, SkillMenuGroup.Offense,
        element switch
        {
            DamageElement.Fire => InheritanceGroup.Fire,
            DamageElement.Ice => InheritanceGroup.Ice,
            DamageElement.Wind => InheritanceGroup.Wind,
            _ => InheritanceGroup.Physical
        },
        new SkillInheritanceDefinition(true),
        targeting: targeting ?? new TargetingDefinition(
            TargetRelation.Enemy,
            TargetSelection.Single,
            TargetLifeState.Alive,
            false),
        effects: [new DamageEffectDefinition(element, 1, 100, new NeverCriticalDefinition(), new HitCountDefinition(1, 1))],
        availability: new SkillAvailabilityDefinition([Battle]),
        costs: costs);

    private static SkillDefinition TimedModifierSkill(string id) => new(
        Id(id),
        id,
        id,
        SkillActivation.Active,
        SkillMenuGroup.Buff,
        InheritanceGroup.Support,
        new SkillInheritanceDefinition(true),
        targeting: new TargetingDefinition(
            TargetRelation.Self,
            TargetSelection.Single,
            TargetLifeState.Alive,
            true),
        effects:
        [
            new ModifyStatStageEffectDefinition(
                [Id("attack")],
                1,
                new TurnDurationDefinition(1, Id("owner_turn_end"), true))
        ],
        availability: new SkillAvailabilityDefinition([Battle]));

    private static SkillDefinition UntargetedActive(
        string id,
        params EffectDefinition[] effects) => new(
        Id(id),
        id,
        id,
        SkillActivation.Active,
        SkillMenuGroup.Buff,
        InheritanceGroup.Support,
        new SkillInheritanceDefinition(true),
        targeting: new TargetingDefinition(
            TargetRelation.None,
            TargetSelection.None,
            TargetLifeState.Any,
            true),
        effects: effects,
        availability: new SkillAvailabilityDefinition([Battle]));

    private static void AssertSameBoundaryTickPreservedOneTurnDuration(
        RecordingStatModifierPolicyService statModifiers)
    {
        (StatModifierTickRequest Request, StatModifierTransitionResult Result) tick = Assert.Single(
            statModifiers.Ticks,
            value => value.Request.State.Tracks.Count > 0 &&
                     value.Request.LifecycleBoundary.EventId == Id("owner_turn_end"));
        RuntimeStatModifierContributionSnapshot before = Assert.Single(
            Assert.Single(tick.Request.State.Tracks).Contributions);
        RuntimeStatModifierContributionSnapshot after = Assert.Single(
            Assert.Single(tick.Result.After.Tracks).Contributions);
        StatModifierLifecycleBoundary applicationBoundary = Assert.IsType<StatModifierLifecycleBoundary>(
            before.LastLifecycleBoundary);

        Assert.Equal(applicationBoundary.EventId, tick.Request.LifecycleBoundary.EventId);
        Assert.Equal(applicationBoundary.Sequence, tick.Request.LifecycleBoundary.Sequence);
        Assert.Equal(1, Assert.IsType<TurnDurationDefinition>(before.Duration).Value);
        Assert.Equal(1, Assert.IsType<TurnDurationDefinition>(after.Duration).Value);
        Assert.False(tick.Result.StateChanged);
    }

    private static CatalogBattleActor RuntimeCatalogActor(
        string entityId,
        string instanceId,
        ContentId teamId,
        IEnumerable<SkillDefinition>? loadout = null,
        CombatDefenseProfile? defense = null,
        IEnumerable<SkillDefinition>? catalogSkills = null,
        IEnumerable<ContentId>? capabilityIds = null)
    {
        SkillDefinition[] skills = loadout?.ToArray() ?? [];
        EntityDefinition entity = Entity(
            $"test.pack:{entityId}",
            skills.Select(skill => skill.Id));
        var state = new RuntimeActorState(
            RuntimeInstanceId.Parse(instanceId),
            entity.Id,
            teamId,
            Id("hp"),
            defense ?? CombatDefenseProfile.Empty,
            [
                new BattleResourceState(Id("hp"), 100, 100),
                new BattleResourceState(Id("sp"), 20, 20)
            ],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(Id("test_host"), teamId),
            skillIds: skills.Select(skill => skill.Id),
            capabilityIds: capabilityIds,
            passiveSkills: skills.Where(skill => skill.Activation == SkillActivation.Passive),
            skillState: new RuntimeSkillStateSnapshot(
                skills.Select(skill => skill.Id),
                skills.Select(skill => skill.Id)));
        return new CatalogBattleActor(
            entity,
            state,
            new SkillRepository((catalogSkills ?? skills).ToArray()));
    }

    private static EntityDefinition Entity(
        string id,
        IEnumerable<ContentId> baseSkills,
        IEnumerable<SkillUnlockDefinition>? unlocks = null) => new(
        Id(id), id, id, Id("companion"), Id("test.pack:race"), 1, 1,
        new EntityCapabilitiesDefinition(false, false, false),
        new EntityInheritanceRulesDefinition(new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
        new Dictionary<ContentId, int>
        {
            [Id("magic")] = 5,
            [Id("vitality")] = 5
        },
        baseSkillIds: baseSkills,
        skillUnlocks: unlocks);

    private static IEnumerable<Type> PublicSignatureTypes(Type type)
    {
        yield return type;
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            foreach (Type nested in Flatten(property.PropertyType)) yield return nested;
        }
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            foreach (Type nested in Flatten(method.ReturnType)) yield return nested;
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                foreach (Type nested in Flatten(parameter.ParameterType)) yield return nested;
            }
        }
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.IsArray)
        {
            foreach (Type nested in Flatten(type.GetElementType()!)) yield return nested;
        }
        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Flatten(argument)) yield return nested;
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Convergence.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find Convergence.sln.");
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static CatalogBattleActorRestoreRequest ActorRestore(RuntimeActorSnapshot snapshot) =>
        new(
            snapshot,
            RuntimeStatSourceKind.Actor,
            MissingHostedEntityBehavior.UseActorBaseStats,
            statModifierPolicy: snapshot.BattleStatus.StatModifiers is null
                ? null
                : PersistentModifierService());

    private static IStatModifierPolicyService PersistentModifierService() =>
        new StatModifierPolicyService(new PersistentStagedStatModifierPolicy(
            Id("test.pack:persistent_stat_modifiers")));

    private static RuntimeStatModifierStateSnapshot PersistentModifiers(
        ContentId trackId,
        int stage) =>
        new(
            Id("test.pack:persistent_stat_modifiers"),
            [
                new RuntimeStatModifierTrackSnapshot(
                    trackId,
                    stage,
                    [new RuntimeStatModifierContributionSnapshot(1, stage)])
            ]);

    private static RuntimeActorSnapshot RestorableActorSnapshot(
        string instanceId,
        EntityDefinition entity,
        IReadOnlyDictionary<ContentId, decimal> baseStats,
        IReadOnlyDictionary<ContentId, decimal>? effectiveStats = null,
        RuntimeCombatProfileIdentitySnapshot? combatProfileIdentity = null) =>
        new(
            new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse(instanceId),
                entity.Id,
                entity.EntityKindId,
                instanceId),
            new RuntimeActorAffiliationSnapshot(Id("runtime"), PlayerTeam),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeProgressionSnapshot(1, 0, 0, 0),
            [
                new RuntimeResourceSnapshot(StandardProgressionIds.Hp, 40m, 120m),
                new RuntimeResourceSnapshot(StandardProgressionIds.Sp, 20m, 66m)
            ],
            new RuntimeStatBlockSnapshot(baseStats, effectiveStats ?? baseStats),
            new RuntimeSkillStateSnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 20m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 6m)
            ],
            StandardProgressionIds.Hp,
            combatProfileIdentity: combatProfileIdentity);

    private static RuntimeActorSnapshot WithPendingSkill(
        RuntimeActorSnapshot source,
        int unlockLevel,
        ContentId skillId) =>
        new(
            source.Identity,
            source.Affiliation,
            source.EncounterPresence,
            source.Progression,
            source.Resources,
            source.Stats,
            new RuntimeSkillStateSnapshot(
                pendingChoices:
                [
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(1),
                        unlockLevel,
                        skillId)
                ],
                revision: 1),
            source.Equipment,
            source.BattleStatus,
            source.BattleActivations,
            source.BaseResourceValues,
            source.VitalResourceId,
            source.CapabilityIds,
            source.CombatProfileIdentity);

    private static RuntimePartyRosterSnapshot PartyRoster(
        RuntimeActorSnapshot owner,
        RuntimeActorReferenceSnapshot activeHostedEntity)
    {
        RuntimeActorReferenceSnapshot ownerReference = new(
            owner.Identity.InstanceId,
            owner.Identity.EntityDefinitionId,
            owner.Identity.DisplayName);
        return new RuntimePartyRosterSnapshot(
            ownerReference,
            activeParty: [ownerReference],
            activeHostedEntity: activeHostedEntity,
            hostedEntityRoster: [activeHostedEntity]);
    }

    private static IReadOnlyDictionary<ContentId, decimal> CoreStats(decimal value) =>
        StandardProgressionIds.CoreStats.ToDictionary(statId => statId, _ => value);

    private sealed class EntityRepository(params EntityDefinition[] entities) : IEntityDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, EntityDefinition> _entities =
            new ReadOnlyDictionary<ContentId, EntityDefinition>(entities.ToDictionary(entity => entity.Id));
        public bool TryGetEntity(ContentId id, out EntityDefinition? definition) => _entities.TryGetValue(id, out definition);
        public EntityDefinition GetRequiredEntity(ContentId id) => _entities[id];
    }

    private sealed class SkillRepository(params SkillDefinition[] skills) : ISkillDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, SkillDefinition> _skills =
            new ReadOnlyDictionary<ContentId, SkillDefinition>(skills.ToDictionary(skill => skill.Id));
        public bool TryGetSkill(ContentId id, out SkillDefinition? definition) => _skills.TryGetValue(id, out definition);
        public SkillDefinition GetRequiredSkill(ContentId id) => _skills[id];
    }

    private sealed class EquipmentRepository(params EquipmentDefinition[] equipment)
        : IEquipmentDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, EquipmentDefinition> _equipment =
            new ReadOnlyDictionary<ContentId, EquipmentDefinition>(
                equipment.ToDictionary(definition => definition.Id));

        public bool TryGetEquipment(ContentId id, out EquipmentDefinition? definition) =>
            _equipment.TryGetValue(id, out definition);

        public EquipmentDefinition GetRequiredEquipment(ContentId id) => _equipment[id];
    }

    private sealed class TestInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level)
        {
            decimal vitality = entity.Stats.GetValueOrDefault(Id("vitality"));
            decimal magic = entity.Stats.GetValueOrDefault(Id("magic"));
            decimal hp = 40 + level * 5 + vitality * 3;
            decimal sp = 10 + level * 2 + magic * 2;
            return new BattleActorInitialization(Id("hp"),
            [
                new BattleResourceState(Id("hp"), hp, hp),
                new BattleResourceState(Id("sp"), sp, sp)
            ]);
        }
    }

    private sealed class RecordingInitializationPolicy : IBattleActorInitializationPolicy
    {
        public int CallCount { get; private set; }
        public int? LastLevel { get; private set; }

        public BattleActorInitialization Initialize(EntityDefinition entity, int level)
        {
            CallCount++;
            LastLevel = level;
            return new BattleActorInitialization(Id("hp"), [new BattleResourceState(Id("hp"), 10, 10)]);
        }
    }

    private sealed class DuplicateResourceInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level) =>
            new(
                Id("hp"),
                [
                    new BattleResourceState(Id("hp"), 10, 10),
                    new BattleResourceState(Id("hp"), 8, 10)
                ]);
    }

    private sealed class NullInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level) => null!;
    }

    private sealed class ThrowingInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level) =>
            throw new InvalidOperationException("Restore must not invoke creation defaults.");
    }

    private sealed class TestDamagePolicy : IDamageExecutionPolicy
    {
        public DamagePolicyResolution Resolve(DamagePolicyRequest request)
        {
            decimal damage = Math.Max(1,
                request.Effect.Power + request.Actor.Stats.GetValueOrDefault(Id("magic")) -
                request.Target.Stats.GetValueOrDefault(Id("vitality")));
            damage *= request.Affinity switch
            {
                ElementalAffinity.Weak => 1.5m,
                ElementalAffinity.Resist => 0.5m,
                _ => 1m
            };
            return new DamagePolicyResolution(
                [new DamageHitResolution(true, damage)],
                request.Affinity);
        }
    }

    private sealed class NeverInstantDeathPolicy : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
    }

    private sealed class TestAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => request.Resistance != ResistanceLevel.Immune;
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => request.Chance > 0;
    }

    private sealed class TestPowerPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
    }

    private sealed class FirstRandomTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) => candidates.Take(count.Minimum).ToArray();
    }

    private sealed class AlwaysMissDamagePolicy : IDamageExecutionPolicy
    {
        public DamagePolicyResolution Resolve(DamagePolicyRequest request) =>
            new([new DamageHitResolution(false, 0m)], request.Affinity);
    }

    private sealed class RecordingAggregateKnowledgeSelector(
        RuntimeInstanceId observedActorId,
        RuntimeInstanceId targetId,
        RuntimeCombatProfileIdentitySnapshot targetProfileIdentity,
        ContentId ailmentId) : IBattleActionSelector
    {
        public bool ObservedAilment { get; private set; }
        public bool ObservedInstantDeath { get; private set; }

        public BattleActionSelection Select(BattleActionSelectionRequest request)
        {
            if (request.Actor.State.InstanceId == observedActorId)
            {
                ObservedAilment = request.Knowledge.TryGetAilmentResistance(
                    targetId,
                    targetProfileIdentity,
                    ailmentId,
                    out ResistanceLevel ailmentResistance,
                    out BattleKnowledgeFactSource ailmentSource,
                    out _) &&
                    ailmentResistance == ResistanceLevel.Resistant &&
                    ailmentSource == BattleKnowledgeFactSource.Encounter;
                ObservedInstantDeath = request.Knowledge.TryGetInstantDeathResistance(
                    targetId,
                    targetProfileIdentity,
                    InstantDeathChannel.Light,
                    out ResistanceLevel instantDeathResistance,
                    out BattleKnowledgeFactSource instantDeathSource,
                    out _) &&
                    instantDeathResistance == ResistanceLevel.Immune &&
                    instantDeathSource == BattleKnowledgeFactSource.Encounter;
            }

            return BattleActionSelection.Pass();
        }
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class CountingRandomTargetPolicy : IRandomTargetSelectionPolicy
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request)
        {
            CallCount++;
            return candidates.Take(count.Minimum).ToArray();
        }
    }

    private sealed class InvalidTargetSelector : IBattleActionSelector
    {
        public BattleActionSelection Select(BattleActionSelectionRequest request) =>
            new(
                BattleActionSelectionStatus.Selected,
                request.Actor.ActiveSkills[0],
                [RuntimeInstanceId.Parse("missing_target")]);
    }

    private sealed class DelegatingBattleActionSelector(
        Func<BattleActionSelectionRequest, BattleActionSelection> select) : IBattleActionSelector
    {
        public BattleActionSelection Select(BattleActionSelectionRequest request) => select(request);
    }

    public enum PreparedSelectionMismatch
    {
        Actor,
        Participants,
        Environment,
        Targets
    }

    private sealed class RestrictedPassTurnHandler : IBattleEncounterTurnHandler
    {
        public ValueTask<BattleEncounterCommandResult> ExecuteTurnAsync(
            BattleEncounterTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ActionTurnConsumption consumption = request.TurnStartOutcome == BattleTurnStartOutcome.CanAct
                ? ActionTurnConsumption.Pass
                : ActionTurnConsumption.Normal;
            return new ValueTask<BattleEncounterCommandResult>(
                BattleEncounterCommandResult.Executed(consumption));
        }
    }

    private sealed class RecordingStatModifierPolicyService(
        IStatModifierPolicyService inner) : IStatModifierPolicyService
    {
        private readonly List<StatModifierLifecycleBoundary> _applicationBoundaries = [];
        private readonly List<(StatModifierTickRequest Request, StatModifierTransitionResult Result)> _ticks = [];

        public ContentId PolicyId => inner.PolicyId;
        public IReadOnlyList<StatModifierLifecycleBoundary> ApplicationBoundaries =>
            _applicationBoundaries.AsReadOnly();
        public IReadOnlyList<(StatModifierTickRequest Request, StatModifierTransitionResult Result)> Ticks =>
            _ticks.AsReadOnly();

        public StatModifierValidationResult ValidateState(RuntimeStatModifierStateSnapshot state) =>
            inner.ValidateState(state);

        public StatModifierTransitionResult AssessApplication(StatModifierApplicationRequest request) =>
            inner.AssessApplication(request);

        public StatModifierTransitionResult Apply(StatModifierApplicationRequest request)
        {
            if (request.ActiveLifecycleBoundary is StatModifierLifecycleBoundary boundary)
            {
                _applicationBoundaries.Add(boundary);
            }

            return inner.Apply(request);
        }

        public StatModifierTransitionResult Tick(StatModifierTickRequest request)
        {
            StatModifierTransitionResult result = inner.Tick(request);
            _ticks.Add((request, result));
            return result;
        }

        public StatModifierTransitionResult Remove(StatModifierRemovalRequest request) =>
            inner.Remove(request);

        public StatModifierTransitionResult Cleanup(StatModifierCleanupRequest request) =>
            inner.Cleanup(request);
    }

    private sealed class RestrictedBattleStatusLifecyclePort(
        BattleStatusEncounterLifecyclePort inner,
        RuntimeInstanceId restrictedActorId,
        BattleTurnStartRestriction restriction) :
        IBattleEncounterLifecyclePort,
        IBattleEncounterDepartureLifecyclePort,
        IBattleEncounterStatModifierBoundarySource
    {
        public List<BattleStatusDepartureReason> DepartureReasons { get; } = [];

        public IReadOnlyList<StatModifierLifecycleBoundary> GetActiveStatModifierBoundaries(
            BattleEncounterTurnLifecycleRequest request) =>
            inner.GetActiveStatModifierBoundaries(request);

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default) =>
            inner.ProcessBattleStartAsync(request, cancellationToken);

        public async ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            BattleTurnStartLifecycleResult result = await inner.ProcessTurnStartAsync(request, cancellationToken);
            return request.Actor.InstanceId == restrictedActorId
                ? new BattleTurnStartLifecycleResult(restriction, result.Events)
                : result;
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default) =>
            inner.ProcessTurnEndAsync(request, cancellationToken);

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
            BattleEncounterLifecycleRequest request,
            ContentId teamId,
            CancellationToken cancellationToken = default) =>
            inner.ProcessPhaseEndAsync(request, teamId, cancellationToken);

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
            BattleEncounterLifecycleRequest request,
            int roundNumber,
            CancellationToken cancellationToken = default) =>
            inner.ProcessRoundEndAsync(request, roundNumber, cancellationToken);

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default) =>
            inner.ProcessBattleEndAsync(request, outcome, cancellationToken);

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessActorDepartureAsync(
            BattleEncounterDepartureLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            DepartureReasons.Add(request.Reason);
            return inner.ProcessActorDepartureAsync(request, cancellationToken);
        }
    }

    private sealed class FixedTurnRestrictionLifecyclePort : IBattleEncounterLifecyclePort
    {
        private readonly RuntimeInstanceId _restrictedActorId;
        private readonly BattleTurnStartRestriction _restriction;

        public FixedTurnRestrictionLifecyclePort(
            RuntimeInstanceId restrictedActorId,
            BattleTurnStartRestriction restriction)
        {
            _restrictedActorId = restrictedActorId;
            _restriction = restriction;
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleStartAsync(
            BattleEncounterLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<BattleTurnStartLifecycleResult> ProcessTurnStartAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BattleTurnStartRestriction restriction = request.Actor.InstanceId == _restrictedActorId
                ? _restriction
                : BattleTurnStartRestriction.CanAct;
            return new ValueTask<BattleTurnStartLifecycleResult>(
                new BattleTurnStartLifecycleResult(restriction, []));
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessTurnEndAsync(
            BattleEncounterTurnLifecycleRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessPhaseEndAsync(
            BattleEncounterLifecycleRequest request,
            ContentId teamId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessRoundEndAsync(
            BattleEncounterLifecycleRequest request,
            int roundNumber,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }

        public ValueTask<IReadOnlyList<BattleEncounterEvent>> ProcessBattleEndAsync(
            BattleEncounterLifecycleRequest request,
            BattleEncounterOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IReadOnlyList<BattleEncounterEvent>>(
                Array.Empty<BattleEncounterEvent>());
        }
    }

    private sealed class FixedRestrictionResultResolver(
        BattleEncounterCommandResult result) : IAutomatedBattleTurnRestrictionResolver
    {
        public ValueTask<BattleEncounterCommandResult> ResolveAsync(
            AutomatedBattleTurnRestrictionRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<BattleEncounterCommandResult>(result);
        }
    }

    private sealed class DefeatingExitRestrictionResolver : IAutomatedBattleTurnRestrictionResolver
    {
        public ValueTask<BattleEncounterCommandResult> ResolveAsync(
            AutomatedBattleTurnRestrictionRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            request.Actor.State.SetResource(Id("hp"), 0m);
            request.Actor.State.SetEncounterPresence(isDeployed: false);
            return new ValueTask<BattleEncounterCommandResult>(
                BattleEncounterCommandResult.Executed(
                    ActionTurnConsumption.Normal,
                    [new BattleEncounterEvent(
                        0,
                        BattleEncounterEventKind.EncounterPresenceChanged,
                        new BattleEncounterPresenceChangedEventPayload(
                            request.Actor.State.InstanceId,
                            false,
                            request.Actor.State.TeamId),
                        $"{request.Actor.State.InstanceId} left while defeated.")]));
        }
    }

    private sealed class RecordingRestrictedActionSource(
        Func<AutomatedBattleRestrictionActionRequest, AutomatedRestrictedActionSelection> select)
        : IAutomatedBattleRestrictionActionSource
    {
        public int CallCount { get; private set; }
        public AutomatedBattleRestrictionActionRequest? LastRequest { get; private set; }

        public ValueTask<AutomatedRestrictedActionSelection> SelectAsync(
            AutomatedBattleRestrictionActionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return new ValueTask<AutomatedRestrictedActionSelection>(select(request));
        }
    }

    private sealed class AllowAllBattleActionAuthorizationPolicy : IBattleActionAuthorizationPolicy
    {
        private AllowAllBattleActionAuthorizationPolicy()
        {
        }

        public static AllowAllBattleActionAuthorizationPolicy Instance { get; } = new();

        public BattleActionAuthorizationResult Authorize(
            RuntimeActorState actor,
            BattleActionCommand command) =>
            BattleActionAuthorizationResult.Authorized;
    }

    private sealed class AlwaysEscapeRuleHandler : IEscapeRuleHandler
    {
        public bool CanEscape(EscapeEffectDefinition effect, EffectExecutionContext context) => true;
    }

    private sealed class ResourceChangingCustomEffectHandler(
        ContentId resourceId,
        decimal delta) : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(
            CustomEffectDefinition effect,
            EffectExecutionContext context)
        {
            context.Actor.AddResource(resourceId, delta);
            return new EffectExecutionResult(
                context.EffectIndex,
                context.Target?.InstanceId,
                EffectExecutionOutcome.Success,
                Value: delta)
            {
                ResourceChanges =
                [
                    new ExecutionResourceChange(
                        context.Actor.InstanceId,
                        resourceId,
                        delta)
                ]
            };
        }
    }

    private sealed class HostRequestCustomEffectHandler(ContentId hostActionId) : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(
            CustomEffectDefinition effect,
            EffectExecutionContext context) =>
            new(
                context.EffectIndex,
                context.Target?.InstanceId,
                EffectExecutionOutcome.Success,
                HostActionRequestIds: [hostActionId]);
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostCount++;
        }
    }

    private static TSnapshot CloneWithProperty<TSnapshot, TValue>(
        TSnapshot source,
        string propertyName,
        TValue value)
        where TSnapshot : class
    {
        MethodInfo memberwiseClone = typeof(object).GetMethod(
            "MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var clone = (TSnapshot)memberwiseClone.Invoke(source, null)!;
        FieldInfo field = typeof(TSnapshot).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                $"Snapshot property '{typeof(TSnapshot).Name}.{propertyName}' has no backing field.");
        field.SetValue(clone, value);
        return clone;
    }

    private sealed record LifecycleScenario(
        CatalogBattleActor Player,
        CatalogBattleActor Enemy,
        BattleExecutionServices Services,
        IBattleEncounterLifecyclePort Lifecycle,
        ContentId AilmentId,
        ContentId BattleStatusId);
}
