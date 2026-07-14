using Convergence.Content;
using Convergence.Battle;

namespace Convergence.Battle;

public sealed record InstantDeathResistanceResolution(
    InstantDeathResistanceMode Mode,
    InstantDeathChannel? Channel,
    ResistanceLevel? Resistance)
{
    public bool BypassesResistance => Mode == InstantDeathResistanceMode.None;
}

public static class InstantDeathResistanceResolver
{
    public static InstantDeathResistanceResolution Resolve(
        CombatDefenseProfile defenseProfile,
        InstantDeathResistanceCheckDefinition resistanceCheck)
    {
        ArgumentNullException.ThrowIfNull(defenseProfile);
        ArgumentNullException.ThrowIfNull(resistanceCheck);

        return resistanceCheck switch
        {
            ChannelInstantDeathResistanceCheckDefinition channelCheck => new InstantDeathResistanceResolution(
                InstantDeathResistanceMode.Channel,
                channelCheck.Channel,
                defenseProfile.GetInstantDeathResistance(channelCheck.Channel)),
            NoInstantDeathResistanceCheckDefinition => new InstantDeathResistanceResolution(
                InstantDeathResistanceMode.None,
                null,
                null),
            _ => throw new ArgumentOutOfRangeException(
                nameof(resistanceCheck),
                resistanceCheck,
                "Unsupported instant-death resistance check.")
        };
    }
}

public static class AilmentResistanceResolver
{
    public static ResistanceLevel Resolve(CombatDefenseProfile defenseProfile, ContentId ailmentId)
    {
        ArgumentNullException.ThrowIfNull(defenseProfile);
        return defenseProfile.GetAilmentResistance(ailmentId);
    }
}
