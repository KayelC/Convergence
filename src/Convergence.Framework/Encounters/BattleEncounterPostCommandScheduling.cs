using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

/// <summary>Identifies how an extension influences scheduling after an accepted command.</summary>
public enum BattleEncounterPostCommandScheduleDecisionKind
{
    FollowScheduler = 0,
    RetainActor = 1
}

/// <summary>Immutable scheduling decision returned by a post-command extension.</summary>
public sealed class BattleEncounterPostCommandScheduleDecision
{
    private BattleEncounterPostCommandScheduleDecision(
        BattleEncounterPostCommandScheduleDecisionKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
    }

    public BattleEncounterPostCommandScheduleDecisionKind Kind { get; }

    public static BattleEncounterPostCommandScheduleDecision FollowScheduler() =>
        new(BattleEncounterPostCommandScheduleDecisionKind.FollowScheduler);

    public static BattleEncounterPostCommandScheduleDecision RetainActor() =>
        new(BattleEncounterPostCommandScheduleDecisionKind.RetainActor);
}

/// <summary>
/// Presents accepted command and turn-economy evidence without granting
/// mutation access to either state.
/// </summary>
public sealed class BattleEncounterPostCommandScheduleRequest
{
    public BattleEncounterPostCommandScheduleRequest(
        RuntimeInstanceId actorId,
        ContentId teamId,
        ActionTurnConsumption turnConsumption,
        BattleTurnEconomySnapshot economyBefore,
        BattleTurnEconomySnapshot economyAfter,
        bool hasRemainingOpportunities,
        int consecutiveImmediateRepeats)
    {
        if (!actorId.IsValid)
        {
            throw new ArgumentException("Post-command actor ID must be valid.", nameof(actorId));
        }

        if (!teamId.IsValid)
        {
            throw new ArgumentException("Post-command team ID must be valid.", nameof(teamId));
        }

        if (consecutiveImmediateRepeats < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(consecutiveImmediateRepeats),
                "Immediate repeat count cannot be negative.");
        }

        ActorId = actorId;
        TeamId = teamId;
        TurnConsumption = turnConsumption ?? throw new ArgumentNullException(nameof(turnConsumption));
        EconomyBefore = economyBefore ?? throw new ArgumentNullException(nameof(economyBefore));
        EconomyAfter = economyAfter ?? throw new ArgumentNullException(nameof(economyAfter));
        HasRemainingOpportunities = hasRemainingOpportunities;
        ConsecutiveImmediateRepeats = consecutiveImmediateRepeats;
    }

    public RuntimeInstanceId ActorId { get; }
    public ContentId TeamId { get; }
    public ActionTurnConsumption TurnConsumption { get; }
    public BattleTurnEconomySnapshot EconomyBefore { get; }
    public BattleTurnEconomySnapshot EconomyAfter { get; }
    public bool HasRemainingOpportunities { get; }
    public int ConsecutiveImmediateRepeats { get; }
}

/// <summary>
/// May choose the recipient of an already-existing post-command opportunity.
/// It cannot execute an action or alter turn-economy state.
/// </summary>
public interface IBattleEncounterPostCommandSchedulePolicy
{
    ContentId PolicyId { get; }

    BattleEncounterPostCommandScheduleDecision Decide(
        BattleEncounterPostCommandScheduleRequest request);
}

/// <summary>Configures an optional and finitely bounded post-command scheduling extension.</summary>
public sealed class BattleEncounterPostCommandScheduleExtension
{
    public BattleEncounterPostCommandScheduleExtension(
        IBattleEncounterPostCommandSchedulePolicy policy,
        int maximumConsecutiveImmediateRepeats)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (maximumConsecutiveImmediateRepeats <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumConsecutiveImmediateRepeats),
                "The immediate-repeat limit must be positive.");
        }

        MaximumConsecutiveImmediateRepeats = maximumConsecutiveImmediateRepeats;
    }

    public IBattleEncounterPostCommandSchedulePolicy Policy { get; }
    public int MaximumConsecutiveImmediateRepeats { get; }
}

internal static class BattleEncounterPostCommandScheduleEvaluator
{
    public static PostCommandScheduleEvaluation Evaluate(
        BattleEncounterPostCommandScheduleExtension? extension,
        BattleEncounterCommandWindowScheduleStep command,
        BattleEncounterScheduleStepOutcome outcome,
        int consecutiveImmediateRepeats)
    {
        if (extension is null ||
            outcome.Kind != BattleEncounterScheduleStepOutcomeKind.CommandCommitted ||
            outcome.HasRemainingOpportunities != true)
        {
            return PostCommandScheduleEvaluation.FollowScheduler();
        }

        if (!extension.Policy.PolicyId.IsValid)
        {
            return PostCommandScheduleEvaluation.Reject(
                BattleEncounterScheduleDiagnosticCode.InvalidPostCommandDecision,
                "The post-command scheduling policy returned an invalid policy ID.");
        }

        var request = new BattleEncounterPostCommandScheduleRequest(
            command.ActorId,
            command.TeamId,
            outcome.TurnConsumption
                ?? throw new InvalidOperationException(
                    "Committed scheduling evidence requires turn consumption."),
            outcome.EconomyBefore
                ?? throw new InvalidOperationException(
                    "Committed scheduling evidence requires before-economy state."),
            outcome.EconomyAfter
                ?? throw new InvalidOperationException(
                    "Committed scheduling evidence requires after-economy state."),
            hasRemainingOpportunities: true,
            consecutiveImmediateRepeats);
        BattleEncounterPostCommandScheduleDecision? decision = extension.Policy.Decide(request);
        if (decision is null || !Enum.IsDefined(decision.Kind))
        {
            return PostCommandScheduleEvaluation.Reject(
                BattleEncounterScheduleDiagnosticCode.InvalidPostCommandDecision,
                $"Post-command scheduling policy '{extension.Policy.PolicyId}' returned " +
                "an invalid decision.");
        }

        if (decision.Kind != BattleEncounterPostCommandScheduleDecisionKind.RetainActor)
        {
            return PostCommandScheduleEvaluation.FollowScheduler();
        }

        if (consecutiveImmediateRepeats >= extension.MaximumConsecutiveImmediateRepeats)
        {
            return PostCommandScheduleEvaluation.Reject(
                BattleEncounterScheduleDiagnosticCode.ImmediateRepeatLimitExceeded,
                $"Post-command scheduling policy '{extension.Policy.PolicyId}' exceeded " +
                $"its limit of {extension.MaximumConsecutiveImmediateRepeats} consecutive " +
                "immediate repeat(s).");
        }

        return PostCommandScheduleEvaluation.RetainCurrentActor();
    }
}

internal sealed class PostCommandScheduleEvaluation
{
    private PostCommandScheduleEvaluation(
        bool retainActor,
        BattleEncounterScheduleDiagnosticCode? rejectionCode,
        string? rejectionMessage)
    {
        RetainActor = retainActor;
        RejectionCode = rejectionCode;
        RejectionMessage = rejectionMessage;
    }

    public bool RetainActor { get; }
    public BattleEncounterScheduleDiagnosticCode? RejectionCode { get; }
    public string? RejectionMessage { get; }
    public bool IsRejected => RejectionCode.HasValue;

    public static PostCommandScheduleEvaluation FollowScheduler() =>
        new(retainActor: false, null, null);

    public static PostCommandScheduleEvaluation RetainCurrentActor() =>
        new(retainActor: true, null, null);

    public static PostCommandScheduleEvaluation Reject(
        BattleEncounterScheduleDiagnosticCode code,
        string message) =>
        new(retainActor: false, code, message);
}
