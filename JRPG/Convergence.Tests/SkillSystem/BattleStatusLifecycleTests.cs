using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Data.SkillSystem.Validation;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class BattleStatusLifecycleTests
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Luck = ContentId.Parse("luck");
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");
    private static readonly ContentId PoisonFormula = ContentId.Parse("legacy_poison_damage");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId Poison = ContentId.Parse("poison");
    private static readonly ContentId Sleep = ContentId.Parse("sleep");

    [Fact]
    public void TurnStart_MapsDeterministicBehavioursAndClearsGuard()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState skip = Actor("skip");
        skip.SetGuarding(true);
        skip.ApplyAilment(Ailment("skip", new SkipAilmentTurnBehaviorDefinition()), Turns(3));
        RuntimeActorState limited = Actor("limited");
        limited.ApplyAilment(Ailment(
            "bind",
            new LimitedActionsAilmentTurnBehaviorDefinition([ContentId.Parse("basic_attack")])), Turns(3));
        RuntimeActorState forced = Actor("forced");
        forced.ApplyAilment(Ailment("rage", new ForcedBasicAttackAilmentTurnBehaviorDefinition()), Turns(3));
        RuntimeActorState confused = Actor("confused");
        confused.ApplyAilment(Ailment("charm", new ConfusedActionAilmentTurnBehaviorDefinition()), Turns(3));

        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(skip)).Outcome);
        Assert.False(skip.IsGuarding);
        Assert.Equal(BattleTurnStartOutcome.LimitedAction, service.ProcessTurnStart(new(limited)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.ForcedPhysical, service.ProcessTurnStart(new(forced)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.ForcedConfusion, service.ProcessTurnStart(new(confused)).Outcome);
    }

    [Fact]
    public void TurnStart_UsesDeterministicChanceSkipAndFearRolls()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource(49, 50, 10, 10, 20, 60));
        RuntimeActorState panicSkip = Actor("panic_skip");
        panicSkip.ApplyAilment(Ailment("panic", new ChanceSkipAilmentTurnBehaviorDefinition(50)), Turns(3));
        RuntimeActorState panicAct = Actor("panic_act");
        panicAct.ApplyAilment(Ailment("panic", new ChanceSkipAilmentTurnBehaviorDefinition(50)), Turns(3));
        var fear = new ChanceSkipOrFleeAilmentTurnBehaviorDefinition(40, 15, DemonFleeOutcome.ReturnToStock);
        RuntimeActorState demonFear = Actor("demon_fear");
        demonFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState humanFear = Actor("human_fear");
        humanFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState skipFear = Actor("skip_fear");
        skipFear.ApplyAilment(Ailment("fear", fear), Turns(3));
        RuntimeActorState actFear = Actor("act_fear");
        actFear.ApplyAilment(Ailment("fear", fear), Turns(3));

        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(panicSkip)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.CanAct, service.ProcessTurnStart(new(panicAct)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.ReturnToStock, service.ProcessTurnStart(new(demonFear, true)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.FleeBattle, service.ProcessTurnStart(new(humanFear)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.Skip, service.ProcessTurnStart(new(skipFear, true)).Outcome);
        Assert.Equal(BattleTurnStartOutcome.CanAct, service.ProcessTurnStart(new(actFear, true)).Outcome);
    }

    [Fact]
    public void AilmentApplication_EnforcesGuardImmunityChanceAndMajorExclusivity()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource(99, 0));
        RuntimeActorState guarded = Actor("guarded");
        guarded.SetGuarding(true);
        BattleAilmentApplicationResult guardedResult = service.TryApplyAilment(new(
            Actor("attacker"),
            guarded,
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            100));
        var immuneDefense = new CombatDefenseProfile(
            ailmentResistances: [new KeyValuePair<ContentId, ResistanceLevel>(Poison, ResistanceLevel.Immune)]);
        RuntimeActorState immune = Actor("immune", defense: immuneDefense);
        BattleAilmentApplicationResult immuneResult = service.TryApplyAilment(new(
            Actor("attacker"),
            immune,
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            100));
        RuntimeActorState target = Actor("target");
        target.ApplyAilment(Ailment("sleep", new SkipAilmentTurnBehaviorDefinition()), Turns(3));
        BattleAilmentApplicationResult missed = service.TryApplyAilment(new(
            Actor("attacker"),
            target,
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            50));
        BattleAilmentApplicationResult applied = service.TryApplyAilment(new(
            Actor("attacker"),
            target,
            Ailment("poison", new NormalAilmentTurnBehaviorDefinition()),
            50));

        Assert.Equal(BattleAilmentApplicationStatus.GuardBlocked, guardedResult.Status);
        Assert.Equal(BattleAilmentApplicationStatus.Immune, immuneResult.Status);
        Assert.Equal(BattleAilmentApplicationStatus.Missed, missed.Status);
        Assert.True(applied.Applied);
        Assert.False(target.HasAilment(Sleep));
        Assert.True(target.HasAilment(Poison));
    }

    [Fact]
    public void TurnEnd_AppliesLethalPoisonSleepRecoveryNaturalRecoveryAndDurationTicks()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource(0));
        BattleExecutionServices services = Services();
        RuntimeActorState poisoned = Actor("poisoned", hp: 1);
        poisoned.ApplyAilment(PoisonAilment(), Turns(3));
        RuntimeActorState sleeping = Actor("sleeping", hp: 50, sp: 40);
        sleeping.ApplyAilment(SleepAilment(), Turns(3));
        RuntimeActorState recovering = Actor("recovering", luck: 40);
        recovering.ApplyAilment(Ailment(
            "fear",
            new NormalAilmentTurnBehaviorDefinition(),
            recovery: new AilmentRecoveryDefinition(new NaturalAilmentRecoveryDefinition(20, Luck, 0.5m))),
            Turns(3));

        BattleTurnEndLifecycleResult poisonResult = service.ProcessTurnEnd(
            new(poisoned, [poisoned], Battle, OwnerTurnEnd),
            services);
        BattleTurnEndLifecycleResult sleepResult = service.ProcessTurnEnd(
            new(sleeping, [sleeping], Battle, OwnerTurnEnd),
            services);
        service.ProcessTurnEnd(new(recovering, [recovering], Battle, OwnerTurnEnd), services);

        Assert.Equal(0, poisoned.GetRequiredResource(Hp).Current);
        Assert.True(poisoned.IsDefeated);
        Assert.Equal(2, Assert.Single(poisoned.Ailments).Value.Duration is TurnDurationDefinition turns ? turns.Value : 0);
        Assert.Contains(poisonResult.Events, ev => ev.Kind == BattleStatusLifecycleEventKind.ResourceChanged && ev.Value < 0);
        Assert.Equal(60, sleeping.GetRequiredResource(Hp).Current);
        Assert.Equal(50, sleeping.GetRequiredResource(Sp).Current);
        Assert.Contains(sleepResult.Events, ev => ev.Kind == BattleStatusLifecycleEventKind.ResourceChanged && ev.RelatedId == Hp);
        Assert.False(recovering.HasAilment(ContentId.Parse("fear")));
    }

    [Fact]
    public void TurnEnd_SuspendsReserveActorTicksDamageAndRecovery()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource(0));
        RuntimeActorState reserve = Actor("reserve", hp: 50, isActive: false);
        reserve.ApplyAilment(PoisonAilment(), Turns(3));

        BattleTurnEndLifecycleResult result = service.ProcessTurnEnd(
            new(reserve, [reserve], Battle, OwnerTurnEnd),
            Services());

        Assert.Empty(result.Events);
        Assert.Equal(50, reserve.GetRequiredResource(Hp).Current);
        Assert.Equal(3, Assert.Single(reserve.Ailments).Value.Duration is TurnDurationDefinition turns ? turns.Value : 0);
    }

    [Fact]
    public void Cleanup_ClearsTransientAndEncounterStatusesWithoutRemovingAilments()
    {
        var service = new BattleStatusLifecycleService(new SequenceRandomSource());
        RuntimeActorState actor = Actor("actor");
        actor.SetGuarding(true);
        actor.GrantShield(ShieldKind.Physical, Turns(1));
        actor.GrantCharge(ChargeKind.Physical, 2, Turns(1));
        actor.ChangeStatStage(ContentId.Parse("attack"), 1, Turns(1));
        actor.OverrideAffinity(DamageElement.Fire, ElementalAffinity.Null, Turns(1));
        actor.AddOtherStatus(ContentId.Parse("marked"), Turns(1));
        actor.ApplyAilment(PoisonAilment(), Turns(3));

        service.Cleanup(new BattleStatusCleanupRequest(actor, BattleStatusCleanupScope.Swap));

        Assert.False(actor.IsGuarding);
        Assert.Empty(actor.Shields);
        Assert.Empty(actor.Charges);
        Assert.NotEmpty(actor.StatStages);
        Assert.NotEmpty(actor.AffinityOverrides);
        Assert.NotEmpty(actor.OtherStatuses);
        Assert.True(actor.HasAilment(Poison));

        service.Cleanup(new BattleStatusCleanupRequest(actor, BattleStatusCleanupScope.BattleEnd));

        Assert.Empty(actor.StatStages);
        Assert.Empty(actor.AffinityOverrides);
        Assert.Empty(actor.OtherStatuses);
        Assert.True(actor.HasAilment(Poison));
    }

    [Fact]
    public void StatusLifecycleDemoPack_LoadsTheElevenLegacyAilments()
    {
        string root = FindRepositoryRoot();
        string jsonRoot = Path.Combine(root, "Data", "Jsons");
        string manifestName = "status_lifecycle_demo.manifest.json";
        string ailmentName = "status_lifecycle_demo.ailments.json";
        var bundle = new ContentPackTextBundle(
            manifestName,
            File.ReadAllText(Path.Combine(jsonRoot, manifestName)),
            [new ContentDocumentText(ailmentName, ailmentName, File.ReadAllText(Path.Combine(jsonRoot, ailmentName)))]);

        GameDataCatalog catalog = new SkillSystemCatalogLoader()
            .Load(new SkillSystemCatalogLoadRequest(Registrations(), [bundle]))
            .RequireCatalog();

        Assert.Equal(11, catalog.Ailments.Count);
        Assert.Contains(ContentId.Parse("convergence.status_lifecycle_demo:poison"), catalog.Ailments.Keys);
        Assert.IsType<ChanceSkipOrFleeAilmentTurnBehaviorDefinition>(
            catalog.GetRequiredAilment(ContentId.Parse("convergence.status_lifecycle_demo:fear")).TurnBehavior);
        Assert.IsType<LimitedActionsAilmentTurnBehaviorDefinition>(
            catalog.GetRequiredAilment(ContentId.Parse("convergence.status_lifecycle_demo:bind")).TurnBehavior);
        Assert.Contains(
            catalog.GetRequiredAilment(ContentId.Parse("convergence.status_lifecycle_demo:sleep")).Triggers,
            trigger => trigger.EventId == OwnerTurnEnd);
    }

    private static RuntimeActorState Actor(
        string id,
        decimal hp = 100,
        decimal sp = 100,
        decimal luck = 10,
        CombatDefenseProfile? defense = null,
        bool isActive = true) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            PlayerTeam,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100), new BattleResourceState(Sp, sp, 100)],
            [new KeyValuePair<ContentId, decimal>(Luck, luck)],
            isActive: isActive);

    private static TurnDurationDefinition Turns(int value) =>
        new(value, OwnerTurnEnd, true);

    private static AilmentDefinition Ailment(
        string id,
        AilmentTurnBehaviorDefinition behavior,
        AilmentRecoveryDefinition? recovery = null,
        IEnumerable<PassiveTriggerDefinition>? triggers = null) =>
        new(
            ContentId.Parse(id),
            id,
            "Test ailment.",
            Turns(3),
            behavior,
            new AilmentModifiersDefinition(1, 0, 1, 1, false),
            recovery ?? new AilmentRecoveryDefinition(),
            [ContentId.Parse("major_ailment")],
            ContentId.Parse("major_ailment"),
            triggers);

    private static AilmentDefinition PoisonAilment() =>
        Ailment(
            "poison",
            new NormalAilmentTurnBehaviorDefinition(),
            triggers:
            [
                new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [new ReduceResourceEffectDefinition(Hp, new FormulaAmountDefinition(PoisonFormula), true)])
            ]);

    private static AilmentDefinition SleepAilment() =>
        Ailment(
            "sleep",
            new SkipAilmentTurnBehaviorDefinition(),
            triggers:
            [
                new PassiveTriggerDefinition(
                    OwnerTurnEnd,
                    [
                        new RestoreResourceEffectDefinition(Hp, new PercentMaximumAmountDefinition(10)),
                        new RestoreResourceEffectDefinition(Sp, new PercentMaximumAmountDefinition(10))
                    ])
            ]);

    private static BattleExecutionServices Services() =>
        new(
            new EmptyAilments(),
            new NoDamagePolicy(),
            new NoInstantDeathPolicy(),
            new AlwaysAilmentPolicy(),
            new AlwaysChancePolicy(),
            new ZeroPowerPolicy(),
            new FirstTargetPolicy(),
            formulaHandlers:
            [
                new KeyValuePair<ContentId, IFormulaAmountHandler>(
                    PoisonFormula,
                    new PoisonFormulaHandler())
            ]);

    private static SkillSystemRegistrationSnapshot Registrations() =>
        new SkillSystemRegistrationBuilder()
            .RegisterResource("hp", "sp")
            .RegisterStat("luck")
            .RegisterEvent("owner_turn_end")
            .RegisterAction("basic_attack", "guard", "pass")
            .RegisterAilmentGroup("major_ailment", "poison", "immobilize", "mental")
            .RegisterFormula("legacy_poison_damage", new AcceptingParameterValidator())
            .SupportEffect<ReduceResourceEffectDefinition>()
            .SupportEffect<RestoreResourceEffectDefinition>()
            .SupportAilmentBehavior<NormalAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<SkipAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<LimitedActionsAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ChanceSkipAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ChanceSkipOrFleeAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ForcedBasicAttackAilmentTurnBehaviorDefinition>()
            .SupportAilmentBehavior<ConfusedActionAilmentTurnBehaviorDefinition>()
            .Build();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JRPG.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find JRPG.sln.");
    }

    private sealed class SequenceRandomSource(params int[] values) : IRandomSource
    {
        private readonly Queue<int> _values = new(values);

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            int value = _values.Count == 0 ? minimumInclusive : _values.Dequeue();
            return Math.Clamp(value, minimumInclusive, maximumExclusive - 1);
        }

        public decimal NextUnitDecimal() => NextInt32(0, 100) / 100m;
    }

    private sealed class PoisonFormulaHandler : IFormulaAmountHandler
    {
        public decimal Resolve(FormulaAmountDefinition amount, AmountResolutionContext context) =>
            Math.Max(1, Math.Floor(context.Target.GetRequiredResource(Hp).Maximum * 0.13m));
    }

    private sealed class EmptyAilments : IAilmentDefinitionRepository
    {
        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = null;
            return false;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) => throw new KeyNotFoundException();
    }

    private sealed class NoDamagePolicy : IDamageExecutionPolicy
    {
        public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request) => [];
    }

    private sealed class NoInstantDeathPolicy : IInstantDeathExecutionPolicy
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

    private sealed class ZeroPowerPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => 0;
    }

    private sealed class FirstTargetPolicy : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) =>
            candidates.Take(count.Maximum).ToArray();
    }

    private sealed class AcceptingParameterValidator : IContentParameterValidator
    {
        public IReadOnlyList<ContentParameterValidationIssue> Validate(
            IReadOnlyDictionary<string, object?> parameters) => [];
    }
}
