using Convergence.Content;
using Convergence.Catalog;
using Convergence.Validation;
using Convergence.Hosting;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;

namespace Convergence.DemoHost;

internal sealed class DemoBattleActorInitializationPolicy : IBattleActorInitializationPolicy
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Magic = ContentId.Parse("magic");
    private static readonly ContentId Vitality = ContentId.Parse("vitality");

    public BattleActorInitialization Initialize(EntityDefinition entity, int level)
    {
        decimal vitality = entity.Stats.GetValueOrDefault(Vitality);
        decimal magic = entity.Stats.GetValueOrDefault(Magic);
        decimal hp = 40 + level * 5 + vitality * 3;
        decimal sp = 10 + level * 2 + magic * 2;
        return new BattleActorInitialization(Hp,
        [
            new BattleResourceState(Hp, hp, hp),
            new BattleResourceState(Sp, sp, sp)
        ]);
    }
}

internal sealed class DemoDamageExecutionPolicy : IDamageExecutionPolicy
{
    private static readonly ContentId Magic = ContentId.Parse("magic");
    private static readonly ContentId Vitality = ContentId.Parse("vitality");

    public DamagePolicyResolution Resolve(DamagePolicyRequest request)
    {
        decimal damage = Math.Max(
            1,
            request.Effect.Power +
            request.Actor.Stats.GetValueOrDefault(Magic) -
            request.Target.Stats.GetValueOrDefault(Vitality));
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

internal sealed class DemoInstantDeathPolicy : IInstantDeathExecutionPolicy
{
    public bool ShouldDefeat(InstantDeathPolicyRequest request) => false;
}

internal sealed class DemoAilmentPolicy : IAilmentApplicationPolicy
{
    public bool ShouldApply(AilmentApplicationPolicyRequest request) =>
        request.Resistance != ResistanceLevel.Immune;
}

internal sealed class DemoChancePolicy : IChanceExecutionPolicy
{
    public bool Roll(ChancePolicyRequest request) => request.Chance > 0;
}

internal sealed class DemoPowerAmountPolicy : IPowerAmountPolicy
{
    public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
}

internal sealed class DemoRandomTargetPolicy : IRandomTargetSelectionPolicy
{
    public IReadOnlyList<RuntimeActorState> Select(
        IReadOnlyList<RuntimeActorState> candidates,
        TargetCountDefinition count,
        SkillExecutionRequest request) =>
        Array.AsReadOnly(candidates.Take(count.Minimum).ToArray());
}

internal sealed class CleanBattleDemoHost
{
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly ContentId BattleStart = ContentId.Parse("battle_start");
    private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");

    private readonly IContentPackTextSource _contentSource;
    private readonly IHostEventSink<string> _eventSink;

    public CleanBattleDemoHost(TextWriter output, string? contentRoot = null)
        : this(
            new FileContentPackSource(contentRoot ?? Path.Combine(AppContext.BaseDirectory, "Content")),
            new TextWriterEventSink(output))
    {
    }

    internal CleanBattleDemoHost(
        IContentPackTextSource contentSource,
        IHostEventSink<string> eventSink)
    {
        _contentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
    }

    public int Run() => RunAsync().GetAwaiter().GetResult();

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        ContentPackTextBundle[] bundles;
        try
        {
            bundles =
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
                    ]), cancellationToken)
            ];
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

        CatalogLoadResult load = new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(BuildRegistrations(), bundles));
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
        CatalogBattleActorCreationResult frostResult = factory.Create(new CatalogBattleActorCreationRequest(
            ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"),
            RuntimeInstanceId.Parse("frost_duelist"),
            PlayerTeam,
            5));
        CatalogBattleActorCreationResult emberResult = factory.Create(new CatalogBattleActorCreationRequest(
            ContentId.Parse("convergence.clean_battle_demo:ember_duelist_demo"),
            RuntimeInstanceId.Parse("ember_duelist"),
            EnemyTeam,
            5));
        if (!frostResult.IsSuccess || !emberResult.IsSuccess)
        {
            foreach (CatalogBattleActorDiagnostic diagnostic in frostResult.Diagnostics.Concat(emberResult.Diagnostics))
            {
                await _eventSink.PublishAsync($"[{diagnostic.Code}] {diagnostic.Message}", cancellationToken);
            }
            return 4;
        }

        BattleExecutionServices services = CreateExecutionServices(catalog);
        var executor = new SkillExecutor(services);
        var lifecycle = new BattleStatusEncounterLifecyclePort(
            new BattleStatusLifecycleService(new SystemRandomSource(0)),
            services,
            BattleStart,
            OwnerTurnEnd);
        var turnEconomy = new BattleTurnEconomyRuleset(
            () => new ActionTokenTurnEconomy(),
            new BattlePhaseProgressPolicy(
                maximumCommands: 256,
                maximumConsecutiveFreeActions: 32));
        var runner = new AutomatedBattleRunner(
            executor,
            new DeterministicBattleActionSelector(executor),
            services,
            lifecycle,
            turnEconomy,
            new AutomatedBattleTurnRestrictionResolver());
        AutomatedBattleResult battle = runner.Run(new AutomatedBattleRequest(
            [frostResult.RequireActor(), emberResult.RequireActor()],
            Battle,
            NormalBattle,
            moonPhaseId: null,
            roundLimit: 10));

        foreach (BattleRuntimeEvent battleEvent in battle.Events)
        {
            await _eventSink.PublishAsync(
                $"{battleEvent.Sequence:D3} [{battleEvent.Kind}] {battleEvent.Message}",
                cancellationToken);
        }
        await _eventSink.PublishAsync(
            battle.WinningTeamId is ContentId winner
                ? $"Outcome: {battle.Outcome}; winner: {winner}"
                : $"Outcome: {battle.Outcome}",
            cancellationToken);
        return battle.Outcome == AutomatedBattleOutcome.Faulted ? 5 : 0;
    }

    private static SkillSystemRegistrationSnapshot BuildRegistrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterContext("battle")
            .RegisterResource("hp", "sp")
            .RegisterStat("strength", "magic", "vitality", "agility", "luck")
            .RegisterEvent("battle_start", "owner_turn_end")
            .RegisterEntityKind("companion")
            .RegisterBattleKind("normal_battle")
            .SupportEffect<DamageEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportCondition<EffectElementConditionDefinition>()
            .SupportModifier<NumericRuleModifierDefinition>()
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
            new OrderedRuntimeTargetSelectionPolicy());
}
