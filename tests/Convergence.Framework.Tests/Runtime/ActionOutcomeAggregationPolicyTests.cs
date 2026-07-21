using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class ActionOutcomeAggregationPolicyTests
{
    private static readonly ContentId Action = ContentId.Parse("test_action");
    private static readonly RuntimeInstanceId Actor = RuntimeInstanceId.Parse("actor");
    private static readonly RuntimeInstanceId FirstTarget = RuntimeInstanceId.Parse("first_target");
    private static readonly RuntimeInstanceId SecondTarget = RuntimeInstanceId.Parse("second_target");
    private readonly StandardActionOutcomeAggregationPolicy _policy = new();

    [Theory]
    [InlineData(TurnEconomyOutcome.Normal, false, false)]
    [InlineData(TurnEconomyOutcome.Weakness, false, false)]
    [InlineData(TurnEconomyOutcome.Critical, true, false)]
    [InlineData(TurnEconomyOutcome.Miss, false, false)]
    [InlineData(TurnEconomyOutcome.Null, false, false)]
    [InlineData(TurnEconomyOutcome.Repel, false, true)]
    [InlineData(TurnEconomyOutcome.Absorb, false, true)]
    public void Aggregate_MapsSingleTargetOutcomes(
        TurnEconomyOutcome outcome,
        bool critical,
        bool terminates)
    {
        EffectExecutionResult effect = outcome switch
        {
            TurnEconomyOutcome.Miss => DamageEffect(FirstTarget, outcome, [Hit(FirstTarget, 0, false)]),
            TurnEconomyOutcome.Critical => DamageEffect(FirstTarget, outcome, [Hit(FirstTarget, 0, true, true)]),
            _ => DamageEffect(FirstTarget, outcome, [Hit(FirstTarget, 0, true)])
        };

        TurnEconomyResolution result = _policy.Aggregate([effect]);

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(critical, result.AnyCritical);
        Assert.Equal(terminates, result.TerminatesPhase);
    }

    [Fact]
    public void Aggregate_CriticalAndEvadedTargetsNormalizeToNormalCost()
    {
        EffectExecutionResult critical = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Critical,
            [Hit(FirstTarget, 0, true, true)]);
        EffectExecutionResult evaded = DamageEffect(
            SecondTarget,
            TurnEconomyOutcome.Miss,
            [Hit(SecondTarget, 0, false), Hit(SecondTarget, 1, false)]);

        TurnEconomyResolution result = _policy.Aggregate([critical, evaded]);

        Assert.Equal(TurnEconomyOutcome.Normal, result.Outcome);
        Assert.True(result.AnyCritical);
        Assert.False(result.TerminatesPhase);
    }

    [Fact]
    public void Aggregate_PartialHitTargetIsNotAnEvasionAndRepeatedMissesDoNotStack()
    {
        EffectExecutionResult partialHit = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Critical,
            [Hit(FirstTarget, 0, false), Hit(FirstTarget, 1, true, true)]);
        EffectExecutionResult firstMiss = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Miss,
            [Hit(FirstTarget, 0, false)]);
        EffectExecutionResult secondMiss = DamageEffect(
            SecondTarget,
            TurnEconomyOutcome.Miss,
            [Hit(SecondTarget, 0, false)]);

        Assert.Equal(
            TurnEconomyOutcome.Critical,
            _policy.Aggregate([partialHit]).Outcome);
        Assert.Equal(
            TurnEconomyOutcome.Miss,
            _policy.Aggregate([firstMiss, secondMiss]).Outcome);
    }

    [Theory]
    [InlineData(TurnEconomyOutcome.Normal, false)]
    [InlineData(TurnEconomyOutcome.Weakness, false)]
    [InlineData(TurnEconomyOutcome.Critical, true)]
    public void Aggregate_GroupsDamageHitsForTheSameTargetAcrossEffects(
        TurnEconomyOutcome landedOutcome,
        bool critical)
    {
        EffectExecutionResult missedEffect = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Miss,
            [Hit(FirstTarget, 0, false)]);
        EffectExecutionResult landedEffect = DamageEffect(
            FirstTarget,
            landedOutcome,
            [Hit(FirstTarget, 0, true, critical)]);

        TurnEconomyResolution result = _policy.Aggregate([missedEffect, landedEffect]);

        Assert.Equal(landedOutcome, result.Outcome);
        Assert.Equal(critical, result.AnyCritical);
        Assert.False(result.TerminatesPhase);
    }

    [Fact]
    public void Aggregate_GroupsRepeatedMissesForTheSameTargetAcrossEffects()
    {
        EffectExecutionResult firstMiss = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Miss,
            [Hit(FirstTarget, 0, false)]);
        EffectExecutionResult secondMiss = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Miss,
            [Hit(FirstTarget, 0, false)]);

        TurnEconomyResolution result = _policy.Aggregate([firstMiss, secondMiss]);

        Assert.Equal(TurnEconomyOutcome.Miss, result.Outcome);
        Assert.False(result.AnyCritical);
        Assert.False(result.TerminatesPhase);
    }

    [Fact]
    public void Aggregate_PreservesEvasionForADifferentTargetAcrossEffects()
    {
        EffectExecutionResult landed = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Normal,
            [Hit(FirstTarget, 0, true)]);
        EffectExecutionResult missed = DamageEffect(
            SecondTarget,
            TurnEconomyOutcome.Miss,
            [Hit(SecondTarget, 0, false)]);

        TurnEconomyResolution result = _policy.Aggregate([landed, missed]);

        Assert.Equal(TurnEconomyOutcome.Miss, result.Outcome);
    }

    [Fact]
    public void Aggregate_NullPenaltyAndPhaseTerminationTakePrecedence()
    {
        EffectExecutionResult critical = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Critical,
            [Hit(FirstTarget, 0, true, true)]);
        EffectExecutionResult nullified = DamageEffect(
            SecondTarget,
            TurnEconomyOutcome.Null,
            [Hit(SecondTarget, 0, true)]);
        EffectExecutionResult absorbed = DamageEffect(
            SecondTarget,
            TurnEconomyOutcome.Absorb,
            [Hit(SecondTarget, 0, true)]);

        Assert.Equal(
            TurnEconomyOutcome.Null,
            _policy.Aggregate([critical, nullified]).Outcome);
        TurnEconomyResolution termination = _policy.Aggregate([critical, absorbed]);
        Assert.Equal(TurnEconomyOutcome.Absorb, termination.Outcome);
        Assert.True(termination.TerminatesPhase);
    }

    [Fact]
    public void Aggregate_PreservesCustomEffectMissCompatibilityWithoutDamageEvidence()
    {
        var customMiss = new EffectExecutionResult(
            0,
            FirstTarget,
            EffectExecutionOutcome.Failure,
            TurnEconomyOutcome.Miss);

        Assert.Equal(TurnEconomyOutcome.Miss, _policy.Aggregate([customMiss]).Outcome);
    }

    [Fact]
    public void Aggregate_DoesNotInferCommittedCriticalFromEvidenceAlone()
    {
        var skippedCriticalEvidence = new EffectExecutionResult(
            0,
            FirstTarget,
            EffectExecutionOutcome.Success,
            TurnEconomyOutcome.Normal,
            IsCritical: false,
            DamageHits: [Hit(FirstTarget, 0, true, critical: true)]);

        TurnEconomyResolution result = _policy.Aggregate([skippedCriticalEvidence]);

        Assert.Equal(TurnEconomyOutcome.Normal, result.Outcome);
        Assert.False(result.AnyCritical);
    }

    [Theory]
    [InlineData(TurnEconomyOutcome.Normal)]
    [InlineData(TurnEconomyOutcome.Weakness)]
    [InlineData(TurnEconomyOutcome.Critical)]
    [InlineData(TurnEconomyOutcome.Miss)]
    [InlineData(TurnEconomyOutcome.Null)]
    [InlineData(TurnEconomyOutcome.Repel)]
    [InlineData(TurnEconomyOutcome.Absorb)]
    public void Aggregate_DefaultItemBehaviorSpendsOneNormalTurnWithoutRewritingEffects(
        TurnEconomyOutcome effectOutcome)
    {
        EffectExecutionResult effect = effectOutcome switch
        {
            TurnEconomyOutcome.Miss =>
                DamageEffect(FirstTarget, effectOutcome, [Hit(FirstTarget, 0, false)]),
            TurnEconomyOutcome.Critical =>
                DamageEffect(FirstTarget, effectOutcome, [Hit(FirstTarget, 0, true, true)]),
            _ => DamageEffect(FirstTarget, effectOutcome, [Hit(FirstTarget, 0, true)])
        };

        TurnEconomyResolution result = _policy.Aggregate(new ActionOutcomeAggregationRequest(
            ActionOutcomeSourceKind.Item,
            [effect]));

        Assert.Equal(TurnEconomyOutcome.Normal, result.Outcome);
        Assert.False(result.AnyCritical);
        Assert.False(result.TerminatesPhase);
        Assert.Equal(effectOutcome, effect.TurnEconomyOutcome);
    }

    [Fact]
    public void Aggregate_EffectDrivenItemsAndOffensiveSourcesRetainTypedOutcomes()
    {
        EffectExecutionResult weakness = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Weakness,
            [Hit(FirstTarget, 0, true)]);
        var effectDriven = new StandardActionOutcomeAggregationPolicy(
            new StandardActionOutcomeAggregationPolicyConfig(
                ItemActionOutcomeBehavior.EffectDriven));

        TurnEconomyResolution item = effectDriven.Aggregate(new ActionOutcomeAggregationRequest(
            ActionOutcomeSourceKind.Item,
            [weakness]));
        TurnEconomyResolution skill = _policy.Aggregate(new ActionOutcomeAggregationRequest(
            ActionOutcomeSourceKind.Skill,
            [weakness]));
        TurnEconomyResolution basicAttack = _policy.Aggregate(new ActionOutcomeAggregationRequest(
            ActionOutcomeSourceKind.BasicAttack,
            [weakness]));

        Assert.Equal(TurnEconomyOutcome.Weakness, item.Outcome);
        Assert.Equal(TurnEconomyOutcome.Weakness, skill.Outcome);
        Assert.Equal(TurnEconomyOutcome.Weakness, basicAttack.Outcome);
    }

    [Fact]
    public void Request_SnapshotsEffectsAndRejectsUndefinedSourceKinds()
    {
        var effects = new List<EffectExecutionResult>
        {
            DamageEffect(
                FirstTarget,
                TurnEconomyOutcome.Normal,
                [Hit(FirstTarget, 0, true)])
        };
        var request = new ActionOutcomeAggregationRequest(ActionOutcomeSourceKind.Item, effects);

        effects.Clear();

        Assert.Single(request.Effects);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EffectExecutionResult>)request.Effects).Clear());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActionOutcomeAggregationRequest((ActionOutcomeSourceKind)99, []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StandardActionOutcomeAggregationPolicyConfig((ItemActionOutcomeBehavior)99));
    }

    [Fact]
    public void SourceAwareDispatch_PreservesExistingCustomPolicyImplementations()
    {
        var legacyPolicy = new LegacyActionOutcomePolicy();
        EffectExecutionResult weakness = DamageEffect(
            FirstTarget,
            TurnEconomyOutcome.Weakness,
            [Hit(FirstTarget, 0, true)]);

        TurnEconomyResolution result = ((IActionOutcomeAggregationPolicy)legacyPolicy).Aggregate(
            new ActionOutcomeAggregationRequest(
                ActionOutcomeSourceKind.Item,
                [weakness]));

        Assert.Equal(1, legacyPolicy.CallCount);
        Assert.Equal(TurnEconomyOutcome.Weakness, result.Outcome);
    }

    private static EffectExecutionResult DamageEffect(
        RuntimeInstanceId targetId,
        TurnEconomyOutcome outcome,
        IReadOnlyList<DamageHitExecutionEvidence> hits) =>
        new(
            0,
            targetId,
            outcome is TurnEconomyOutcome.Repel or TurnEconomyOutcome.Absorb
                ? EffectExecutionOutcome.Interrupted
                : outcome is TurnEconomyOutcome.Miss or TurnEconomyOutcome.Null
                    ? EffectExecutionOutcome.Failure
                    : EffectExecutionOutcome.Success,
            outcome,
            IsCritical: hits.Any(hit => hit.Critical),
            DamageHits: hits);

    private static DamageHitExecutionEvidence Hit(
        RuntimeInstanceId targetId,
        int hitIndex,
        bool landed,
        bool critical = false) =>
        new(
            Action,
            Actor,
            targetId,
            effectIndex: 0,
            new DamageHitResolution(
                hitIndex,
                landed,
                landed ? 10m : 0m,
                critical,
                authoredAccuracy: 90,
                finalAccuracy: 80,
                accuracyRoll: landed ? 10m : 90m,
                criticalEligible: landed ? true : null,
                criticalEligibilityReason: landed
                    ? Convergence.Battle.CriticalEligibilityReason.Eligible
                    : null,
                criticalChance: landed ? 20 : null,
                criticalRoll: landed ? critical ? 5m : 50m : null,
                ElementalAffinity.Normal,
                chargeKind: null,
                chargeMultiplier: 1m),
            ElementalAffinity.Normal);

    private sealed class LegacyActionOutcomePolicy : IActionOutcomeAggregationPolicy
    {
        public int CallCount { get; private set; }

        public TurnEconomyResolution Aggregate(IReadOnlyList<EffectExecutionResult> effects)
        {
            CallCount++;
            return new TurnEconomyResolution(
                effects[0].TurnEconomyOutcome,
                effects[0].IsCritical,
                effects[0].TurnEconomyOutcome is TurnEconomyOutcome.Repel or TurnEconomyOutcome.Absorb);
        }
    }
}
