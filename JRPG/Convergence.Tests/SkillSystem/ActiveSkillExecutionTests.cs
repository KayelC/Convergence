using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Execution;
using Xunit;

namespace Convergence.Tests.SkillSystem;

public sealed class ActiveSkillExecutionTests
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Battle = ContentId.Parse("battle");
    private static readonly ContentId NormalBattle = ContentId.Parse("normal_battle");
    private static readonly ContentId NewMoon = ContentId.Parse("new_moon");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId EnemyTeam = ContentId.Parse("enemy_team");
    private static readonly ContentId Poison = ContentId.Parse("poison");

    public static IEnumerable<object[]> DamageOutcomeCases()
    {
        yield return [ElementalAffinity.Normal, true, false, PressTurnOutcome.Normal, EffectExecutionOutcome.Success];
        yield return [ElementalAffinity.Normal, true, true, PressTurnOutcome.Critical, EffectExecutionOutcome.Success];
        yield return [ElementalAffinity.Weak, true, false, PressTurnOutcome.Weakness, EffectExecutionOutcome.Success];
        yield return [ElementalAffinity.Normal, false, false, PressTurnOutcome.Miss, EffectExecutionOutcome.Failure];
        yield return [ElementalAffinity.Null, true, false, PressTurnOutcome.Null, EffectExecutionOutcome.Failure];
        yield return [ElementalAffinity.Repel, true, false, PressTurnOutcome.Repel, EffectExecutionOutcome.Interrupted];
        yield return [ElementalAffinity.Absorb, true, false, PressTurnOutcome.Absorb, EffectExecutionOutcome.Interrupted];
    }

    [Fact]
    public void Execute_RejectsIndependentPreflightErrorsWithoutSpendingResources()
    {
        BattleActorState actor = Actor("actor", PlayerTeam, hp: 100, sp: 5);
        BattleActorState target = Actor("target", EnemyTeam);
        SkillDefinition skill = ActiveSkill(
            [new DamageEffectDefinition(DamageElement.Fire, 10, 100, new NeverCriticalDefinition(), FixedHits())],
            costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(10))],
            availability: [ContentId.Parse("field")]);
        var executor = new SkillExecutor(Services());

        SkillExecutionResult result = executor.Execute(Request(skill, actor, [actor, target], [ContentId.Parse("missing_target")]));

        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SkillExecutionDiagnosticCode.ContextUnavailable);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SkillExecutionDiagnosticCode.TargetSelectionInvalid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SkillExecutionDiagnosticCode.InsufficientResource);
        Assert.Equal(5, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
        Assert.False(result.CostsCommitted);
    }

    [Fact]
    public void Execute_ComposesDamageAndAilmentInAuthoredOrder()
    {
        BattleActorState actor = Actor("actor", PlayerTeam, sp: 30);
        BattleActorState target = Actor(
            "target",
            EnemyTeam,
            defense: new CombatDefenseProfile([new(DamageElement.Ice, ElementalAffinity.Weak)]));
        SkillDefinition skill = ActiveSkill(
        [
            new DamageEffectDefinition(DamageElement.Ice, 40, 100, new ChanceCriticalDefinition(20), FixedHits()),
            new ApplyAilmentEffectDefinition(Poison, 40)
        ],
        costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(8))]);
        BattleExecutionServices services = Services(
            damage: _ => [new DamageHitResolution(true, 25, true)]);

        SkillExecutionResult result = new SkillExecutor(services).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.Equal([0, 1], result.Effects.Select(effect => effect.EffectIndex));
        Assert.All(result.Effects, effect => Assert.Equal(EffectExecutionOutcome.Success, effect.Outcome));
        Assert.Equal(75, target.GetRequiredResource(Hp).Current);
        Assert.True(target.HasAilment(Poison));
        Assert.Equal(22, actor.GetRequiredResource(Sp).Current);
        Assert.True(result.CostsCommitted);
        Assert.Equal(PressTurnOutcome.Weakness, result.PressTurn.Outcome);
        Assert.True(result.PressTurn.AnyCritical);
    }

    [Fact]
    public void Execute_FalseConditionSkipsWithoutActivatingStopAction()
    {
        BattleActorState actor = Actor("actor", PlayerTeam);
        BattleActorState target = Actor("target", EnemyTeam, hp: 100);
        SkillDefinition skill = ActiveSkill(
        [
            new DamageEffectDefinition(
                DamageElement.Fire,
                10,
                100,
                new NeverCriticalDefinition(),
                FixedHits(),
                When: new HasCapabilityConditionDefinition(ConditionSubject.Target, ContentId.Parse("missing_capability")),
                OnFailure: EffectFailurePolicy.StopAction),
            new RestoreResourceEffectDefinition(Hp, new FullAmountDefinition())
        ]);

        SkillExecutionResult result = new SkillExecutor(Services()).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal([EffectExecutionOutcome.Skipped, EffectExecutionOutcome.Success], result.Effects.Select(effect => effect.Outcome));
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
        Assert.Equal(0, result.Effects[1].Value);
    }

    [Fact]
    public void Execute_StopTargetSuppressesLaterEffectsOnlyForFailedTarget()
    {
        BattleActorState actor = Actor("actor", PlayerTeam);
        BattleActorState first = Actor("first", EnemyTeam, hp: 100);
        BattleActorState second = Actor("second", EnemyTeam, hp: 50);
        SkillDefinition skill = ActiveSkill(
        [
            new DamageEffectDefinition(
                DamageElement.Fire,
                10,
                100,
                new NeverCriticalDefinition(),
                FixedHits(),
                OnFailure: EffectFailurePolicy.StopTarget),
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(10))
        ],
        targeting: new TargetingDefinition(TargetRelation.Enemy, TargetSelection.All, TargetLifeState.Alive, false));
        BattleExecutionServices services = Services(damage: request =>
            request.Target.InstanceId == first.InstanceId
                ? [new DamageHitResolution(false, 0)]
                : [new DamageHitResolution(true, 10)]);

        SkillExecutionResult result = new SkillExecutor(services).Execute(Request(skill, actor, [actor, first, second]));

        Assert.Equal(3, result.Effects.Count);
        Assert.Equal(
            [(0, first.InstanceId), (0, second.InstanceId), (1, second.InstanceId)],
            result.Effects.Select(effect => (effect.EffectIndex, effect.TargetId!.Value)));
        Assert.Equal(100, first.GetRequiredResource(Hp).Current);
        Assert.Equal(50, second.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void Execute_StopActionEndsAfterOrdinaryFailure()
    {
        BattleActorState actor = Actor("actor", PlayerTeam);
        BattleActorState target = Actor("target", EnemyTeam, hp: 40);
        SkillDefinition skill = ActiveSkill(
        [
            new InstantKillEffectDefinition(
                50,
                new ChannelInstantDeathResistanceCheckDefinition(InstantDeathChannel.Light),
                OnFailure: EffectFailurePolicy.StopAction),
            new RestoreResourceEffectDefinition(Hp, new FullAmountDefinition())
        ]);

        SkillExecutionResult result = new SkillExecutor(Services(instantDeath: _ => false)).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Single(result.Effects);
        Assert.Equal(EffectExecutionOutcome.Failure, result.Effects[0].Outcome);
        Assert.Equal(40, target.GetRequiredResource(Hp).Current);
        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
    }

    [Fact]
    public void Execute_RepelInterruptsActionRegardlessOfFailurePolicy()
    {
        BattleActorState actor = Actor("actor", PlayerTeam, hp: 100);
        BattleActorState target = Actor(
            "target",
            EnemyTeam,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Repel)]));
        SkillDefinition skill = ActiveSkill(
        [
            new DamageEffectDefinition(DamageElement.Fire, 10, 100, new NeverCriticalDefinition(), FixedHits()),
            new RestoreResourceEffectDefinition(Hp, new FullAmountDefinition())
        ]);

        SkillExecutionResult result = new SkillExecutor(Services(damage: _ => [new DamageHitResolution(true, 30)])).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Interrupted, result.Status);
        Assert.Single(result.Effects);
        Assert.Equal(EffectExecutionOutcome.Interrupted, result.Effects[0].Outcome);
        Assert.Equal(PressTurnOutcome.Repel, result.PressTurn.Outcome);
        Assert.True(result.PressTurn.TerminatesPhase);
        Assert.Equal(70, actor.GetRequiredResource(Hp).Current);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Theory]
    [MemberData(nameof(DamageOutcomeCases))]
    public void Execute_DamagePreservesPressTurnOutcomes(
        ElementalAffinity affinity,
        bool hit,
        bool critical,
        PressTurnOutcome expectedPressTurn,
        EffectExecutionOutcome expectedExecution)
    {
        BattleActorState actor = Actor("actor", PlayerTeam, hp: 100);
        BattleActorState target = Actor(
            "target",
            EnemyTeam,
            hp: 50,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, affinity)]));
        SkillDefinition skill = ActiveSkill(
            [new DamageEffectDefinition(DamageElement.Fire, 10, 100, new ChanceCriticalDefinition(10), FixedHits())]);
        BattleExecutionServices services = Services(
            damage: _ => [new DamageHitResolution(hit, hit ? 10 : 0, critical)]);

        SkillExecutionResult result = new SkillExecutor(services).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(expectedExecution, result.Effects[0].Outcome);
        Assert.Equal(expectedPressTurn, result.PressTurn.Outcome);
        Assert.Equal(critical, result.PressTurn.AnyCritical);
        Assert.Equal(
            expectedPressTurn is PressTurnOutcome.Repel or PressTurnOutcome.Absorb,
            result.PressTurn.TerminatesPhase);
    }

    [Fact]
    public void Execute_ResolvesFormulaCostOnceBeforeCommit()
    {
        BattleActorState actor = Actor("actor", PlayerTeam, sp: 20);
        BattleActorState target = Actor("target", EnemyTeam);
        ContentId formulaId = ContentId.Parse("fixed_cost");
        var formula = new CountingFormulaHandler(6);
        SkillDefinition skill = ActiveSkill(
            [new AnalyzeEffectDefinition([AnalysisLayer.Stats])],
            costs: [new SkillCostDefinition(Sp, new FormulaAmountDefinition(formulaId))]);
        BattleExecutionServices services = Services(formulas: [new(formulaId, formula)]);

        SkillExecutionResult result = new SkillExecutor(services).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        Assert.Equal(1, formula.CallCount);
        Assert.Equal(14, actor.GetRequiredResource(Sp).Current);
    }

    [Fact]
    public void Execute_RejectsMissingRuntimeHandlersBeforeCommittingCost()
    {
        BattleActorState actor = Actor("actor", PlayerTeam, sp: 20);
        SkillDefinition skill = ActiveSkill(
        [
            new CustomEffectDefinition(
                ContentId.Parse("missing_effect"),
                when: new CustomConditionDefinition(ContentId.Parse("missing_condition")))
        ],
        costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(5))],
        targeting: Untargeted());

        SkillExecutionResult result = new SkillExecutor(Services()).Execute(Request(skill, actor, [actor]));

        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SkillExecutionDiagnosticCode.CustomEffectHandlerMissing);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SkillExecutionDiagnosticCode.CustomConditionHandlerMissing);
        Assert.Equal(20, actor.GetRequiredResource(Sp).Current);
    }

    [Fact]
    public void Execute_RejectsInvalidRandomPolicyResult()
    {
        BattleActorState actor = Actor("actor", PlayerTeam, sp: 20);
        BattleActorState target = Actor("target", EnemyTeam);
        BattleActorState outsider = Actor("outsider", EnemyTeam);
        SkillDefinition skill = ActiveSkill(
            [new AnalyzeEffectDefinition([AnalysisLayer.Stats])],
            costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(5))],
            targeting: new TargetingDefinition(
                TargetRelation.Enemy,
                TargetSelection.Random,
                TargetLifeState.Alive,
                false,
                new TargetCountDefinition(1, 1)));
        BattleExecutionServices services = Services(randomTargets: (_, _, _) => [outsider]);

        SkillExecutionResult result = new SkillExecutor(services).Execute(Request(skill, actor, [actor, target]));

        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SkillExecutionDiagnosticCode.TargetSelectionInvalid);
        Assert.Equal(20, actor.GetRequiredResource(Sp).Current);
    }

    [Fact]
    public void Execute_RejectsEmptySkillAndDuplicateParticipantIdentity()
    {
        BattleActorState actor = Actor("actor", PlayerTeam, sp: 20);
        SkillDefinition skill = ActiveSkill(
            [],
            costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(5))],
            targeting: new TargetingDefinition(TargetRelation.Self, TargetSelection.Single, TargetLifeState.Alive, true));

        SkillExecutionResult result = new SkillExecutor(Services()).Execute(
            Request(skill, actor, [actor, actor], [actor.InstanceId]));

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SkillExecutionDiagnosticCode.SkillHasNoEffects);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == SkillExecutionDiagnosticCode.TargetSelectionInvalid);
        Assert.Equal(20, actor.GetRequiredResource(Sp).Current);
    }

    [Fact]
    public void Execute_AilmentLifecycleUsesAilmentIdentityAndExclusivity()
    {
        BattleActorState actor = Actor("actor", PlayerTeam);
        BattleActorState target = Actor("target", EnemyTeam);
        ContentId sleep = ContentId.Parse("sleep");
        ContentId mental = ContentId.Parse("mental_state");
        var poison = Ailment(Poison, mental);
        var sleepDefinition = Ailment(sleep, mental);
        BattleExecutionServices services = Services(ailments: new TestAilmentRepository([poison, sleepDefinition]));

        SkillExecutionResult poisonResult = new SkillExecutor(services).Execute(Request(
            ActiveSkill([new ApplyAilmentEffectDefinition(Poison, 100)]),
            actor,
            [actor, target],
            [target.InstanceId]));
        SkillExecutionResult sleepResult = new SkillExecutor(services).Execute(Request(
            ActiveSkill([new ApplyAilmentEffectDefinition(sleep, 100)]),
            actor,
            [actor, target],
            [target.InstanceId]));
        SkillExecutionResult cureResult = new SkillExecutor(services).Execute(Request(
            ActiveSkill([new RemoveAilmentEffectDefinition(AilmentRemovalScope.Selected, [sleep])]),
            actor,
            [actor, target],
            [target.InstanceId]));

        Assert.Equal(EffectExecutionOutcome.Success, poisonResult.Effects[0].Outcome);
        Assert.Equal(EffectExecutionOutcome.Success, sleepResult.Effects[0].Outcome);
        Assert.False(target.HasAilment(Poison));
        Assert.Equal(EffectExecutionOutcome.Success, cureResult.Effects[0].Outcome);
        Assert.False(target.HasAilment(sleep));
    }

    [Fact]
    public void Execute_EvaluatesCompleteConditionVocabularyPerTarget()
    {
        ContentId knownSkill = ContentId.Parse("known_skill");
        ContentId capability = ContentId.Parse("can_cast");
        ContentId attack = ContentId.Parse("attack");
        ContentId customCondition = ContentId.Parse("custom_gate");
        BattleActorState actor = Actor(
            "actor",
            PlayerTeam,
            skillIds: [knownSkill],
            capabilityIds: [capability]);
        BattleActorState target = Actor(
            "target",
            EnemyTeam,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Weak)]));
        target.ApplyAilment(Ailment(Poison), new BattleDurationDefinition());
        target.ChangeStatStage(attack, 1, null);
        ConditionDefinition[] conditions =
        [
            new AllConditionDefinition([
                new LifeStateConditionDefinition(ConditionSubject.Target, TargetLifeState.Alive),
                new HasAilmentConditionDefinition(ConditionSubject.Target, [Poison])]),
            new AnyConditionDefinition([
                new HasCapabilityConditionDefinition(ConditionSubject.Actor, ContentId.Parse("missing_capability")),
                new HasCapabilityConditionDefinition(ConditionSubject.Actor, capability)]),
            new NotConditionDefinition(
                new HasCapabilityConditionDefinition(ConditionSubject.Actor, ContentId.Parse("missing_capability"))),
            new ResourcePercentageConditionDefinition(ConditionSubject.Target, Hp, NumericComparison.Equal, 100),
            new HasAilmentConditionDefinition(ConditionSubject.Target, [Poison]),
            new HasSkillConditionDefinition(ConditionSubject.Actor, knownSkill),
            new HasBuffConditionDefinition(ConditionSubject.Target, attack),
            new HasAffinityConditionDefinition(ConditionSubject.Target, DamageElement.Fire, ElementalAffinity.Weak),
            new HasCapabilityConditionDefinition(ConditionSubject.Actor, capability),
            new LifeStateConditionDefinition(ConditionSubject.Target, TargetLifeState.Alive),
            new BattleKindConditionDefinition([NormalBattle]),
            new MoonPhaseConditionDefinition([NewMoon]),
            new PartySizeConditionDefinition(NumericComparison.Equal, 1),
            new ChanceConditionDefinition(1),
            new CustomConditionDefinition(customCondition)
        ];
        var effects = conditions
            .Select(condition => (EffectDefinition)new AnalyzeEffectDefinition([AnalysisLayer.Stats], condition))
            .Append(new DamageEffectDefinition(
                DamageElement.Fire,
                1,
                100,
                new NeverCriticalDefinition(),
                FixedHits(),
                When: new EffectElementConditionDefinition(DamageElement.Fire)))
            .ToArray();
        BattleExecutionServices services = Services(
            damage: _ => [new DamageHitResolution(true, 1)],
            customConditions: [new(customCondition, new ConstantCustomConditionHandler(true))]);

        SkillExecutionResult result = new SkillExecutor(services).Execute(
            Request(ActiveSkill(effects), actor, [actor, target], [target.InstanceId]));

        Assert.Equal(effects.Length, result.Effects.Count);
        Assert.All(result.Effects, effect => Assert.Equal(EffectExecutionOutcome.Success, effect.Outcome));
    }

    [Fact]
    public void Execute_InstantKillUsesHostPolicyAndTypedResistance()
    {
        BattleActorState actor = Actor("actor", PlayerTeam);
        BattleActorState target = Actor(
            "target",
            EnemyTeam,
            defense: new CombatDefenseProfile(
                instantDeathResistances: [new(InstantDeathChannel.Dark, ResistanceLevel.Resistant)]));
        InstantDeathPolicyRequest? observed = null;
        BattleExecutionServices services = Services(instantDeath: request =>
        {
            observed = request;
            return true;
        });
        var effect = new InstantKillEffectDefinition(
            25,
            new ChannelInstantDeathResistanceCheckDefinition(InstantDeathChannel.Dark));

        SkillExecutionResult result = new SkillExecutor(services).Execute(Request(
            ActiveSkill([effect]), actor, [actor, target], [target.InstanceId]));

        Assert.Equal(EffectExecutionOutcome.Success, result.Effects[0].Outcome);
        Assert.True(target.IsDefeated);
        Assert.NotNull(observed);
        Assert.Equal(ResistanceLevel.Resistant, observed.Resistance.Resistance);
        Assert.Equal(25, observed.Effect.Chance);
    }

    [Fact]
    public void Execute_ResourceAndRevivalEffectsMutateTypedResources()
    {
        BattleActorState actor = Actor("actor", PlayerTeam);
        BattleActorState target = Actor("target", EnemyTeam, hp: 40, sp: 50);
        var executor = new SkillExecutor(Services());

        SkillExecutionResult restore = ExecuteEffect(
            executor, new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)), actor, target);
        SkillExecutionResult reduce = ExecuteEffect(
            executor, new ReduceResourceEffectDefinition(Sp, new FlatAmountDefinition(10), false), actor, target);
        SkillExecutionResult set = ExecuteEffect(
            executor, new SetResourceEffectDefinition(Sp, new FlatAmountDefinition(5)), actor, target);
        target.SetResource(Hp, 0);
        SkillExecutionResult revive = ExecuteEffect(
            executor,
            new ReviveEffectDefinition(Hp, new FlatAmountDefinition(25)),
            actor,
            target,
            TargetLifeState.Dead);

        Assert.Equal(25, target.GetRequiredResource(Hp).Current);
        Assert.Equal(5, target.GetRequiredResource(Sp).Current);
        Assert.All([restore, reduce, set, revive], execution =>
            Assert.Equal(EffectExecutionOutcome.Success, execution.Effects[0].Outcome));
        Assert.Equal(20, restore.Effects[0].Value);
        Assert.Equal(25, revive.Effects[0].Value);
    }

    [Fact]
    public void Execute_StatusEffectsUseIndependentTypedStores()
    {
        BattleActorState actor = Actor("actor", PlayerTeam);
        BattleActorState target = Actor("target", EnemyTeam);
        var executor = new SkillExecutor(Services());
        ContentId attack = ContentId.Parse("attack");
        ContentId mark = ContentId.Parse("marked");

        ExecuteEffect(executor, new ModifyStatStageEffectDefinition([attack], 2), actor, target);
        ExecuteEffect(executor, new GrantChargeEffectDefinition(ChargeKind.Magical, 2.5m), actor, target);
        ExecuteEffect(executor, new GrantShieldEffectDefinition(ShieldKind.Physical), actor, target);
        ExecuteEffect(
            executor,
            new OverrideAffinityEffectDefinition(
                [DamageElement.Fire],
                ElementalAffinity.Null,
                new BattleDurationDefinition()),
            actor,
            target);
        target.AddOtherStatus(mark);
        ExecuteEffect(
            executor,
            new RemoveStatusEffectDefinition([StatusEffectKind.Other], [mark]),
            actor,
            target);

        Assert.Equal(2, target.StatStages[attack].Stage);
        Assert.Equal(2.5m, target.Charges[ChargeKind.Magical].Multiplier);
        Assert.True(target.Shields.ContainsKey(ShieldKind.Physical));
        Assert.Equal(ElementalAffinity.Null, target.GetElementalAffinity(DamageElement.Fire));
        Assert.DoesNotContain(mark, target.OtherStatuses);
    }

    [Fact]
    public void TemporaryAffinityOverrideRemainsBelowShieldAndBreakPrecedence()
    {
        var profile = new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Weak)]);

        Assert.Equal(
            ElementalAffinity.Absorb,
            ElementalAffinityResolver.Resolve(
                profile,
                DamageElement.Fire,
                activeOverride: ElementalAffinity.Absorb));
        Assert.Equal(
            ElementalAffinity.Repel,
            ElementalAffinityResolver.Resolve(
                profile,
                DamageElement.Fire,
                activeShields: [ShieldKind.Magical],
                activeOverride: ElementalAffinity.Absorb));
        Assert.Equal(
            ElementalAffinity.Normal,
            ElementalAffinityResolver.Resolve(
                profile,
                DamageElement.Fire,
                isBroken: true,
                activeOverride: ElementalAffinity.Absorb));
    }

    [Fact]
    public void Execute_AnalyzeEscapeAndCustomEffectsRemainHostNeutral()
    {
        BattleActorState actor = Actor("actor", PlayerTeam, sp: 10);
        BattleActorState target = Actor("target", EnemyTeam);
        ContentId escapeRule = ContentId.Parse("standard_escape");
        ContentId customHandlerId = ContentId.Parse("restore_actor_sp");
        var customHandler = new MutatingCustomEffectHandler(Sp, 3);
        BattleExecutionServices services = Services(
            escapeRules: [new(escapeRule, new DelegateEscapeRuleHandler(_ => true))],
            customEffects: [new(customHandlerId, customHandler)]);
        var executor = new SkillExecutor(services);

        SkillExecutionResult analyze = ExecuteEffect(
            executor, new AnalyzeEffectDefinition([AnalysisLayer.Stats, AnalysisLayer.Skills]), actor, target);
        SkillExecutionResult escape = new SkillExecutor(services).Execute(Request(
            ActiveSkill([new EscapeEffectDefinition(escapeRule, 100)], targeting: Untargeted()),
            actor,
            [actor]));
        SkillExecutionResult custom = new SkillExecutor(services).Execute(Request(
            ActiveSkill([new CustomEffectDefinition(customHandlerId)], targeting: Untargeted()),
            actor,
            [actor]));

        Assert.Equal([AnalysisLayer.Stats, AnalysisLayer.Skills], actor.GetAnalysis(target.InstanceId).Order());
        Assert.Equal(EffectExecutionOutcome.Success, analyze.Effects[0].Outcome);
        Assert.True(escape.EscapeRequested);
        Assert.Equal(13, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(0, custom.Effects[0].EffectIndex);
        Assert.Null(custom.Effects[0].TargetId);
    }

    [Fact]
    public void DefaultRegistrySupportsEveryApprovedActiveEffect()
    {
        EffectExecutorRegistry registry = EffectExecutorRegistry.CreateDefault();
        Type[] effectTypes =
        [
            typeof(DamageEffectDefinition),
            typeof(InstantKillEffectDefinition),
            typeof(ApplyAilmentEffectDefinition),
            typeof(RestoreResourceEffectDefinition),
            typeof(RemoveAilmentEffectDefinition),
            typeof(ReviveEffectDefinition),
            typeof(ModifyStatStageEffectDefinition),
            typeof(GrantChargeEffectDefinition),
            typeof(GrantShieldEffectDefinition),
            typeof(OverrideAffinityEffectDefinition),
            typeof(RemoveStatusEffectDefinition),
            typeof(ReduceResourceEffectDefinition),
            typeof(SetResourceEffectDefinition),
            typeof(AnalyzeEffectDefinition),
            typeof(EscapeEffectDefinition),
            typeof(CustomEffectDefinition)
        ];

        Assert.All(effectTypes, type => Assert.True(registry.Supports(type), type.Name));
    }

    [Fact]
    public void BattleActorState_DefensivelyCopiesAuthoredRuntimeInputs()
    {
        var hp = new BattleResourceState(Hp, 80, 100);
        var skills = new List<ContentId> { ContentId.Parse("agi") };
        var capabilities = new List<ContentId> { ContentId.Parse("can_cast") };
        var actor = new BattleActorState(
            ContentId.Parse("actor"),
            ContentId.Parse("actor_entity"),
            PlayerTeam,
            Hp,
            CombatDefenseProfile.Empty,
            [hp],
            skillIds: skills,
            capabilityIds: capabilities);

        skills.Clear();
        capabilities.Clear();

        Assert.Equal(80, actor.GetRequiredResource(Hp).Current);
        Assert.NotSame(hp, actor.GetRequiredResource(Hp));
        Assert.True(actor.HasSkill(ContentId.Parse("agi")));
        Assert.True(actor.HasCapability(ContentId.Parse("can_cast")));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ContentId, BattleResourceState>)actor.Resources).Add(
                Sp,
                new BattleResourceState(Sp, 1, 1)));
    }

    [Fact]
    public void ExecutionPublicApiDoesNotExposeLegacyOrHostSpecificTypes()
    {
        Type[] publicTypes = typeof(SkillExecutor).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "JRPGPrototype.Logic.Battle.Execution")
            .ToArray();
        string[] forbidden = ["Newtonsoft", "System.Text.Json", "Godot", "SkillData", "PersonaData", "Combatant"];

        IEnumerable<Type> exposedTypes = publicTypes.SelectMany(PublicSignatureTypes);

        Assert.DoesNotContain(exposedTypes, type =>
            forbidden.Any(token => (type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal)));
    }

    private static SkillExecutionResult ExecuteEffect(
        SkillExecutor executor,
        EffectDefinition effect,
        BattleActorState actor,
        BattleActorState target,
        TargetLifeState lifeState = TargetLifeState.Alive) =>
        executor.Execute(Request(
            ActiveSkill(
                [effect],
                targeting: new TargetingDefinition(TargetRelation.Any, TargetSelection.Single, lifeState, true)),
            actor,
            [actor, target],
            [target.InstanceId]));

    private static SkillDefinition ActiveSkill(
        IEnumerable<EffectDefinition> effects,
        IEnumerable<SkillCostDefinition>? costs = null,
        TargetingDefinition? targeting = null,
        IEnumerable<ContentId>? availability = null) =>
        new(
            ContentId.Parse("test_skill"),
            "Test Skill",
            "Executes typed effects.",
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Physical,
            new SkillInheritanceDefinition(true),
            costs: costs,
            targeting: targeting ?? new TargetingDefinition(
                TargetRelation.Enemy,
                TargetSelection.Single,
                TargetLifeState.Alive,
                false),
            effects: effects,
            availability: new SkillAvailabilityDefinition(availability ?? [Battle]));

    private static SkillExecutionRequest Request(
        SkillDefinition skill,
        BattleActorState actor,
        IEnumerable<BattleActorState> participants,
        IEnumerable<ContentId>? selectedTargets = null) =>
        new(skill, actor, participants, Battle, NormalBattle, NewMoon, selectedTargets);

    private static BattleActorState Actor(
        string id,
        ContentId team,
        decimal hp = 100,
        decimal sp = 100,
        CombatDefenseProfile? defense = null,
        IEnumerable<ContentId>? skillIds = null,
        IEnumerable<ContentId>? capabilityIds = null) =>
        new(
            ContentId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            team,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100), new BattleResourceState(Sp, sp, 100)],
            skillIds: skillIds,
            capabilityIds: capabilityIds);

    private static TargetingDefinition Untargeted() =>
        new(TargetRelation.None, TargetSelection.None, TargetLifeState.Any, false);

    private static HitCountDefinition FixedHits() => new(1, 1);

    private static AilmentDefinition Ailment(ContentId id, ContentId? exclusivity = null) =>
        new(
            id,
            id.ToString(),
            "Test ailment.",
            new TurnDurationDefinition(3, ContentId.Parse("owner_turn_end"), false),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1, 0, 1, 1, false),
            new AilmentRecoveryDefinition(),
            exclusivityGroupId: exclusivity);

    private static BattleExecutionServices Services(
        Func<DamagePolicyRequest, IReadOnlyList<DamageHitResolution>>? damage = null,
        Func<InstantDeathPolicyRequest, bool>? instantDeath = null,
        IAilmentDefinitionRepository? ailments = null,
        Func<IReadOnlyList<BattleActorState>, TargetCountDefinition, SkillExecutionRequest, IReadOnlyList<BattleActorState>>? randomTargets = null,
        IEnumerable<KeyValuePair<ContentId, IFormulaAmountHandler>>? formulas = null,
        IEnumerable<KeyValuePair<ContentId, IEscapeRuleHandler>>? escapeRules = null,
        IEnumerable<KeyValuePair<ContentId, ICustomConditionHandler>>? customConditions = null,
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffects = null) =>
        new(
            ailments ?? new TestAilmentRepository([Ailment(Poison)]),
            new DelegateDamagePolicy(damage ?? (_ => [new DamageHitResolution(true, 10)])),
            new DelegateInstantDeathPolicy(instantDeath ?? (_ => true)),
            new AlwaysApplyAilmentPolicy(),
            new AlwaysChancePolicy(),
            new PowerAmountPolicy(),
            new DelegateRandomTargetPolicy(randomTargets ?? ((candidates, count, _) =>
                candidates.Take(count.Minimum).ToArray())),
            formulaHandlers: formulas,
            escapeRuleHandlers: escapeRules,
            customConditionHandlers: customConditions,
            customEffectHandlers: customEffects);

    private static IEnumerable<Type> PublicSignatureTypes(Type type)
    {
        yield return type;
        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var method in type.GetMethods())
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }

        foreach (var property in type.GetProperties())
        {
            yield return property.PropertyType;
        }
    }

    private sealed class TestAilmentRepository : IAilmentDefinitionRepository
    {
        private readonly Dictionary<ContentId, AilmentDefinition> _ailments;

        public TestAilmentRepository(IEnumerable<AilmentDefinition> ailments)
        {
            _ailments = ailments.ToDictionary(ailment => ailment.Id);
        }

        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition) =>
            _ailments.TryGetValue(id, out definition);

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            _ailments.TryGetValue(id, out AilmentDefinition? definition)
                ? definition
                : throw new KeyNotFoundException(id.ToString());
    }

    private sealed class DelegateDamagePolicy(
        Func<DamagePolicyRequest, IReadOnlyList<DamageHitResolution>> resolve) : IDamageExecutionPolicy
    {
        public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request) => resolve(request);
    }

    private sealed class DelegateInstantDeathPolicy(Func<InstantDeathPolicyRequest, bool> resolve)
        : IInstantDeathExecutionPolicy
    {
        public bool ShouldDefeat(InstantDeathPolicyRequest request) => resolve(request);
    }

    private sealed class AlwaysApplyAilmentPolicy : IAilmentApplicationPolicy
    {
        public bool ShouldApply(AilmentApplicationPolicyRequest request) => true;
    }

    private sealed class AlwaysChancePolicy : IChanceExecutionPolicy
    {
        public bool Roll(ChancePolicyRequest request) => true;
    }

    private sealed class PowerAmountPolicy : IPowerAmountPolicy
    {
        public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) => amount.Power;
    }

    private sealed class DelegateRandomTargetPolicy(
        Func<IReadOnlyList<BattleActorState>, TargetCountDefinition, SkillExecutionRequest, IReadOnlyList<BattleActorState>> select)
        : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<BattleActorState> Select(
            IReadOnlyList<BattleActorState> candidates,
            TargetCountDefinition count,
            SkillExecutionRequest request) => select(candidates, count, request);
    }

    private sealed class CountingFormulaHandler(decimal value) : IFormulaAmountHandler
    {
        public int CallCount { get; private set; }

        public decimal Resolve(FormulaAmountDefinition amount, AmountResolutionContext context)
        {
            CallCount++;
            return value;
        }
    }

    private sealed class DelegateEscapeRuleHandler(Func<EffectExecutionContext, bool> canEscape)
        : IEscapeRuleHandler
    {
        public bool CanEscape(EscapeEffectDefinition effect, EffectExecutionContext context) => canEscape(context);
    }

    private sealed class MutatingCustomEffectHandler(ContentId resourceId, decimal amount) : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context)
        {
            context.Actor.AddResource(resourceId, amount);
            return new EffectExecutionResult(999, ContentId.Parse("forged_target"), EffectExecutionOutcome.Success, Value: amount);
        }
    }

    private sealed class ConstantCustomConditionHandler(bool value) : ICustomConditionHandler
    {
        public bool Evaluate(CustomConditionDefinition condition, BattleConditionContext context) => value;
    }
}
