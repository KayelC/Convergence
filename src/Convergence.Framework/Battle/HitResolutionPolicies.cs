using Convergence.Content;
using Convergence.Execution;
using Convergence.Hosting;
using Convergence.Internal;

namespace Convergence.Battle;

public sealed record StandardHitResolutionPolicyConfig
{
    public decimal AttackerAgilityCoefficient { get; init; } = 2m;
    public decimal TargetAgilityCoefficient { get; init; } = 2m;
    public int MinimumChance { get; init; }
    public int MaximumChance { get; init; } = 100;

    public void Validate()
    {
        if (AttackerAgilityCoefficient < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AttackerAgilityCoefficient),
                AttackerAgilityCoefficient,
                "The attacker Agility coefficient cannot be negative.");
        }
        if (TargetAgilityCoefficient < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TargetAgilityCoefficient),
                TargetAgilityCoefficient,
                "The target Agility coefficient cannot be negative.");
        }
        if (MinimumChance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumChance),
                MinimumChance,
                "The minimum hit chance must be within 0-100.");
        }
        if (MaximumChance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumChance),
                MaximumChance,
                "The maximum hit chance must be within 0-100.");
        }
        if (MinimumChance > MaximumChance)
        {
            throw new ArgumentException("The minimum hit chance cannot exceed the maximum hit chance.");
        }
    }
}

public sealed class HitResolutionRequest
{
    public HitResolutionRequest(
        int authoredAccuracy,
        decimal attackerAgility,
        decimal targetAgility,
        decimal accuracyMultiplier = 1m,
        decimal evasionMultiplier = 1m,
        IEnumerable<NumericRuleModifierDefinition>? accuracyModifiers = null,
        IEnumerable<NumericRuleModifierDefinition>? evasionModifiers = null,
        bool targetIsRigid = false)
    {
        AuthoredPercentage.RequireValid(
            authoredAccuracy,
            nameof(authoredAccuracy),
            "Authored accuracy");
        if (attackerAgility < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attackerAgility),
                attackerAgility,
                "Attacker Agility cannot be negative.");
        }
        if (targetAgility < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetAgility),
                targetAgility,
                "Target Agility cannot be negative.");
        }
        if (accuracyMultiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(accuracyMultiplier),
                accuracyMultiplier,
                "The Accuracy multiplier cannot be negative.");
        }
        if (evasionMultiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evasionMultiplier),
                evasionMultiplier,
                "The Evasion multiplier cannot be negative.");
        }

        AuthoredAccuracy = authoredAccuracy;
        AttackerAgility = attackerAgility;
        TargetAgility = targetAgility;
        AccuracyMultiplier = accuracyMultiplier;
        EvasionMultiplier = evasionMultiplier;
        AccuracyModifiers = SnapshotModifiers(
            accuracyModifiers,
            NumericRuleModifierType.Accuracy,
            nameof(accuracyModifiers));
        EvasionModifiers = SnapshotModifiers(
            evasionModifiers,
            NumericRuleModifierType.Evasion,
            nameof(evasionModifiers));
        TargetIsRigid = targetIsRigid;
    }

    public int AuthoredAccuracy { get; }
    public decimal AttackerAgility { get; }
    public decimal TargetAgility { get; }
    public decimal AccuracyMultiplier { get; }
    public decimal EvasionMultiplier { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> AccuracyModifiers { get; }
    public IReadOnlyList<NumericRuleModifierDefinition> EvasionModifiers { get; }
    public bool TargetIsRigid { get; }

    private static IReadOnlyList<NumericRuleModifierDefinition> SnapshotModifiers(
        IEnumerable<NumericRuleModifierDefinition>? modifiers,
        NumericRuleModifierType expectedType,
        string parameterName)
    {
        NumericRuleModifierDefinition[] snapshot = modifiers?.ToArray() ?? [];
        if (snapshot.Any(modifier => modifier is null))
        {
            throw new ArgumentException("Hit modifier collections cannot contain null entries.", parameterName);
        }
        if (snapshot.Any(modifier => modifier.ModifierType != expectedType))
        {
            throw new ArgumentException(
                $"Every modifier in '{parameterName}' must use modifier type '{expectedType}'.",
                parameterName);
        }
        if (snapshot.Any(modifier => !Enum.IsDefined(modifier.Operation)))
        {
            throw new ArgumentException("Hit modifiers must use a defined operation.", parameterName);
        }
        if (snapshot.Any(modifier =>
                modifier.Operation == ModifierOperation.Multiply && modifier.Value <= 0m))
        {
            throw new ArgumentException("Multiplicative hit modifiers must be positive.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}

public sealed record HitResolutionResult(
    bool Hit,
    int AuthoredAccuracy,
    decimal AttackerAgilityContribution,
    decimal TargetAgilityContribution,
    decimal AccuracyScoreBeforeModifiers,
    decimal EvasionScoreBeforeModifiers,
    decimal ResolvedAccuracyScore,
    decimal ResolvedEvasionScore,
    decimal RawChance,
    int FinalChance,
    decimal? Roll,
    bool GuaranteedByRigidState = false);

public interface IHitResolutionPolicy
{
    HitResolutionResult Resolve(HitResolutionRequest request);
}

public sealed class StandardHitResolutionPolicy : IHitResolutionPolicy
{
    private readonly IRandomSource _random;
    private readonly StandardHitResolutionPolicyConfig _config;
    private readonly INumericModifierStackingPolicy _stacking;

    public StandardHitResolutionPolicy(
        IRandomSource random,
        StandardHitResolutionPolicyConfig? config = null,
        INumericModifierStackingPolicy? stacking = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _config = config ?? new StandardHitResolutionPolicyConfig();
        _config.Validate();
        _stacking = stacking ?? new AddThenMultiplyStackingPolicy();
    }

    public StandardHitResolutionPolicyConfig Config => _config;

    public HitResolutionResult Resolve(HitResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        decimal attackerAgilityContribution = CombatArithmetic.SaturatingMultiply(
            request.AttackerAgility,
            _config.AttackerAgilityCoefficient);
        decimal targetAgilityContribution = CombatArithmetic.SaturatingMultiply(
            request.TargetAgility,
            _config.TargetAgilityCoefficient);
        decimal accuracyBeforeModifiers = CombatArithmetic.SaturatingAdd(
            request.AuthoredAccuracy,
            attackerAgilityContribution);
        decimal evasionBeforeModifiers = targetAgilityContribution;

        IReadOnlyList<NumericRuleModifierDefinition> accuracyModifiers = WithMultiplier(
            request.AccuracyModifiers,
            NumericRuleModifierType.Accuracy,
            request.AccuracyMultiplier);
        IReadOnlyList<NumericRuleModifierDefinition> evasionModifiers = WithMultiplier(
            request.EvasionModifiers,
            NumericRuleModifierType.Evasion,
            request.EvasionMultiplier);
        decimal resolvedAccuracy = _stacking.Resolve(accuracyBeforeModifiers, accuracyModifiers);
        decimal resolvedEvasion = _stacking.Resolve(evasionBeforeModifiers, evasionModifiers);
        decimal rawChance = CombatArithmetic.SaturatingSubtract(resolvedAccuracy, resolvedEvasion);
        int finalChance = (int)Math.Clamp(
            Math.Floor(rawChance),
            _config.MinimumChance,
            _config.MaximumChance);

        if (request.TargetIsRigid)
        {
            return Result(true, 100, null, true);
        }
        if (finalChance == 0)
        {
            return Result(false, finalChance, null, false);
        }
        if (finalChance == 100)
        {
            return Result(true, finalChance, null, false);
        }

        decimal unit = RandomSourceContract.NextUnitDecimal(_random);
        decimal roll = CombatArithmetic.SaturatingMultiply(unit, 100m);
        return Result(roll < finalChance, finalChance, roll, false);

        HitResolutionResult Result(
            bool hit,
            int chance,
            decimal? roll,
            bool guaranteedByRigidState) =>
            new(
                hit,
                request.AuthoredAccuracy,
                attackerAgilityContribution,
                targetAgilityContribution,
                accuracyBeforeModifiers,
                evasionBeforeModifiers,
                resolvedAccuracy,
                resolvedEvasion,
                rawChance,
                chance,
                roll,
                guaranteedByRigidState);
    }

    private static IReadOnlyList<NumericRuleModifierDefinition> WithMultiplier(
        IReadOnlyList<NumericRuleModifierDefinition> modifiers,
        NumericRuleModifierType modifierType,
        decimal multiplier)
    {
        if (multiplier == 1m)
        {
            return modifiers;
        }

        return Array.AsReadOnly(modifiers
            .Append(new NumericRuleModifierDefinition(
                modifierType,
                ModifierOperation.Multiply,
                multiplier))
            .ToArray());
    }
}
