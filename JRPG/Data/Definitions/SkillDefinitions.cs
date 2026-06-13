namespace JRPGPrototype.Data.Definitions;

public sealed record SkillDefinition
{
    public SkillDefinition(
        ContentId id,
        string displayName,
        string description,
        SkillActivation activation,
        SkillMenuGroup? menuGroup,
        InheritanceGroup inheritanceGroup,
        SkillInheritanceDefinition inheritance,
        SkillMutationDefinition? mutation = null,
        IEnumerable<SkillCostDefinition>? costs = null,
        TargetingDefinition? targeting = null,
        IEnumerable<EffectDefinition>? effects = null,
        IEnumerable<PassiveTriggerDefinition>? triggers = null,
        IEnumerable<RuleModifierDefinition>? modifiers = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Activation = activation;
        MenuGroup = menuGroup;
        InheritanceGroup = inheritanceGroup;
        Inheritance = inheritance;
        Mutation = mutation;
        Costs = DefinitionCollections.Snapshot(costs);
        Targeting = targeting;
        Effects = DefinitionCollections.Snapshot(effects);
        Triggers = DefinitionCollections.Snapshot(triggers);
        Modifiers = DefinitionCollections.Snapshot(modifiers);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public SkillActivation Activation { get; }
    public SkillMenuGroup? MenuGroup { get; }
    public InheritanceGroup InheritanceGroup { get; }
    public SkillInheritanceDefinition Inheritance { get; }
    public SkillMutationDefinition? Mutation { get; }
    public IReadOnlyList<SkillCostDefinition> Costs { get; }
    public TargetingDefinition? Targeting { get; }
    public IReadOnlyList<EffectDefinition> Effects { get; }
    public IReadOnlyList<PassiveTriggerDefinition> Triggers { get; }
    public IReadOnlyList<RuleModifierDefinition> Modifiers { get; }
}
