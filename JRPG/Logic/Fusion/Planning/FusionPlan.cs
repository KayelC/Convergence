using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// Calculated ritual state after the player has selected materials, but before they commit.
    /// The conductor can pass this through bridge confirmation and mutator execution without
    /// rebuilding inheritance pools or preview baselines in multiple places.
    /// </summary>
    public sealed class FusionPlan
    {
        public FusionOperationType Operation { get; }
        public string TargetId { get; }
        public bool IsAccident { get; }
        public FusionParticipant FirstParent { get; }
        public FusionParticipant SecondParent { get; }
        public FusionParticipant? Sacrifice { get; }
        public IReadOnlyList<Combatant> CombatantMaterials { get; }
        public IReadOnlyList<string> InherentSkills { get; }
        public IReadOnlyList<string> PickableSkills { get; }
        public IReadOnlyList<string> ExclusiveSkills { get; }
        public IReadOnlyList<string> DisplaySkills { get; }
        public int MaxInheritanceSlots { get; }
        public Combatant PreviewBaseline { get; }

        public FusionPlan(
            FusionOperationType operation,
            string targetId,
            bool isAccident,
            FusionParticipant firstParent,
            FusionParticipant secondParent,
            FusionParticipant? sacrifice,
            IReadOnlyList<Combatant> combatantMaterials,
            IReadOnlyList<string> inherentSkills,
            IReadOnlyList<string> pickableSkills,
            IReadOnlyList<string> exclusiveSkills,
            IReadOnlyList<string> displaySkills,
            int maxInheritanceSlots,
            Combatant previewBaseline)
        {
            Operation = operation;
            TargetId = targetId;
            IsAccident = isAccident;
            FirstParent = firstParent;
            SecondParent = secondParent;
            Sacrifice = sacrifice;
            CombatantMaterials = combatantMaterials;
            InherentSkills = inherentSkills;
            PickableSkills = pickableSkills;
            ExclusiveSkills = exclusiveSkills;
            DisplaySkills = displaySkills;
            MaxInheritanceSlots = maxInheritanceSlots;
            PreviewBaseline = previewBaseline;
        }
    }
}
