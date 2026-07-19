using Convergence.Content;
using Convergence.Hosting;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Battle;

public sealed record ProductionCombatRulesetConfig
{
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
    public int CriticalChanceMinimum { get; init; } = 2;
    public int CriticalChanceMaximum { get; init; } = 40;
    public int CriticalChanceBase { get; init; } = 5;
    public int InstantDeathChanceMinimum { get; init; } = 5;
    public int InstantDeathChanceMaximum { get; init; } = 95;
    public decimal EnemiesPerLevelForExperience { get; init; } = 50m;
    public decimal ExpectedStatLevelMultiplier { get; init; } = 3m;
    public decimal ExpectedStatBase { get; init; } = 15m;
    public decimal StatDensityDivisor { get; init; } = 100m;
    public decimal MaximumStatDensityMultiplier { get; init; } = 2m;
    public decimal CurrencyBaseMultiplier { get; init; } = 0.25m;
    public decimal CurrencyLuckMultiplier { get; init; } = 5m;
    public decimal CurrencyVarianceMinimum { get; init; } = 0.9m;
    public decimal CurrencyVarianceMaximum { get; init; } = 1.1m;
    public decimal InitiativeVarianceMinimum { get; init; } = 0.9m;
    public decimal InitiativeVarianceMaximum { get; init; } = 1.1m;

    public void Validate()
    {
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
            CriticalChanceMinimum,
            CriticalChanceMaximum,
            nameof(CriticalChanceMinimum),
            nameof(CriticalChanceMaximum));
        RequirePercent(CriticalChanceBase, nameof(CriticalChanceBase));
        RequireOrderedPercentRange(
            InstantDeathChanceMinimum,
            InstantDeathChanceMaximum,
            nameof(InstantDeathChanceMinimum),
            nameof(InstantDeathChanceMaximum));
        RequirePositive(EnemiesPerLevelForExperience, nameof(EnemiesPerLevelForExperience));
        RequireNonNegative(ExpectedStatLevelMultiplier, nameof(ExpectedStatLevelMultiplier));
        RequireNonNegative(ExpectedStatBase, nameof(ExpectedStatBase));
        RequirePositive(StatDensityDivisor, nameof(StatDensityDivisor));
        RequirePositive(MaximumStatDensityMultiplier, nameof(MaximumStatDensityMultiplier));
        RequireNonNegative(CurrencyBaseMultiplier, nameof(CurrencyBaseMultiplier));
        RequireNonNegative(CurrencyLuckMultiplier, nameof(CurrencyLuckMultiplier));
        RequireOrderedNonNegativeRange(
            CurrencyVarianceMinimum,
            CurrencyVarianceMaximum,
            nameof(CurrencyVarianceMinimum),
            nameof(CurrencyVarianceMaximum));
        RequireOrderedNonNegativeRange(
            InitiativeVarianceMinimum,
            InitiativeVarianceMaximum,
            nameof(InitiativeVarianceMinimum),
            nameof(InitiativeVarianceMaximum));
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

public sealed record ProductionCriticalCheckRequest(
    ProductionCombatantProfile Attacker,
    ProductionCombatantProfile Target,
    DamageElement Element,
    CriticalDefinition Critical);

public sealed record ProductionCriticalCheckResult(bool Critical, int Chance);

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
        IEnumerable<NumericRuleModifierDefinition>? evasionModifiers = null)
    {
        Attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Element = element;
        Affinity = affinity;
        Power = power;
        Accuracy = accuracy;
        Critical = critical ?? throw new ArgumentNullException(nameof(critical));
        Hits = hits ?? throw new ArgumentNullException(nameof(hits));
        ChargeMultiplier = chargeMultiplier;
        ChargeKind = chargeKind;
        AccuracyModifiers = Snapshot(accuracyModifiers, nameof(accuracyModifiers));
        EvasionModifiers = Snapshot(evasionModifiers, nameof(evasionModifiers));
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

public sealed record ProductionDamageResolutionHit(
    bool Hit,
    decimal Damage,
    bool Critical,
    int HitChance,
    int CriticalChance);

public sealed record ProductionDamageResolutionResult
{
    public ProductionDamageResolutionResult(
        IEnumerable<ProductionDamageResolutionHit> hits,
        ElementalAffinity resolvedAffinity)
    {
        Hits = Array.AsReadOnly((hits ?? throw new ArgumentNullException(nameof(hits))).ToArray());
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

public sealed record ProductionInstantDeathResult(bool Defeated, int Chance);

public sealed record ProductionAilmentApplicationRequest(
    ProductionCombatantProfile Attacker,
    ProductionCombatantProfile Target,
    int BaseChance,
    ResistanceLevel Resistance);

public sealed record ProductionAilmentApplicationResult(bool Applied, int Chance);

public sealed class ProductionCombatRuleset :
    IDamageExecutionPolicy,
    IInstantDeathExecutionPolicy,
    IAilmentApplicationPolicy,
    IChanceExecutionPolicy,
    IPowerAmountPolicy
{
    private readonly IRandomSource _random;
    private readonly ProductionCombatRulesetConfig _config;
    private readonly IStatStageScalingPolicy _stageScaling;
    private readonly IHitResolutionPolicy _hitPolicy;

    public ProductionCombatRuleset(
        IRandomSource random,
        ProductionCombatRulesetConfig? config = null,
        IStatStageScalingPolicy? stageScaling = null,
        IHitResolutionPolicy? hitPolicy = null)
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
    }

    public ProductionCombatRulesetConfig Config => _config;
    public IStatStageScalingPolicy StageScalingPolicy => _stageScaling;
    public IHitResolutionPolicy HitPolicy => _hitPolicy;

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
            request.EvasionModifiers));

        return new DamagePolicyResolution(
            result.Hits.Select(hit => new DamageHitResolution(hit.Hit, hit.Damage, hit.Critical)),
            result.ResolvedAffinity);
    }

    public bool ShouldDefeat(InstantDeathPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProductionInstantDeathResult result = ResolveInstantDeath(new ProductionInstantDeathRequest(
            CreateCombatantProfile(request.Actor),
            CreateCombatantProfile(request.Target),
            request.Effect.Chance,
            request.Resistance.Resistance,
            request.Resistance.BypassesResistance));
        return result.Defeated;
    }

    public bool ShouldApply(AilmentApplicationPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProductionAilmentApplicationResult result = ResolveAilmentApplication(
            new ProductionAilmentApplicationRequest(
                CreateCombatantProfile(request.Actor),
                CreateCombatantProfile(request.Target),
                request.Chance,
                request.Resistance));
        return result.Applied;
    }

    public bool Roll(ChancePolicyRequest request) =>
        RollPercent(request.Chance);

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
            HitResolutionResult hit = CheckHit(new ProductionHitCheckRequest(
                request.Attacker,
                request.Target,
                request.Accuracy,
                request.AccuracyModifiers,
                request.EvasionModifiers));
            if (!hit.Hit)
            {
                hits.Add(new ProductionDamageResolutionHit(false, 0m, false, hit.FinalChance, 0));
                continue;
            }

            ProductionCriticalCheckResult critical = CheckCritical(new ProductionCriticalCheckRequest(
                request.Attacker,
                request.Target,
                request.Element,
                request.Critical));
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
                true,
                Math.Floor(CombatArithmetic.SaturatingMultiply(
                    damage,
                    RollVariance(_config.DamageVarianceMinimum, _config.DamageVarianceMaximum))),
                critical.Critical,
                hit.FinalChance,
                critical.Chance));
        }

        return new ProductionDamageResolutionResult(hits, resolvedAffinity);
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

        if (!IsPhysical(request.Element) || request.Target.Status.IsGuarding)
        {
            return new ProductionCriticalCheckResult(false, 0);
        }
        if (request.Target.Status.IsRigidBody)
        {
            return new ProductionCriticalCheckResult(true, 100);
        }
        if (request.Critical is NeverCriticalDefinition)
        {
            return new ProductionCriticalCheckResult(false, 0);
        }

        decimal baseChance = CombatArithmetic.SaturatingAdd(
            CombatArithmetic.SaturatingDivide(
                CombatArithmetic.SaturatingSubtract(
                    request.Attacker.Stats.Luck,
                    request.Target.Stats.Luck),
                2m),
            _config.CriticalChanceBase);
        baseChance = CombatArithmetic.SaturatingAdd(
            baseChance,
            request.Target.Modifiers.CriticalChanceTakenBonus);
        if (request.Critical is ChanceCriticalDefinition chanceCritical)
        {
            baseChance = Math.Max(baseChance, chanceCritical.Chance);
        }

        baseChance = CombatArithmetic.SaturatingMultiply(
            baseChance,
            request.Attacker.Modifiers.CriticalChanceMultiplier);
        int clamped = ClampPercent(baseChance, _config.CriticalChanceMinimum, _config.CriticalChanceMaximum);
        return new ProductionCriticalCheckResult(RollPercent(clamped), clamped);
    }

    public int CalculateCriticalChance(ProductionCombatantProfile attacker, ProductionCombatantProfile target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        decimal chance = CombatArithmetic.SaturatingMultiply(
            CombatArithmetic.SaturatingAdd(
                CombatArithmetic.SaturatingDivide(
                    CombatArithmetic.SaturatingSubtract(attacker.Stats.Luck, target.Stats.Luck),
                    2m),
                _config.CriticalChanceBase),
            attacker.Modifiers.CriticalChanceMultiplier);
        return ClampPercent(chance, _config.CriticalChanceMinimum, _config.CriticalChanceMaximum);
    }

    public ProductionInstantDeathResult ResolveInstantDeath(ProductionInstantDeathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.BypassesResistance && request.Resistance == ResistanceLevel.Immune)
        {
            return new ProductionInstantDeathResult(false, 0);
        }

        decimal chance = CombatArithmetic.SaturatingAdd(
            request.BaseChance,
            CombatArithmetic.SaturatingSubtract(
                request.Attacker.Stats.Luck,
                request.Target.Stats.Luck));
        int clamped = ClampPercent(chance, _config.InstantDeathChanceMinimum, _config.InstantDeathChanceMaximum);
        return new ProductionInstantDeathResult(RollPercent(clamped), clamped);
    }

    public ProductionAilmentApplicationResult ResolveAilmentApplication(ProductionAilmentApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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

    public int CalculateExperienceYield(ProductionCombatantProfile enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);

        decimal level = enemy.Level;
        decimal levelCubed = CombatArithmetic.SaturatingMultiply(
            CombatArithmetic.SaturatingMultiply(level, level),
            level);
        decimal baseYield = CombatArithmetic.SaturatingDivide(
            CombatArithmetic.SaturatingMultiply(1.5m, levelCubed),
            _config.EnemiesPerLevelForExperience);
        decimal expectedStats = CombatArithmetic.SaturatingAdd(
            CombatArithmetic.SaturatingMultiply(level, _config.ExpectedStatLevelMultiplier),
            _config.ExpectedStatBase);
        decimal actualStats = CombatArithmetic.SaturatingSum(
        [
            enemy.Stats.Strength,
            enemy.Stats.Magic,
            enemy.Stats.Vitality,
            enemy.Stats.Agility,
            enemy.Stats.Luck
        ]);
        decimal statMultiplier = CombatArithmetic.SaturatingAdd(
            1m,
            Math.Max(0m, CombatArithmetic.SaturatingDivide(
                CombatArithmetic.SaturatingSubtract(actualStats, expectedStats),
                _config.StatDensityDivisor)));
        statMultiplier = Math.Min(_config.MaximumStatDensityMultiplier, statMultiplier);
        return Math.Max(1, CombatArithmetic.SaturatingFloorToInt(
            CombatArithmetic.SaturatingMultiply(baseYield, statMultiplier)));
    }

    public int CalculateCurrencyYield(ProductionCombatantProfile enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);

        decimal level = enemy.Level;
        decimal baseCurrency = CombatArithmetic.SaturatingMultiply(
            _config.CurrencyBaseMultiplier,
            CombatArithmetic.SaturatingMultiply(level, level));
        decimal luckBonus = CombatArithmetic.SaturatingMultiply(
            enemy.Stats.Luck,
            _config.CurrencyLuckMultiplier);
        decimal variance = RollVariance(_config.CurrencyVarianceMinimum, _config.CurrencyVarianceMaximum);
        return CombatArithmetic.SaturatingFloorToInt(CombatArithmetic.SaturatingMultiply(
            CombatArithmetic.SaturatingAdd(baseCurrency, luckBonus),
            variance));
    }

    public bool RollInitiative(decimal playerAverageAgility, decimal enemyAverageAgility)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(playerAverageAgility);
        ArgumentOutOfRangeException.ThrowIfNegative(enemyAverageAgility);
        decimal playerRoll = CombatArithmetic.SaturatingMultiply(
            playerAverageAgility,
            RollVariance(_config.InitiativeVarianceMinimum, _config.InitiativeVarianceMaximum));
        decimal enemyRoll = CombatArithmetic.SaturatingMultiply(
            enemyAverageAgility,
            RollVariance(_config.InitiativeVarianceMinimum, _config.InitiativeVarianceMaximum));
        return playerRoll >= enemyRoll;
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
        if (hits.Minimum <= 0 || hits.Maximum < hits.Minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(hits), "Hit counts must be positive and ordered.");
        }
        if (hits.Distribution == HitDistribution.Fixed && hits.Minimum != hits.Maximum)
        {
            throw new ArgumentException("Fixed hit counts require equal minimum and maximum values.", nameof(hits));
        }

        if (hits.Minimum == hits.Maximum || hits.Distribution == HitDistribution.Fixed)
        {
            return hits.Minimum;
        }

        int width = checked((int)(((long)hits.Maximum - hits.Minimum) + 1L));
        return checked(hits.Minimum + _random.NextInt32(0, width));
    }

    private bool RollPercent(int chance)
    {
        if (chance <= 0)
        {
            return false;
        }
        if (chance >= 100)
        {
            return true;
        }

        return CombatArithmetic.SaturatingMultiply(_random.NextUnitDecimal(), 100m) < chance;
    }

    private decimal RollVariance(decimal minimum, decimal maximum) =>
        CombatArithmetic.SaturatingAdd(
            minimum,
            CombatArithmetic.SaturatingMultiply(
                _random.NextUnitDecimal(),
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
