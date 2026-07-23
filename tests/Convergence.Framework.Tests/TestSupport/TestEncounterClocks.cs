using Convergence.Content;
using Convergence.Encounters;

namespace Convergence.Framework.Tests.TestSupport;

internal static class TestEncounterClocks
{
    public static ExplicitBattleEncounterLifecycleClockPolicy Standard(
        ContentId? playerTeam = null,
        ContentId? enemyTeam = null) =>
        new(
            [
                new BattleTeamPhaseClockDefinition(
                    playerTeam ?? ContentId.Parse("player_team"),
                    ContentId.Parse("player_phase"),
                    ContentId.Parse("player_phase_end")),
                new BattleTeamPhaseClockDefinition(
                    enemyTeam ?? ContentId.Parse("enemy_team"),
                    ContentId.Parse("enemy_phase"),
                    ContentId.Parse("enemy_phase_end"))
            ],
            ContentId.Parse("round_end"));
}
