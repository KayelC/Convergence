using Convergence.Content;
using Convergence.Catalog;
using Convergence.Battle;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

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
        yield return [ElementalAffinity.Normal, true, false, TurnEconomyOutcome.Normal, EffectExecutionOutcome.Success];
        yield return [ElementalAffinity.Normal, true, true, TurnEconomyOutcome.Critical, EffectExecutionOutcome.Success];
        yield return [ElementalAffinity.Weak, true, false, TurnEconomyOutcome.Weakness, EffectExecutionOutcome.Success];
        yield return [ElementalAffinity.Normal, false, false, TurnEconomyOutcome.Miss, EffectExecutionOutcome.Failure];
        yield return [ElementalAffinity.Null, true, false, TurnEconomyOutcome.Null, EffectExecutionOutcome.Failure];
        yield return [ElementalAffinity.Repel, true, false, TurnEconomyOutcome.Repel, EffectExecutionOutcome.Interrupted];
        yield return [ElementalAffinity.Absorb, true, false, TurnEconomyOutcome.Absorb, EffectExecutionOutcome.Interrupted];
    }

    [Fact]
    public void ExecutionServices_RequireExplicitSkillAndRuntimeRandomTargetPolicies()
    {
        System.Reflection.ConstructorInfo constructor = Assert.Single(typeof(BattleExecutionServices).GetConstructors());
        System.Reflection.ParameterInfo skillPolicy = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.Name == "randomTargetPolicy");
        System.Reflection.ParameterInfo runtimePolicy = Assert.Single(
            constructor.GetParameters(),
            parameter => parameter.Name == "runtimeRandomTargetPolicy");

        Assert.False(skillPolicy.IsOptional);
        Assert.False(runtimePolicy.IsOptional);

        Assert.Throws<ArgumentNullException>(() => new BattleExecutionServices(
            new TestAilmentRepository([Ailment(Poison)]),
            new DelegateDamagePolicy(_ => [new DamageHitResolution(true, 1)]),
            new DelegateInstantDeathPolicy(_ => false),
            new AlwaysApplyAilmentPolicy(),
            new AlwaysChancePolicy(),
            new PowerAmountPolicy(),
            null!,
            new OrderedRuntimeTargetSelectionPolicy()));
        Assert.Throws<ArgumentNullException>(() => new BattleExecutionServices(
            new TestAilmentRepository([Ailment(Poison)]),
            new DelegateDamagePolicy(_ => [new DamageHitResolution(true, 1)]),
            new DelegateInstantDeathPolicy(_ => false),
            new AlwaysApplyAilmentPolicy(),
            new AlwaysChancePolicy(),
            new PowerAmountPolicy(),
            new DelegateRandomTargetPolicy((candidates, count, _) => candidates.Take(count.Minimum).ToArray()),
            null!));
    }

    [Fact]
    public void Execute_RejectsIndependentPreflightErrorsWithoutSpendingResources()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, hp: 100, sp: 5);
        RuntimeActorState target = Actor("target", EnemyTeam);
        SkillDefinition skill = ActiveSkill(
            [new DamageEffectDefinition(DamageElement.Fire, 10, 100, new NeverCriticalDefinition(), FixedHits())],
            costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(10))],
            availability: [ContentId.Parse("field")]);
        var executor = new SkillExecutor(Services());

        SkillExecutionResult result = executor.Execute(Request(skill, actor, [actor, target], [RuntimeInstanceId.Parse("missing_target")]));

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
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 30);
        RuntimeActorState target = Actor(
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
        ExecutionResourceChange costChange = Assert.Single(result.CommittedCostChanges);
        Assert.Equal(actor.InstanceId, costChange.ActorId);
        Assert.Equal(Sp, costChange.ResourceId);
        Assert.Equal(-8, costChange.Delta);
        ExecutionResourceChange damageChange = Assert.Single(result.Effects[0].ResourceChanges);
        Assert.Equal(target.InstanceId, damageChange.ActorId);
        Assert.Equal(Hp, damageChange.ResourceId);
        Assert.Equal(-25, damageChange.Delta);
        Assert.Empty(result.Effects[1].ResourceChanges);
        Assert.Equal(TurnEconomyOutcome.Weakness, result.TurnEconomy.Outcome);
        Assert.True(result.TurnEconomy.AnyCritical);
    }

    [Fact]
    public void Execute_SaturatesExtremeMultiHitAndPercentResourceArithmetic()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        var damageTarget = new RuntimeActorState(
            RuntimeInstanceId.Parse("damage_target"),
            ContentId.Parse("damage_target_entity"),
            EnemyTeam,
            Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, decimal.MaxValue, decimal.MaxValue)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true));
        SkillDefinition damageSkill = ActiveSkill(
        [
            new DamageEffectDefinition(
                DamageElement.Fire,
                10,
                100,
                new NeverCriticalDefinition(),
                FixedHits())
        ]);
        SkillExecutionResult damage = new SkillExecutor(Services(
            damage: _ =>
            [
                new DamageHitResolution(true, decimal.MaxValue),
                new DamageHitResolution(true, decimal.MaxValue)
            ])).Execute(Request(
                damageSkill,
                actor,
                [actor, damageTarget],
                [damageTarget.InstanceId]));

        decimal halfMaximum = decimal.MaxValue / 2m;
        var recoveryTarget = new RuntimeActorState(
            RuntimeInstanceId.Parse("recovery_target"),
            ContentId.Parse("recovery_target_entity"),
            EnemyTeam,
            Hp,
            CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, halfMaximum, decimal.MaxValue)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true));
        SkillDefinition recoverySkill = ActiveSkill(
            [new RestoreResourceEffectDefinition(Hp, new PercentMaximumAmountDefinition(100m))]);
        SkillExecutionResult recovery = new SkillExecutor(Services()).Execute(Request(
            recoverySkill,
            actor,
            [actor, recoveryTarget],
            [recoveryTarget.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, damage.Status);
        Assert.Equal(decimal.MaxValue, Assert.Single(damage.Effects).Value);
        Assert.Equal(0m, damageTarget.GetRequiredResource(Hp).Current);
        Assert.Equal(SkillExecutionStatus.Executed, recovery.Status);
        Assert.Equal(decimal.MaxValue, recoveryTarget.GetRequiredResource(Hp).Current);
        Assert.Equal(decimal.MaxValue - halfMaximum, Assert.Single(recovery.Effects).Value);
    }

    [Fact]
    public void Assess_RejectsAnUnrepresentableAggregateSkillCostWithoutMutation()
    {
        var actor = new RuntimeActorState(
            RuntimeInstanceId.Parse("actor"),
            ContentId.Parse("actor_entity"),
            PlayerTeam,
            Hp,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(Hp, 100m, 100m),
                new BattleResourceState(Sp, decimal.MaxValue, decimal.MaxValue)
            ],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true));
        RuntimeActorState target = Actor("target", EnemyTeam);
        SkillDefinition skill = ActiveSkill(
            [new DamageEffectDefinition(DamageElement.Fire, 10, 100, new NeverCriticalDefinition(), FixedHits())],
            costs:
            [
                new SkillCostDefinition(Sp, new FlatAmountDefinition(decimal.MaxValue), true),
                new SkillCostDefinition(Sp, new FlatAmountDefinition(decimal.MaxValue), true)
            ]);
        var executor = new SkillExecutor(Services());
        SkillExecutionRequest request = Request(skill, actor, [actor, target], [target.InstanceId]);

        SkillExecutionAssessment assessment = executor.Assess(request);
        SkillExecutionResult execution = executor.Execute(request, assessment);

        Assert.False(assessment.CanExecute);
        Assert.Contains(assessment.Diagnostics, diagnostic =>
            diagnostic.Code == SkillExecutionDiagnosticCode.InsufficientResource);
        Assert.Equal(SkillExecutionStatus.Rejected, execution.Status);
        Assert.Equal(decimal.MaxValue, actor.GetRequiredResource(Sp).Current);
        Assert.False(execution.CostsCommitted);
    }

    [Fact]
    public void Execute_PreparedAssessmentRejectsWhenCurrentResourceCanNoLongerPay()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 10);
        RuntimeActorState target = Actor("target", EnemyTeam);
        SkillDefinition skill = ActiveSkill(
            [new DamageEffectDefinition(DamageElement.Fire, 10, 100, new NeverCriticalDefinition(), FixedHits())],
            costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(10), CanReduceToZero: true)]);
        var executor = new SkillExecutor(Services());
        SkillExecutionRequest request = Request(skill, actor, [actor, target], [target.InstanceId]);
        SkillExecutionAssessment assessment = executor.Assess(request);
        actor.SetResource(Sp, 0);

        SkillExecutionResult result = executor.Execute(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == SkillExecutionDiagnosticCode.InsufficientResource);
        Assert.Empty(result.Effects);
        Assert.False(result.CostsCommitted);
        Assert.Equal(0, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);

        actor.SetResource(Sp, 10);
        SkillExecutionResult reused = executor.Execute(request, assessment);
        Assert.Equal(SkillExecutionStatus.Rejected, reused.Status);
        Assert.Equal(SkillExecutionDiagnosticCode.AssessmentInvalid, Assert.Single(reused.Diagnostics).Code);
        Assert.Equal(10, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void Execute_PreparedAssessmentRevalidatesTheNonZeroResourceFloor()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 11);
        RuntimeActorState target = Actor("target", EnemyTeam);
        SkillDefinition skill = ActiveSkill(
            [new DamageEffectDefinition(DamageElement.Fire, 10, 100, new NeverCriticalDefinition(), FixedHits())],
            costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(10), CanReduceToZero: false)]);
        var executor = new SkillExecutor(Services());
        SkillExecutionRequest request = Request(skill, actor, [actor, target], [target.InstanceId]);
        SkillExecutionAssessment assessment = executor.Assess(request);
        actor.SetResource(Sp, 10);

        SkillExecutionResult result = executor.Execute(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == SkillExecutionDiagnosticCode.InsufficientResource);
        Assert.Empty(result.Effects);
        Assert.False(result.CostsCommitted);
        Assert.Equal(10, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void Execute_PreparedAssessmentRejectsTargetThatBecameIneligible()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 10);
        RuntimeActorState target = Actor("target", EnemyTeam);
        SkillDefinition skill = ActiveSkill(
            [new DamageEffectDefinition(DamageElement.Fire, 10, 100, new NeverCriticalDefinition(), FixedHits())],
            costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(3))]);
        var executor = new SkillExecutor(Services());
        SkillExecutionRequest request = Request(skill, actor, [actor, target], [target.InstanceId]);
        SkillExecutionAssessment assessment = executor.Assess(request);
        target.SetResource(Hp, 0);

        SkillExecutionResult result = executor.Execute(request, assessment);

        Assert.True(assessment.CanExecute);
        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == SkillExecutionDiagnosticCode.TargetSelectionInvalid);
        Assert.Empty(result.Effects);
        Assert.False(result.CostsCommitted);
        Assert.Equal(10, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(0, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void Execute_TypedAilmentUsesAuthoritativeGuardRule()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam);
        target.SetGuarding(true);
        SkillDefinition skill = ActiveSkill([new ApplyAilmentEffectDefinition(Poison, 100)]);

        SkillExecutionResult result = new SkillExecutor(Services()).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        EffectExecutionResult effect = Assert.Single(result.Effects);
        Assert.Equal(EffectExecutionOutcome.Failure, effect.Outcome);
        Assert.Contains(nameof(BattleAilmentApplicationStatus.GuardBlocked), effect.Detail, StringComparison.Ordinal);
        Assert.False(target.HasAilment(Poison));
        Assert.True(target.IsGuarding);
    }

    [Fact]
    public void Execute_TypedAilmentDelegatesToInjectedApplicationAuthority()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam);
        var authority = new RecordingAilmentApplicationService();
        SkillDefinition skill = ActiveSkill([new ApplyAilmentEffectDefinition(Poison, 73)]);

        SkillExecutionResult result = new SkillExecutor(Services(ailmentApplications: authority)).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(1, authority.CallCount);
        Assert.Equal(73, authority.LastRequest!.Chance);
        Assert.Equal(Poison, authority.LastRequest.Ailment.Id);
        Assert.Equal(EffectExecutionOutcome.Failure, Assert.Single(result.Effects).Outcome);
        Assert.False(target.HasAilment(Poison));
    }

    [Fact]
    public void Execute_FalseConditionSkipsWithoutActivatingStopAction()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam, hp: 100);
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
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState first = Actor("first", EnemyTeam, hp: 100);
        RuntimeActorState second = Actor("second", EnemyTeam, hp: 50);
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
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam, hp: 40);
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
        RuntimeActorState actor = Actor("actor", PlayerTeam, hp: 100);
        RuntimeActorState target = Actor(
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
        Assert.Equal(TurnEconomyOutcome.Repel, result.TurnEconomy.Outcome);
        Assert.True(result.TurnEconomy.TerminatesPhase);
        Assert.Equal(70, actor.GetRequiredResource(Hp).Current);
        Assert.Equal(100, target.GetRequiredResource(Hp).Current);
        ExecutionResourceChange reflected = Assert.Single(result.Effects[0].ResourceChanges);
        Assert.Equal(actor.InstanceId, reflected.ActorId);
        Assert.Equal(Hp, reflected.ResourceId);
        Assert.Equal(-30, reflected.Delta);
    }

    [Theory]
    [MemberData(nameof(DamageOutcomeCases))]
    public void Execute_DamagePreservesTurnEconomyOutcomes(
        ElementalAffinity affinity,
        bool hit,
        bool critical,
        TurnEconomyOutcome expectedTurnEconomy,
        EffectExecutionOutcome expectedExecution)
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, hp: 100);
        RuntimeActorState target = Actor(
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
        Assert.Equal(expectedTurnEconomy, result.TurnEconomy.Outcome);
        Assert.Equal(critical, result.TurnEconomy.AnyCritical);
        Assert.Equal(
            expectedTurnEconomy is TurnEconomyOutcome.Repel or TurnEconomyOutcome.Absorb,
            result.TurnEconomy.TerminatesPhase);
    }

    [Fact]
    public void Execute_DamageUsesThePolicyResolvedAffinityForTurnEconomy()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, hp: 100);
        RuntimeActorState target = Actor(
            "target",
            EnemyTeam,
            hp: 50,
            defense: new CombatDefenseProfile([new(DamageElement.Fire, ElementalAffinity.Weak)]));
        SkillDefinition skill = ActiveSkill(
            [new DamageEffectDefinition(DamageElement.Fire, 10, 100, new NeverCriticalDefinition(), FixedHits())]);

        SkillExecutionResult result = new SkillExecutor(Services(
            damagePolicy: new NormalizingDamagePolicy())).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        EffectExecutionResult effect = Assert.Single(result.Effects);
        Assert.Equal(ElementalAffinity.Normal, effect.ResolvedAffinity);
        Assert.Equal(TurnEconomyOutcome.Normal, result.TurnEconomy.Outcome);
        Assert.Equal(40, target.GetRequiredResource(Hp).Current);
    }

    [Fact]
    public void Execute_ResolvesFormulaCostOnceBeforeCommit()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 20);
        RuntimeActorState target = Actor("target", EnemyTeam);
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
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 20);
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
    public void Execute_ThrowingEffectRollsBackPriorEffectsAndSkillCosts()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 20);
        RuntimeActorState target = Actor("target", EnemyTeam, hp: 40);
        ContentId handlerId = ContentId.Parse("throw_after_mutation");
        ContentId attack = ContentId.Parse("attack");
        SkillDefinition skill = ActiveSkill(
        [
            new RestoreResourceEffectDefinition(Hp, new FlatAmountDefinition(20)),
            new ApplyAilmentEffectDefinition(Poison, 100),
            new ModifyStatStageEffectDefinition([attack], 1),
            new GrantChargeEffectDefinition(ChargeKind.Magical, 2m),
            new GrantShieldEffectDefinition(ShieldKind.Magical),
            new BreakAffinityEffectDefinition(
                [DamageElement.Fire],
                new BattleDurationDefinition()),
            new OverrideAffinityEffectDefinition(
                [DamageElement.Fire],
                ElementalAffinity.Null,
                new BattleDurationDefinition()),
            new AnalyzeEffectDefinition([AnalysisLayer.Stats]),
            new CustomEffectDefinition(handlerId)
        ],
        costs: [new SkillCostDefinition(Sp, new FlatAmountDefinition(5))]);
        BattleExecutionServices services = Services(
            customEffects: [new(handlerId, new ThrowingCustomEffectHandler())]);

        SkillExecutionResult result = new SkillExecutor(services).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.Equal(
            SkillExecutionDiagnosticCode.ExecutionFailed,
            Assert.Single(result.Diagnostics).Code);
        Assert.False(result.CostsCommitted);
        Assert.Equal(20, actor.GetRequiredResource(Sp).Current);
        Assert.Equal(40, target.GetRequiredResource(Hp).Current);
        Assert.False(target.HasAilment(Poison));
        Assert.Empty(target.StatStages);
        Assert.Empty(target.Charges);
        Assert.Empty(target.Shields);
        Assert.Empty(target.AffinityBreaks);
        Assert.Empty(target.AffinityOverrides);
        Assert.Empty(actor.GetAnalysis(target.InstanceId));
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void Assess_ThrowingFormulaReturnsTypedFailureWithoutMutation()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 20);
        RuntimeActorState target = Actor("target", EnemyTeam);
        ContentId formulaId = ContentId.Parse("throwing_formula");
        SkillDefinition skill = ActiveSkill(
            [new AnalyzeEffectDefinition([AnalysisLayer.Stats])],
            costs: [new SkillCostDefinition(Sp, new FormulaAmountDefinition(formulaId))]);
        BattleExecutionServices services = Services(
            formulas: [new(formulaId, new ThrowingFormulaHandler())]);

        SkillExecutionResult result = new SkillExecutor(services).Execute(
            Request(skill, actor, [actor, target], [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Rejected, result.Status);
        Assert.Equal(
            SkillExecutionDiagnosticCode.ExecutionFailed,
            Assert.Single(result.Diagnostics).Code);
        Assert.Equal(20, actor.GetRequiredResource(Sp).Current);
        Assert.Empty(actor.GetAnalysis(target.InstanceId));
    }

    [Fact]
    public void Execute_RejectsInvalidRandomPolicyResult()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 20);
        RuntimeActorState target = Actor("target", EnemyTeam);
        RuntimeActorState outsider = Actor("outsider", EnemyTeam);
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
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 20);
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
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam);
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
        RuntimeActorState actor = Actor(
            "actor",
            PlayerTeam,
            skillIds: [knownSkill],
            capabilityIds: [capability]);
        RuntimeActorState target = Actor(
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
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor(
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
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam, hp: 40, sp: 50);
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
        Assert.Equal(20, Assert.Single(restore.Effects[0].ResourceChanges).Delta);
        Assert.Equal(-10, Assert.Single(reduce.Effects[0].ResourceChanges).Delta);
        Assert.Equal(-35, Assert.Single(set.Effects[0].ResourceChanges).Delta);
        Assert.Equal(25, Assert.Single(revive.Effects[0].ResourceChanges).Delta);
    }

    [Fact]
    public void Execute_StatusEffectsUseIndependentTypedStores()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam);
        var executor = new SkillExecutor(Services());
        ContentId attack = ContentId.Parse("attack");
        ContentId mark = ContentId.Parse("marked");

        SkillExecutionResult stage = ExecuteEffect(
            executor,
            new ModifyStatStageEffectDefinition([attack], 2),
            actor,
            target);
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
        Assert.Empty(stage.Effects[0].ResourceChanges);
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
    public void AffinityBreak_ExecutesAsTimedElementSpecificStateAndDrivesDamageResolution()
    {
        ContentId ownerTurnEnd = ContentId.Parse("owner_turn_end");
        var duration = new TurnDurationDefinition(2, ownerTurnEnd, false);
        var profile = new CombatDefenseProfile(
        [
            new KeyValuePair<DamageElement, ElementalAffinity>(DamageElement.Fire, ElementalAffinity.Absorb),
            new KeyValuePair<DamageElement, ElementalAffinity>(DamageElement.Ice, ElementalAffinity.Null)
        ]);
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam, defense: profile);
        target.OverrideAffinity(DamageElement.Fire, ElementalAffinity.Resist, new BattleDurationDefinition());
        var executor = new SkillExecutor(Services());

        SkillExecutionResult applied = ExecuteEffect(
            executor,
            new BreakAffinityEffectDefinition([DamageElement.Fire], duration),
            actor,
            target);

        Assert.Equal(SkillExecutionStatus.Executed, applied.Status);
        Assert.Equal(duration, target.AffinityBreaks[DamageElement.Fire].Duration);
        Assert.Equal(ElementalAffinity.Normal, target.GetElementalAffinity(DamageElement.Fire));
        Assert.Equal(
            ElementalAffinity.Normal,
            target.GetElementalAffinity(DamageElement.Fire, [ElementalAffinity.Absorb]));
        Assert.Equal(ElementalAffinity.Null, target.GetElementalAffinity(DamageElement.Ice));
        Assert.Throws<ArgumentException>(() =>
            target.BreakAffinity(DamageElement.Almighty, duration));

        target.GrantShield(ShieldKind.Magical, null);
        Assert.Equal(ElementalAffinity.Repel, target.GetElementalAffinity(DamageElement.Fire));
        target.RemoveStatuses([StatusEffectKind.Shield], []);

        SkillExecutionResult damage = ExecuteEffect(
            executor,
            new DamageEffectDefinition(
                DamageElement.Fire,
                10,
                100,
                new NeverCriticalDefinition(),
                FixedHits()),
            actor,
            target);
        Assert.Equal(ElementalAffinity.Normal, Assert.Single(damage.Effects).ResolvedAffinity);
        Assert.Equal(90, target.GetRequiredResource(Hp).Current);

        BattleDurationTickResult firstTick = Assert.Single(target.TickTimedStatuses(ownerTurnEnd));
        Assert.False(firstTick.Expired);
        Assert.Equal(1, Assert.IsType<TurnDurationDefinition>(firstTick.CurrentDuration).Value);
        BattleDurationTickResult secondTick = Assert.Single(target.TickTimedStatuses(ownerTurnEnd));
        Assert.True(secondTick.Expired);
        Assert.Empty(target.AffinityBreaks);
        Assert.Equal(ElementalAffinity.Resist, target.GetElementalAffinity(DamageElement.Fire));

        target.BreakAffinity(DamageElement.Fire, duration);
        ExecuteEffect(
            executor,
            new RemoveStatusEffectDefinition([StatusEffectKind.AffinityBreak]),
            actor,
            target);
        Assert.Empty(target.AffinityBreaks);
        Assert.Equal(ElementalAffinity.Resist, target.GetElementalAffinity(DamageElement.Fire));
    }

    [Fact]
    public void InstantDuration_RemainsVisibleWithinTheOrderedActionAndExpiresBeforeTheNextAction()
    {
        var profile = new CombatDefenseProfile(
        [
            new KeyValuePair<DamageElement, ElementalAffinity>(
                DamageElement.Fire,
                ElementalAffinity.Weak)
        ]);
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam, defense: profile);
        var executor = new SkillExecutor(Services());
        SkillDefinition skill = ActiveSkill(
        [
            new BreakAffinityEffectDefinition(
                [DamageElement.Fire],
                new InstantDurationDefinition()),
            new DamageEffectDefinition(
                DamageElement.Fire,
                10,
                100,
                new NeverCriticalDefinition(),
                FixedHits())
        ]);

        SkillExecutionResult result = executor.Execute(Request(
            skill,
            actor,
            [actor, target],
            [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        EffectExecutionResult damage = Assert.Single(result.Effects, effect => effect.EffectIndex == 1);
        Assert.Equal(ElementalAffinity.Normal, damage.ResolvedAffinity);
        Assert.Empty(target.AffinityBreaks);
        Assert.Equal(ElementalAffinity.Weak, target.GetElementalAffinity(DamageElement.Fire));
    }

    [Fact]
    public void InstantDuration_IsNotExpiredByANestedOrderedEffectAction()
    {
        ContentId nestedHandlerId = ContentId.Parse("run_nested_action");
        var profile = new CombatDefenseProfile(
        [
            new KeyValuePair<DamageElement, ElementalAffinity>(
                DamageElement.Fire,
                ElementalAffinity.Weak)
        ]);
        RuntimeActorState actor = Actor("actor", PlayerTeam);
        RuntimeActorState target = Actor("target", EnemyTeam, defense: profile);
        BattleExecutionServices services = Services(
            customEffects: [new(nestedHandlerId, new NestedActionCustomEffectHandler())]);
        var executor = new SkillExecutor(services);
        SkillDefinition skill = ActiveSkill(
        [
            new BreakAffinityEffectDefinition(
                [DamageElement.Fire],
                new InstantDurationDefinition()),
            new CustomEffectDefinition(nestedHandlerId),
            new DamageEffectDefinition(
                DamageElement.Fire,
                10,
                100,
                new NeverCriticalDefinition(),
                FixedHits())
        ]);

        SkillExecutionResult result = executor.Execute(Request(
            skill,
            actor,
            [actor, target],
            [target.InstanceId]));

        Assert.Equal(SkillExecutionStatus.Executed, result.Status);
        EffectExecutionResult damage = Assert.Single(result.Effects, effect => effect.EffectIndex == 2);
        Assert.Equal(ElementalAffinity.Normal, damage.ResolvedAffinity);
        Assert.Empty(target.AffinityBreaks);
        Assert.Equal(ElementalAffinity.Weak, target.GetElementalAffinity(DamageElement.Fire));
    }

    [Fact]
    public void Execute_AnalyzeEscapeAndCustomEffectsRemainHostNeutral()
    {
        RuntimeActorState actor = Actor("actor", PlayerTeam, sp: 10);
        RuntimeActorState target = Actor("target", EnemyTeam);
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
            typeof(BreakAffinityEffectDefinition),
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
    public void RuntimeActorState_DefensivelyCopiesAuthoredRuntimeInputs()
    {
        var hp = new BattleResourceState(Hp, 80, 100);
        var skills = new List<ContentId> { ContentId.Parse("ember_dart") };
        var capabilities = new List<ContentId> { ContentId.Parse("can_cast") };
        var actor = new RuntimeActorState(
            RuntimeInstanceId.Parse("actor"),
            ContentId.Parse("actor_entity"),
            PlayerTeam,
            Hp,
            CombatDefenseProfile.Empty,
            [hp],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            skillIds: skills,
            capabilityIds: capabilities);

        skills.Clear();
        capabilities.Clear();

        Assert.Equal(80, actor.GetRequiredResource(Hp).Current);
        Assert.NotSame(hp, actor.GetRequiredResource(Hp));
        Assert.True(actor.HasSkill(ContentId.Parse("ember_dart")));
        Assert.True(actor.HasCapability(ContentId.Parse("can_cast")));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<ContentId, BattleResourceState>)actor.Resources).Add(
                Sp,
                new BattleResourceState(Sp, 1, 1)));
    }

    [Fact]
    public void EffectExecutionResult_SnapshotsConstructorAndRecordCloneCollections()
    {
        var originalActivation = new PassiveTriggerExecutionResult(
            ContentId.Parse("original_passive"),
            0,
            ContentId.Parse("original_event"),
            RuntimeInstanceId.Parse("original_target"),
            PassiveTriggerOutcome.Executed,
            []);
        var replacementActivation = new PassiveTriggerExecutionResult(
            ContentId.Parse("replacement_passive"),
            1,
            ContentId.Parse("replacement_event"),
            RuntimeInstanceId.Parse("replacement_target"),
            PassiveTriggerOutcome.Executed,
            []);
        var originalActivations = new List<PassiveTriggerExecutionResult> { originalActivation };
        var originalHostRequests = new List<ContentId> { ContentId.Parse("original_request") };
        var originalResourceChanges = new List<ExecutionResourceChange>
        {
            new(RuntimeInstanceId.Parse("original_target"), Hp, -5)
        };
        var replacementActivations = new List<PassiveTriggerExecutionResult> { replacementActivation };
        var replacementHostRequests = new List<ContentId> { ContentId.Parse("replacement_request") };
        var replacementResourceChanges = new List<ExecutionResourceChange>
        {
            new(RuntimeInstanceId.Parse("replacement_target"), Hp, 4)
        };
        var original = new EffectExecutionResult(
            0,
            RuntimeInstanceId.Parse("target"),
            EffectExecutionOutcome.Success,
            PassiveActivations: originalActivations,
            HostActionRequestIds: originalHostRequests)
        {
            ResourceChanges = originalResourceChanges
        };

        EffectExecutionResult clone = original with
        {
            Detail = "cloned",
            PassiveActivations = replacementActivations,
            HostActionRequestIds = replacementHostRequests,
            ResourceChanges = replacementResourceChanges
        };

        originalActivations.Clear();
        originalHostRequests.Clear();
        originalResourceChanges.Clear();
        replacementActivations.Clear();
        replacementHostRequests.Clear();
        replacementResourceChanges.Clear();

        Assert.Equal("cloned", clone.Detail);
        Assert.Equal(originalActivation, Assert.Single(original.PassiveActivations));
        Assert.Equal(ContentId.Parse("original_request"), Assert.Single(original.HostActionRequestIds));
        Assert.Equal(replacementActivation, Assert.Single(clone.PassiveActivations));
        Assert.Equal(ContentId.Parse("replacement_request"), Assert.Single(clone.HostActionRequestIds));
        Assert.Equal(-5, Assert.Single(original.ResourceChanges).Delta);
        Assert.Equal(4, Assert.Single(clone.ResourceChanges).Delta);
        Assert.NotSame(replacementActivations, clone.PassiveActivations);
        Assert.NotSame(replacementHostRequests, clone.HostActionRequestIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PassiveTriggerExecutionResult>)clone.PassiveActivations).Add(originalActivation));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContentId>)clone.HostActionRequestIds).Add(ContentId.Parse("forged_request")));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ExecutionResourceChange>)clone.ResourceChanges).Add(
                new ExecutionResourceChange(RuntimeInstanceId.Parse("forged_target"), Hp, 1)));
    }

    [Fact]
    public void ExecutionResourceChange_RejectsInvalidIdentityAndZeroDelta()
    {
        Assert.Throws<ArgumentException>(() =>
            new ExecutionResourceChange(default, Hp, 1));
        Assert.Throws<ArgumentException>(() =>
            new ExecutionResourceChange(RuntimeInstanceId.Parse("actor"), default, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExecutionResourceChange(RuntimeInstanceId.Parse("actor"), Hp, 0));
    }

    [Fact]
    public void ExecutionPublicApiDoesNotExposeLegacyOrHostSpecificTypes()
    {
        Type[] publicTypes = typeof(SkillExecutor).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == "Convergence.Execution")
            .ToArray();
        string[] forbidden =
        [
            "Newtonsoft",
            "System.Text.Json",
            "Godot",
            "SkillData",
            string.Concat("Per", "sona", "Data"),
            "Combatant"
        ];

        IEnumerable<Type> exposedTypes = publicTypes.SelectMany(PublicSignatureTypes);

        Assert.DoesNotContain(exposedTypes, type =>
            forbidden.Any(token => (type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal)));
    }

    private static SkillExecutionResult ExecuteEffect(
        SkillExecutor executor,
        EffectDefinition effect,
        RuntimeActorState actor,
        RuntimeActorState target,
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
        RuntimeActorState actor,
        IEnumerable<RuntimeActorState> participants,
        IEnumerable<RuntimeInstanceId>? selectedTargets = null) =>
        new(skill, actor, participants, Battle, NormalBattle, NewMoon, selectedTargets);

    private static RuntimeActorState Actor(
        string id,
        ContentId team,
        decimal hp = 100,
        decimal sp = 100,
        CombatDefenseProfile? defense = null,
        IEnumerable<ContentId>? skillIds = null,
        IEnumerable<ContentId>? capabilityIds = null) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse($"{id}_entity"),
            team,
            Hp,
            defense ?? CombatDefenseProfile.Empty,
            [new BattleResourceState(Hp, hp, 100), new BattleResourceState(Sp, sp, 100)],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
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
        IDamageExecutionPolicy? damagePolicy = null,
        Func<InstantDeathPolicyRequest, bool>? instantDeath = null,
        IAilmentDefinitionRepository? ailments = null,
        Func<IReadOnlyList<RuntimeActorState>, TargetCountDefinition, SkillExecutionRequest, IReadOnlyList<RuntimeActorState>>? randomTargets = null,
        IEnumerable<KeyValuePair<ContentId, IFormulaAmountHandler>>? formulas = null,
        IEnumerable<KeyValuePair<ContentId, IEscapeRuleHandler>>? escapeRules = null,
        IEnumerable<KeyValuePair<ContentId, ICustomConditionHandler>>? customConditions = null,
        IEnumerable<KeyValuePair<ContentId, ICustomEffectHandler>>? customEffects = null,
        IBattleAilmentApplicationService? ailmentApplications = null) =>
        new(
            ailments ?? new TestAilmentRepository([Ailment(Poison)]),
            damagePolicy ?? new DelegateDamagePolicy(damage ?? (_ => [new DamageHitResolution(true, 10)])),
            new DelegateInstantDeathPolicy(instantDeath ?? (_ => true)),
            new AlwaysApplyAilmentPolicy(),
            new AlwaysChancePolicy(),
            new PowerAmountPolicy(),
            new DelegateRandomTargetPolicy(randomTargets ?? ((candidates, count, _) =>
                candidates.Take(count.Minimum).ToArray())),
            new OrderedRuntimeTargetSelectionPolicy(),
            formulaHandlers: formulas,
            escapeRuleHandlers: escapeRules,
            customConditionHandlers: customConditions,
            customEffectHandlers: customEffects,
            ailmentApplications: ailmentApplications);

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
        public DamagePolicyResolution Resolve(DamagePolicyRequest request) =>
            new(resolve(request), request.Affinity);
    }

    private sealed class NormalizingDamagePolicy : IDamageExecutionPolicy
    {
        public DamagePolicyResolution Resolve(DamagePolicyRequest request) =>
            new([new DamageHitResolution(true, 10)], ElementalAffinity.Normal);
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

    private sealed class RecordingAilmentApplicationService : IBattleAilmentApplicationService
    {
        public int CallCount { get; private set; }
        public BattleAilmentApplicationRequest? LastRequest { get; private set; }

        public BattleAilmentApplicationResult Apply(
            BattleAilmentApplicationRequest request,
            BattleExecutionServices services)
        {
            CallCount++;
            LastRequest = request;
            return new BattleAilmentApplicationResult(
                BattleAilmentApplicationStatus.Missed,
                []);
        }
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
        Func<IReadOnlyList<RuntimeActorState>, TargetCountDefinition, SkillExecutionRequest, IReadOnlyList<RuntimeActorState>> select)
        : IRandomTargetSelectionPolicy
    {
        public IReadOnlyList<RuntimeActorState> Select(
            IReadOnlyList<RuntimeActorState> candidates,
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

    private sealed class ThrowingFormulaHandler : IFormulaAmountHandler
    {
        public decimal Resolve(FormulaAmountDefinition amount, AmountResolutionContext context) =>
            throw new InvalidOperationException("Formula extension failed deliberately.");
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
            return new EffectExecutionResult(999, RuntimeInstanceId.Parse("forged_target"), EffectExecutionOutcome.Success, Value: amount);
        }
    }

    private sealed class NestedActionCustomEffectHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context)
        {
            RuntimeActorState target = context.Target
                ?? throw new InvalidOperationException("The nested-action test requires a target.");
            SkillDefinition nestedSkill = ActiveSkill(
                [new AnalyzeEffectDefinition([AnalysisLayer.Stats])],
                targeting: new TargetingDefinition(
                    TargetRelation.Any,
                    TargetSelection.Single,
                    TargetLifeState.Any,
                    true));
            SkillExecutionResult nested = new SkillExecutor(context.Services).Execute(
                new SkillExecutionRequest(
                    nestedSkill,
                    context.Actor,
                    context.Request.Participants,
                    context.Request.ContextId,
                    context.Request.BattleKindId,
                    context.Request.MoonPhaseId,
                    [target.InstanceId]));
            if (nested.Status == SkillExecutionStatus.Rejected)
            {
                throw new InvalidOperationException("The nested action was rejected during the duration-scope test.");
            }

            return new EffectExecutionResult(
                context.EffectIndex,
                target.InstanceId,
                EffectExecutionOutcome.Success);
        }
    }

    private sealed class ThrowingCustomEffectHandler : ICustomEffectHandler
    {
        public EffectExecutionResult Execute(CustomEffectDefinition effect, EffectExecutionContext context) =>
            throw new InvalidOperationException("Custom effect failed deliberately.");
    }

    private sealed class ConstantCustomConditionHandler(bool value) : ICustomConditionHandler
    {
        public bool Evaluate(CustomConditionDefinition condition, BattleConditionContext context) => value;
    }
}
