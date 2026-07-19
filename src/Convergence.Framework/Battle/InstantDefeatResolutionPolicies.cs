using Convergence.Content;
using Convergence.Hosting;

namespace Convergence.Battle;

public enum InstantDefeatResolutionReason
{
    Defeated,
    ProbabilityFailed,
    ResistanceBlocked
}

public sealed record StandardInstantDefeatResolutionPolicyConfig
{
    public decimal VulnerableMultiplier { get; init; } = 1.5m;
    public decimal NormalMultiplier { get; init; } = 1m;
    public decimal ResistantMultiplier { get; init; } = 0.5m;
    public decimal ImmuneMultiplier { get; init; }
    public int MinimumChance { get; init; }
    public int MaximumChance { get; init; } = 100;

    internal void Validate()
    {
        RequireNonNegative(VulnerableMultiplier, nameof(VulnerableMultiplier));
        RequireNonNegative(NormalMultiplier, nameof(NormalMultiplier));
        RequireNonNegative(ResistantMultiplier, nameof(ResistantMultiplier));
        RequireNonNegative(ImmuneMultiplier, nameof(ImmuneMultiplier));
        if (MinimumChance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumChance),
                MinimumChance,
                "Minimum chance must be within 0-100.");
        }
        if (MaximumChance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumChance),
                MaximumChance,
                "Maximum chance must be within 0-100.");
        }
        if (MinimumChance > MaximumChance)
        {
            throw new ArgumentException("Minimum chance cannot exceed maximum chance.");
        }
    }

    private static void RequireNonNegative(decimal value, string name)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(name, value, "Resistance multipliers cannot be negative.");
        }
    }
}

public sealed record InstantDefeatResolutionRequest
{
    public InstantDefeatResolutionRequest(
        int authoredChance,
        ResistanceLevel? resistance,
        bool bypassesResistance = false)
    {
        if (authoredChance is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoredChance),
                authoredChance,
                "Authored instant-defeat chance must be within 0-100.");
        }
        if (resistance is ResistanceLevel value && !Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resistance),
                value,
                "Resistance must be defined.");
        }
        if (!bypassesResistance && resistance is null)
        {
            throw new ArgumentException(
                "A resistance level is required unless resistance is explicitly bypassed.",
                nameof(resistance));
        }

        AuthoredChance = authoredChance;
        Resistance = resistance;
        BypassesResistance = bypassesResistance;
    }

    public int AuthoredChance { get; }
    public ResistanceLevel? Resistance { get; }
    public bool BypassesResistance { get; }
}

public sealed record InstantDefeatResolutionResult(
    bool Defeated,
    int AuthoredChance,
    ResistanceLevel? Resistance,
    bool BypassedResistance,
    decimal ResistanceMultiplier,
    decimal ResolvedChance,
    int FinalChance,
    decimal? Roll,
    InstantDefeatResolutionReason Reason);

public interface IInstantDefeatResolutionPolicy
{
    InstantDefeatResolutionResult Resolve(InstantDefeatResolutionRequest request);
}

public sealed class StandardInstantDefeatResolutionPolicy : IInstantDefeatResolutionPolicy
{
    private readonly IRandomSource _random;
    private readonly StandardInstantDefeatResolutionPolicyConfig _config;

    public StandardInstantDefeatResolutionPolicy(
        IRandomSource random,
        StandardInstantDefeatResolutionPolicyConfig? config = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _config = config ?? new StandardInstantDefeatResolutionPolicyConfig();
        _config.Validate();
    }

    public InstantDefeatResolutionResult Resolve(InstantDefeatResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        decimal multiplier = request.BypassesResistance
            ? 1m
            : MultiplierFor(request.Resistance!.Value);
        decimal resolvedChance = CombatArithmetic.SaturatingMultiply(
            request.AuthoredChance,
            multiplier);
        if (!request.BypassesResistance &&
            request.Resistance == ResistanceLevel.Immune &&
            multiplier == 0m)
        {
            return new InstantDefeatResolutionResult(
                false,
                request.AuthoredChance,
                request.Resistance,
                false,
                multiplier,
                resolvedChance,
                0,
                null,
                InstantDefeatResolutionReason.ResistanceBlocked);
        }

        int finalChance = (int)Math.Clamp(
            Math.Floor(resolvedChance),
            _config.MinimumChance,
            _config.MaximumChance);
        if (finalChance == 0)
        {
            return Result(false, null, InstantDefeatResolutionReason.ProbabilityFailed);
        }
        if (finalChance == 100)
        {
            return Result(true, null, InstantDefeatResolutionReason.Defeated);
        }

        decimal unit = _random.NextUnitDecimal();
        if (unit is < 0m or >= 1m)
        {
            throw new InvalidOperationException("Random sources must return unit decimals within [0, 1).");
        }
        decimal roll = CombatArithmetic.SaturatingMultiply(unit, 100m);
        bool defeated = roll < finalChance;
        return Result(
            defeated,
            roll,
            defeated
                ? InstantDefeatResolutionReason.Defeated
                : InstantDefeatResolutionReason.ProbabilityFailed);

        InstantDefeatResolutionResult Result(
            bool defeated,
            decimal? roll,
            InstantDefeatResolutionReason reason) =>
            new(
                defeated,
                request.AuthoredChance,
                request.Resistance,
                request.BypassesResistance,
                multiplier,
                resolvedChance,
                finalChance,
                roll,
                reason);
    }

    private decimal MultiplierFor(ResistanceLevel resistance) => resistance switch
    {
        ResistanceLevel.Vulnerable => _config.VulnerableMultiplier,
        ResistanceLevel.Normal => _config.NormalMultiplier,
        ResistanceLevel.Resistant => _config.ResistantMultiplier,
        ResistanceLevel.Immune => _config.ImmuneMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(resistance), resistance, "Resistance must be defined.")
    };
}
