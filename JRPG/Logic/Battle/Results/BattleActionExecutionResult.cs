using System;
using System.Collections.Generic;
using JRPGPrototype.Core;

namespace JRPGPrototype.Logic.Battle.Results
{
    public enum BattleActionExecutionKind
    {
        Executed,
        Rejected,
        Escaped
    }

    public sealed record BattleActionExecutionResult(
        BattleActionExecutionKind Kind,
        IReadOnlyList<CombatResult> CombatResults)
    {
        public static BattleActionExecutionResult Rejected()
            => new BattleActionExecutionResult(BattleActionExecutionKind.Rejected, Array.Empty<CombatResult>());

        public static BattleActionExecutionResult Escaped()
            => new BattleActionExecutionResult(BattleActionExecutionKind.Escaped, Array.Empty<CombatResult>());

        public static BattleActionExecutionResult Executed(IReadOnlyList<CombatResult> combatResults)
            => new BattleActionExecutionResult(BattleActionExecutionKind.Executed, combatResults);
    }
}
