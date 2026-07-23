using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Execution;

/// <summary>Identifies whether an ailment may proceed to resistance and chance resolution.</summary>
public enum BattleAilmentApplicationGateOutcome
{
    Allowed,
    Blocked
}

/// <summary>Provides a stable reason for an ailment application gate decision.</summary>
public enum BattleAilmentApplicationGateReason
{
    None,
    Guarding,
    PolicyRejected
}

/// <summary>Immutable input supplied to an ailment application gate policy.</summary>
public sealed record BattleAilmentApplicationGateRequest
{
    public BattleAilmentApplicationGateRequest(
        RuntimeActorState actor,
        RuntimeActorState target,
        AilmentDefinition ailment,
        IEnumerable<RuntimeActorState> participants,
        ContentId? sourceId = null)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Ailment = ailment ?? throw new ArgumentNullException(nameof(ailment));
        RuntimeActorState[] snapshot = (participants ?? throw new ArgumentNullException(nameof(participants)))
            .ToArray();
        if (snapshot.Any(participant => participant is null))
        {
            throw new ArgumentException("Ailment gate participants cannot contain null actors.", nameof(participants));
        }
        if (sourceId is ContentId suppliedSourceId && !suppliedSourceId.IsValid)
        {
            throw new ArgumentException("Ailment gate source ID must be valid when supplied.", nameof(sourceId));
        }

        Participants = Array.AsReadOnly(snapshot);
        SourceId = sourceId;
    }

    public RuntimeActorState Actor { get; }
    public RuntimeActorState Target { get; }
    public AilmentDefinition Ailment { get; }
    public IReadOnlyList<RuntimeActorState> Participants { get; }
    public ContentId? SourceId { get; }
}

/// <summary>Typed decision returned by an ailment application gate.</summary>
public sealed record BattleAilmentApplicationGateDecision
{
    public BattleAilmentApplicationGateDecision(
        BattleAilmentApplicationGateOutcome outcome,
        BattleAilmentApplicationGateReason reason = BattleAilmentApplicationGateReason.None)
    {
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }
        if ((outcome == BattleAilmentApplicationGateOutcome.Allowed) !=
            (reason == BattleAilmentApplicationGateReason.None))
        {
            throw new ArgumentException(
                "Allowed gate decisions require no rejection reason; blocked decisions require one.",
                nameof(reason));
        }

        Outcome = outcome;
        Reason = reason;
    }

    public BattleAilmentApplicationGateOutcome Outcome { get; }
    public BattleAilmentApplicationGateReason Reason { get; }
    public bool Allowed => Outcome == BattleAilmentApplicationGateOutcome.Allowed;

    public static BattleAilmentApplicationGateDecision Allow { get; } =
        new(BattleAilmentApplicationGateOutcome.Allowed);

    public static BattleAilmentApplicationGateDecision GuardBlocked { get; } =
        new(BattleAilmentApplicationGateOutcome.Blocked, BattleAilmentApplicationGateReason.Guarding);
}

/// <summary>Decides whether an ailment may proceed before resistance and chance resolution.</summary>
public interface IBattleAilmentApplicationGatePolicy
{
    BattleAilmentApplicationGateDecision Evaluate(BattleAilmentApplicationGateRequest request);
}

/// <summary>Supplied policy that blocks ailments while their target is guarding.</summary>
public sealed class GuardBlocksAilmentsApplicationGatePolicy : IBattleAilmentApplicationGatePolicy
{
    public static GuardBlocksAilmentsApplicationGatePolicy Instance { get; } = new();

    private GuardBlocksAilmentsApplicationGatePolicy()
    {
    }

    public BattleAilmentApplicationGateDecision Evaluate(BattleAilmentApplicationGateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Target.IsGuarding
            ? BattleAilmentApplicationGateDecision.GuardBlocked
            : BattleAilmentApplicationGateDecision.Allow;
    }
}

/// <summary>Supplied policy for games where guarding does not block ailments.</summary>
public sealed class AllowAilmentsApplicationGatePolicy : IBattleAilmentApplicationGatePolicy
{
    public static AllowAilmentsApplicationGatePolicy Instance { get; } = new();

    private AllowAilmentsApplicationGatePolicy()
    {
    }

    public BattleAilmentApplicationGateDecision Evaluate(BattleAilmentApplicationGateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BattleAilmentApplicationGateDecision.Allow;
    }
}
