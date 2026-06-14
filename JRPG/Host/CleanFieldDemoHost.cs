using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;

namespace JRPGPrototype.Host;

internal sealed class AcceptAnyParametersValidator : IContentParameterValidator
{
    public IReadOnlyList<ContentParameterValidationIssue> Validate(
        IReadOnlyDictionary<string, object?> parameters) => [];
}

internal sealed class DemoEscapeRuleHandler : IEscapeRuleHandler
{
    public bool CanEscape(EscapeEffectDefinition effect, EffectExecutionContext context) => true;
}

internal sealed class DungeonExitRequestHandler : ICustomEffectHandler
{
    private static readonly ContentId RequestId = ContentId.Parse("request_dungeon_exit");

    public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context) =>
        new(
            context.EffectIndex,
            context.Target?.InstanceId,
            EffectExecutionOutcome.Success,
            RelatedId: effect.HandlerId,
            Detail: "The host received a dungeon-exit request.",
            HostActionRequestIds: [RequestId]);
}

internal sealed class CleanFieldDemoHost
{
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId Field = ContentId.Parse("field");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId NewMoon = ContentId.Parse("new_moon");
    private static readonly ContentId Party = ContentId.Parse("party");

    private readonly TextWriter _output;
    private readonly string _contentRoot;

    public CleanFieldDemoHost(TextWriter output, string? contentRoot = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _contentRoot = contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Data", "Jsons");
    }

    public int Run()
    {
        CatalogLoadResult load;
        try
        {
            var source = new FileContentPackSource(_contentRoot);
            load = new SkillSystemCatalogLoader().Load(new SkillSystemCatalogLoadRequest(
                BuildRegistrations(),
                [
                    source.Read(
                        "skill_system_redesign.manifest.sample.json",
                        "skill_system_redesign.races.sample.json",
                        "skill_system_redesign.skills.sample.json",
                        "skill_system_redesign.entities.sample.json"),
                    source.Read(
                        "clean_battle_demo.manifest.json",
                        "clean_battle_demo.races.json",
                        "clean_battle_demo.skills.json",
                        "clean_battle_demo.entities.json"),
                    source.Read(
                        "shared_effects_demo.manifest.json",
                        "shared_effects_demo.ailments.json",
                        "shared_effects_demo.skills.json",
                        "shared_effects_demo.entities.json",
                        "shared_effects_demo.items.json")
                ]));
        }
        catch (Exception exception)
        {
            _output.WriteLine($"Content read failed: {exception.Message}");
            return 2;
        }

        if (!load.IsSuccess || load.Catalog is null)
        {
            foreach (CatalogLoadDiagnostic diagnostic in load.Diagnostics)
            {
                _output.WriteLine($"[{diagnostic.Code}] {diagnostic.SourceName} {diagnostic.JsonPath}: {diagnostic.Message}");
            }
            return 3;
        }

        GameDataCatalog catalog = load.Catalog;
        var factory = new CatalogBattleActorFactory(catalog, catalog, new DemoBattleActorInitializationPolicy());
        CatalogBattleActorCreationResult medicResult = factory.Create(new CatalogBattleActorCreationRequest(
            ContentId.Parse("convergence.shared_effects_demo:field_medic_demo"),
            ContentId.Parse("field_medic"),
            Party,
            5));
        CatalogBattleActorCreationResult allyResult = factory.Create(new CatalogBattleActorCreationRequest(
            ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo"),
            ContentId.Parse("field_ally"),
            Party,
            5));
        if (!medicResult.IsSuccess || !allyResult.IsSuccess)
        {
            foreach (CatalogBattleActorDiagnostic diagnostic in medicResult.Diagnostics.Concat(allyResult.Diagnostics))
            {
                _output.WriteLine($"[{diagnostic.Code}] {diagnostic.Message}");
            }
            return 4;
        }

        CatalogBattleActor medic = medicResult.RequireActor();
        CatalogBattleActor ally = allyResult.RequireActor();
        RuntimeActorState[] participants = [medic.State, ally.State];
        BattleExecutionServices services = CreateExecutionServices(catalog);
        var skillExecutor = new SkillExecutor(services);
        var itemExecutor = new ItemExecutor(services);
        var inventory = catalog.Items.Keys.ToDictionary(id => id, _ => 1);
        int sequence = 1;

        ally.State.AddResource(ally.State.VitalResourceId, -30);
        SkillDefinition fieldRecovery = catalog.GetRequiredSkill(
            ContentId.Parse("convergence.shared_effects_demo:field_recovery_demo"));
        SkillExecutionResult skill = skillExecutor.Execute(new SkillExecutionRequest(
            fieldRecovery,
            medic.State,
            participants,
            new EffectExecutionEnvironment(Field),
            [ally.State.InstanceId]));
        Print(ref sequence, "skill", $"{fieldRecovery.DisplayName}: {skill.Status}; restored {skill.Effects.Sum(effect => effect.Value ?? 0)} HP.");

        ally.State.AddResource(ally.State.VitalResourceId, -20);
        ExecuteItem("medicine_demo", Field, ally.State.InstanceId, itemExecutor, catalog, participants, inventory, ref sequence);

        AilmentDefinition poison = catalog.GetRequiredAilment(
            ContentId.Parse("convergence.shared_effects_demo:poison_demo"));
        ally.State.ApplyAilment(poison, poison.DefaultDuration);
        ExecuteItem("dis_poison_demo", Field, ally.State.InstanceId, itemExecutor, catalog, participants, inventory, ref sequence);

        ally.State.SetResource(ally.State.VitalResourceId, 0);
        ExecuteItem("revival_bead_demo", Field, ally.State.InstanceId, itemExecutor, catalog, participants, inventory, ref sequence);

        ExecuteItem(
            "traesto_gem_demo",
            Battle,
            null,
            itemExecutor,
            catalog,
            participants,
            inventory,
            ref sequence,
            NormalBattle,
            NewMoon);
        ExecuteItem("goho_m_demo", Field, null, itemExecutor, catalog, participants, inventory, ref sequence);

        Print(ref sequence, "outcome", "Shared field effects demo completed successfully.");
        return 0;
    }

    private void ExecuteItem(
        string localId,
        ContentId contextId,
        ContentId? targetId,
        ItemExecutor executor,
        GameDataCatalog catalog,
        IReadOnlyList<RuntimeActorState> participants,
        IDictionary<ContentId, int> inventory,
        ref int sequence,
        ContentId? battleKindId = null,
        ContentId? moonPhaseId = null)
    {
        ContentId itemId = ContentId.Parse($"convergence.shared_effects_demo:{localId}");
        ItemDefinition item = catalog.GetRequiredItem(itemId);
        ItemExecutionResult result = executor.Execute(new ItemExecutionRequest(
            item,
            participants[0],
            participants,
            new EffectExecutionEnvironment(contextId, battleKindId, moonPhaseId),
            targetId is ContentId selected ? [selected] : []));

        if (result.Consumption == ItemConsumptionDecision.ConsumeOne)
        {
            inventory[itemId]--;
        }

        string hostRequests = result.HostActionRequestIds.Count == 0
            ? "none"
            : string.Join(",", result.HostActionRequestIds);
        Print(
            ref sequence,
            "item",
            $"{item.DisplayName}: {result.Status}; consume={result.Consumption}; escape={result.EscapeRequested}; hostRequests={hostRequests}; remaining={inventory[itemId]}.");
    }

    private void Print(ref int sequence, string kind, string message) =>
        _output.WriteLine($"{sequence++:D3} [{kind}] {message}");

    private static SkillSystemRegistrationSnapshot BuildRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterEntityKind("demon")
            .RegisterAilmentGroup("poison")
            .RegisterBattleKind("normal_battle")
            .RegisterMoonPhase("new_moon")
            .RegisterEscapeRule("standard_escape")
            .RegisterCustomEffect("request_dungeon_exit", new AcceptAnyParametersValidator())
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

    private static BattleExecutionServices CreateExecutionServices(GameDataCatalog catalog) =>
        new(
            catalog,
            new DemoDamageExecutionPolicy(),
            new DemoInstantDeathPolicy(),
            new DemoAilmentPolicy(),
            new DemoChancePolicy(),
            new DemoPowerAmountPolicy(),
            new DemoRandomTargetPolicy(),
            escapeRuleHandlers:
            [
                new KeyValuePair<ContentId, IEscapeRuleHandler>(
                    ContentId.Parse("standard_escape"),
                    new DemoEscapeRuleHandler())
            ],
            customEffectHandlers:
            [
                new KeyValuePair<ContentId, ICustomEffectHandler>(
                    ContentId.Parse("request_dungeon_exit"),
                    new DungeonExitRequestHandler())
            ]);
}
