using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Battle;

public sealed record ProductionCombatRulesetConfig
{
    public decimal DamageFormulaScalar { get; init; } = 5.0m;
    public decimal DamageVarianceMinimum { get; init; } = 0.95m;
    public decimal DamageVarianceMaximum { get; init; } = 1.05m;
    public decimal ChargeMultiplier { get; init; } = 1.9m;
    public decimal CriticalDamageMultiplier { get; init; } = 1.5m;
    public decimal WeakDamageMultiplier { get; init; } = 1.5m;
    public decimal ResistDamageMultiplier { get; init; } = 0.5m;
    public decimal GuardDamageMultiplier { get; init; } = 0.5m;
    public int DefaultHitAccuracy { get; init; } = 95;
    public int HitChanceMinimum { get; init; } = 5;
    public int HitChanceMaximum { get; init; } = 99;
    public int CriticalChanceMinimum { get; init; } = 2;
    public int CriticalChanceMaximum { get; init; } = 40;
    public int CriticalChanceBase { get; init; } = 5;
    public int InstantDeathChanceMinimum { get; init; } = 5;
    public int InstantDeathChanceMaximum { get; init; } = 95;
    public int DefaultInstantDeathChance { get; init; } = 40;
    public decimal EnemiesPerLevelForExperience { get; init; } = 50m;
    public decimal ExpectedStatLevelMultiplier { get; init; } = 3m;
    public decimal ExpectedStatBase { get; init; } = 15m;
    public decimal StatDensityDivisor { get; init; } = 100m;
    public decimal MaximumStatDensityMultiplier { get; init; } = 2m;
    public decimal MaccaBaseMultiplier { get; init; } = 0.25m;
    public decimal MaccaLuckMultiplier { get; init; } = 5m;
    public decimal MaccaVarianceMinimum { get; init; } = 0.9m;
    public decimal MaccaVarianceMaximum { get; init; } = 1.1m;
    public decimal InitiativeVarianceMinimum { get; init; } = 0.9m;
    public decimal InitiativeVarianceMaximum { get; init; } = 1.1m;
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
    bool IsRigidBody = false,
    bool HasPhysicalCharge = false,
    bool HasMagicalCharge = false);

public sealed record ProductionCombatModifiers(
    decimal DamageDealtMultiplier = 1m,
    decimal DamageTakenMultiplier = 1m,
    decimal HitMultiplier = 1m,
    decimal EvasionMultiplier = 1m,
    decimal CriticalChanceMultiplier = 1m,
    int CriticalChanceTakenBonus = 0);

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
    }

    public int Level { get; }
    public ProductionCombatStats Stats { get; }
    public ProductionCombatStatus Status { get; }
    public ProductionCombatModifiers Modifiers { get; }
}

public sealed record ProductionHitCheckRequest(
    ProductionCombatantProfile Attacker,
    ProductionCombatantProfile Target,
    int BaseAccuracy);

public sealed record ProductionHitCheckResult(bool Hit, int Chance);

public sealed record ProductionCriticalCheckRequest(
    ProductionCombatantProfile Attacker,
    ProductionCombatantProfile Target,
    DamageElement Element,
    CriticalDefinition Critical);

public sealed record ProductionCriticalCheckResult(bool Critical, int Chance);

public sealed record ProductionRawDamageRequest(
    ProductionCombatantProfile Attacker,
    ProductionCombatantProfile Target,
    int Power,
    DamageElement Element,
    bool AllowCritical = true);

public sealed record ProductionRawDamageResult(decimal Damage, bool Critical);

public sealed record ProductionDamageApplicationRequest(
    ProductionCombatantProfile Target,
    decimal Damage,
    DamageElement Element,
    ElementalAffinity Affinity,
    bool Critical);

public sealed record ProductionDamageApplicationResult(
    decimal DamageDealt,
    decimal Recovered,
    ElementalAffinity Affinity,
    bool Critical,
    PressTurnOutcome Outcome,
    string Message);

public sealed record ProductionDamageResolutionRequest(
    ProductionCombatantProfile Attacker,
    ProductionCombatantProfile Target,
    DamageElement Element,
    ElementalAffinity Affinity,
    int Power,
    int Accuracy,
    CriticalDefinition Critical,
    HitCountDefinition Hits);

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
        ElementalAffinity affinity)
    {
        Hits = Array.AsReadOnly((hits ?? throw new ArgumentNullException(nameof(hits))).ToArray());
        Affinity = affinity;
    }

    public IReadOnlyList<ProductionDamageResolutionHit> Hits { get; }
    public ElementalAffinity Affinity { get; }
    public decimal TotalDamage => Hits.Where(hit => hit.Hit).Sum(hit => hit.Damage);
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

    public ProductionCombatRuleset(
        IRandomSource random,
        ProductionCombatRulesetConfig? config = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _config = config ?? new ProductionCombatRulesetConfig();
    }

    public ProductionCombatRulesetConfig Config => _config;

    public IReadOnlyList<DamageHitResolution> Resolve(DamagePolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProductionDamageResolutionResult result = ResolveDamage(new ProductionDamageResolutionRequest(
            FromRuntimeActor(request.Actor),
            FromRuntimeActor(request.Target),
            request.Effect.Element,
            request.Affinity,
            request.Effect.Power,
            request.Effect.Accuracy,
            request.Effect.Critical,
            request.Effect.Hits));

        return Array.AsReadOnly(result.Hits
            .Select(hit => new DamageHitResolution(hit.Hit, hit.Damage, hit.Critical))
            .ToArray());
    }

    public bool ShouldDefeat(InstantDeathPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProductionInstantDeathResult result = ResolveInstantDeath(new ProductionInstantDeathRequest(
            FromRuntimeActor(request.Actor),
            FromRuntimeActor(request.Target),
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
                FromRuntimeActor(request.Actor),
                FromRuntimeActor(request.Target),
                request.Effect.Chance,
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

        int hitCount = ResolveHitCount(request.Hits);
        List<ProductionDamageResolutionHit> hits = new(hitCount);
        for (int i = 0; i < hitCount; i++)
        {
            ProductionHitCheckResult hit = CheckHit(new ProductionHitCheckRequest(
                request.Attacker,
                request.Target,
                request.Accuracy));
            if (!hit.Hit)
            {
                hits.Add(new ProductionDamageResolutionHit(false, 0m, false, hit.Chance, 0));
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
            damage *= request.Target.Modifiers.DamageTakenMultiplier;
            if (critical.Critical)
            {
                damage *= _config.CriticalDamageMultiplier;
            }
            if (request.Target.Status.IsGuarding)
            {
                damage *= _config.GuardDamageMultiplier;
            }

            damage = ApplyAffinityMultiplier(damage, NormalizeGuardedAffinity(
                request.Affinity,
                request.Target.Status.IsGuarding));
            hits.Add(new ProductionDamageResolutionHit(
                true,
                Math.Floor(damage * RollVariance(_config.DamageVarianceMinimum, _config.DamageVarianceMaximum)),
                critical.Critical,
                hit.Chance,
                critical.Chance));
        }

        return new ProductionDamageResolutionResult(hits, request.Affinity);
    }

    public ProductionRawDamageResult CalculateRawDamage(ProductionRawDamageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool critical = false;
        decimal damage = CalculateBaseDamage(
            request.Attacker,
            request.Target,
            request.Power,
            request.Element);
        damage *= request.Target.Modifiers.DamageTakenMultiplier;

        if (request.AllowCritical)
        {
            ProductionCriticalCheckResult criticalResult = CheckCritical(new ProductionCriticalCheckRequest(
                request.Attacker,
                request.Target,
                request.Element,
                new ChanceCriticalDefinition(_config.CriticalChanceBase)));
            critical = criticalResult.Critical;
            if (critical)
            {
                damage *= _config.CriticalDamageMultiplier;
            }
        }

        damage *= RollVariance(_config.DamageVarianceMinimum, _config.DamageVarianceMaximum);
        return new ProductionRawDamageResult(Math.Floor(damage), critical);
    }

    public ProductionDamageApplicationResult ApplyDamage(ProductionDamageApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ElementalAffinity affinity = NormalizeGuardedAffinity(
            request.Affinity,
            request.Target.Status.IsGuarding);
        decimal damage = request.Damage;
        bool critical = request.Critical;
        if (request.Target.Status.IsGuarding)
        {
            damage *= _config.GuardDamageMultiplier;
            critical = false;
        }
        if (request.Target.Status.IsRigidBody && IsPhysical(request.Element))
        {
            critical = true;
        }
        if (critical)
        {
            damage *= _config.CriticalDamageMultiplier;
        }

        damage = Math.Floor(damage);
        return affinity switch
        {
            ElementalAffinity.Weak => new ProductionDamageApplicationResult(
                Math.Floor(damage * _config.WeakDamageMultiplier),
                0m,
                affinity,
                critical,
                PressTurnOutcome.Weakness,
                "WEAKNESS STRUCK!"),
            ElementalAffinity.Resist => new ProductionDamageApplicationResult(
                Math.Floor(damage * _config.ResistDamageMultiplier),
                0m,
                affinity,
                critical,
                PressTurnOutcome.Normal,
                critical ? "CRITICAL (Resisted)!" : "Resisted."),
            ElementalAffinity.Null => new ProductionDamageApplicationResult(
                0m,
                0m,
                affinity,
                critical,
                PressTurnOutcome.Null,
                "Blocked!"),
            ElementalAffinity.Repel => new ProductionDamageApplicationResult(
                0m,
                0m,
                affinity,
                critical,
                PressTurnOutcome.Repel,
                "Repelled!"),
            ElementalAffinity.Absorb => new ProductionDamageApplicationResult(
                0m,
                damage,
                affinity,
                critical,
                PressTurnOutcome.Absorb,
                $"Absorbed {damage} HP!"),
            _ => new ProductionDamageApplicationResult(
                damage,
                0m,
                affinity,
                critical,
                critical ? PressTurnOutcome.Critical : PressTurnOutcome.Normal,
                critical ? "CRITICAL HIT!" : string.Empty)
        };
    }

    public ProductionHitCheckResult CheckHit(ProductionHitCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Target.Status.IsRigidBody)
        {
            return new ProductionHitCheckResult(true, _config.HitChanceMaximum);
        }

        decimal attackerAgility = request.Attacker.Stats.Agility * request.Attacker.Modifiers.HitMultiplier;
        decimal targetAgility = request.Target.Stats.Agility * request.Target.Modifiers.EvasionMultiplier;
        decimal chance = request.BaseAccuracy +
            ((attackerAgility - targetAgility) * 2m) +
            (request.Attacker.Stats.Luck - request.Target.Stats.Luck);
        int clamped = ClampPercent(chance, _config.HitChanceMinimum, _config.HitChanceMaximum);
        return new ProductionHitCheckResult(RollPercent(clamped), clamped);
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

        decimal baseChance = ((request.Attacker.Stats.Luck - request.Target.Stats.Luck) / 2m) +
            _config.CriticalChanceBase +
            request.Target.Modifiers.CriticalChanceTakenBonus;
        if (request.Critical is ChanceCriticalDefinition chanceCritical)
        {
            baseChance = Math.Max(baseChance, chanceCritical.Chance);
        }

        baseChance *= request.Attacker.Modifiers.CriticalChanceMultiplier;
        int clamped = ClampPercent(baseChance, _config.CriticalChanceMinimum, _config.CriticalChanceMaximum);
        return new ProductionCriticalCheckResult(RollPercent(clamped), clamped);
    }

    public int CalculateCriticalChance(ProductionCombatantProfile attacker, ProductionCombatantProfile target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        decimal chance = (((attacker.Stats.Luck - target.Stats.Luck) / 2m) + _config.CriticalChanceBase) *
            attacker.Modifiers.CriticalChanceMultiplier;
        return ClampPercent(chance, _config.CriticalChanceMinimum, _config.CriticalChanceMaximum);
    }

    public ProductionInstantDeathResult ResolveInstantDeath(ProductionInstantDeathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.BypassesResistance && request.Resistance == ResistanceLevel.Immune)
        {
            return new ProductionInstantDeathResult(false, 0);
        }

        decimal chance = request.BaseChance +
            (request.Attacker.Stats.Luck - request.Target.Stats.Luck);
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
        chance *= request.Resistance switch
        {
            ResistanceLevel.Vulnerable => 1.25m,
            ResistanceLevel.Resistant => 0.5m,
            _ => 1m
        };
        int clamped = ClampPercent(chance, 0, 100);
        return new ProductionAilmentApplicationResult(RollPercent(clamped), clamped);
    }

    public int CalculateExperienceYield(ProductionCombatantProfile enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);

        decimal baseYield = (1.5m * (decimal)Math.Pow(enemy.Level, 3)) / _config.EnemiesPerLevelForExperience;
        decimal expectedStats = (enemy.Level * _config.ExpectedStatLevelMultiplier) + _config.ExpectedStatBase;
        decimal actualStats =
            enemy.Stats.Strength +
            enemy.Stats.Magic +
            enemy.Stats.Vitality +
            enemy.Stats.Agility +
            enemy.Stats.Luck;
        decimal statMultiplier = 1m +
            Math.Max(0m, (actualStats - expectedStats) / _config.StatDensityDivisor);
        statMultiplier = Math.Min(_config.MaximumStatDensityMultiplier, statMultiplier);
        return Math.Max(1, (int)Math.Floor(baseYield * statMultiplier));
    }

    public int CalculateMaccaYield(ProductionCombatantProfile enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);

        decimal baseMacca = _config.MaccaBaseMultiplier * (decimal)Math.Pow(enemy.Level, 2);
        decimal luckBonus = enemy.Stats.Luck * _config.MaccaLuckMultiplier;
        decimal variance = RollVariance(_config.MaccaVarianceMinimum, _config.MaccaVarianceMaximum);
        return (int)Math.Floor((baseMacca + luckBonus) * variance);
    }

    public bool RollInitiative(decimal playerAverageAgility, decimal enemyAverageAgility)
    {
        decimal playerRoll = playerAverageAgility *
            RollVariance(_config.InitiativeVarianceMinimum, _config.InitiativeVarianceMaximum);
        decimal enemyRoll = enemyAverageAgility *
            RollVariance(_config.InitiativeVarianceMinimum, _config.InitiativeVarianceMaximum);
        return playerRoll >= enemyRoll;
    }

    private decimal CalculateBaseDamage(
        ProductionCombatantProfile attacker,
        ProductionCombatantProfile target,
        int power,
        DamageElement element)
    {
        decimal attack = IsPhysical(element) ? attacker.Stats.Strength : attacker.Stats.Magic;
        decimal defense = Math.Max(1m, target.Stats.Vitality + target.Stats.Defense);
        attack *= attacker.Modifiers.DamageDealtMultiplier;
        if (IsPhysical(element) && attacker.Status.HasPhysicalCharge)
        {
            attack *= _config.ChargeMultiplier;
        }
        else if (!IsPhysical(element) && attacker.Status.HasMagicalCharge)
        {
            attack *= _config.ChargeMultiplier;
        }

        decimal ratio = attack / defense;
        return _config.DamageFormulaScalar * (decimal)Math.Sqrt((double)(power * ratio));
    }

    private decimal ApplyAffinityMultiplier(decimal damage, ElementalAffinity affinity) => affinity switch
    {
        ElementalAffinity.Weak => damage * _config.WeakDamageMultiplier,
        ElementalAffinity.Resist => damage * _config.ResistDamageMultiplier,
        _ => damage
    };

    private int ResolveHitCount(HitCountDefinition hits)
    {
        if (hits.Minimum == hits.Maximum || hits.Distribution == HitDistribution.Fixed)
        {
            return hits.Minimum;
        }

        return _random.NextInt32(hits.Minimum, hits.Maximum + 1);
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

        return _random.NextUnitDecimal() * 100m < chance;
    }

    private decimal RollVariance(decimal minimum, decimal maximum) =>
        minimum + ((_random.NextUnitDecimal()) * (maximum - minimum));

    private static ElementalAffinity NormalizeGuardedAffinity(ElementalAffinity affinity, bool isGuarding) =>
        isGuarding && affinity == ElementalAffinity.Weak ? ElementalAffinity.Normal : affinity;

    private static bool IsPhysical(DamageElement element) => element == DamageElement.Physical;

    private static int ClampPercent(decimal chance, int minimum, int maximum) =>
        (int)Math.Clamp(Math.Floor(chance), minimum, maximum);

    private static ProductionCombatantProfile FromRuntimeActor(RuntimeActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        decimal strength = actor.Stats.GetValueOrDefault(StandardProgressionIds.Strength);
        decimal magic = actor.Stats.GetValueOrDefault(StandardProgressionIds.Magic);
        decimal vitality = actor.Stats.GetValueOrDefault(StandardProgressionIds.Vitality);
        decimal agility = actor.Stats.GetValueOrDefault(StandardProgressionIds.Agility);
        decimal luck = actor.Stats.GetValueOrDefault(StandardProgressionIds.Luck);
        bool physicalCharge = actor.Charges.ContainsKey(ChargeKind.Physical);
        bool magicalCharge = actor.Charges.ContainsKey(ChargeKind.Magical);
        decimal damageDealt = 1m;
        decimal damageTaken = 1m;
        decimal evasion = 1m;
        int criticalTakenBonus = 0;
        bool rigid = false;

        foreach (ActiveAilmentState ailment in actor.Ailments.Values)
        {
            damageDealt *= ailment.Definition.Modifiers.DamageDealtMultiplier;
            damageTaken *= ailment.Definition.Modifiers.DamageTakenMultiplier;
            evasion *= ailment.Definition.Modifiers.EvasionMultiplier;
            criticalTakenBonus += ailment.Definition.Modifiers.CriticalChanceTakenBonus;
            rigid |= ailment.Definition.Modifiers.IsRigidBody;
        }

        return new ProductionCombatantProfile(
            level: 1,
            new ProductionCombatStats(strength, magic, vitality, agility, luck),
            new ProductionCombatStatus(
                IsGuarding: actor.IsGuarding,
                IsRigidBody: rigid,
                HasPhysicalCharge: physicalCharge,
                HasMagicalCharge: magicalCharge),
            new ProductionCombatModifiers(
                DamageDealtMultiplier: damageDealt,
                DamageTakenMultiplier: damageTaken,
                EvasionMultiplier: evasion,
                CriticalChanceTakenBonus: criticalTakenBonus));
    }
}
