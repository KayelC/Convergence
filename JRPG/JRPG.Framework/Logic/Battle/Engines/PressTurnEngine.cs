using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Battle.Execution;

namespace JRPGPrototype.Logic.Battle.Engines;

public sealed record PressTurnEconomySnapshot : BattleTurnEconomySnapshot
{
    public PressTurnEconomySnapshot(int fullIcons, int blinkingIcons)
        : base(PressTurnEngine.EconomyId, CheckedTotal(fullIcons, blinkingIcons))
    {
        FullIcons = fullIcons;
        BlinkingIcons = blinkingIcons;
    }

    public int FullIcons { get; }
    public int BlinkingIcons { get; }

    private static int CheckedTotal(int fullIcons, int blinkingIcons)
    {
        if (fullIcons < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fullIcons));
        }

        if (blinkingIcons < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blinkingIcons));
        }

        return checked(fullIcons + blinkingIcons);
    }
}

/// <summary>
/// Optional turn economy that tracks full and blinking action icons. The
/// encounter runner depends only on <see cref="IBattleTurnEconomy"/>.
/// </summary>
public sealed class PressTurnEngine : IBattleTurnEconomy
{
    public static ContentId EconomyId { get; } = ContentId.Parse("press_turn");

    private int _fullIcons;
    private int _blinkingIcons;

    public int FullIcons => _fullIcons;
    public int BlinkingIcons => _blinkingIcons;
    public bool HasTurnsRemaining() => _fullIcons + _blinkingIcons > 0;

    public void StartPhase(int activeActorCount)
    {
        if (activeActorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeActorCount));
        }

        _fullIcons = activeActorCount;
        _blinkingIcons = 0;
    }

    public int GetTotalIconCount() => _fullIcons + _blinkingIcons;

    public BattleTurnEconomySnapshot CaptureSnapshot() =>
        new PressTurnEconomySnapshot(_fullIcons, _blinkingIcons);

    public void Apply(ActionTurnConsumption consumption)
    {
        ArgumentNullException.ThrowIfNull(consumption);
        switch (consumption.Kind)
        {
            case ActionTurnConsumptionKind.None:
                return;
            case ActionTurnConsumptionKind.Pass:
                Pass();
                return;
            case ActionTurnConsumptionKind.PressTurn when consumption.PressTurn is not null:
                ConsumeAction(consumption.PressTurn);
                return;
            case ActionTurnConsumptionKind.TerminatePhase:
                TerminatePhase();
                return;
            default:
                ConsumeAction(new PressTurnResolution(PressTurnOutcome.Normal, false, false));
                return;
        }
    }

    public void ConsumeAction(PressTurnResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!HasTurnsRemaining())
        {
            return;
        }

        if (resolution.TerminatesPhase ||
            resolution.Outcome is PressTurnOutcome.Repel or PressTurnOutcome.Absorb)
        {
            TerminatePhase();
            return;
        }

        if (resolution.Outcome is PressTurnOutcome.Miss or PressTurnOutcome.Null)
        {
            ConsumeIcons(2);
            return;
        }

        if (resolution.Outcome is PressTurnOutcome.Weakness or PressTurnOutcome.Critical ||
            resolution.AnyCritical)
        {
            if (_fullIcons > 0)
            {
                _fullIcons--;
                _blinkingIcons++;
            }
            else
            {
                _blinkingIcons--;
            }

            return;
        }

        if (_blinkingIcons > 0)
        {
            _blinkingIcons--;
        }
        else
        {
            _fullIcons--;
        }
    }

    public void Pass()
    {
        if (_blinkingIcons > 0)
        {
            _blinkingIcons--;
        }
        else if (_fullIcons > 0)
        {
            _fullIcons--;
            _blinkingIcons++;
        }
    }

    public void TerminatePhase()
    {
        _fullIcons = 0;
        _blinkingIcons = 0;
    }

    private void ConsumeIcons(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_blinkingIcons > 0)
            {
                _blinkingIcons--;
            }
            else if (_fullIcons > 0)
            {
                _fullIcons--;
            }
        }
    }
}
