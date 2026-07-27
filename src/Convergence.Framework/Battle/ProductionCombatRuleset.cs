using Convergence.Content;
using Convergence.Hosting;
using Convergence.Execution;
using Convergence.Internal;
using Convergence.Runtime;

namespace Convergence.Battle;

public sealed record ProductionCombatRulesetConfig
{
    /// <summary>
    /// Gets the greatest authored hit count accepted for one damage effect by the supplied policy.
    /// </summary>
    public int MaximumHitsPerDamageEffect { get; init; } =
        CombatExecutionLimits.DefaultMaximumHitsPerDamageEffect;

    public decimal DamageFormulaScalar { get; init; } = 5.0m;
    public decimal DamageVarianceMinimum { get; init; } = 0.95m;
    public decimal DamageVarianceMaximum { get; init; } = 1.05m;
    public decimal CriticalDamageMultiplier { get; init; } = 1.5m;
    public decimal WeakDamageMultiplier { get; init; } = 1.5m;
    public decimal ResistDamageMultiplier { get; init; } = 0.5m;
    public decimal GuardDamageMultiplier { get; init; } = 0.5m;
    public decimal HitAttackerAgilityCoefficient { get; init; } = 2m;
    public decimal HitTargetAgilityCoefficient { get; init; } = 2m;
    public int HitChanceMinimum { get; init; }
    public int HitChanceMaximum { get; init; } = 100;
    public int InstantDeathChanceMinimum { get; init; }
    public int InstantDeathChanceMaximum { get; init; } = 100;
    public decimal InstantDeathVulnerableMultiplier { get; init; } = 1.5m;
    public decimal InstantDeathNormalMultiplier { get; init; } = 1m;
    public decimal InstantDeathResistantMultiplier { get; init; } = 0.5m;
    public decimal InstantDeathImmuneMultiplier { get; init; }

    public void Validate()
    {
        if (MaximumHitsPerDamageEffect is < 1 or > CombatExecutionLimits.MaximumAuthoredHitsPerDamageEffect)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumHitsPerDamageEffect),
                MaximumHitsPerDamageEffect,
                $"Maximum hits per damage effect must be within 1-{CombatExecutionLimits.MaximumAuthoredHitsPerDamageEffect}.");
        }

        RequirePositive(DamageFormulaScalar, nameof(DamageFormulaScalar));
        RequireOrderedNonNegativeRange(
            DamageVarianceMinimum,
            DamageVarianceMaximum,
            nameof(DamageVarianceMinimum),
            nameof(DamageVarianceMaximum));
        RequireNonNegative(CriticalDamageMultiplier, nameof(CriticalDamageMultiplier));
        RequireNonNegative(WeakDamageMultiplier, nameof(WeakDamageMultiplier));
        RequireNonNegative(ResistDamageMultiplier, nameof(ResistDamageMultiplier));
        RequireNonNegative(GuardDamageMultiplier, nameof(GuardDamageMultiplier));
        RequireNonNegative(HitAttackerAgilityCoefficient, nameof(HitAttackerAgilityCoefficient));
        RequireNonNegative(HitTargetAgilityCoefficient, nameof(HitTargetAgilityCoefficient));
        RequireOrderedPercentRange(
            HitChanceMinimum,
            HitChanceMaximum,
            nameof(HitChanceMinimum),
            nameof(HitChanceMaximum));
        RequireOrderedPercentRange(
            InstantDeathChanceMinimum,
            InstantDeathChanceMaximum,
            nameof(InstantDeathChanceMinimum),
            nameof(InstantDeathChanceMaximum));
        RequireNonNegative(InstantDeathVulnerableMultiplier, nameof(InstantDeathVulnerableMultiplier));
        RequireNonNegative(InstantDeathNormalMultiplier, nameof(InstantDeathNormalMultiplier));
        RequireNonNegative(InstantDeathResistantMultiplier, nameof(InstantDeathResistantMultiplier));
        RequireNonNegative(InstantDeathImmuneMultiplier, nameof(InstantDeathImmuneMultiplier));
    }

    private static void RequirePositive(decimal value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Configuration value must be positive.");
        }
    }

    private static void RequireNonNegative(decimal value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Configuration value cannot be negative.");
        }
    }

    private static void RequirePercent(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, value, "Configuration percentage must be within 0-100.");
        }
    }

    private static void RequireOrderedPercentRange(
        int minimum,
        int maximum,
        string minimumName,
        string maximumName)
    {
        RequirePercent(minimum, minimumName);
        RequirePercent(maximum, maximumName);
        if (minimum > maximum)
        {
            throw new ArgumentException(
                $"Configuration range '{minimumName}'-'{maximumName}' must be ordered.",
                minimumName);
        }
    }

    private static void RequireOrderedNonNegativeRange(
        decimal minimum,
        decimal maximum,
        string minimumName,
        string maximumName)
    {
        RequireNonNegative(minimum, minimumName);
        RequireNonNegative(maximum, maximumName);
        if (minimum > maximum)
        {
            throw new ArgumentException(
                $"Configuration range '{minimumName}'-'{maximumName}' must be ordered.",
                minimumName);
        }
    }
}

public interface IBattleInitiativeRollPolicy
{
    bool IsPlayerFirst(decimal playerAverageAgility, decimal enemyAverageAgility);
}

public sealed record StandardBattleInitiativeRollPolicyConfig
{
    public decimal VarianceMinimum { get; init; } = 0.9m;
    public decimal VarianceMaximum { get; init; } = 1.1m;

    public void Validate()
    {
        if (VarianceMinimum < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(VarianceMinimum),
                VarianceMinimum,
                "Initiative variance cannot be negative.");
        }
        if (VarianceMaximum < VarianceMinimum)
        {
            throw new ArgumentException(
                "Initiative variance maximum cannot be lower than its minimum.",
                nameof(VarianceMaximum));
        }
    }
}

public sealed class StandardBattleInitiativeRollPolicy : IBattleInitiativeRollPolicy
{
    private readonly IRandomSource _random;

    public StandardBattleInitiativeRollPolicy(
        IRandomSource random,
        StandardBattleInitiativeRollPolicyConfig? config = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        Config = config ?? new StandardBattleInitiativeRollPolicyConfig();
        Config.Validate();
    }

    public StandardBattleInitiativeRollPolicyConfig Config { get; }

    public bool IsPlayerFirst(decimal playerAverageAgility, decimal enemyAverageAgility)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(playerAverageAgility);
        ArgumentOutOfRangeException.ThrowIfNegative(enemyAverageAgility);
        decimal playerRoll = CombatArithmetic.SaturatingMultiply(
            playerAverageAgility,
            RollVariance());
        decimal enemyRoll = CombatArithmetic.SaturatingMultiply(
            enemyAverageAgility,
            RollVariance());
        return playerRoll >= enemyRoll;
    }

    private decimal RollVariance() =>
        CombatArithmetic.SaturatingAdd(
            Config.VarianceMinimum,
            CombatArithmetic.SaturatingMultiply(
                Config.VarianceMaximum - Config.VarianceMinimum,
                RandomSourceContract.NextUnitDecimal(_random)));
}

public sealed record ProductionCombatStats(
    decimal Strength,
    decimal Magic,
    decimal Vitality,
    decimal Agility,
    decimal Luck,
    decimal Defense = 0m);

public sealed record ProductionCombatStatus(
    bool IsGuarding = false,
    bool IsRigidBody = false);

public sealed record ProductionCombatModifiers(
    decimal DamageDealtMultiplier = 1m,
    decimal DamageTakenMultiplier = 1m,
    decimal HitMultiplier = 1m,
    decimal EvasionMultiplier = 1m,
    decimal CriticalChanceMultiplier = 1m,
    int CriticalChanceTakenBonus = 0,
    decimal PhysicalDamageDealtMultiplier = 1m,
    decimal MagicalDamageDealtMultiplier = 1m);

public sealed record ProductionCombatantProfile
{
    public ProductionCombatantProfile(
        int level,
        ProductionCombatStats stats,
        ProductionCombatStatus? status = null,
        ProductionCombatModifiers? modifiers = null)
    {
        if (level <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Level must be positive.");
        }

        Level = level;
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Status = status ?? new ProductionCombatStatus();
        Modifiers = modifiers ?? new ProductionCombatModifiers();
        ValidateStats(Stats);
        ValidateModifiers(Modifiers);
    }

    public int Level { get; }
    public ProductionCombatStats Stats { get; }
    public ProductionCombatStatus Status { get; }
    public ProductionCombatModifiers Modifiers { get; }

    private static void ValidateStats(ProductionCombatStats stats)
    {
        if (stats.Strength < 0 || stats.Magic < 0 || stats.Vitality < 0 ||
            stats.Agility < 0 || stats.Luck < 0 || stats.Defense < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stats), "Combat stats cannot be negative.");
        }
    }

    private static void ValidateModifiers(ProductionCombatModifiers modifiers)
    {
        if (modifiers.DamageDealtMultiplier < 0 ||
            modifiers.DamageTakenMultiplier < 0 ||
            modifiers.HitMultiplier < 0 ||
            modifiers.EvasionMultiplier < 0 ||
            modifiers.CriticalChanceMultiplier < 0 ||
            modifiers.PhysicalDamageDealtMultiplier < 0 ||
            modifiers.MagicalDamageDealtMultiplier < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modifiers), "Combat multipliers cannot be negative.");
        }
    }
}

public sealed class ProductionHitCheckRequest
{
    public ProductionHitCheckRequest(
        ProductionCombatantProfile attacker,
        ProductionCombatantProfile target,
        int authoredAccuracy,
        IEnumerable<NumericRuleModifierDefinition>? accuracyModifiers = null,
        IEnumerable<NumericRuleModifierDefinition>? evasionModifiers = null)
    {
        Attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        AuthoredPercentage.RequireValid(
            authoredAccuracy,
            nameof(authoredAccuracy),
            "Authored accuracy");
        AuthoredAccuracy = authoredAccuracy;
        AccuracyModifiers = Snapshot(accuracyModifiers, nameof(accuracyModifiers));
        EvasionModifiers = Snapshot(evasionModifiers, nameof(evasionModifiers));
    }

    public ProductionCombatantProfile Attacker { get; }
    public ProductionCombatantProfile Target { get; }
    public int AuthoredAccuracy { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> AccuracyModifiers { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> EvasionModifiers { get; }

    private static IReadOnlyList<NumericRuleModifierDefinition> Snapshot(
        IEnumerable<NumericRuleModifierDefinition>? modifiers,
        string parameterName)
    {
        NumericRuleModifierDefinition[] snapshot = modifiers?.ToArray() ?? [];
        if (snapshot.Any(modifier => modifier is null))
        {
            throw new ArgumentException("Hit modifier collections cannot contain null entries.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed class ProductionCriticalCheckRequest
{
    public ProductionCriticalCheckRequest(
        ProductionCombatantProfile attacker,
        ProductionCombatantProfile target,
        DamageElement element,
        CriticalDefinition critical,
        int authoredAccuracy,
        int finalHitChance,
        IEnumerable<NumericRuleModifierDefinition>? criticalChanceModifiers = null)
    {
        Attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (!Enum.IsDefined(element))
        {
            throw new ArgumentOutOfRangeException(nameof(element), element, "Damage element must be defined.");
        }
        Critical = critical ?? throw new ArgumentNullException(nameof(critical));
        if (critical is ChanceCriticalDefinition chance)
        {
            AuthoredPercentage.RequireValid(
                chance.Chance,
                nameof(critical),
                "Authored critical chance");
        }
        AuthoredPercentage.RequireValid(
            authoredAccuracy,
            nameof(authoredAccuracy),
            "Authored accuracy");
        AuthoredPercentage.RequireValid(
            finalHitChance,
            nameof(finalHitChance),
            "Final hit chance");

        Element = element;
        AuthoredAccuracy = authoredAccuracy;
        FinalHitChance = finalHitChance;
        CriticalChanceModifiers = Snapshot(criticalChanceModifiers);
    }

    public ProductionCombatantProfile Attacker { get; }
    public ProductionCombatantProfile Target { get; }
    public DamageElement Element { get; }
    public CriticalDefinition Critical { get; }
    public int AuthoredAccuracy { get; }
    public int FinalHitChance { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> CriticalChanceModifiers { get; }

    private static IReadOnlyList<NumericRuleModifierDefinition> Snapshot(
        IEnumerable<NumericRuleModifierDefinition>? modifiers)
    {
        NumericRuleModifierDefinition[] snapshot = modifiers?.ToArray() ?? [];
        if (snapshot.Any(modifier => modifier is null))
        {
            throw new ArgumentException(
                "Critical chance modifiers cannot contain null entries.",
                nameof(modifiers));
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record ProductionCriticalCheckResult(
    bool Critical,
    bool Eligible,
    int Chance,
    decimal? Roll,
    CriticalEligibilityReason EligibilityReason,
    bool GuaranteedByRigidState = false);

public sealed class ProductionDamageResolutionRequest
{
    public ProductionDamageResolutionRequest(
        ProductionCombatantProfile attacker,
        ProductionCombatantProfile target,
        DamageElement element,
        ElementalAffinity affinity,
        int power,
        int accuracy,
        CriticalDefinition critical,
        HitCountDefinition hits,
        decimal chargeMultiplier = 1m,
        ChargeKind? chargeKind = null,
        IEnumerable<NumericRuleModifierDefinition>? accuracyModifiers = null,
        IEnumerable<NumericRuleModifierDefinition>? evasionModifiers = null,
        IEnumerable<NumericRuleModifierDefinition>? criticalChanceModifiers = null)
        : this(
            attacker,
            target,
            element,
            affinity,
            power,
            accuracy,
            critical,
            hits,
            chargeMultiplier,
            chargeKind,
            accuracyModifiers,
            evasionModifiers,
            criticalChanceModifiers,
            DamageContactMode.Independent)
    {
    }

    public ProductionDamageResolutionRequest(
        ProductionCombatantProfile attacker,
        ProductionCombatantProfile target,
        DamageElement element,
        ElementalAffinity affinity,
        int power,
        int accuracy,
        CriticalDefinition critical,
        HitCountDefinition hits,
        decimal chargeMultiplier,
        ChargeKind? chargeKind,
        IEnumerable<NumericRuleModifierDefinition>? accuracyModifiers,
        IEnumerable<NumericRuleModifierDefinition>? evasionModifiers,
        IEnumerable<NumericRuleModifierDefinition>? criticalChanceModifiers,
        DamageContactMode contactMode)
    {
        Attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (!Enum.IsDefined(element))
        {
            throw new ArgumentOutOfRangeException(nameof(element), element, "Damage element must be defined.");
        }
        if (!Enum.IsDefined(affinity))
        {
            throw new ArgumentOutOfRangeException(nameof(affinity), affinity, "Affinity must be defined.");
        }
        if (!Enum.IsDefined(contactMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(contactMode),
                contactMode,
                "Damage contact mode must be defined.");
        }

        Element = element;
        Affinity = affinity;
        Power = power;
        AuthoredPercentage.RequireValid(
            accuracy,
            nameof(accuracy),
            "Authored accuracy");
        if (critical is ChanceCriticalDefinition criticalChance)
        {
            AuthoredPercentage.RequireValid(
                criticalChance.Chance,
                nameof(critical),
                "Authored critical chance");
        }
        Accuracy = accuracy;
        Critical = critical ?? throw new ArgumentNullException(nameof(critical));
        Hits = hits ?? throw new ArgumentNullException(nameof(hits));
        ChargeMultiplier = chargeMultiplier;
        ChargeKind = chargeKind;
        AccuracyModifiers = Snapshot(accuracyModifiers, nameof(accuracyModifiers));
        EvasionModifiers = Snapshot(evasionModifiers, nameof(evasionModifiers));
        CriticalChanceModifiers = Snapshot(criticalChanceModifiers, nameof(criticalChanceModifiers));
        ContactMode = contactMode;
    }

    public ProductionCombatantProfile Attacker { get; }
    public ProductionCombatantProfile Target { get; }
    public DamageElement Element { get; }
    public ElementalAffinity Affinity { get; }
    public int Power { get; }
    public int Accuracy { get; }
    public CriticalDefinition Critical { get; }
    public HitCountDefinition Hits { get; }
    public decimal ChargeMultiplier { get; }
    public ChargeKind? ChargeKind { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> AccuracyModifiers { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> EvasionModifiers { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> CriticalChanceModifiers { get; }

    /// <summary>Gets whether this request rolls accuracy or reuses contact established by its caller.</summary>
    public DamageContactMode ContactMode { get; }

    private static IReadOnlyList<NumericRuleModifierDefinition> Snapshot(
        IEnumerable<NumericRuleModifierDefinition>? modifiers,
        string parameterName)
    {
        NumericRuleModifierDefinition[] snapshot = modifiers?.ToArray() ?? [];
        if (snapshot.Any(modifier => modifier is null))
        {
            throw new ArgumentException("Damage modifier collections cannot contain null entries.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed class ProductionDamageResolutionHit
{
    public ProductionDamageResolutionHit(
        int hitIndex,
        bool hit,
        decimal damage,
        bool critical,
        HitResolutionResult hitResolution,
        ProductionCriticalCheckResult? criticalResolution,
        ElementalAffinity resolvedAffinity,
        ChargeKind? chargeKind,
        decimal chargeMultiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hitIndex);
        HitResolution = hitResolution ?? throw new ArgumentNullException(nameof(hitResolution));
        if (damage < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), damage, "Resolved damage cannot be negative.");
        }
        if (hitResolution.Hit != hit)
        {
            throw new ArgumentException("Hit evidence must agree with the resolved hit state.", nameof(hit));
        }
        if (!hit && (damage != 0m || criticalResolution is not null))
        {
            throw new ArgumentException("A missed hit cannot contain damage or critical evidence.", nameof(hit));
        }
        if (!Enum.IsDefined(resolvedAffinity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedAffinity),
                resolvedAffinity,
                "Resolved affinity must be defined.");
        }
        if (chargeKind is ChargeKind kind && !Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(chargeKind), chargeKind, "Charge kind must be defined.");
        }
        if (chargeMultiplier <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chargeMultiplier),
                chargeMultiplier,
                "Charge multiplier must be positive.");
        }
        if (hit && criticalResolution is null)
        {
            throw new ArgumentNullException(
                nameof(criticalResolution),
                "A landed production hit requires critical eligibility evidence.");
        }
        if (criticalResolution is not null && criticalResolution.Critical != critical)
        {
            throw new ArgumentException("Critical evidence must agree with the resolved critical state.", nameof(critical));
        }

        HitIndex = hitIndex;
        Hit = hit;
        Damage = damage;
        Critical = critical;
        CriticalResolution = criticalResolution;
        ResolvedAffinity = resolvedAffinity;
        ChargeKind = chargeKind;
        ChargeMultiplier = chargeMultiplier;
    }

    public int HitIndex { get; }
    public bool Hit { get; }
    public decimal Damage { get; }
    public bool Critical { get; }
    public HitResolutionResult HitResolution { get; }
    public ProductionCriticalCheckResult? CriticalResolution { get; }
    public int HitChance => HitResolution.FinalChance;
    public int CriticalChance => CriticalResolution?.Chance ?? 0;
    public ElementalAffinity ResolvedAffinity { get; }
    public ChargeKind? ChargeKind { get; }
    public decimal ChargeMultiplier { get; }
}

public sealed record ProductionDamageResolutionResult
{
    public ProductionDamageResolutionResult(
        IEnumerable<ProductionDamageResolutionHit> hits,
        ElementalAffinity resolvedAffinity)
    {
        ProductionDamageResolutionHit[] snapshot =
            (hits ?? throw new ArgumentNullException(nameof(hits))).ToArray();
        if (snapshot.Length == 0 || snapshot.Any(hit => hit is null))
        {
            throw new ArgumentException(
                "Production damage resolution requires at least one non-null hit.",
                nameof(hits));
        }
        if (!Enum.IsDefined(resolvedAffinity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedAffinity),
                resolvedAffinity,
                "Resolved affinity must be defined.");
        }

        Hits = Array.AsReadOnly(snapshot);
        ResolvedAffinity = resolvedAffinity;
    }

    public IReadOnlyList<ProductionDamageResolutionHit> Hits { get; }
    public ElementalAffinity ResolvedAffinity { get; }
    public decimal TotalDamage => CombatArithmetic.SaturatingSum(
        Hits.Where(hit => hit.Hit).Select(hit => hit.Damage));
    public bool AnyCritical => Hits.Any(hit => hit.Critical);
}

public sealed record ProductionInstantDeathRequest(
    ProductionCombatantProfile Attacker,
    ProductionCombatantProfile Target,
    int BaseChance,
    ResistanceLevel? Resistance,
    bool BypassesResistance = false);

public sealed record ProductionInstantDeathResult(
    bool Defeated,
    int Chance,
    decimal? Roll,
    ResistanceLevel? Resistance,
    bool BypassedResistance,
    decimal ResistanceMultiplier,
    decimal ResolvedChance,
    InstantDefeatResolutionReason Reason);

public sealed record ProductionAilmentApplicationRequest(
    ProductionCombatantProfile Attacker,
    ProductionCombatantProfile Target,
    int BaseChance,
    ResistanceLevel Resistance);

public sealed record ProductionAilmentApplicationResult(bool Applied, int Chance);

public sealed class ProductionCombatRuleset :
    ICombatDamageExecutionPolicy,
    ICombatInstantDefeatExecutionPolicy,
    ITypedInstantDeathExecutionPolicy,
    IAilmentApplicationPolicy,
    IChanceExecutionPolicy,
    IPowerAmountPolicy
{
    private readonly IRandomSource _random;
    private readonly ProductionCombatRulesetConfig _config;
    private readonly IStatStageScalingPolicy _stageScaling;
    private readonly IHitResolutionPolicy _hitPolicy;
    private readonly ICriticalEligibilityPolicy _criticalEligibilityPolicy;
    private readonly ICriticalChancePolicy _criticalChancePolicy;
    private readonly IInstantDefeatResolutionPolicy _instantDefeatPolicy;

    public ProductionCombatRuleset(
        IRandomSource random,
        ProductionCombatRulesetConfig? config = null,
        IStatStageScalingPolicy? stageScaling = null,
        IHitResolutionPolicy? hitPolicy = null,
        ICriticalEligibilityPolicy? criticalEligibilityPolicy = null,
        ICriticalChancePolicy? criticalChancePolicy = null,
        IInstantDefeatResolutionPolicy? instantDefeatPolicy = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _config = config ?? new ProductionCombatRulesetConfig();
        _config.Validate();
        _stageScaling = stageScaling ?? new StandardStatStageScalingPolicy();
        _hitPolicy = hitPolicy ?? new StandardHitResolutionPolicy(
            _random,
            new StandardHitResolutionPolicyConfig
            {
                AttackerAgilityCoefficient = _config.HitAttackerAgilityCoefficient,
                TargetAgilityCoefficient = _config.HitTargetAgilityCoefficient,
                MinimumChance = _config.HitChanceMinimum,
                MaximumChance = _config.HitChanceMaximum
            });
        _criticalEligibilityPolicy = criticalEligibilityPolicy ?? new PhysicalOnlyCriticalEligibilityPolicy();
        _criticalChancePolicy = criticalChancePolicy ?? new AuthoredCriticalChancePolicy(_random);
        _instantDefeatPolicy = instantDefeatPolicy ?? new StandardInstantDefeatResolutionPolicy(
            _random,
            new StandardInstantDefeatResolutionPolicyConfig
            {
                VulnerableMultiplier = _config.InstantDeathVulnerableMultiplier,
                NormalMultiplier = _config.InstantDeathNormalMultiplier,
                ResistantMultiplier = _config.InstantDeathResistantMultiplier,
                ImmuneMultiplier = _config.InstantDeathImmuneMultiplier,
                MinimumChance = _config.InstantDeathChanceMinimum,
                MaximumChance = _config.InstantDeathChanceMaximum
            });
    }

    public ProductionCombatRulesetConfig Config => _config;
    public IStatStageScalingPolicy StageScalingPolicy => _stageScaling;
    public IHitResolutionPolicy HitPolicy => _hitPolicy;
    public ICriticalEligibilityPolicy CriticalEligibilityPolicy => _criticalEligibilityPolicy;
    public ICriticalChancePolicy CriticalChancePolicy => _criticalChancePolicy;
    public IInstantDefeatResolutionPolicy InstantDefeatPolicy => _instantDefeatPolicy;
    IHitResolutionPolicy ICombatDamageExecutionPolicy.HitResolution => _hitPolicy;
    ICriticalEligibilityPolicy ICombatDamageExecutionPolicy.CriticalEligibility =>
        _criticalEligibilityPolicy;
    ICriticalChancePolicy ICombatDamageExecutionPolicy.CriticalChance => _criticalChancePolicy;
    IInstantDefeatResolutionPolicy ICombatInstantDefeatExecutionPolicy.Resolution =>
        _instantDefeatPolicy;

    public DamagePolicyResolution Resolve(DamagePolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProductionDamageResolutionResult result = ResolveDamage(new ProductionDamageResolutionRequest(
            CreateCombatantProfile(request.Actor),
            CreateCombatantProfile(request.Target),
            request.Effect.Element,
            request.Affinity,
            request.Effect.Power,
            request.Effect.Accuracy,
            request.Effect.Critical,
            request.Effect.Hits,
            request.ChargeMultiplier,
            request.ChargeKind,
            request.AccuracyModifiers,
            request.EvasionModifiers,
            request.CriticalChanceModifiers,
            request.Effect.ContactMode));

        return new DamagePolicyResolution(
            result.Hits.Select(hit => new DamageHitResolution(
                hit.HitIndex,
                hit.Hit,
                hit.Damage,
                hit.Critical,
                hit.HitResolution.AuthoredAccuracy,
                hit.HitResolution.FinalChance,
                hit.HitResolution.Roll,
                hit.CriticalResolution?.Eligible,
                hit.CriticalResolution?.EligibilityReason,
                hit.CriticalResolution?.Chance,
                hit.CriticalResolution?.Roll,
                hit.ResolvedAffinity,
                hit.ChargeKind,
                hit.ChargeMultiplier)),
            result.ResolvedAffinity);
    }

    public bool ShouldDefeat(InstantDeathPolicyRequest request) => Resolve(request).Defeated;

    public InstantDeathExecutionResolution Resolve(InstantDeathPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AuthoredPercentage.RequireValid(
            request.Effect.Chance,
            nameof(request),
            "Authored instant-defeat chance");

        ProductionInstantDeathResult result = ResolveInstantDeath(new ProductionInstantDeathRequest(
            CreateCombatantProfile(request.Actor),
            CreateCombatantProfile(request.Target),
            request.Effect.Chance,
            request.Resistance.Resistance,
            request.Resistance.BypassesResistance));
        return new InstantDeathExecutionResolution(result.Defeated, result.Reason);
    }

    public bool ShouldApply(AilmentApplicationPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AuthoredPercentage.RequireValid(
            request.Chance,
            nameof(request),
            "Authored ailment chance");

        ProductionAilmentApplicationResult result = ResolveAilmentApplication(
            new ProductionAilmentApplicationRequest(
                CreateCombatantProfile(request.Actor),
                CreateCombatantProfile(request.Target),
                request.Chance,
                request.Resistance));
        return result.Applied;
    }

    public bool Roll(ChancePolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AuthoredPercentage.RequireValid(
            request.Chance,
            nameof(request),
            "Authored chance");
        return RollPercent(request.Chance);
    }

    public decimal Resolve(PowerAmountDefinition amount, AmountResolutionContext context) =>
        amount.Power;

    public ProductionDamageResolutionResult ResolveDamage(ProductionDamageResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Power);
        if (request.ChargeMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Charge multiplier must be positive.");
        }
        if (request.ChargeKind is ChargeKind chargeKind && !Enum.IsDefined(chargeKind))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Charge kind must be defined.");
        }

        int hitCount = ResolveHitCount(request.Hits);
        List<ProductionDamageResolutionHit> hits = new(hitCount);
        ElementalAffinity resolvedAffinity = NormalizeGuardedAffinity(
            request.Affinity,
            request.Target.Status.IsGuarding);
        for (int i = 0; i < hitCount; i++)
        {
            HitResolutionResult hit = request.ContactMode switch
            {
                DamageContactMode.Independent => CheckHit(new ProductionHitCheckRequest(
                    request.Attacker,
                    request.Target,
                    request.Accuracy,
                    request.AccuracyModifiers,
                    request.EvasionModifiers)),
                DamageContactMode.SharedContact => SharedContactHit(request.Accuracy),
                _ => throw new InvalidOperationException(
                    $"Unsupported damage contact mode '{request.ContactMode}'.")
            };
            if (!hit.Hit)
            {
                hits.Add(new ProductionDamageResolutionHit(
                    i,
                    false,
                    0m,
                    false,
                    hit,
                    criticalResolution: null,
                    resolvedAffinity,
                    request.ChargeKind,
                    request.ChargeMultiplier));
                continue;
            }

            ProductionCriticalCheckResult critical = CheckCritical(new ProductionCriticalCheckRequest(
                request.Attacker,
                request.Target,
                request.Element,
                request.Critical,
                request.Accuracy,
                hit.FinalChance,
                request.CriticalChanceModifiers));
            decimal damage = CalculateBaseDamage(
                request.Attacker,
                request.Target,
                request.Power,
                request.Element);
            damage = CombatArithmetic.SaturatingMultiply(
                damage,
                request.Target.Modifiers.DamageTakenMultiplier);
            if (critical.Critical)
            {
                damage = CombatArithmetic.SaturatingMultiply(damage, _config.CriticalDamageMultiplier);
            }
            if (request.Target.Status.IsGuarding)
            {
                damage = CombatArithmetic.SaturatingMultiply(damage, _config.GuardDamageMultiplier);
            }

            damage = ApplyAffinityMultiplier(damage, resolvedAffinity);
            damage = CombatArithmetic.SaturatingMultiply(damage, request.ChargeMultiplier);
            hits.Add(new ProductionDamageResolutionHit(
                i,
                true,
                Math.Floor(CombatArithmetic.SaturatingMultiply(
                    damage,
                    RollVariance(_config.DamageVarianceMinimum, _config.DamageVarianceMaximum))),
                critical.Critical,
                hit,
                critical,
                resolvedAffinity,
                request.ChargeKind,
                request.ChargeMultiplier));
        }

        return new ProductionDamageResolutionResult(hits, resolvedAffinity);
    }

    private static HitResolutionResult SharedContactHit(int authoredAccuracy)
    {
        if (authoredAccuracy is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoredAccuracy),
                authoredAccuracy,
                "Authored accuracy must be within 0-100.");
        }

        return new HitResolutionResult(
            true,
            authoredAccuracy,
            AttackerAgilityContribution: 0m,
            TargetAgilityContribution: 0m,
            AccuracyScoreBeforeModifiers: authoredAccuracy,
            EvasionScoreBeforeModifiers: 0m,
            ResolvedAccuracyScore: authoredAccuracy,
            ResolvedEvasionScore: 0m,
            RawChance: authoredAccuracy,
            FinalChance: authoredAccuracy,
            Roll: null);
    }

    public HitResolutionResult CheckHit(ProductionHitCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _hitPolicy.Resolve(new HitResolutionRequest(
            request.AuthoredAccuracy,
            request.Attacker.Stats.Agility,
            request.Target.Stats.Agility,
            request.Attacker.Modifiers.HitMultiplier,
            request.Target.Modifiers.EvasionMultiplier,
            request.AccuracyModifiers,
            request.EvasionModifiers,
            request.Target.Status.IsRigidBody));
    }

    public ProductionCriticalCheckResult CheckCritical(ProductionCriticalCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        CriticalEligibilityResult eligibility = _criticalEligibilityPolicy.Assess(
            new CriticalEligibilityRequest(
                request.Element,
                request.Critical,
                request.Target.Status.IsGuarding,
                request.Target.Status.IsRigidBody));
        if (!eligibility.Eligible)
        {
            return new ProductionCriticalCheckResult(
                false,
                false,
                0,
                null,
                eligibility.Reason);
        }
        if (eligibility.GuaranteedByRigidState)
        {
            return new ProductionCriticalCheckResult(
                true,
                true,
                100,
                null,
                eligibility.Reason,
                true);
        }

        CriticalChanceResult chance = _criticalChancePolicy.Resolve(new CriticalChanceRequest(
            request.Critical,
            request.AuthoredAccuracy,
            request.FinalHitChance,
            request.Attacker.Modifiers.CriticalChanceMultiplier,
            request.Target.Modifiers.CriticalChanceTakenBonus,
            request.CriticalChanceModifiers));
        return new ProductionCriticalCheckResult(
            chance.Critical,
            true,
            chance.FinalChance,
            chance.Roll,
            eligibility.Reason);
    }

    public ProductionInstantDeathResult ResolveInstantDeath(ProductionInstantDeathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attacker);
        ArgumentNullException.ThrowIfNull(request.Target);
        AuthoredPercentage.RequireValid(
            request.BaseChance,
            nameof(request),
            "Authored instant-defeat chance");

        InstantDefeatResolutionResult result = _instantDefeatPolicy.Resolve(
            new InstantDefeatResolutionRequest(
                request.BaseChance,
                request.Resistance,
                request.BypassesResistance));
        return new ProductionInstantDeathResult(
            result.Defeated,
            result.FinalChance,
            result.Roll,
            result.Resistance,
            result.BypassedResistance,
            result.ResistanceMultiplier,
            result.ResolvedChance,
            result.Reason);
    }

    public ProductionAilmentApplicationResult ResolveAilmentApplication(ProductionAilmentApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attacker);
        ArgumentNullException.ThrowIfNull(request.Target);
        AuthoredPercentage.RequireValid(
            request.BaseChance,
            nameof(request),
            "Authored ailment chance");
        if (!Enum.IsDefined(request.Resistance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Resistance,
                "Ailment resistance must be defined.");
        }

        if (request.Resistance == ResistanceLevel.Immune)
        {
            return new ProductionAilmentApplicationResult(false, 0);
        }

        decimal chance = request.BaseChance;
        chance = CombatArithmetic.SaturatingMultiply(
            chance,
            request.Resistance switch
            {
                ResistanceLevel.Vulnerable => 1.25m,
                ResistanceLevel.Resistant => 0.5m,
                _ => 1m
            });
        int clamped = ClampPercent(chance, 0, 100);
        return new ProductionAilmentApplicationResult(RollPercent(clamped), clamped);
    }

    private decimal CalculateBaseDamage(
        ProductionCombatantProfile attacker,
        ProductionCombatantProfile target,
        int power,
        DamageElement element)
    {
        decimal attack = IsPhysical(element) ? attacker.Stats.Strength : attacker.Stats.Magic;
        decimal defense = Math.Max(
            1m,
            CombatArithmetic.SaturatingAdd(target.Stats.Vitality, target.Stats.Defense));
        attack = CombatArithmetic.SaturatingMultiply(
            attack,
            attacker.Modifiers.DamageDealtMultiplier);
        attack = CombatArithmetic.SaturatingMultiply(
            attack,
            IsPhysical(element)
                ? attacker.Modifiers.PhysicalDamageDealtMultiplier
                : attacker.Modifiers.MagicalDamageDealtMultiplier);
        decimal ratio = CombatArithmetic.SaturatingDivide(attack, defense);
        // The formula already requires a square root; multiplying in double avoids a decimal-only overflow before it.
        double radicand = (double)power * (double)ratio;
        decimal root = CombatArithmetic.SaturatingFromDouble(Math.Sqrt(radicand));
        return CombatArithmetic.SaturatingMultiply(_config.DamageFormulaScalar, root);
    }

    private decimal ApplyAffinityMultiplier(decimal damage, ElementalAffinity affinity) => affinity switch
    {
        ElementalAffinity.Weak => CombatArithmetic.SaturatingMultiply(damage, _config.WeakDamageMultiplier),
        ElementalAffinity.Resist => CombatArithmetic.SaturatingMultiply(damage, _config.ResistDamageMultiplier),
        _ => damage
    };

    internal int ResolveHitCount(HitCountDefinition hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        if (!Enum.IsDefined(hits.Distribution))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hits),
                hits.Distribution,
                "Hit distribution must be defined.");
        }
        if (hits.Minimum <= 0 || hits.Maximum < hits.Minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(hits), "Hit counts must be positive and ordered.");
        }
        if (hits.Distribution == HitDistribution.Fixed && hits.Minimum != hits.Maximum)
        {
            throw new ArgumentException("Fixed hit counts require equal minimum and maximum values.", nameof(hits));
        }
        if (hits.Maximum > _config.MaximumHitsPerDamageEffect)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hits),
                hits.Maximum,
                $"Authored hit count exceeds the configured maximum of {_config.MaximumHitsPerDamageEffect} per damage effect.");
        }

        if (hits.Minimum == hits.Maximum || hits.Distribution == HitDistribution.Fixed)
        {
            return hits.Minimum;
        }

        int width = checked((int)(((long)hits.Maximum - hits.Minimum) + 1L));
        int offset = RandomSourceContract.NextInt32(_random, 0, width);

        return checked(hits.Minimum + offset);
    }

    private bool RollPercent(int chance)
    {
        AuthoredPercentage.RequireValid(chance, nameof(chance), "Chance");
        if (chance == 0)
        {
            return false;
        }
        if (chance == 100)
        {
            return true;
        }

        return CombatArithmetic.SaturatingMultiply(
            RandomSourceContract.NextUnitDecimal(_random),
            100m) < chance;
    }

    private decimal RollVariance(decimal minimum, decimal maximum) =>
        CombatArithmetic.SaturatingAdd(
            minimum,
            CombatArithmetic.SaturatingMultiply(
                RandomSourceContract.NextUnitDecimal(_random),
                CombatArithmetic.SaturatingSubtract(maximum, minimum)));

    private static ElementalAffinity NormalizeGuardedAffinity(ElementalAffinity affinity, bool isGuarding) =>
        isGuarding && affinity == ElementalAffinity.Weak ? ElementalAffinity.Normal : affinity;

    private static bool IsPhysical(DamageElement element) => element == DamageElement.Physical;

    private static int ClampPercent(decimal chance, int minimum, int maximum) =>
        (int)Math.Clamp(Math.Floor(chance), minimum, maximum);

    internal ProductionCombatantProfile CreateCombatantProfile(RuntimeActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        decimal strength = actor.Stats.GetValueOrDefault(StandardProgressionIds.Strength);
        decimal magic = actor.Stats.GetValueOrDefault(StandardProgressionIds.Magic);
        decimal vitality = actor.Stats.GetValueOrDefault(StandardProgressionIds.Vitality);
        decimal agility = actor.Stats.GetValueOrDefault(StandardProgressionIds.Agility);
        decimal luck = actor.Stats.GetValueOrDefault(StandardProgressionIds.Luck);
        decimal damageDealt = 1m;
        RuntimeStatStageSnapshot[] stages = actor.StatStages
            .Select(pair => new RuntimeStatStageSnapshot(pair.Key, pair.Value.Stage, pair.Value.Duration))
            .ToArray();
        decimal physicalDamageDealt = ResolveStageMultiplier(
            StatStageScalingChannel.PhysicalDamageDealt,
            stages);
        decimal magicalDamageDealt = ResolveStageMultiplier(
            StatStageScalingChannel.MagicalDamageDealt,
            stages);
        decimal damageTaken = ResolveStageMultiplier(
            StatStageScalingChannel.DamageTaken,
            stages);
        decimal hit = ResolveStageMultiplier(
            StatStageScalingChannel.HitChance,
            stages);
        decimal evasion = ResolveStageMultiplier(
            StatStageScalingChannel.Evasion,
            stages);
        int criticalTakenBonus = 0;
        bool rigid = false;

        foreach (ActiveAilmentState ailment in actor.Ailments.Values)
        {
            damageDealt = CombatArithmetic.SaturatingMultiply(
                damageDealt,
                ailment.Definition.Modifiers.DamageDealtMultiplier);
            damageTaken = CombatArithmetic.SaturatingMultiply(
                damageTaken,
                ailment.Definition.Modifiers.DamageTakenMultiplier);
            evasion = CombatArithmetic.SaturatingMultiply(
                evasion,
                ailment.Definition.Modifiers.EvasionMultiplier);
            criticalTakenBonus = CombatArithmetic.SaturatingAdd(
                criticalTakenBonus,
                ailment.Definition.Modifiers.CriticalChanceTakenBonus);
            rigid |= ailment.Definition.Modifiers.IsRigidBody;
        }

        return new ProductionCombatantProfile(
            actor.Progression.Level,
            new ProductionCombatStats(strength, magic, vitality, agility, luck),
            new ProductionCombatStatus(
                IsGuarding: actor.IsGuarding,
                IsRigidBody: rigid),
            new ProductionCombatModifiers(
                DamageDealtMultiplier: damageDealt,
                DamageTakenMultiplier: damageTaken,
                HitMultiplier: hit,
                EvasionMultiplier: evasion,
                CriticalChanceTakenBonus: criticalTakenBonus,
                PhysicalDamageDealtMultiplier: physicalDamageDealt,
                MagicalDamageDealtMultiplier: magicalDamageDealt));
    }

    private decimal ResolveStageMultiplier(
        StatStageScalingChannel channel,
        IReadOnlyList<RuntimeStatStageSnapshot> stages) =>
        _stageScaling.Resolve(new StatStageScalingRequest(channel, stages)).Multiplier;
}
