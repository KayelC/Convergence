using System.Reflection;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.DemoHost;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.DemoHost.Tests.Content;

public sealed class SharedEffectsRuntimeTests
{
    private static readonly ContentId Battle = Id("battle");
    private static readonly ContentId Field = Id("field");
    private static readonly ContentId Team = Id("team");
    private static readonly ContentId Hp = Id("hp");
    private static readonly ContentId Sp = Id("sp");

    [Fact]
    public void ItemDocuments_StrictlyDeserializeEveryKindAndRejectUnknownFields()
    {
        string json =
            """
            {
              "schemaVersion": 6,
              "items": [
                {
                  "id": "medicine", "displayName": "Medicine", "description": "Heal.",
                  "itemKind": "consumable", "stackLimit": 99, "baseValue": 100,
                  "usage": {
                    "contexts": ["field"], "consumeOn": "successful_execution",
                    "targeting": { "relation": "ally", "selection": "single", "lifeState": "alive", "allowSelf": true },
                    "effects": [{ "type": "restore_resource", "resourceId": "hp", "amount": { "type": "flat", "value": 10 } }]
                  }
                },
                { "id": "key", "displayName": "Key", "description": "Key.", "itemKind": "key", "stackLimit": 1, "baseValue": 0 },
                { "id": "ore", "displayName": "Ore", "description": "Ore.", "itemKind": "material", "stackLimit": 99, "baseValue": 10 },
                { "id": "coin", "displayName": "Coin", "description": "Coin.", "itemKind": "valuable", "stackLimit": 99, "baseValue": 1000 }
              ]
            }
            """;
        var deserializer = new SkillSystemJsonDeserializer();

        DeserializedContentDocument<ItemDefinition> document = deserializer.DeserializeItems(json, "items.json");
        string unknown = json.Replace("\"stackLimit\": 1", "\"stackLimit\": 1, \"mystery\": true", StringComparison.Ordinal);

        Assert.Equal([ItemKind.Consumable, ItemKind.Key, ItemKind.Material, ItemKind.Valuable],
            document.Records.Select(item => item.ItemKind));
        Assert.NotNull(document.Records[0].Usage);
        Assert.All(document.Records.Skip(1), item => Assert.Null(item.Usage));
        Assert.Throws<ContentDeserializationException>(() => deserializer.DeserializeItems(unknown, "unknown.json"));
    }

    [Fact]
    public void SharedDemoPack_LoadsQualifiedItemsAndReferences()
    {
        GameDataCatalog catalog = LoadCatalog();

        Assert.Equal(6, catalog.Items.Count);
        ItemDefinition cure = catalog.GetRequiredItem(Id("convergence.shared_effects_demo:dis_poison_demo"));
        var remove = Assert.IsType<RemoveAilmentEffectDefinition>(Assert.Single(cure.Usage!.Effects));
        EntityDefinition medic = catalog.GetRequiredEntity(Id("convergence.shared_effects_demo:field_medic_demo"));

        Assert.Equal(Id("convergence.shared_effects_demo:poison_demo"), Assert.Single(remove.AilmentIds));
        Assert.Equal(Id("convergence.clean_battle_demo:demo_spirit"), medic.RaceId);
        Assert.Equal(Id("convergence.shared_effects_demo:field_recovery_demo"), Assert.Single(medic.BaseSkillIds));
        Assert.Throws<ArgumentException>(() => catalog.GetRequiredItem(Id("medicine_demo")));
    }

    [Fact]
    public void ItemValidation_ReportsShapeContextAndRangeErrors()
    {
        ItemDefinition item = new(
            Id("invalid"), "Invalid", "Invalid", ItemKind.Consumable, 0, -1,
            new ItemUsageDefinition(
                [Id("missing_context")],
                new TargetingDefinition(TargetRelation.Ally, TargetSelection.Single, TargetLifeState.Alive, true),
                []));
        var manifest = new ContentPackManifest(
            2, "test.pack", SemanticVersion.Parse("1.0.0"), "Test", null, null,
            [new ContentPackDocumentReference("items", "items.json")]);
        var request = new SkillSystemValidationRequest(
            manifest,
            "manifest.json",
            new SkillSystemRegistrationBuilder().Build(),
            itemDocuments:
            [
                new SourceContentDocument<ItemDefinition>(
                    "items.json",
                    "items.json",
                    new DeserializedContentDocument<ItemDefinition>(3, [item]))
            ]);

        ContentValidationResult result = new SkillSystemContentValidator().Validate(request);

        Assert.Contains(result.Errors, error => error.JsonPath == "$.items[0].stackLimit" && error.Code == ContentValidationErrorCode.ValueMustBePositive);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.items[0].baseValue" && error.Code == ContentValidationErrorCode.ValueMustBeNonNegative);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.items[0].usage.contexts[0]" && error.Code == ContentValidationErrorCode.RegistrationMissing);
        Assert.Contains(result.Errors, error => error.JsonPath == "$.items[0].usage.effects" && error.Code == ContentValidationErrorCode.ShapeInvalid);
    }

    [Fact]
    public void FieldSkillAndMedicine_UseEquivalentRestoreEffects()
    {
        GameDataCatalog catalog = LoadCatalog();
        BattleExecutionServices services = Services(catalog);
        RuntimeActorState skillActor = Actor("skill_actor", 100, 100, 20, 20);
        RuntimeActorState skillTarget = Actor("skill_target", 50, 100, 20, 20);
        RuntimeActorState itemActor = Actor("item_actor", 100, 100, 20, 20);
        RuntimeActorState itemTarget = Actor("item_target", 50, 100, 20, 20);
        SkillDefinition skill = catalog.GetRequiredSkill(Id("convergence.shared_effects_demo:field_recovery_demo"));
        ItemDefinition item = catalog.GetRequiredItem(Id("convergence.shared_effects_demo:medicine_demo"));

        SkillExecutionResult skillResult = new SkillExecutor(services).Execute(new SkillExecutionRequest(
            skill, skillActor, [skillActor, skillTarget], new EffectExecutionEnvironment(Field), [skillTarget.InstanceId]));
        ItemExecutionResult itemResult = new ItemExecutor(services).Execute(new ItemExecutionRequest(
            item, itemActor, [itemActor, itemTarget], new EffectExecutionEnvironment(Field), [itemTarget.InstanceId]));

        Assert.Equal(75, skillTarget.GetRequiredResource(Hp).Current);
        Assert.Equal(75, itemTarget.GetRequiredResource(Hp).Current);
        Assert.Equal(skillResult.Effects[0].Value, itemResult.Effects[0].Value);
        Assert.Equal(ItemConsumptionDecision.ConsumeOne, itemResult.Consumption);
    }

    [Fact]
    public void ItemExecutor_RejectsKnownNoEffectWithoutMutationOrConsumption()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorState actor = Actor("actor", 100, 100, 20, 20);
        ItemDefinition medicine = catalog.GetRequiredItem(Id("convergence.shared_effects_demo:medicine_demo"));
        var executor = new ItemExecutor(Services(catalog));

        ItemExecutionResult result = executor.Execute(new ItemExecutionRequest(
            medicine, actor, [actor], new EffectExecutionEnvironment(Field), [actor.InstanceId]));

        Assert.Equal(ItemExecutionStatus.Rejected, result.Status);
        Assert.Equal(ItemConsumptionDecision.None, result.Consumption);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == ItemExecutionDiagnosticCode.NoApplicableEffect);
        Assert.Equal(100, actor.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void ItemHealing_UsesPassiveHealingModifiersWithoutInspectingDisplayText()
    {
        var passive = new SkillDefinition(
            Id("healing_mastery"),
            "Unrelated Display Text",
            "The runtime uses typed modifiers.",
            SkillActivation.Passive,
            null,
            InheritanceGroup.Passive,
            new SkillInheritanceDefinition(true),
            modifiers:
            [
                new NumericRuleModifierDefinition(
                    NumericRuleModifierType.HealingGiven,
                    ModifierOperation.Multiply,
                    2)
            ]);
        RuntimeActorState actor = new(
            RuntimeInstanceId.Parse("actor"), Id("entity_actor"), Team, Hp, new CombatDefenseProfile(),
            [new BattleResourceState(Hp, 100, 100), new BattleResourceState(Sp, 20, 20)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(Id("test_host"), Team),
            passiveSkills: [passive]);
        RuntimeActorState target = Actor("target", 50, 100, 20, 20);
        ItemDefinition item = Consumable(
            "medicine",
            new TargetingDefinition(TargetRelation.Ally, TargetSelection.Single, TargetLifeState.Alive, true),
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10)));

        ItemExecutionResult result = new ItemExecutor(Services(EmptyAilments.Instance)).Execute(new ItemExecutionRequest(
            item, actor, [actor, target], new EffectExecutionEnvironment(Field), [target.InstanceId]));

        Assert.Equal(70, target.GetRequiredResource(Hp).Current);
        Assert.Equal(20, Assert.Single(result.Effects).Value);
    }

    [Fact]
    public void MultiTargetItem_ConsumesOnceWhenOnlyOneTargetChanges()
    {
        RuntimeActorState actor = Actor("actor", 100, 100, 20, 20);
        RuntimeActorState hurt = Actor("hurt", 50, 100, 20, 20);
        ItemDefinition item = Consumable(
            "party_medicine",
            new TargetingDefinition(TargetRelation.Ally, TargetSelection.All, TargetLifeState.Alive, true),
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10)));
        var executor = new ItemExecutor(Services(EmptyAilments.Instance));

        ItemExecutionResult result = executor.Execute(new ItemExecutionRequest(
            item, actor, [actor, hurt], new EffectExecutionEnvironment(Field)));

        Assert.Equal(ItemConsumptionDecision.ConsumeOne, result.Consumption);
        Assert.Equal(100, actor.GetRequiredResource(Hp).Current);
        Assert.Equal(60, hurt.GetRequiredResource(Hp).Current);
        Assert.Equal(2, result.Effects.Count);
    }

    [Fact]
    public void CureReviveEscapeAndHostRequest_AreTypedAndConsumeOnSuccess()
    {
        GameDataCatalog catalog = LoadCatalog();
        RuntimeActorState actor = Actor("actor", 100, 100, 20, 20);
        RuntimeActorState target = Actor("target", 100, 100, 20, 20);
        target.ApplyAilment(
            catalog.GetRequiredAilment(Id("convergence.shared_effects_demo:poison_demo")),
            new TurnDurationDefinition(3, Id("owner_turn_end"), false));
        var executor = new ItemExecutor(Services(catalog));

        ItemExecutionResult cure = Execute(executor, catalog, "dis_poison_demo", Field, actor, target);
        target.SetResource(Hp, 0);
        ItemExecutionResult revive = Execute(executor, catalog, "revival_bead_demo", Field, actor, target);
        ItemExecutionResult escape = Execute(executor, catalog, "battle_exit_charm_demo", Battle, actor, null);
        ItemExecutionResult returnBeacon = Execute(executor, catalog, "return_beacon_demo", Field, actor, null);

        Assert.False(target.HasAilment(Id("convergence.shared_effects_demo:poison_demo")));
        Assert.False(target.IsDefeated);
        Assert.True(escape.EscapeRequested);
        Assert.Equal(Id("request_dungeon_exit"), Assert.Single(returnBeacon.HostActionRequestIds));
        Assert.All([cure, revive, escape, returnBeacon], result => Assert.Equal(ItemConsumptionDecision.ConsumeOne, result.Consumption));
    }

    [Fact]
    public void BattleOnlyConditions_AreFalseWhenFieldMetadataIsAbsent()
    {
        RuntimeActorState actor = Actor("actor", 50, 100, 20, 20);
        ItemDefinition item = Consumable(
            "battle_condition",
            new TargetingDefinition(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Alive, true),
            new RestoreResourceEffectDefinition(
                Hp,
                new FlatAmountDefinition(10),
                new BattleKindConditionDefinition([Id("normal_battle")])));

        ItemExecutionResult result = new ItemExecutor(Services(EmptyAilments.Instance)).Execute(new ItemExecutionRequest(
            item, actor, [actor], new EffectExecutionEnvironment(Field), [actor.InstanceId]));

        Assert.Equal(ItemExecutionStatus.Executed, result.Status);
        Assert.Equal(EffectExecutionOutcome.Skipped, Assert.Single(result.Effects).Outcome);
        Assert.Equal(ItemConsumptionDecision.None, result.Consumption);
        Assert.Equal(50, actor.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void RuntimeActorAndItemCollections_AreDefensiveSnapshots()
    {
        var contexts = new List<ContentId> { Field };
        var effects = new List<EffectDefinition> { new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10)) };
        var usage = new ItemUsageDefinition(
            contexts,
            new TargetingDefinition(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Alive, true),
            effects);
        RuntimeActorState actor = Actor("actor", 50, 100, 20, 20);

        contexts.Clear();
        effects.Clear();

        Assert.Single(usage.ContextIds);
        Assert.Single(usage.Effects);
        Assert.Throws<NotSupportedException>(() => ((IList<ContentId>)usage.ContextIds).Add(Battle));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<ContentId, BattleResourceState>)actor.Resources).Add(
            Id("mp"), new BattleResourceState(Id("mp"), 1, 1)));
    }

    [Fact]
    public void CleanFieldDemo_RunsWithoutInput()
    {
        var output = new StringWriter();
        int exitCode = new CleanFieldDemoHost(output, Path.Combine(AppContext.BaseDirectory, "Content")).Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("request_dungeon_exit", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("completed successfully", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedEffectsPublicBoundary_ExposesNoHostOrLegacyInfrastructure()
    {
        Type[] publicTypes =
        [
            typeof(RuntimeActorState), typeof(EffectExecutionEnvironment), typeof(IItemExecutor),
            typeof(ItemExecutionRequest), typeof(ItemExecutionResult), typeof(IItemDefinitionRepository)
        ];
        string[] forbidden =
        [
            "System.Text.Json", "Newtonsoft", "Godot", "System.IO", "Database",
            "Combatant", "SkillData", string.Concat("Per", "sona", "Data"), "ItemData"
        ];

        IEnumerable<Type> signatures = publicTypes.SelectMany(PublicSignatureTypes);

        Assert.DoesNotContain(signatures, type =>
            forbidden.Any(token => (type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal)));
    }

    private static ItemExecutionResult Execute(
        ItemExecutor executor,
        GameDataCatalog catalog,
        string itemId,
        ContentId context,
        RuntimeActorState actor,
        RuntimeActorState? target)
    {
        RuntimeActorState[] participants = target is null ? [actor] : [actor, target];
        return executor.Execute(new ItemExecutionRequest(
            catalog.GetRequiredItem(Id($"convergence.shared_effects_demo:{itemId}")),
            actor,
            participants,
            context == Battle
                ? new EffectExecutionEnvironment(Battle, Id("normal_battle"), Id("new_moon"))
                : new EffectExecutionEnvironment(Field),
            target is null ? [] : [target.InstanceId]));
    }

    private static ItemDefinition Consumable(
        string id,
        TargetingDefinition targeting,
        params EffectDefinition[] effects) =>
        new(
            Id(id), id, id, ItemKind.Consumable, 99, 1,
            new ItemUsageDefinition([Battle, Field], targeting, effects));

    private static RuntimeActorState Actor(
        string id,
        decimal hp,
        decimal maxHp,
        decimal sp,
        decimal maxSp) =>
        new(
            RuntimeInstanceId.Parse(id), Id($"entity_{id}"), Team, Hp, new CombatDefenseProfile(),
            [new BattleResourceState(Hp, hp, maxHp), new BattleResourceState(Sp, sp, maxSp)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(Id("test_host"), Team));

    private static BattleExecutionServices Services(IAilmentDefinitionRepository ailments) =>
        new(
            ailments,
            new ZeroDamagePolicy(),
            new NeverInstantDeathPolicy(),
            new AlwaysAilmentPolicy(),
            new AlwaysChancePolicy(),
            new FlatPowerPolicy(),
            new FirstBattleTargetPolicy(),
            new OrderedRuntimeTargetSelectionPolicy(),
            DemoHostTestStatModifierPolicy.CreatePersistent(),
            new SplitChargePolicy(),
            escapeRuleHandlers:
            [
                new KeyValuePair<ContentId, IEscapeRuleHandler>(
                    Id("standard_escape"),
                    new AlwaysEscapeHandler())
            ],
            customEffectHandlers:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(
                    Id("request_dungeon_exit"),
                    new HostRequestHandler())
            ]);

    private static GameDataCatalog LoadCatalog()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Content");
        ContentPackTextBundle reference = Bundle(root,
            "skill_system_redesign.manifest.sample.json",
            "skill_system_redesign.races.sample.json",
            "skill_system_redesign.skills.sample.json",
            "skill_system_redesign.entities.sample.json");
        ContentPackTextBundle battle = Bundle(root,
            "clean_battle_demo.manifest.json",
            "clean_battle_demo.races.json",
            "clean_battle_demo.skills.json",
            "clean_battle_demo.entities.json");
        ContentPackTextBundle shared = Bundle(root,
            "shared_effects_demo.manifest.json",
            "shared_effects_demo.ailments.json",
            "shared_effects_demo.skills.json",
            "shared_effects_demo.entities.json",
            "shared_effects_demo.items.json");

        return new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(Registrations(), [reference, battle, shared])).RequireCatalog();
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
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterEntityKind("companion")
            .RegisterAilmentGroup("poison")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
            .RegisterEscapeRule("standard_escape")
            .RegisterCustomEffect("request_dungeon_exit", new AcceptParameters())
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportEffect<RemoveAilmentEffectDefinition>()
            .SupportEffect<ReviveEffectDefinition>()
            .SupportEffect<EscapeEffectDefinition>()
            .SupportEffect<CustomEffectDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .Build();

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

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class EmptyAilments : IAilmentDefinitionRepository
    {
        public static EmptyAilments Instance { get; } = new();
        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = null;
            return false;
        }
        public AilmentDefinition GetRequiredAilment(ContentId id) => throw new KeyNotFoundException();
    }

    private sealed class ZeroDamagePolicy : IDamageExecutionPolicy
    {
        public DamagePolicyResolution Resolve(DamagePolicyRequest request) =>
            new([new DamageHitResolution(true, 0)], request.Affinity);
    }

    private sealed class NeverInstantDeathPolicy : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
    }

    private sealed class AlwaysAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => true;
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => true;
    }

    private sealed class FlatPowerPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
    }

    private sealed class FirstBattleTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) => candidates.Take(count.Minimum).ToArray();
    }

    private sealed class AlwaysEscapeHandler : IEscapeRuleHandler
    {
        public bool CanEscape(EscapeEffectDefinition effect, EffectExecutionContext context) => true;
    }

    private sealed class HostRequestHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context) =>
            new(
                context.EffectIndex,
                context.Target?.InstanceId,
                EffectExecutionOutcome.Success,
                HostActionRequestIds: [Id("request_dungeon_exit")]);
    }

    private sealed class AcceptParameters : IContentParameterValidator
    {
        public IReadOnlyList<ContentParameterValidationIssue> Validate(
            IReadOnlyDictionary<string, object?> parameters) => [];
    }
}
