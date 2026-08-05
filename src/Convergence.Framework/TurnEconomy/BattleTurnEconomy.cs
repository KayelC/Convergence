using Convergence.Content;

namespace Convergence.Execution;

/// <summary>
/// Immutable state exposed by a turn-economy implementation. Hosts may use a
/// concrete subtype for presentation, while encounter orchestration relies
/// only on the generic remaining-action count.
/// </summary>
public abstract record BattleTurnEconomySnapshot
{
    protected BattleTurnEconomySnapshot(ContentId economyId, int remainingActions)
    {
        if (!economyId.IsValid)
        {
            throw new ArgumentException("Turn-economy ID must be valid.", nameof(economyId));
        }

        if (remainingActions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingActions));
        }

        EconomyId = economyId;
        RemainingActions = remainingActions;
    }

    public ContentId EconomyId { get; }
    public int RemainingActions { get; }
}

public sealed record StandardActionTurnEconomySnapshot : BattleTurnEconomySnapshot
{
    public StandardActionTurnEconomySnapshot(int remainingActions)
        : base(StandardActionTurnEconomy.EconomyId, remainingActions)
    {
    }
}

/// <summary>
/// Defines how an encounter phase allocates and consumes action opportunities.
/// Implementations are stateful per phase and must return immutable snapshots.
/// </summary>
public interface IBattleTurnEconomy
{
    void StartPhase(int activeActorCount);
    bool HasTurnsRemaining();
    BattleTurnEconomySnapshot CaptureSnapshot();
    void Apply(ActionTurnConsumption consumption);
}

/// <summary>
/// Neutral economy that grants one action to each actor present at phase start.
/// It is available to games that do not opt into a specialized turn system.
/// </summary>
public sealed class StandardActionTurnEconomy : IBattleTurnEconomy
{
    public static ContentId EconomyId { get; } = ContentId.Parse("standard_actions");

    private int _remainingActions;

    public bool HasTurnsRemaining() => _remainingActions > 0;

    public void StartPhase(int activeActorCount)
    {
        if (activeActorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeActorCount));
        }

        _remainingActions = activeActorCount;
    }

    public BattleTurnEconomySnapshot CaptureSnapshot() =>
        new StandardActionTurnEconomySnapshot(_remainingActions);

    public void Apply(ActionTurnConsumption consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        if (!HasTurnsRemaining())
        {
            return;
        }

        switch (consumption.Kind)
        {
            case ActionTurnConsumptionKind.None:
                return;
            case ActionTurnConsumptionKind.TerminatePhase:
                _remainingActions = 0;
                return;
            case ActionTurnConsumptionKind.Normal:
            case ActionTurnConsumptionKind.Pass:
            case ActionTurnConsumptionKind.TurnEconomy:
                _remainingActions--;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(consumption));
        }
    }
}

/// <summary>
/// Mandatory finite limits for one encounter phase. These are safety bounds,
/// not balance rules. <see cref="MaximumCommands"/> retains its pre-release
/// name but counts accepted actor turn windows that reach turn-start lifecycle,
/// whether or not a command handler later runs.
/// </summary>
public sealed record BattlePhaseProgressPolicy
{
    public BattlePhaseProgressPolicy(int maximumCommands, int maximumConsecutiveFreeActions)
    {
        if (maximumCommands <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCommands),
                "Maximum accepted turn windows must be positive.");
        }

        if (maximumConsecutiveFreeActions < 0 || maximumConsecutiveFreeActions >= maximumCommands)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumConsecutiveFreeActions),
                "The free-action limit must be nonnegative and lower than the turn-window limit.");
        }

        MaximumCommands = maximumCommands;
        MaximumConsecutiveFreeActions = maximumConsecutiveFreeActions;
    }

    /// <summary>
    /// Gets the maximum number of actor turn windows that may reach turn-start
    /// lifecycle during one phase.
    /// </summary>
    public int MaximumCommands { get; }

    /// <summary>Gets the maximum accepted consecutive no-cost actions.</summary>
    public int MaximumConsecutiveFreeActions { get; }
}
