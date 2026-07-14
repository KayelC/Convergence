using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;

namespace JRPGPrototype.Logic.Fusion
{
    public static class FusionInventoryTransaction
    {
        public static void ConsumeDemon(FusionContext context, Combatant demon)
        {
            LegacyPartyStockAdapter.Shared.ConsumeDemon(context.Party, context.Owner, demon);
        }

        public static void ConsumePersona(Combatant owner, Persona persona)
        {
            LegacyPartyStockAdapter.Shared.ConsumePersona(owner, persona);
        }

        public static void ReplaceDemon(FusionContext context, Combatant oldDemon, Combatant newDemon)
        {
            newDemon.OwnerId = oldDemon.OwnerId;
            newDemon.Controller = oldDemon.Controller;
            newDemon.BattleControl = oldDemon.BattleControl;

            LegacyPartyStockAdapter.Shared.ReplaceDemon(context.Party, context.Owner, oldDemon, newDemon);

            context.Owner.RecalculateResources();
        }

        public static void ReplacePersona(Combatant owner, Persona oldPersona, Persona newPersona)
        {
            LegacyPartyStockAdapter.Shared.ReplacePersona(owner, oldPersona, newPersona);

            owner.RecalculateResources();
        }
    }
}
