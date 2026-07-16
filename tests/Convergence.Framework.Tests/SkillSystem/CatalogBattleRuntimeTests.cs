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
            entity.Id, RuntimeInstanceId.Parse("instance"), PlayerTeam, 5, IsDeployed: true)).RequireActor();

        Assert.Equal([first.Id, second.Id, third.Id], actor.SkillLoadout.Select(skill => skill.Id));
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
            entity.Id, RuntimeInstanceId.Parse("instance"), PlayerTeam, 0, IsDeployed: true));
        CatalogBattleActorCreationResult missingEntity = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:unknown"), RuntimeInstanceId.Parse("instance"), PlayerTeam, 1, IsDeployed: true));

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
            ControllerId: default(ContentId)));

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
            progression)).RequireActor();

        Assert.Equal(1, initialization.CallCount);
        Assert.Equal(5, initialization.LastLevel);
        Assert.Same(progression, actor.State.Progression);
        Assert.Equal([unlocked.Id], actor.SkillLoadout.Select(skill => skill.Id));
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
            IsDeployed: true));

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
            IsDeployed: true));

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
            new RuntimeActorOwnershipSnapshot(Id("host"), PlayerTeam),
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
                statStages: [new RuntimeStatStageSnapshot(Id("attack"), 2, new PhaseDurationDefinition(Id("phase_end")))],
                affinityOverrides:
                [
                    new RuntimeAffinityOverrideSnapshot(
                        DamageElement.Ice,
                        ElementalAffinity.Resist,
                        new BattleDurationDefinition())
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
        Assert.IsType<PhaseDurationDefinition>(state.StatStages[Id("attack")].Duration);
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
            new RuntimeActorOwnershipSnapshot(Id("host"), PlayerTeam),
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            new RuntimeProgressionSnapshot(1, 0, 0, 0),
            [new RuntimeResourceSnapshot(Id("hp"), 1, 1)],
            new RuntimeStatBlockSnapshot(),
            new RuntimeSkillStateSnapshot([default], [default]),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(
                ailments: [new RuntimeTimedStateSnapshot(default, new BattleDurationDefinition())]),
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
            effectiveStats: CoreStats(999m));
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vesselSnapshot, hostedReference);

        CatalogBattleActorCreationResult vesselRestore = factory.Restore(
            new CatalogBattleActorRestoreRequest(
                vesselSnapshot,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                hostedState,
                partyRoster));

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
        Assert.Equal(CatalogBattleActorDiagnosticCode.SnapshotStatCompositionFailed, diagnostic.Code);
        Assert.Contains("no supplied runtime state", diagnostic.Message, StringComparison.Ordinal);
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
        var knowledge = new ElementalAffinityKnowledge();
        var request = new BattleActionSelectionRequest(
            frost, [frost, ember], Battle, NormalBattle, NewMoon, knowledge);

        BattleActionSelection first = selector.Select(request);
        knowledge.Learn(ember.Entity.Id, DamageElement.Fire, ElementalAffinity.Resist);
        BattleActionSelection afterResistance = selector.Select(request);
        knowledge.Learn(ember.Entity.Id, DamageElement.Ice, ElementalAffinity.Null);
        BattleActionSelection afterNull = selector.Select(request);

        Assert.Equal(Id("convergence.clean_battle_demo:ember_bolt_demo"), first.Skill!.Id);
        Assert.Equal(Id("convergence.clean_battle_demo:frost_lance_demo"), afterResistance.Skill!.Id);
        Assert.True(afterResistance.Assessment!.CanExecute);
        Assert.Equal(Id("convergence.clean_battle_demo:ember_bolt_demo"), afterNull.Skill!.Id);
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
        var knowledge = new ElementalAffinityKnowledge();
        knowledge.Learn(firstTarget.Entity.Id, DamageElement.Fire, ElementalAffinity.Weak);
        knowledge.Learn(blockingTarget.Entity.Id, DamageElement.Fire, ElementalAffinity.Null);
        var executor = new SkillExecutor(Services(catalog));
        var selector = new DeterministicBattleActionSelector(executor);

        BattleActionSelection result = selector.Select(new BattleActionSelectionRequest(
            actor,
            [actor, firstTarget, secondSafeTarget, blockingTarget],
            Battle,
            NormalBattle,
            NewMoon,
            knowledge));

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
            result.Events.First(battleEvent => battleEvent.Kind == BattleRuntimeEventKind.SkillSelected).SkillId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.SkillSelected &&
            battleEvent.SkillId == Id("convergence.clean_battle_demo:frost_lance_demo"));
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.PassiveActivated &&
            battleEvent.SkillId == Id("convergence.clean_battle_demo:regenerate_demo"));
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.EffectResolved &&
            battleEvent.Message.Contains("Weakness", StringComparison.Ordinal));
        Assert.True(result.Events.Select(battleEvent => battleEvent.Sequence).SequenceEqual(
            Enumerable.Range(1, result.Events.Count)));
        Assert.True(result.FinalActors.Single(actor => actor.TeamId == EnemyTeam).IsDefeated);
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

        BattleRuntimeEvent[] resourceChanges = result.Events.Where(battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.ResourceChanged).ToArray();
        Assert.Equal(2, resourceChanges.Length);
        BattleRuntimeEvent cost = resourceChanges[0];
        Assert.Equal(attacker.State.InstanceId, cost.ActorId);
        Assert.Equal(attacker.State.InstanceId, cost.TargetId);
        Assert.Equal(attack.Id, cost.SkillId);
        Assert.Equal(-3, cost.Value);
        BattleRuntimeEvent reflected = resourceChanges[1];
        Assert.Equal(attacker.State.InstanceId, reflected.ActorId);
        Assert.Equal(attacker.State.InstanceId, reflected.TargetId);
        Assert.Equal(attack.Id, reflected.SkillId);
        Assert.Equal(-1, reflected.Value);
        Assert.DoesNotContain(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.ResourceChanged &&
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
            [BattleRuntimeEventKind.BattleFaulted, BattleRuntimeEventKind.BattleEnded],
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
            battleEvent.Kind == BattleRuntimeEventKind.EffectResolved));
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
        frost.State.AddOtherStatus(phaseStatus, new PhaseDurationDefinition(PlayerTeam));
        frost.State.AddOtherStatus(battleStatus, new BattleDurationDefinition());
        frost.State.AddOtherStatus(permanentStatus, new PermanentDurationDefinition());
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
    public void Runner_RequiresExplicitLifecycleAndTurnEconomyDependencies()
    {
        ConstructorInfo constructor = Assert.Single(typeof(AutomatedBattleRunner).GetConstructors());

        Assert.Equal(
        [
            typeof(ISkillExecutor),
            typeof(IBattleActionSelector),
            typeof(BattleExecutionServices),
            typeof(IBattleEncounterLifecyclePort),
            typeof(BattleTurnEconomyRuleset),
            typeof(IAutomatedBattleTurnRestrictionResolver)
        ], constructor.GetParameters().Select(parameter => parameter.ParameterType));
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
            battleEvent.Kind == BattleRuntimeEventKind.SkillSelected &&
            battleEvent.ActorId == player.State.InstanceId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.SkillPassed &&
            battleEvent.ActorId == player.State.InstanceId);
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
            battleEvent.Kind == BattleRuntimeEventKind.SkillSelected &&
            battleEvent.ActorId == player.State.InstanceId &&
            battleEvent.SkillId == attack.Id);
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
    public void Runner_ForcedPhysicalExecutesTheTypedBasicAttackSelectedByTheHostPolicy()
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
                    new EquipmentBasicAttackDefinition(DamageElement.Physical, 10, 100, false),
                    new TargetingDefinition(
                        TargetRelation.Enemy,
                        TargetSelection.Single,
                        TargetLifeState.Alive,
                        false),
                    [request.Participants.Single(actor => actor.State.TeamId == EnemyTeam).State.InstanceId])));
        var lifecycle = new FixedTurnRestrictionLifecyclePort(
            player.State.InstanceId,
            new BattleTurnStartRestriction(BattleTurnStartOutcome.ForcedPhysical));

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
            battleEvent.Kind == BattleRuntimeEventKind.SkillSelected &&
            battleEvent.ActorId == player.State.InstanceId &&
            battleEvent.SkillId == Id("basic_attack"));
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
                    new EquipmentBasicAttackDefinition(DamageElement.Physical, 7, 100, false),
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
            battleEvent.Kind == BattleRuntimeEventKind.EffectResolved &&
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
        BattleRuntimeEvent presenceChanged = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.EncounterPresenceChanged &&
            battleEvent.ActorId == player.State.InstanceId &&
            battleEvent.Message.Contains(expectedMessage, StringComparison.Ordinal));
        Assert.Equal(player.State.InstanceId, presenceChanged.ActorId);
        Assert.False(presenceChanged.IsDeployed);
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
            new BattleTurnStartRestriction(BattleTurnStartOutcome.ForcedPhysical));

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
            battleEvent.Kind == BattleRuntimeEventKind.TurnRestricted &&
            battleEvent.ActorId == scenario.Player.State.InstanceId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.ResourceChanged &&
            battleEvent.ActorId == scenario.Player.State.InstanceId);
        Assert.Contains(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.StatusChanged &&
            battleEvent.ActorId == scenario.Player.State.InstanceId);
        Assert.All(
            result.Events.Where(battleEvent => battleEvent.Kind == BattleRuntimeEventKind.TurnEconomyChanged),
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

        BattleRuntimeEvent[] economyEvents = result.Events
            .Where(battleEvent => battleEvent.Kind == BattleRuntimeEventKind.TurnEconomyChanged)
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
                direct.Lifecycle,
                new RestrictedPassTurnHandler(),
                new LastTeamStandingCompletionPolicy(),
                directEconomy.CreateEconomy,
                directEconomy.PhaseProgress));

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
            .Where(battleEvent => battleEvent.Kind == BattleRuntimeEventKind.TurnEconomyChanged)
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
            Id("test.pack:player"), RuntimeInstanceId.Parse("player"), PlayerTeam, 1, IsDeployed: true)).RequireActor();
        CatalogBattleActor enemy = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:enemy"), RuntimeInstanceId.Parse("enemy"), EnemyTeam, 1, IsDeployed: true)).RequireActor();
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = CreateAutomatedRunner(
            executor, new DeterministicBattleActionSelector(executor), services).Run(
            new AutomatedBattleRequest([player, enemy], Battle, NormalBattle, NewMoon, 1));

        BattleRuntimeEvent activation = Assert.Single(result.Events, battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.PassiveActivated &&
            battleEvent.SkillId == openingPassive.Id);
        Assert.True(activation.Sequence < result.Events.First(battleEvent =>
            battleEvent.Kind == BattleRuntimeEventKind.RoundStarted).Sequence);
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
        Assert.Contains(result.Events, battleEvent => battleEvent.Kind == BattleRuntimeEventKind.BattleFaulted);
    }

    [Theory]
    [InlineData(TurnEconomyOutcome.Normal, false, false, 1, 0)]
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

        engine.ConsumeAction(new TurnEconomyResolution(outcome, critical, terminates));

        Assert.Equal(expectedFull, engine.FullTokens);
        Assert.Equal(expectedBlinking, engine.PartialTokens);
    }

    [Fact]
    public void ActionTokenPass_ConsumesAnExistingPartialTokenBeforeAFullToken()
    {
        var engine = new ActionTokenTurnEconomy();
        engine.StartPhase(2);
        engine.ConsumeAction(new TurnEconomyResolution(
            TurnEconomyOutcome.Weakness,
            AnyCritical: false,
            TerminatesPhase: false));

        Assert.Equal(1, engine.FullTokens);
        Assert.Equal(1, engine.PartialTokens);

        engine.Pass();

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
                IsDeployed: true)).RequireActor();

    private static BattleExecutionServices Services(
        GameDataCatalog catalog,
        IRandomTargetSelectionPolicy? randomTargetPolicy = null) => new(
        catalog,
        new TestDamagePolicy(),
        new NeverInstantDeathPolicy(),
        new TestAilmentPolicy(),
        new AlwaysChancePolicy(),
        new TestPowerPolicy(),
        randomTargetPolicy ?? new FirstRandomTargetPolicy(),
        new OrderedRuntimeTargetSelectionPolicy());

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
            services,
            lifecycle ?? new BattleStatusEncounterLifecyclePort(
                new BattleStatusLifecycleService(new MinimumRandomSource()),
                services,
                Id("battle_start"),
                Id("owner_turn_end")),
            turnEconomy ?? StandardTurnEconomy(),
            restrictionResolver ?? new AutomatedBattleTurnRestrictionResolver());

    private static AutomatedBattleTurnRestrictionResolver RestrictionResolver(
        ISkillExecutor skillExecutor,
        BattleExecutionServices services,
        IAutomatedBattleRestrictionActionSource source) =>
        new(
            new BattleActionExecutor(
                skillExecutor,
                new ItemExecutor(services),
                services),
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
        var duration = new TurnDurationDefinition(2, Id("owner_turn_end"), false);
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
        player.State.AddOtherStatus(battleStatusId, new BattleDurationDefinition());

        BattleExecutionServices services = Services(catalog);
        var lifecycle = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new MinimumRandomSource()),
            services,
            Id("battle_start"),
            Id("owner_turn_end"));
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

    private static CatalogBattleActor RuntimeCatalogActor(
        string entityId,
        string instanceId,
        ContentId teamId,
        IEnumerable<SkillDefinition>? loadout = null,
        CombatDefenseProfile? defense = null)
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
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true));
        return new CatalogBattleActor(entity, state, skills);
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
            MissingHostedEntityBehavior.UseActorBaseStats);

    private static RuntimeActorSnapshot RestorableActorSnapshot(
        string instanceId,
        EntityDefinition entity,
        IReadOnlyDictionary<ContentId, decimal> baseStats,
        IReadOnlyDictionary<ContentId, decimal>? effectiveStats = null) =>
        new(
            new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse(instanceId),
                entity.Id,
                entity.EntityKindId,
                instanceId),
            new RuntimeActorOwnershipSnapshot(Id("runtime"), PlayerTeam),
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
            StandardProgressionIds.Hp);

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
            owner.Progression.Level,
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

    private sealed record LifecycleScenario(
        CatalogBattleActor Player,
        CatalogBattleActor Enemy,
        BattleExecutionServices Services,
        IBattleEncounterLifecyclePort Lifecycle,
        ContentId AilmentId,
        ContentId BattleStatusId);
}
