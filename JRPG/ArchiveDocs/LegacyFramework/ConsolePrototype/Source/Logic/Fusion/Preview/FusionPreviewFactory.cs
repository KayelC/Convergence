using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Fusion
{
    public sealed class FusionPreviewFactory
    {
        public Combatant? CreatePreview(FusionPlan plan, IReadOnlyList<string> chosenSkills)
        {
            return CreatePreview(
                plan.Operation,
                plan.TargetId,
                plan.FirstParent,
                plan.SecondParent,
                plan.Sacrifice,
                chosenSkills);
        }

        public Combatant? CreatePreview(
            FusionOperationType operation,
            string targetId,
            FusionParticipant firstParent,
            FusionParticipant secondParent,
            FusionParticipant? sacrifice,
            IReadOnlyList<string> chosenSkills)
        {
            if (!Database.Personas.TryGetValue(targetId.ToLower(), out PersonaData? template))
            {
                return null;
            }

            Combatant parentA = firstParent.CombatantView;
            Combatant parentB = secondParent.CombatantView;
            int previewLevel = operation == FusionOperationType.StatBoostFusion
                ? GetStatBoostTarget(parentA, parentB).Level
                : template.Level;

            Combatant staged = CombatantFactory.CreatePlayerDemon(targetId, previewLevel);
            staged.ExtraSkills.Clear();
            staged.ExtraSkills.AddRange(chosenSkills);

            if (operation == FusionOperationType.StatBoostFusion)
            {
                Combatant target = GetStatBoostTarget(parentA, parentB);
                Combatant mitama = GetStatBoostMitama(parentA, parentB);

                staged.Exp = target.Exp;
                staged.LifetimeEarnedExp = target.LifetimeEarnedExp;
                foreach (var stat in target.CharacterStats) staged.CharacterStats[stat.Key] = stat.Value;
                foreach (var mod in target.ActivePersona!.StatModifiers) staged.ActivePersona!.StatModifiers[mod.Key] = mod.Value;

                ApplyPreviewBoost(staged, mitama.ActivePersona!.Name);
                staged.RecalculateResources();
            }
            else if (operation == FusionOperationType.RankUpParent || operation == FusionOperationType.RankDownParent)
            {
                Combatant original = parentA.ActivePersona!.Race != "Element" ? parentA : parentB;
                foreach (var mod in original.ActivePersona!.StatModifiers) staged.ActivePersona!.StatModifiers[mod.Key] = mod.Value;
                staged.RecalculateResources();
            }

            if (sacrifice != null)
            {
                int transferXP = (int)(sacrifice.CombatantView.LifetimeEarnedExp / 1.5);
                staged.GainExp(transferXP);
            }

            return staged;
        }

        public static Combatant GetStatBoostTarget(Combatant parentA, Combatant parentB)
        {
            return parentA.ActivePersona?.Race == "Mitama" ? parentB : parentA;
        }

        public static Combatant GetStatBoostMitama(Combatant parentA, Combatant parentB)
        {
            return parentA.ActivePersona?.Race == "Mitama" ? parentA : parentB;
        }

        private static void ApplyPreviewBoost(Combatant demon, string mitamaName)
        {
            Dictionary<StatType, int> boosts = new Dictionary<StatType, int>();
            switch (mitamaName)
            {
                case "Ara Mitama": boosts.Add(StatType.St, 2); boosts.Add(StatType.Ag, 1); break;
                case "Nigi Mitama": boosts.Add(StatType.Ma, 2); boosts.Add(StatType.Lu, 1); break;
                case "Kusi Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Ag, 1); break;
                case "Saki Mitama": boosts.Add(StatType.Vi, 2); boosts.Add(StatType.Lu, 1); break;
            }

            foreach (var entry in boosts)
            {
                var mods = demon.ActivePersona!.StatModifiers;
                int current = mods.GetValueOrDefault(entry.Key, 0);
                mods[entry.Key] = System.Math.Min(40, current + entry.Value);
            }
        }
    }
}
