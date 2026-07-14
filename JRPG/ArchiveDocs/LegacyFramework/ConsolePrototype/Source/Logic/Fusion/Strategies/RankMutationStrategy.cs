using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using System;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion.Strategies
{
    /// <summary>
    /// Strategy for Tier-based mutations using Elemental participants.
    /// Preserves existing stat modifiers and handles sacrificial EXP bonuses.
    /// </summary>
    public class RankMutationStrategy : IFusionStrategy
    {
        public void Execute(FusionContext context)
        {
            if (context.Owner.Class == ClassType.Operator)
            {
                // Identify the non-elemental target undergoing the rank change
                Combatant original = (Combatant)context.Materials.First(m =>
                    ((Combatant)m).ActivePersona.Race != "Element");

                // Handle Operator-class Sacrifice (Combatant)
                if (context.Sacrifice is Combatant sacrificialCom)
                {
                    FusionInventoryTransaction.ConsumeDemon(context, sacrificialCom);
                }

                // Create the new higher/lower tier demon combatant
                Combatant newD = CombatantFactory.CreatePlayerDemon(context.ResultId,
                    Database.Personas[context.ResultId.ToLower()].Level);

                // Rule - Inherit the exact chosen skill set from the sequence
                newD.ExtraSkills.Clear();
                newD.ExtraSkills.AddRange(context.ChosenSkills);
                newD.ExtraSkills = newD.ExtraSkills.Distinct().ToList();

                // Carry over custom stat modifiers from the original form to the new form
                foreach (var mod in original.ActivePersona.StatModifiers)
                {
                    newD.ActivePersona.StatModifiers[mod.Key] = mod.Value;
                }

                // Apply Transfer formula (Earned XP / 1.5)
                if (context.Sacrifice is Combatant offer)
                {
                    int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
                    newD.GainExp(transferXP);
                }

                context.Messenger.Publish($"{original.Name} has transformed into {newD.Name}!", ConsoleColor.Magenta);

                // Atomic replacement in party/stock
                ReplaceDemon(context, original, newD);
            }
            else if (context.Owner.Class == ClassType.WildCard)
            {
                // WildCards handle spiritual transition (Persona masks only)
                Persona original = (Persona)context.Materials.First(m => ((Persona)m).Race != "Element");

                // Handle WildCard-class Sacrifice (Persona mask)
                if (context.Sacrifice is Persona sacrificialPersona)
                {
                    FusionInventoryTransaction.ConsumePersona(context.Owner, sacrificialPersona);
                }

                Persona newP = Database.Personas[context.ResultId.ToLower()].ToPersona();

                // Inherit state and skills
                newP.SkillSet.Clear();
                newP.SkillSet.AddRange(context.ChosenSkills);
                newP.SkillSet = newP.SkillSet.Distinct().ToList();

                // Carry over modifiers
                foreach (var mod in original.StatModifiers) newP.StatModifiers[mod.Key] = mod.Value;

                // Apply Transfer formula (Earned XP / 1.5)
                if (context.Sacrifice is Persona offer)
                {
                    int transferXP = (int)(offer.LifetimeEarnedExp / 1.5);
                    newP.GainExp(transferXP);
                }

                context.Messenger.Publish($"{original.Name} has transformed into {newP.Name}!", ConsoleColor.Magenta);
                ReplacePersona(context, original, newP);
            }
        }

        private void ReplaceDemon(FusionContext context, Combatant oldD, Combatant newD)
        {
            FusionInventoryTransaction.ReplaceDemon(context, oldD, newD);
        }

        private void ReplacePersona(FusionContext context, Persona oldP, Persona newP)
        {
            FusionInventoryTransaction.ReplacePersona(context.Owner, oldP, newP);
        }
    }
}
