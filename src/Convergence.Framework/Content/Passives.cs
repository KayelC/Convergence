namespace Convergence.Content;

public enum PassiveTriggerTargetScope
{
    Owner,
    EventTargets,
    OwnerTeam,
    OpposingTeams,
    AllParticipants
}

public sealed record PassiveTriggerTargetingDefinition
{
    public PassiveTriggerTargetingDefinition(
        PassiveTriggerTargetScope scope,
        TargetLifeState lifeState,
        bool includeReserveActors)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), "Passive trigger target scope is not supported.");
        }
        if (!Enum.IsDefined(lifeState))
        {
            throw new ArgumentOutOfRangeException(nameof(lifeState), "Passive trigger target life state is not supported.");
        }

        Scope = scope;
        LifeState = lifeState;
        IncludeReserveActors = includeReserveActors;
    }

    public PassiveTriggerTargetScope Scope { get; }
    public TargetLifeState LifeState { get; }
    public bool IncludeReserveActors { get; }
}

public static class StandardPassiveTriggerTargeting
{
    public static PassiveTriggerTargetingDefinition Owner { get; } =
        new(PassiveTriggerTargetScope.Owner, TargetLifeState.Any, includeReserveActors: true);

    public static PassiveTriggerTargetingDefinition EventTargets { get; } =
        new(PassiveTriggerTargetScope.EventTargets, TargetLifeState.Any, includeReserveActors: true);

    public static PassiveTriggerTargetingDefinition LivingOwnerTeam { get; } =
        new(PassiveTriggerTargetScope.OwnerTeam, TargetLifeState.Alive, includeReserveActors: false);
}

public sealed record PassiveTriggerDefinition
{
    public PassiveTriggerDefinition(
        ContentId eventId,
        IEnumerable<EffectDefinition> effects,
        ConditionDefinition? when = null)
        : this(eventId, effects, StandardPassiveTriggerTargeting.EventTargets, when)
    {
    }

    public PassiveTriggerDefinition(
        ContentId eventId,
        IEnumerable<EffectDefinition> effects,
        PassiveTriggerTargetingDefinition targeting,
        ConditionDefinition? when = null)
    {
        EventId = eventId;
        Effects = DefinitionCollections.Snapshot(effects);
        When = when;
        Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
    }

    public ContentId EventId { get; }
    public IReadOnlyList<EffectDefinition> Effects { get; }
    public ConditionDefinition? When { get; }
    public PassiveTriggerTargetingDefinition Targeting { get; }
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

public sealed record AilmentResistanceRuleModifierDefinition(
    ContentId AilmentId,
    ResistanceLevel Resistance,
    ConditionDefinition? When = null)
    : RuleModifierDefinition(When);

public sealed record BasicAttackRuleModifierDefinition(
    DamageElement? Element = null,
    TargetingDefinition? Targeting = null,
    DamageDrainMode? Drain = null,
    ConditionDefinition? When = null)
    : RuleModifierDefinition(When);
