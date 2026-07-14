using JRPGPrototype.Logic.Battle.Engines;

namespace JRPGPrototype.Logic.Battle.Bridges;

/// <summary>
/// Console-only rendering for the optional Press Turn economy.
/// </summary>
internal static class PressTurnIconFormatter
{
    public static string Format(PressTurnEngine economy)
    {
        ArgumentNullException.ThrowIfNull(economy);
        if (!economy.HasTurnsRemaining())
        {
            return "[EMPTY]";
        }

        return string.Join(
            " ",
            Enumerable.Repeat("[O]", economy.FullIcons)
                .Concat(Enumerable.Repeat("[X]", economy.BlinkingIcons)));
    }
}
