using System.Collections.ObjectModel;
using System.Reflection;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.SkillSystem;

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
            entity.Id, RuntimeInstanceId.Parse("instance"), PlayerTeam, 5)).RequireActor();

        Assert.Equal([first.Id, second.Id, third.Id], actor.SkillLoadout.Select(skill => skill.Id));
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
            entity.Id, RuntimeInstanceId.Parse("instance"), PlayerTeam, 0));
        CatalogBattleActorCreationResult missingEntity = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:unknown"), RuntimeInstanceId.Parse("instance"), PlayerTeam, 1));

        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.InvalidLevel);
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.SkillMissing);
        Assert.Contains(missingEntity.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.EntityMissing);
        Assert.False(invalid.IsSuccess);
        Assert.Throws<CatalogBattleActorCreationException>(() => invalid.RequireActor());
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
            1));

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
            1));

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
            new RuntimeActorDeploymentSnapshot(RuntimeActorDeployment.Reserve, false, true),
            new RuntimeProgressionSnapshot(9, 12, 100, 3),
            [
                new RuntimeResourceSnapshot(Id("life"), 7, 25),
                new RuntimeResourceSnapshot(Id("sp"), 4, 11)
            ],
            new RuntimeStatBlockSnapshot(
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 5)],
                [new KeyValuePair<ContentId, decimal>(Id("magic"), 8)]),
            new RuntimeSkillStateSnapshot(),
            new RuntimeFormStockSnapshot(),
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

        CatalogBattleActorCreationResult result = factory.Restore(snapshot);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        RuntimeActorState state = result.RequireActor().State;
        Assert.Equal(Id("life"), state.VitalResourceId);
        Assert.Equal(7, state.GetRequiredResource(Id("life")).Current);
        Assert.Equal(9, state.Progression.Level);
        Assert.False(state.IsActive);
        Assert.True(state.Deployment.HasSwappedThisTurn);
        Assert.True(state.IsGuarding);
        Assert.IsType<PhaseDurationDefinition>(state.StatStages[Id("attack")].Duration);
        Assert.IsType<BattleDurationDefinition>(state.AffinityOverrides[DamageElement.Ice].Duration);
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
    public void Runner_ExecutesDeterministicKnowledgePassiveAndPressTurnLifecycle()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var runner = new AutomatedBattleRunner(executor, new DeterministicBattleActionSelector(executor), services);

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
    public void Runner_AllowsMissingMoonPhaseWhenContentDoesNotUseMoonConditions()
    {
        GameDataCatalog catalog = LoadDemoCatalog();
        CatalogBattleActor frost = CreateDemoActor(catalog, "frost_duelist_demo", "frost", PlayerTeam);
        CatalogBattleActor ember = CreateDemoActor(catalog, "ember_duelist_demo", "ember", EnemyTeam);
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);
        var runner = new AutomatedBattleRunner(executor, new DeterministicBattleActionSelector(executor), services);

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

        AutomatedBattleResult result = new AutomatedBattleRunner(
            executor, new DeterministicBattleActionSelector(executor), services).Run(
            new AutomatedBattleRequest([frost, ember], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Draw, result.Outcome);
        Assert.Null(result.WinningTeamId);
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

        AutomatedBattleResult result = new AutomatedBattleRunner(
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

        new AutomatedBattleRunner(
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
            Id("test.pack:player"), RuntimeInstanceId.Parse("player"), PlayerTeam, 1)).RequireActor();
        CatalogBattleActor enemy = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:enemy"), RuntimeInstanceId.Parse("enemy"), EnemyTeam, 1)).RequireActor();
        BattleExecutionServices services = Services(catalog);
        var executor = new SkillExecutor(services);

        AutomatedBattleResult result = new AutomatedBattleRunner(
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

        AutomatedBattleResult result = new AutomatedBattleRunner(
            executor, new InvalidTargetSelector(), services).Run(
            new AutomatedBattleRequest([frost, ember], Battle, NormalBattle, NewMoon, 1));

        Assert.Equal(AutomatedBattleOutcome.Faulted, result.Outcome);
        Assert.NotNull(result.FaultMessage);
        Assert.Contains(result.Events, battleEvent => battleEvent.Kind == BattleRuntimeEventKind.BattleFaulted);
    }

    [Theory]
    [InlineData(PressTurnOutcome.Normal, false, false, 1, 0)]
    [InlineData(PressTurnOutcome.Weakness, false, false, 1, 1)]
    [InlineData(PressTurnOutcome.Critical, true, false, 1, 1)]
    [InlineData(PressTurnOutcome.Miss, false, false, 0, 0)]
    [InlineData(PressTurnOutcome.Null, false, false, 0, 0)]
    [InlineData(PressTurnOutcome.Repel, false, true, 0, 0)]
    [InlineData(PressTurnOutcome.Absorb, false, true, 0, 0)]
    public void CleanPressTurnOverload_ConsumesEveryTypedOutcome(
        PressTurnOutcome outcome,
        bool critical,
        bool terminates,
        int expectedFull,
        int expectedBlinking)
    {
        var engine = new PressTurnEngine();
        engine.StartPhase(2);

        engine.ConsumeAction(new PressTurnResolution(outcome, critical, terminates));

        Assert.Equal(expectedFull, engine.FullIcons);
        Assert.Equal(expectedBlinking, engine.BlinkingIcons);
    }

    [Fact]
    public void RuntimePublicApi_DoesNotExposeHostSerializerFilesystemOrLegacyTypes()
    {
        Type[] publicTypes = typeof(CatalogBattleActorFactory).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "JRPGPrototype.Logic.Battle.Runtime")
            .ToArray();
        string[] forbidden =
        [
            "Newtonsoft", "System.Text.Json", "Godot", "System.IO.File", "Database",
            "Combatant", "SkillData", "PersonaData"
        ];

        IEnumerable<Type> signatures = publicTypes.SelectMany(PublicSignatureTypes);

        Assert.DoesNotContain(signatures, type =>
            forbidden.Any(token => (type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal)));
    }

    private static GameDataCatalog LoadDemoCatalog()
    {
        string root = Path.Combine(FindRepositoryRoot(), "Data", "Jsons");
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
            File.ReadAllText(Path.Combine(root, manifest)),
            documents.Select(path => new ContentDocumentText(
                path,
                path,
                File.ReadAllText(Path.Combine(root, path)))));

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterEntityKind("demon")
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
                5)).RequireActor();

    private static BattleExecutionServices Services(
        GameDataCatalog catalog,
        IRandomTargetSelectionPolicy? randomTargetPolicy = null) => new(
        catalog,
        new TestDamagePolicy(),
        new NeverInstantDeathPolicy(),
        new TestAilmentPolicy(),
        new AlwaysChancePolicy(),
        new TestPowerPolicy(),
        randomTargetPolicy ?? new FirstRandomTargetPolicy());

    private static SkillDefinition Active(
        string id,
        DamageElement element,
        TargetingDefinition? targeting = null) => new(
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
        availability: new SkillAvailabilityDefinition([Battle]));

    private static CatalogBattleActor RuntimeCatalogActor(
        string entityId,
        string instanceId,
        ContentId teamId,
        IEnumerable<SkillDefinition>? loadout = null)
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
            CombatDefenseProfile.Empty,
            [new BattleResourceState(Id("hp"), 100, 100)]);
        return new CatalogBattleActor(entity, state, skills);
    }

    private static EntityDefinition Entity(
        string id,
        IEnumerable<ContentId> baseSkills,
        IEnumerable<SkillUnlockDefinition>? unlocks = null) => new(
        Id(id), id, id, Id("demon"), Id("test.pack:race"), 1, 1,
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
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JRPG.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find JRPG.sln.");
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

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
        public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request)
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
            return [new DamageHitResolution(true, damage)];
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
}
