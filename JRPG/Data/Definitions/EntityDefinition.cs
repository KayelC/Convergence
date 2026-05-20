using System.Collections.Generic;
using JRPGPrototype.Core;

namespace JRPGPrototype.Data.Definitions
{
    public sealed record EntityDefinition(
        string Id,
        string DisplayName,
        string Race,
        int Rank,
        int Level,
        string InheritanceType,
        IReadOnlyDictionary<StatType, int> Stats,
        IReadOnlyDictionary<Element, Affinity> Affinities,
        IReadOnlyList<string> BaseSkillIds,
        IReadOnlyDictionary<int, string> LearnedSkillIds);
}
