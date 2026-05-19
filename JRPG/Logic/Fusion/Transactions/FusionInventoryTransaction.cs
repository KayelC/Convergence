using JRPGPrototype.Entities;

namespace JRPGPrototype.Logic.Fusion
{
    public static class FusionInventoryTransaction
    {
        public static void ConsumeDemon(FusionContext context, Combatant demon)
        {
            if (context.Party.ActiveParty.Contains(demon))
            {
                context.Party.ReturnDemon(context.Owner, demon);
            }

            context.Owner.DemonStock.Remove(demon);
        }

        public static void ConsumePersona(Combatant owner, Persona persona)
        {
            if (owner.ActivePersona == persona)
            {
                owner.ActivePersona = null;
            }

            owner.PersonaStock.Remove(persona);
        }

        public static void ReplaceDemon(FusionContext context, Combatant oldDemon, Combatant newDemon)
        {
            newDemon.OwnerId = oldDemon.OwnerId;
            newDemon.Controller = oldDemon.Controller;
            newDemon.BattleControl = oldDemon.BattleControl;

            int activeIndex = context.Party.ActiveParty.IndexOf(oldDemon);
            if (activeIndex != -1)
            {
                context.Party.ActiveParty[activeIndex] = newDemon;
                newDemon.PartySlot = activeIndex;
                oldDemon.PartySlot = -1;
            }

            int stockIndex = context.Owner.DemonStock.IndexOf(oldDemon);
            if (stockIndex != -1)
            {
                context.Owner.DemonStock[stockIndex] = newDemon;
            }
            else if (activeIndex == -1)
            {
                context.Owner.DemonStock.Add(newDemon);
            }

            context.Owner.RecalculateResources();
        }

        public static void ReplacePersona(Combatant owner, Persona oldPersona, Persona newPersona)
        {
            if (owner.ActivePersona == oldPersona)
            {
                owner.ActivePersona = newPersona;
            }
            else
            {
                owner.PersonaStock.Remove(oldPersona);
                owner.PersonaStock.Add(newPersona);
            }

            owner.RecalculateResources();
        }
    }
}
