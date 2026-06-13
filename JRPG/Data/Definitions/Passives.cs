namespace JRPGPrototype.Data.Definitions;

public sealed record PassiveTriggerDefinition
{
    public PassiveTriggerDefinition(
        ContentId eventId,
        IEnumerable<EffectDefinition> effects,
        ConditionDefinition? when = null)
    {
        EventId = eventId;
        Effects = DefinitionCollections.Snapshot(effects);
        When = when;
    }

    public ContentId EventId { get; }
    public IReadOnlyList<EffectDefinition> Effects { get; }
    public ConditionDefinition? When { get; }
}

public abstract record RuleModifierDefinition(ConditionDefinition? When = null);

public sealed record NumericRuleModifierDefinition(
    NumericRuleModifierType ModifierType,
    ModifierOperation Operation,
    decimal Value,
    ConditionDefinition? When = null)
    : RuleModifierDefinition(When);

public sealed record ElementalAffinityRuleModifierDefinition(
    DamageElement Element,
    ElementalAffinity Affinity,
    ConditionDefinition? When = null)
    : RuleModifierDefinition(When);

public sealed record BasicAttackRuleModifierDefinition(
    DamageElement? Element = null,
    TargetingDefinition? Targeting = null,
    DamageDrainMode? Drain = null,
    ConditionDefinition? When = null)
    : RuleModifierDefinition(When);
