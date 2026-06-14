using System.Collections.ObjectModel;
using System.Reflection;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle.Engines;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
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
            entity.Id, Id("instance"), PlayerTeam, 5)).RequireActor();

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
            entity.Id, Id("instance"), PlayerTeam, 0));
        CatalogBattleActorCreationResult missingEntity = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:unknown"), Id("instance"), PlayerTeam, 1));

        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.InvalidLevel);
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.SkillMissing);
        Assert.Contains(missingEntity.Diagnostics, diagnostic => diagnostic.Code == CatalogBattleActorDiagnosticCode.EntityMissing);
        Assert.False(invalid.IsSuccess);
        Assert.Throws<CatalogBattleActorCreationException>(() => invalid.RequireActor());
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
            Id("test.pack:player"), Id("player"), PlayerTeam, 1)).RequireActor();
        CatalogBattleActor enemy = factory.Create(new CatalogBattleActorCreationRequest(
            Id("test.pack:enemy"), Id("enemy"), EnemyTeam, 1)).RequireActor();
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
                Id(instanceId),
                teamId,
                5)).RequireActor();

    private static BattleExecutionServices Services(GameDataCatalog catalog) => new(
        catalog,
        new TestDamagePolicy(),
        new NeverInstantDeathPolicy(),
        new TestAilmentPolicy(),
        new AlwaysChancePolicy(),
        new TestPowerPolicy(),
        new FirstRandomTargetPolicy());

    private static SkillDefinition Active(string id, DamageElement element) => new(
        Id(id), id, id, SkillActivation.Active, SkillMenuGroup.Offense,
        element switch
        {
            DamageElement.Fire => InheritanceGroup.Fire,
            DamageElement.Ice => InheritanceGroup.Ice,
            DamageElement.Wind => InheritanceGroup.Wind,
            _ => InheritanceGroup.Physical
        },
        new SkillInheritanceDefinition(true),
        targeting: new TargetingDefinition(TargetRelation.Enemy, TargetSelection.Single, TargetLifeState.Alive, false),
        effects: [new DamageEffectDefinition(element, 1, 100, new NeverCriticalDefinition(), new HitCountDefinition(1, 1))],
        availability: new SkillAvailabilityDefinition([Battle]));

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
        public IReadOnlyList<BattleActorState> Select(
            IReadOnlyList<BattleActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) => candidates.Take(count.Minimum).ToArray();
    }

    private sealed class InvalidTargetSelector : IBattleActionSelector
    {
        public BattleActionSelection Select(BattleActionSelectionRequest request) =>
            new(
                BattleActionSelectionStatus.Selected,
                request.Actor.ActiveSkills[0],
                [Id("missing_target")]);
    }
}
