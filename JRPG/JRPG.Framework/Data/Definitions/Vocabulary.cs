namespace JRPGPrototype.Data.Definitions;

public enum DamageElement
{
    Physical,
    Fire,
    Ice,
    Electric,
    Wind,
    Light,
    Dark,
    Almighty
}

public enum ElementalAffinity
{
    Weak,
    Normal,
    Resist,
    Null,
    Repel,
    Absorb
}

public enum ResistanceLevel
{
    Vulnerable,
    Normal,
    Resistant,
    Immune
}

public enum SkillActivation
{
    Active,
    Passive
}

public enum SkillMenuGroup
{
    Offense,
    Ailment,
    Recovery,
    Buff,
    Debuff,
    Utility
}

public enum InheritanceGroup
{
    Physical,
    Fire,
    Ice,
    Electric,
    Wind,
    Light,
    Dark,
    Almighty,
    Recovery,
    Ailment,
    Support,
    Utility,
    Passive
}

public enum EffectFailurePolicy
{
    Continue,
    StopTarget,
    StopAction
}

public enum InstantDeathResistanceMode
{
    Channel,
    None
}

public enum InstantDeathChannel
{
    Light,
    Dark
}

public enum TargetRelation
{
    None,
    Self,
    Ally,
    Enemy,
    Any
}

public enum TargetSelection
{
    None,
    Single,
    All,
    Random
}

public enum TargetLifeState
{
    Alive,
    Dead,
    Any
}

public enum AmountKind
{
    Flat,
    PercentMaximum,
    PercentCurrent,
    Full,
    Power,
    Formula
}

public enum DurationKind
{
    Instant,
    Turns,
    Phase,
    Battle,
    Permanent
}

public enum CriticalMode
{
    Never,
    Chance
}

public enum HitDistribution
{
    Fixed,
    Uniform
}

public enum DamageDrainMode
{
    None,
    Hp,
    Sp
}

public enum ChargeKind
{
    Physical,
    Magical
}

public enum ShieldKind
{
    Physical,
    Magical
}

public enum ModifierOperation
{
    Add,
    Multiply
}

public enum NumericRuleModifierType
{
    DamageDealt,
    DamageTaken,
    Accuracy,
    Evasion,
    CriticalChance,
    AilmentInfliction,
    HealingReceived,
    HealingGiven,
    ResourceCost,
    MaximumResource,
    ExperienceGain
}

public enum ConditionSubject
{
    Actor,
    Target
}

public enum NumericComparison
{
    LessThan,
    LessThanOrEqual,
    Equal,
    GreaterThanOrEqual,
    GreaterThan
}

public enum AilmentRemovalScope
{
    Selected,
    AllRemovable
}

public enum StatusEffectKind
{
    Buff,
    Debuff,
    Charge,
    Shield,
    AffinityBreak,
    AffinityOverride,
    Other
}

public enum AnalysisLayer
{
    Stats,
    Affinities,
    Skills,
    Ailments,
    Full
}

public enum InheritanceGroupPolicyMode
{
    DenyList,
    AllowList
}

public enum AilmentTurnBehaviorKind
{
    Normal,
    Skip,
    LimitedActions,
    ChanceSkip,
    ChanceSkipOrFlee,
    ForcedBasicAttack,
    ConfusedAction,
    Custom
}

public enum DemonFleeOutcome
{
    ReturnToStock,
    EscapeBattle
}
