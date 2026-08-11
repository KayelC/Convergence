using Convergence.Content;

namespace Convergence.Runtime;

/// <summary>Stable IDs for the pricing policies supplied with Convergence.</summary>
public static class StandardShopPricingPolicyIds
{
    public static ContentId Standard { get; } = ContentId.Parse("standard_shop_pricing");
    public static ContentId LuckAdjusted { get; } = ContentId.Parse("luck_adjusted_shop_pricing");
}

public enum ShopPriceOperation
{
    Purchase,
    Resale
}

public enum ShopPriceCalculationCode
{
    Applied,
    NegativePurchasePrice,
    NegativeLuck,
    NumericOverflow,
    PolicyRejected
}

public sealed record ShopPriceCalculationRequest
{
    public ShopPriceCalculationRequest(
        int authoredPurchasePrice,
        int luck,
        ShopPriceOperation operation)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Shop price operation is not supported.");
        }

        AuthoredPurchasePrice = authoredPurchasePrice;
        Luck = luck;
        Operation = operation;
    }

    public int AuthoredPurchasePrice { get; }
    public int Luck { get; }
    public ShopPriceOperation Operation { get; }
}

public sealed record ShopPriceCalculationResult
{
    public ShopPriceCalculationResult(
        ShopPriceCalculationCode code,
        int price = 0,
        string? message = null)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Shop price result code is not supported.");
        }
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "Shop prices cannot be negative.");
        }
        if (code != ShopPriceCalculationCode.Applied && price != 0)
        {
            throw new ArgumentException("Rejected shop pricing cannot carry a price.", nameof(price));
        }

        Code = code;
        Price = price;
        Message = string.IsNullOrWhiteSpace(message) ? null : message;
    }

    public ShopPriceCalculationCode Code { get; }
    public int Price { get; }
    public string? Message { get; }
    public bool IsSuccess => Code == ShopPriceCalculationCode.Applied;

    public static ShopPriceCalculationResult Applied(int price) =>
        new(ShopPriceCalculationCode.Applied, price);

    public static ShopPriceCalculationResult Rejected(
        ShopPriceCalculationCode code,
        string message) =>
        new(code, message: message);
}

/// <summary>Calculates purchase and resale prices from one authored purchase price.</summary>
public interface IShopPricingPolicy
{
    ShopPriceCalculationResult Calculate(ShopPriceCalculationRequest request);
}

/// <summary>
/// Preserves the authored purchase price and derives resale through a configurable percentage.
/// Nonnegative fractional resale values are truncated toward zero.
/// </summary>
public sealed class StandardShopPricingPolicy : IShopPricingPolicy
{
    public const decimal DefaultResalePercentage = 0.50m;

    public StandardShopPricingPolicy(decimal resalePercentage = DefaultResalePercentage)
    {
        if (resalePercentage < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resalePercentage),
                resalePercentage,
                "Shop resale percentage cannot be negative.");
        }

        ResalePercentage = resalePercentage;
    }

    public decimal ResalePercentage { get; }

    public ShopPriceCalculationResult Calculate(ShopPriceCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ShopPriceCalculationResult? invalid = ShopPricingArithmetic.ValidateInputs(request);
        if (invalid is not null)
        {
            return invalid;
        }

        return request.Operation == ShopPriceOperation.Purchase
            ? ShopPriceCalculationResult.Applied(request.AuthoredPurchasePrice)
            : ShopPricingArithmetic.ApplyMultiplier(
                request,
                ResalePercentage,
                "standard resale");
    }
}

/// <summary>Supplies the former optional Luck-sensitive purchase and resale formula.</summary>
public sealed class LuckAdjustedShopPricingPolicy : IShopPricingPolicy
{
    public const decimal MinimumPurchaseMultiplier = 0.50m;
    public const decimal BaseResaleMultiplier = 0.50m;
    public const decimal LuckPriceStep = 0.01m;

    public ShopPriceCalculationResult Calculate(ShopPriceCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ShopPriceCalculationResult? invalid = ShopPricingArithmetic.ValidateInputs(request);
        if (invalid is not null)
        {
            return invalid;
        }

        decimal multiplier = request.Operation == ShopPriceOperation.Purchase
            ? Math.Max(MinimumPurchaseMultiplier, 1m - (request.Luck * LuckPriceStep))
            : BaseResaleMultiplier + (request.Luck * LuckPriceStep);
        return ShopPricingArithmetic.ApplyMultiplier(
            request,
            multiplier,
            "Luck-adjusted");
    }
}

public enum ShopPricingPolicyDiagnosticCode
{
    UnsupportedPolicy,
    MissingParameter,
    UnknownParameter,
    InvalidParameterType,
    InvalidParameterValue,
    PolicyFactoryFailure
}

public sealed record ShopPricingPolicyDiagnostic(
    ShopPricingPolicyDiagnosticCode Code,
    string Message,
    string? ParameterName = null,
    ContentId? PolicyId = null);

public sealed record BoundShopPricingPolicy
{
    public BoundShopPricingPolicy(ContentId policyId, IShopPricingPolicy policy)
    {
        if (!policyId.IsValid || policyId.IsQualified)
        {
            throw new ArgumentException(
                "Shop pricing policy IDs must be valid unqualified IDs.",
                nameof(policyId));
        }

        PolicyId = policyId;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public ContentId PolicyId { get; }
    public IShopPricingPolicy Policy { get; }
}

public sealed record ShopPricingPolicyBindingResult
{
    public ShopPricingPolicyBindingResult(
        BoundShopPricingPolicy? policy,
        IEnumerable<ShopPricingPolicyDiagnostic>? diagnostics = null)
    {
        Policy = policy;
        Diagnostics = Array.AsReadOnly((diagnostics ?? []).ToArray());
    }

    public BoundShopPricingPolicy? Policy { get; }
    public IReadOnlyList<ShopPricingPolicyDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Policy is not null && Diagnostics.Count == 0;

    public BoundShopPricingPolicy RequirePolicy() =>
        IsSuccess && Policy is not null
            ? Policy
            : throw new InvalidOperationException(
                "Shop pricing policy binding failed: " +
                string.Join("; ", Diagnostics.Select(diagnostic => diagnostic.Message)));
}

/// <summary>Creates one configured shop-pricing policy from authored parameters.</summary>
public interface IShopPricingPolicyFactory
{
    ContentId PolicyId { get; }

    ShopPricingPolicyBindingResult Create(
        IReadOnlyDictionary<string, object?> parameters);
}

internal sealed class StandardShopPricingPolicyFactory : IShopPricingPolicyFactory
{
    public ContentId PolicyId => StandardShopPricingPolicyIds.Standard;

    public ShopPricingPolicyBindingResult Create(
        IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var diagnostics = new List<ShopPricingPolicyDiagnostic>();
        decimal resalePercentage = StandardShopPricingPolicy.DefaultResalePercentage;

        foreach ((string key, object? value) in parameters)
        {
            if (!string.Equals(key, "resalePercentage", StringComparison.Ordinal))
            {
                diagnostics.Add(new ShopPricingPolicyDiagnostic(
                    ShopPricingPolicyDiagnosticCode.UnknownParameter,
                    $"Shop pricing policy '{PolicyId}' does not support parameter '{key}'.",
                    key,
                    PolicyId));
                continue;
            }

            if (!RulesetPolicyFactoryParameters.TryReadDecimal(value, out decimal parsed))
            {
                diagnostics.Add(new ShopPricingPolicyDiagnostic(
                    ShopPricingPolicyDiagnosticCode.InvalidParameterType,
                    $"Shop pricing policy '{PolicyId}' parameter '{key}' must be a decimal number.",
                    key,
                    PolicyId));
                continue;
            }

            if (parsed < 0m)
            {
                diagnostics.Add(new ShopPricingPolicyDiagnostic(
                    ShopPricingPolicyDiagnosticCode.InvalidParameterValue,
                    $"Shop pricing policy '{PolicyId}' resale percentage cannot be negative.",
                    key,
                    PolicyId));
                continue;
            }

            resalePercentage = parsed;
        }

        return diagnostics.Count == 0
            ? new ShopPricingPolicyBindingResult(
                new BoundShopPricingPolicy(
                    PolicyId,
                    new StandardShopPricingPolicy(resalePercentage)))
            : new ShopPricingPolicyBindingResult(null, diagnostics);
    }
}

internal sealed class LuckAdjustedShopPricingPolicyFactory : IShopPricingPolicyFactory
{
    public ContentId PolicyId => StandardShopPricingPolicyIds.LuckAdjusted;

    public ShopPricingPolicyBindingResult Create(
        IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ShopPricingPolicyDiagnostic[] diagnostics = parameters.Keys
            .Select(key => new ShopPricingPolicyDiagnostic(
                ShopPricingPolicyDiagnosticCode.UnknownParameter,
                $"Shop pricing policy '{PolicyId}' does not support parameter '{key}'.",
                key,
                PolicyId))
            .ToArray();

        return diagnostics.Length == 0
            ? new ShopPricingPolicyBindingResult(
                new BoundShopPricingPolicy(PolicyId, new LuckAdjustedShopPricingPolicy()))
            : new ShopPricingPolicyBindingResult(null, diagnostics);
    }
}

/// <summary>Resolves authored pricing-policy IDs through supplied typed factories.</summary>
public sealed class ShopPricingPolicyFactoryRegistry
{
    private readonly IReadOnlyDictionary<ContentId, IShopPricingPolicyFactory> _factories;

    public ShopPricingPolicyFactoryRegistry(
        IEnumerable<IShopPricingPolicyFactory>? factories = null)
    {
        var result = new Dictionary<ContentId, IShopPricingPolicyFactory>();
        foreach (IShopPricingPolicyFactory factory in factories ?? [])
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (!factory.PolicyId.IsValid || factory.PolicyId.IsQualified)
            {
                throw new ArgumentException(
                    "Shop pricing policy factory IDs must be valid unqualified IDs.",
                    nameof(factories));
            }
            if (!result.TryAdd(factory.PolicyId, factory))
            {
                throw new ArgumentException(
                    $"Duplicate shop pricing policy factory ID '{factory.PolicyId}'.",
                    nameof(factories));
            }
        }

        _factories = result;
    }

    public IReadOnlyCollection<ContentId> PolicyIds =>
        Array.AsReadOnly(_factories.Keys.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray());

    public static ShopPricingPolicyFactoryRegistry CreateStandard(
        IEnumerable<IShopPricingPolicyFactory>? additionalFactories = null) =>
        new(
            new IShopPricingPolicyFactory[]
            {
                new StandardShopPricingPolicyFactory(),
                new LuckAdjustedShopPricingPolicyFactory()
            }.Concat(additionalFactories ?? []));

    public ShopPricingPolicyBindingResult Bind(
        ContentId policyId,
        IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!policyId.IsValid || policyId.IsQualified || !_factories.TryGetValue(policyId, out IShopPricingPolicyFactory? factory))
        {
            return new ShopPricingPolicyBindingResult(
                null,
                [new ShopPricingPolicyDiagnostic(
                    ShopPricingPolicyDiagnosticCode.UnsupportedPolicy,
                    $"Shop pricing policy '{policyId}' is not registered.",
                    PolicyId: policyId)]);
        }

        try
        {
            ShopPricingPolicyBindingResult result = factory.Create(parameters) ??
                throw new InvalidOperationException("Shop pricing policy factory returned null.");
            if (result.IsSuccess && result.Policy?.PolicyId != policyId)
            {
                return FactoryFailure(
                    policyId,
                    $"Shop pricing policy factory '{policyId}' returned policy '{result.Policy?.PolicyId}'.");
            }
            if (!result.IsSuccess && result.Diagnostics.Count == 0)
            {
                return FactoryFailure(
                    policyId,
                    $"Shop pricing policy factory '{policyId}' rejected configuration without a diagnostic.");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return FactoryFailure(
                policyId,
                $"Shop pricing policy factory '{policyId}' failed: {exception.Message}");
        }
    }

    private static ShopPricingPolicyBindingResult FactoryFailure(
        ContentId policyId,
        string message) =>
        new(
            null,
            [new ShopPricingPolicyDiagnostic(
                ShopPricingPolicyDiagnosticCode.PolicyFactoryFailure,
                message,
                PolicyId: policyId)]);
}

/// <summary>Transient resolved pricing authority carried by one runtime shop offer.</summary>
public sealed record RuntimeShopPricingProfile
{
    public RuntimeShopPricingProfile(
        int authoredPurchasePrice,
        BoundShopPricingPolicy policy)
    {
        if (authoredPurchasePrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoredPurchasePrice),
                authoredPurchasePrice,
                "Authored shop purchase price cannot be negative.");
        }

        AuthoredPurchasePrice = authoredPurchasePrice;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public int AuthoredPurchasePrice { get; }
    public BoundShopPricingPolicy Policy { get; }

    public ShopPriceCalculationResult Calculate(
        ShopPriceOperation operation,
        int luck)
    {
        var request = new ShopPriceCalculationRequest(
            AuthoredPurchasePrice,
            luck,
            operation);

        try
        {
            return Policy.Policy.Calculate(request) ??
                ShopPriceCalculationResult.Rejected(
                    ShopPriceCalculationCode.PolicyRejected,
                    $"Shop pricing policy '{Policy.PolicyId}' returned no result.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ShopPriceCalculationResult.Rejected(
                ShopPriceCalculationCode.PolicyRejected,
                $"Shop pricing policy '{Policy.PolicyId}' failed: {exception.Message}");
        }
    }
}

internal static class ShopPricingArithmetic
{
    public static ShopPriceCalculationResult? ValidateInputs(
        ShopPriceCalculationRequest request)
    {
        if (request.AuthoredPurchasePrice < 0)
        {
            return ShopPriceCalculationResult.Rejected(
                ShopPriceCalculationCode.NegativePurchasePrice,
                $"Authored shop purchase price cannot be negative (received {request.AuthoredPurchasePrice}).");
        }
        if (request.Luck < 0)
        {
            return ShopPriceCalculationResult.Rejected(
                ShopPriceCalculationCode.NegativeLuck,
                $"Shop pricing Luck cannot be negative (received {request.Luck}).");
        }

        return null;
    }

    public static ShopPriceCalculationResult ApplyMultiplier(
        ShopPriceCalculationRequest request,
        decimal multiplier,
        string policyName)
    {
        try
        {
            decimal calculated = decimal.Truncate(checked(request.AuthoredPurchasePrice * multiplier));
            if (calculated is < 0m or > int.MaxValue)
            {
                return Overflow(request, policyName);
            }

            return ShopPriceCalculationResult.Applied(decimal.ToInt32(calculated));
        }
        catch (OverflowException)
        {
            return Overflow(request, policyName);
        }
    }

    private static ShopPriceCalculationResult Overflow(
        ShopPriceCalculationRequest request,
        string policyName) =>
        ShopPriceCalculationResult.Rejected(
            ShopPriceCalculationCode.NumericOverflow,
            $"{policyName} shop {request.Operation.ToString().ToLowerInvariant()} price for authored purchase price " +
            $"{request.AuthoredPurchasePrice} and Luck {request.Luck} exceeds the supported integer range.");
}
