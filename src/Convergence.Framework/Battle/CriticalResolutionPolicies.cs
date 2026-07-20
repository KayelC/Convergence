using Convergence.Content;
using Convergence.Execution;
using Convergence.Hosting;
using Convergence.Internal;

namespace Convergence.Battle;

public enum CriticalEligibilityReason
{
    Eligible,
    DefinitionDisallowsCritical,
    DamageElementIneligible,
    TargetGuarding
}

public sealed record CriticalEligibilityRequest(
    DamageElement Element,
    CriticalDefinition Critical,
    bool TargetIsGuarding = false,
    bool TargetIsRigid = false);

public sealed record CriticalEligibilityResult(
    bool Eligible,
    CriticalEligibilityReason Reason,
    bool GuaranteedByRigidState = false);

public interface ICriticalEligibilityPolicy
{
    CriticalEligibilityResult Assess(CriticalEligibilityRequest request);
}

public sealed class PhysicalOnlyCriticalEligibilityPolicy : ICriticalEligibilityPolicy
{
    public CriticalEligibilityResult Assess(CriticalEligibilityRequest request)
    {
        Validate(request);
        if (request.Critical is NeverCriticalDefinition)
        {
            return Rejected(CriticalEligibilityReason.DefinitionDisallowsCritical);
        }
        if (request.Element != DamageElement.Physical)
        {
            return Rejected(CriticalEligibilityReason.DamageElementIneligible);
        }
        if (request.TargetIsGuarding)
        {
            return Rejected(CriticalEligibilityReason.TargetGuarding);
        }

        return new CriticalEligibilityResult(
            true,
            CriticalEligibilityReason.Eligible,
            request.TargetIsRigid);
    }

    private static CriticalEligibilityResult Rejected(CriticalEligibilityReason reason) =>
        new(false, reason);

    internal static void Validate(CriticalEligibilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Critical);
        if (!Enum.IsDefined(request.Element))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Element,
                "The critical eligibility element must be defined.");
        }
        if (!Enum.IsDefined(request.Critical.Mode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Critical.Mode,
                "The critical definition mode must be defined.");
        }
    }
}

public sealed class AllDamageCriticalEligibilityPolicy : ICriticalEligibilityPolicy
{
    public CriticalEligibilityResult Assess(CriticalEligibilityRequest request)
    {
        PhysicalOnlyCriticalEligibilityPolicy.Validate(request);
        if (request.Critical is NeverCriticalDefinition)
        {
            return new CriticalEligibilityResult(
                false,
                CriticalEligibilityReason.DefinitionDisallowsCritical);
        }
        if (request.TargetIsGuarding)
        {
            return new CriticalEligibilityResult(false, CriticalEligibilityReason.TargetGuarding);
        }

        return new CriticalEligibilityResult(
            true,
            CriticalEligibilityReason.Eligible,
            request.TargetIsRigid);
    }
}

public sealed class CriticalChanceRequest
{
    public CriticalChanceRequest(
        CriticalDefinition critical,
        int authoredAccuracy,
        int finalHitChance,
        decimal criticalChanceMultiplier = 1m,
        int targetCriticalChanceBonus = 0,
        IEnumerable<NumericRuleModifierDefinition>? criticalChanceModifiers = null)
    {
        Critical = critical ?? throw new ArgumentNullException(nameof(critical));
        if (critical is ChanceCriticalDefinition chance && chance.Chance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(critical),
                chance.Chance,
                "Authored critical chance must be within 0-100.");
        }
        if (authoredAccuracy is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoredAccuracy),
                authoredAccuracy,
                "Authored accuracy must be within 0-100.");
        }
        if (finalHitChance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalHitChance),
                finalHitChance,
                "Final hit chance must be within 0-100.");
        }
        if (criticalChanceMultiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(criticalChanceMultiplier),
                criticalChanceMultiplier,
                "Critical chance multiplier cannot be negative.");
        }

        AuthoredAccuracy = authoredAccuracy;
        FinalHitChance = finalHitChance;
        CriticalChanceMultiplier = criticalChanceMultiplier;
        TargetCriticalChanceBonus = targetCriticalChanceBonus;
        CriticalChanceModifiers = SnapshotModifiers(criticalChanceModifiers);
    }

    public CriticalDefinition Critical { get; }
    public int AuthoredAccuracy { get; }
    public int FinalHitChance { get; }
    public decimal CriticalChanceMultiplier { get; }
    public int TargetCriticalChanceBonus { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> CriticalChanceModifiers { get; }

    private static IReadOnlyList<NumericRuleModifierDefinition> SnapshotModifiers(
        IEnumerable<NumericRuleModifierDefinition>? modifiers)
    {
        NumericRuleModifierDefinition[] snapshot = modifiers?.ToArray() ?? [];
        if (snapshot.Any(modifier => modifier is null))
        {
            throw new ArgumentException(
                "Critical chance modifiers cannot contain null entries.",
                nameof(modifiers));
        }
        if (snapshot.Any(modifier => modifier.ModifierType != NumericRuleModifierType.CriticalChance))
        {
            throw new ArgumentException(
                "Every critical modifier must use the CriticalChance modifier type.",
                nameof(modifiers));
        }
        if (snapshot.Any(modifier => !Enum.IsDefined(modifier.Operation)))
        {
            throw new ArgumentException(
                "Critical chance modifiers must use a defined operation.",
                nameof(modifiers));
        }
        if (snapshot.Any(modifier =>
                modifier.Operation == ModifierOperation.Multiply && modifier.Value <= 0m))
        {
            throw new ArgumentException(
                "Multiplicative critical chance modifiers must be positive.",
                nameof(modifiers));
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record CriticalChanceResult(
    int AuthoredChance,
    decimal PolicyBaseChance,
    decimal ResolvedChance,
    int FinalChance,
    decimal? Roll,
    bool Critical);

public interface ICriticalChancePolicy
{
    CriticalChanceResult Resolve(CriticalChanceRequest request);
}

public sealed class AuthoredCriticalChancePolicy : CriticalChancePolicyBase
{
    public AuthoredCriticalChancePolicy(
        IRandomSource random,
        INumericModifierStackingPolicy? stacking = null)
        : base(random, stacking)
    {
    }

    protected override decimal ResolveBaseChance(CriticalChanceRequest request) =>
        AuthoredChance(request.Critical);
}

public sealed class AccuracyScaledCriticalChancePolicy : CriticalChancePolicyBase
{
    public AccuracyScaledCriticalChancePolicy(
        IRandomSource random,
        INumericModifierStackingPolicy? stacking = null)
        : base(random, stacking)
    {
    }

    protected override decimal ResolveBaseChance(CriticalChanceRequest request)
    {
        int authoredChance = AuthoredChance(request.Critical);
        if (authoredChance == 0 || request.AuthoredAccuracy == 0)
        {
            return 0m;
        }

        return CombatArithmetic.SaturatingMultiply(
            authoredChance,
            CombatArithmetic.SaturatingDivide(
                request.FinalHitChance,
                request.AuthoredAccuracy));
    }
}

public abstract class CriticalChancePolicyBase : ICriticalChancePolicy
{
    private readonly IRandomSource _random;
    private readonly INumericModifierStackingPolicy _stacking;

    protected CriticalChancePolicyBase(
        IRandomSource random,
        INumericModifierStackingPolicy? stacking)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _stacking = stacking ?? new AddThenMultiplyStackingPolicy();
    }

    public CriticalChanceResult Resolve(CriticalChanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        int authoredChance = AuthoredChance(request.Critical);
        decimal baseChance = ResolveBaseChance(request);
        IReadOnlyList<NumericRuleModifierDefinition> modifiers = Array.AsReadOnly(
            request.CriticalChanceModifiers
                .Prepend(new NumericRuleModifierDefinition(
                    NumericRuleModifierType.CriticalChance,
                    ModifierOperation.Add,
                    request.TargetCriticalChanceBonus))
                .Append(new NumericRuleModifierDefinition(
                    NumericRuleModifierType.CriticalChance,
                    ModifierOperation.Multiply,
                    request.CriticalChanceMultiplier))
                .ToArray());
        decimal resolvedChance = _stacking.Resolve(baseChance, modifiers);
        int finalChance = (int)Math.Clamp(Math.Floor(resolvedChance), 0m, 100m);

        if (finalChance == 0)
        {
            return Result(null, false);
        }
        if (finalChance == 100)
        {
            return Result(null, true);
        }

        decimal unit = RandomSourceContract.NextUnitDecimal(_random);
        decimal roll = CombatArithmetic.SaturatingMultiply(unit, 100m);
        return Result(roll, roll < finalChance);

        CriticalChanceResult Result(decimal? roll, bool critical) =>
            new(authoredChance, baseChance, resolvedChance, finalChance, roll, critical);
    }

    protected abstract decimal ResolveBaseChance(CriticalChanceRequest request);

    protected static int AuthoredChance(CriticalDefinition critical) => critical switch
    {
        NeverCriticalDefinition => 0,
        ChanceCriticalDefinition chance => chance.Chance,
        _ => throw new ArgumentException(
            $"Unsupported critical definition '{critical.GetType().Name}'.",
            nameof(critical))
    };
}
