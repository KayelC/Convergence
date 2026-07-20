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
}
