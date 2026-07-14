namespace JRPGPrototype.Data.Definitions;

public sealed record EntityCapabilitiesDefinition(
    bool Recruitable,
    bool FusionEligible,
    bool CompendiumEligible);

public sealed record InheritanceGroupPolicyDefinition : IEquatable<InheritanceGroupPolicyDefinition>
{
    public InheritanceGroupPolicyDefinition(
        InheritanceGroupPolicyMode mode,
        IEnumerable<InheritanceGroup>? groupIds = null)
    {
        Mode = mode;
        GroupIds = DefinitionCollections.Snapshot(groupIds);
    }

    public InheritanceGroupPolicyMode Mode { get; }
    public IReadOnlyList<InheritanceGroup> GroupIds { get; }

    public bool Equals(InheritanceGroupPolicyDefinition? other)
    {
        return other is not null && Mode == other.Mode && GroupIds.SequenceEqual(other.GroupIds);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mode);
        foreach (InheritanceGroup group in GroupIds)
        {
            hash.Add(group);
        }

        return hash.ToHashCode();
    }
}

public sealed record EntityInheritanceRulesDefinition
{
    public EntityInheritanceRulesDefinition(
        InheritanceGroupPolicyDefinition groupPolicy,
        IEnumerable<ContentId>? blockedSkillIds = null,
        IEnumerable<ContentId>? allowedSkillIds = null)
    {
        GroupPolicy = groupPolicy;
        BlockedSkillIds = DefinitionCollections.Snapshot(blockedSkillIds);
        AllowedSkillIds = DefinitionCollections.Snapshot(allowedSkillIds);
    }

    public InheritanceGroupPolicyDefinition GroupPolicy { get; }
    public IReadOnlyList<ContentId> BlockedSkillIds { get; }
    public IReadOnlyList<ContentId> AllowedSkillIds { get; }
}

public sealed record SkillUnlockDefinition(int Level, ContentId SkillId);

public sealed record EntityDefinition
{
    public EntityDefinition(
        ContentId id,
        string displayName,
        string description,
        ContentId entityKindId,
        ContentId raceId,
        int rank,
        int baseLevel,
        EntityCapabilitiesDefinition capabilities,
        EntityInheritanceRulesDefinition inheritanceRules,
        IEnumerable<KeyValuePair<ContentId, int>> stats,
        IEnumerable<KeyValuePair<DamageElement, ElementalAffinity>>? elementalAffinities = null,
        IEnumerable<KeyValuePair<ContentId, ResistanceLevel>>? ailmentResistances = null,
        IEnumerable<KeyValuePair<InstantDeathChannel, ResistanceLevel>>? instantDeathResistances = null,
        IEnumerable<ContentId>? baseSkillIds = null,
        IEnumerable<SkillUnlockDefinition>? skillUnlocks = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        EntityKindId = entityKindId;
        RaceId = raceId;
        Rank = rank;
        BaseLevel = baseLevel;
        Capabilities = capabilities;
        InheritanceRules = inheritanceRules;
        Stats = DefinitionCollections.SnapshotDictionary(stats);
        ElementalAffinities = DefinitionCollections.SnapshotDictionary(elementalAffinities);
        AilmentResistances = DefinitionCollections.SnapshotDictionary(ailmentResistances);
        InstantDeathResistances = DefinitionCollections.SnapshotDictionary(instantDeathResistances);
        BaseSkillIds = DefinitionCollections.Snapshot(baseSkillIds);
        SkillUnlocks = DefinitionCollections.Snapshot(skillUnlocks);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ContentId EntityKindId { get; }
    public ContentId RaceId { get; }
    public int Rank { get; }
    public int BaseLevel { get; }
    public EntityCapabilitiesDefinition Capabilities { get; }
    public EntityInheritanceRulesDefinition InheritanceRules { get; }
    public IReadOnlyDictionary<ContentId, int> Stats { get; }
    public IReadOnlyDictionary<DamageElement, ElementalAffinity> ElementalAffinities { get; }
    public IReadOnlyDictionary<ContentId, ResistanceLevel> AilmentResistances { get; }
    public IReadOnlyDictionary<InstantDeathChannel, ResistanceLevel> InstantDeathResistances { get; }
    public IReadOnlyList<ContentId> BaseSkillIds { get; }
    public IReadOnlyList<SkillUnlockDefinition> SkillUnlocks { get; }
}
