using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Hosting;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;

namespace Convergence.DemoHost;

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
    private static readonly ContentId Party = ContentId.Parse("party");

    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _eventSink;

    public CleanFieldDemoHost(TextWriter output, string? contentRoot = null)
        : this(
            new FileContentPackSource(contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Content")),
            new TextWriterEventSink(output))
    {
    }

    internal CleanFieldDemoHost(
        IContentPackTextSource contentSource,
        IHostEventSink<string> eventSink)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public int Run() => RunAsync().GetAwaiter().GetResult();

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        CatalogLoadResult load;
        try
        {
            load = new SkillSystemCatalogLoader().Load(new SkillSystemCatalogLoadRequest(
                BuildRegistrations(),
                [
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "reference/skill-system-redesign/skill_system_redesign.manifest.sample.json",
                        [
                            "skill_system_redesign.races.sample.json",
                            "skill_system_redesign.skills.sample.json",
                            "skill_system_redesign.entities.sample.json"
                        ]), cancellationToken),
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "demos/clean-battle/clean_battle_demo.manifest.json",
                        [
                            "clean_battle_demo.races.json",
                            "clean_battle_demo.skills.json",
                            "clean_battle_demo.entities.json"
                        ]), cancellationToken),
                    await _contentSource.ReadAsync(new ContentPackTextRequest(
                        "demos/shared-effects/shared_effects_demo.manifest.json",
                        [
                            "shared_effects_demo.ailments.json",
                            "shared_effects_demo.skills.json",
                            "shared_effects_demo.entities.json",
                            "shared_effects_demo.items.json"
                        ]), cancellationToken)
                ]));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _eventSink.PublishAsync($"Content read failed: {exception.Message}", cancellationToken);
            return 2;
        }

        if (!load.IsSuccess || load.Catalog is null)
        {
            foreach (CatalogLoadDiagnostic diagnostic in load.Diagnostics)
            {
                await _eventSink.PublishAsync(
                    $"[{diagnostic.Code}] {diagnostic.SourceName} {diagnostic.JsonPath}: {diagnostic.Message}",
                    cancellationToken);
            }
            return 3;
        }

        GameDataCatalog catalog = load.Catalog;
        var factory = new CatalogBattleActorFactory(catalog, catalog, new DemoBattleActorInitializationPolicy());
        CatalogBattleActorCreationResult medicResult = factory.Create(new CatalogBattleActorCreationRequest(
            ContentId.Parse("convergence.shared_effects_demo:field_medic_demo"),
            RuntimeInstanceId.Parse("field_medic"),
            Party,
            5,
            IsDeployed: false,
            ContentId.Parse("clean_field_demo")));
        CatalogBattleActorCreationResult allyResult = factory.Create(new CatalogBattleActorCreationRequest(
            ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo"),
            RuntimeInstanceId.Parse("field_ally"),
            Party,
            5,
            IsDeployed: false,
            ContentId.Parse("clean_field_demo")));
        if (!medicResult.IsSuccess || !allyResult.IsSuccess)
        {
            foreach (CatalogBattleActorDiagnostic diagnostic in medicResult.Diagnostics.Concat(allyResult.Diagnostics))
            {
                await _eventSink.PublishAsync($"[{diagnostic.Code}] {diagnostic.Message}", cancellationToken);
            }
            return 4;
        }

        CatalogBattleActor medic = medicResult.RequireActor();
        CatalogBattleActor ally = allyResult.RequireActor();
        RuntimeActorState[] participants = [medic.State, ally.State];
        BattleExecutionServices services = CreateExecutionServices(catalog);
        var actionExecutor = new BattleActionExecutor(
            new SkillExecutor(services),
            new ItemExecutor(services),
            services);
        var inventory = catalog.Items.Keys.ToDictionary(id => id, _ => 1);
        int sequence = 1;

        ally.State.AddResource(ally.State.VitalResourceId, -30);
        SkillDefinition fieldRecovery = catalog.GetRequiredSkill(
            ContentId.Parse("convergence.shared_effects_demo:field_recovery_demo"));
        BattleActionExecutionResult skill = await actionExecutor.ExecuteAsync(new BattleActionExecutionRequest(
            new SkillBattleActionCommand(fieldRecovery, [ally.State.InstanceId]),
            medic.State,
            participants,
            new EffectExecutionEnvironment(Field)), cancellationToken);
        await PrintAsync(
            sequence++,
            "skill",
            $"{fieldRecovery.DisplayName}: {skill.Status}; restored {skill.Effects.Sum(effect => effect.Value ?? 0)} HP.",
            cancellationToken);

        ally.State.AddResource(ally.State.VitalResourceId, -20);
        sequence = await ExecuteItemAsync(
            "medicine_demo", Field, ally.State.InstanceId, actionExecutor, catalog, participants, inventory, sequence,
            cancellationToken: cancellationToken);

        AilmentDefinition poison = catalog.GetRequiredAilment(
            ContentId.Parse("convergence.shared_effects_demo:poison_demo"));
        ally.State.ApplyAilment(poison, poison.DefaultDuration);
        sequence = await ExecuteItemAsync(
            "dis_poison_demo", Field, ally.State.InstanceId, actionExecutor, catalog, participants, inventory, sequence,
            cancellationToken: cancellationToken);

        ally.State.SetResource(ally.State.VitalResourceId, 0);
        sequence = await ExecuteItemAsync(
            "revival_bead_demo", Field, ally.State.InstanceId, actionExecutor, catalog, participants, inventory, sequence,
            cancellationToken: cancellationToken);

        sequence = await ExecuteItemAsync(
            "battle_exit_charm_demo",
            Battle,
            null,
            actionExecutor,
            catalog,
            participants,
            inventory,
            sequence,
            NormalBattle,
            moonPhaseId: null,
            cancellationToken: cancellationToken);
        sequence = await ExecuteItemAsync(
            "return_beacon_demo", Field, null, actionExecutor, catalog, participants, inventory, sequence,
            cancellationToken: cancellationToken);

        await PrintAsync(sequence, "outcome", "Shared field effects demo completed successfully.", cancellationToken);
        return 0;
    }

    private async Task<int> ExecuteItemAsync(
        string localId,
        ContentId contextId,
        RuntimeInstanceId? targetId,
        BattleActionExecutor executor,
        GameDataCatalog catalog,
        IReadOnlyList<RuntimeActorState> participants,
        IDictionary<ContentId, int> inventory,
        int sequence,
        ContentId? battleKindId = null,
        ContentId? moonPhaseId = null,
        CancellationToken cancellationToken = default)
    {
        ContentId itemId = ContentId.Parse($"convergence.shared_effects_demo:{localId}");
        ItemDefinition item = catalog.GetRequiredItem(itemId);
        BattleActionExecutionResult result = await executor.ExecuteAsync(new BattleActionExecutionRequest(
            new ItemBattleActionCommand(item, targetId is RuntimeInstanceId selected ? [selected] : []),
            participants[0],
            participants,
            new EffectExecutionEnvironment(contextId, battleKindId, moonPhaseId),
            new DemoItemActionInventory(inventory)), cancellationToken);

        string hostRequests = result.HostActionRequestIds.Count == 0
            ? "none"
            : string.Join(",", result.HostActionRequestIds);
        await PrintAsync(
            sequence,
            "item",
            $"{item.DisplayName}: {result.Status}; consume={result.ItemConsumption}; escape={result.EscapeRequested}; hostRequests={hostRequests}; remaining={inventory[itemId]}.",
            cancellationToken);
        return sequence + 1;
    }

    private ValueTask PrintAsync(
        int sequence,
        string kind,
        string message,
        CancellationToken cancellationToken) =>
        _eventSink.PublishAsync($"{sequence:D3} [{kind}] {message}", cancellationToken);

    private static SkillSystemRegistrationSnapshot BuildRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle", "field")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterEntityKind("companion")
            .RegisterAilmentGroup("poison")
            .RegisterBattleKind("normal_battle")
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
            new OrderedRuntimeTargetSelectionPolicy(),
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

            public ItemActionReservationTransitionResult Commit()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        "Item reservation has already been completed.");
                }

                quantities[ItemId] -= Quantity;
                IsCommitted = true;
                return ItemActionReservationTransitionResult.Success;
            }

            public ItemActionReservationTransitionResult Rollback()
            {
                if (IsCommitted || IsRolledBack)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        "Item reservation has already been completed.");
                }

                IsRolledBack = true;
                return ItemActionReservationTransitionResult.Success;
            }
        }
    }
}
