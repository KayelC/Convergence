using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JRPGPrototype.Logic.Fusion
{
    public sealed class FusionPlanFactory
    {
        private readonly FusionCalculator _calculator;

        public FusionPlanFactory(FusionCalculator calculator)
        {
            _calculator = calculator;
        }

        public bool TryCreate(
            FusionParticipant firstParent,
            FusionParticipant secondParent,
            FusionParticipant? sacrifice,
            bool isSacrificial,
            int moonPhase,
            out FusionPlan? plan)
        {
            plan = null;

            var (operation, targetId, isAccident) = _calculator.CalculateResult(
                firstParent.CombatantView,
                secondParent.CombatantView,
                moonPhase);

            if (operation == FusionOperationType.NoFusionPossible || string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            List<Combatant> combatantMaterials = new List<Combatant>
            {
                firstParent.CombatantView,
                secondParent.CombatantView
            };

            if (sacrifice != null)
            {
                combatantMaterials.Add(sacrifice.CombatantView);
            }

            List<string> inherentSkills = GetInherentSkills(operation, targetId, firstParent.CombatantView, secondParent.CombatantView);
            List<string> pickableSkills = _calculator.GetInheritableSkills(combatantMaterials.ToArray());
            List<string> exclusiveSkills = _calculator.GetExclusiveSkills(combatantMaterials.ToArray());
            List<string> displaySkills = pickableSkills.Union(exclusiveSkills).ToList();
            int maxSlots = Math.Min(8, _calculator.GetInheritanceSlotCount(combatantMaterials.ToArray()) + (isSacrificial ? 2 : 0));
            Combatant previewBaseline = operation == FusionOperationType.StatBoostFusion
                ? FusionPreviewFactory.GetStatBoostTarget(firstParent.CombatantView, secondParent.CombatantView)
                : (firstParent.Race != "Element" ? firstParent.CombatantView : secondParent.CombatantView);

            plan = new FusionPlan(
                operation,
                targetId,
                isAccident,
                firstParent,
                secondParent,
                sacrifice,
                combatantMaterials,
                inherentSkills,
                pickableSkills,
                exclusiveSkills,
                displaySkills,
                maxSlots,
                previewBaseline);

            return true;
        }

        private static List<string> GetInherentSkills(FusionOperationType operation, string targetId, Combatant parentA, Combatant parentB)
        {
            if (operation == FusionOperationType.StatBoostFusion)
            {
                return FusionPreviewFactory.GetStatBoostTarget(parentA, parentB).GetConsolidatedSkills();
            }

            if (operation == FusionOperationType.CreateNewDemon ||
                operation == FusionOperationType.RankUpParent ||
                operation == FusionOperationType.RankDownParent)
            {
                return Database.Personas.TryGetValue(targetId.ToLower(), out PersonaData? resultTemplate)
                    ? resultTemplate.BaseSkills
                    : new List<string>();
            }

            return new List<string>();
        }
    }
}
