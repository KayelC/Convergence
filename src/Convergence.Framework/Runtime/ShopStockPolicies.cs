using Convergence.Content;

namespace Convergence.Runtime;

/// <summary>Stable IDs for the shop-stock policies supplied with Convergence.</summary>
public static class StandardShopStockPolicyIds
{
    public static ContentId Standard { get; } = ContentId.Parse("standard_shop_stock");
}

public enum ShopStockOperation
{
    Purchase,
    Resale
}

public enum ShopStockTransitionCode
{
    Applied,
    Unavailable,
    InvalidQuantity,
    PolicyRejected
}

public sealed record ShopStockTransitionRequest
{
    public ShopStockTransitionRequest(
        ShopStockOperation operation,
        int currentQuantity)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Shop stock operation is not supported.");
        }

        Operation = operation;
        CurrentQuantity = currentQuantity;
    }

    public ShopStockOperation Operation { get; }
    public int CurrentQuantity { get; }
}

public sealed record ShopStockTransitionResult
{
    public ShopStockTransitionResult(
        ShopStockTransitionCode code,
        int remainingQuantity,
        string? message = null)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(
                nameof(code),
                code,
                "Shop stock transition code is not supported.");
        }
        if (remainingQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remainingQuantity),
                remainingQuantity,
                "Remaining shop stock cannot be negative.");
        }

        Code = code;
        RemainingQuantity = remainingQuantity;
        Message = string.IsNullOrWhiteSpace(message) ? null : message;
    }

    public ShopStockTransitionCode Code { get; }
    public int RemainingQuantity { get; }
    public string? Message { get; }
    public bool IsSuccess => Code == ShopStockTransitionCode.Applied;

    public static ShopStockTransitionResult Applied(int remainingQuantity) =>
        new(ShopStockTransitionCode.Applied, remainingQuantity);

    public static ShopStockTransitionResult Rejected(
        ShopStockTransitionCode code,
        int currentQuantity,
        string message)
    {
        if (code == ShopStockTransitionCode.Applied)
        {
            throw new ArgumentException(
                "A rejected shop-stock transition cannot use the applied code.",
                nameof(code));
        }

        return new ShopStockTransitionResult(code, currentQuantity, message);
    }
}

/// <summary>
/// Calculates one limited offer's next remaining quantity without mutating external state.
/// Implementations must be deterministic for the supplied request and side-effect free because
/// the candidate transition is committed only if inventory and currency transitions also succeed.
/// </summary>
public interface IShopStockPolicy
{
    ShopStockTransitionResult Apply(ShopStockTransitionRequest request);
}

/// <summary>Decrements purchases and deliberately leaves resale stock unchanged.</summary>
public sealed class StandardShopStockPolicy : IShopStockPolicy
{
    public ShopStockTransitionResult Apply(ShopStockTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CurrentQuantity < 0)
        {
            return ShopStockTransitionResult.Rejected(
                ShopStockTransitionCode.InvalidQuantity,
                currentQuantity: 0,
                $"Shop stock cannot be negative (received {request.CurrentQuantity}).");
        }

        if (request.Operation == ShopStockOperation.Resale)
        {
            return ShopStockTransitionResult.Applied(request.CurrentQuantity);
        }
        if (request.CurrentQuantity == 0)
        {
            return ShopStockTransitionResult.Rejected(
                ShopStockTransitionCode.Unavailable,
                request.CurrentQuantity,
                "Shop stock is unavailable.");
        }

        return ShopStockTransitionResult.Applied(request.CurrentQuantity - 1);
    }
}

public enum ShopStockPolicyDiagnosticCode
{
    UnsupportedPolicy,
    UnknownParameter,
    InvalidParameterType,
    InvalidParameterValue,
    PolicyFactoryFailure
}

public sealed record ShopStockPolicyDiagnostic(
    ShopStockPolicyDiagnosticCode Code,
    string Message,
    string? ParameterName = null,
    ContentId? PolicyId = null);

public sealed record BoundShopStockPolicy
{
    public BoundShopStockPolicy(ContentId policyId, IShopStockPolicy policy)
    {
        if (!policyId.IsValid || policyId.IsQualified)
        {
            throw new ArgumentException(
                "Shop stock policy IDs must be valid unqualified IDs.",
                nameof(policyId));
        }

        PolicyId = policyId;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public ContentId PolicyId { get; }
    public IShopStockPolicy Policy { get; }
}

public sealed record ShopStockPolicyBindingResult
{
    public ShopStockPolicyBindingResult(
        BoundShopStockPolicy? policy,
        IEnumerable<ShopStockPolicyDiagnostic>? diagnostics = null)
    {
        ShopStockPolicyDiagnostic[] copy = (diagnostics ?? []).ToArray();
        if (copy.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException(
                "Shop stock policy diagnostics cannot contain null entries.",
                nameof(diagnostics));
        }
        if (copy.Any(diagnostic =>
                !Enum.IsDefined(diagnostic.Code) || string.IsNullOrWhiteSpace(diagnostic.Message)))
        {
            throw new ArgumentException(
                "Shop stock policy diagnostics must have defined codes and nonempty messages.",
                nameof(diagnostics));
        }
        if ((policy is null) == (copy.Length == 0))
        {
            throw new ArgumentException(
                "A shop stock policy binding must contain either one policy or one or more diagnostics.");
        }

        Policy = policy;
        Diagnostics = Array.AsReadOnly(copy);
    }

    public BoundShopStockPolicy? Policy { get; }
    public IReadOnlyList<ShopStockPolicyDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Policy is not null && Diagnostics.Count == 0;

    public BoundShopStockPolicy RequirePolicy() =>
        IsSuccess && Policy is not null
            ? Policy
            : throw new InvalidOperationException(
                "Shop stock policy binding failed: " +
                string.Join("; ", Diagnostics.Select(diagnostic => diagnostic.Message)));
}

/// <summary>Creates one configured shop-stock policy from authored parameters.</summary>
public interface IShopStockPolicyFactory
{
    ContentId PolicyId { get; }

    ShopStockPolicyBindingResult Create(
        IReadOnlyDictionary<string, object?> parameters);
}

internal sealed class StandardShopStockPolicyFactory : IShopStockPolicyFactory
{
    public ContentId PolicyId => StandardShopStockPolicyIds.Standard;

    public ShopStockPolicyBindingResult Create(
        IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ShopStockPolicyDiagnostic[] diagnostics = parameters.Keys
            .Select(key => new ShopStockPolicyDiagnostic(
                ShopStockPolicyDiagnosticCode.UnknownParameter,
                $"Shop stock policy '{PolicyId}' does not support parameter '{key}'.",
                key,
                PolicyId))
            .ToArray();

        return diagnostics.Length == 0
            ? new ShopStockPolicyBindingResult(
                new BoundShopStockPolicy(PolicyId, new StandardShopStockPolicy()))
            : new ShopStockPolicyBindingResult(null, diagnostics);
    }
}

/// <summary>Resolves authored stock-policy IDs through supplied typed factories.</summary>
public sealed class ShopStockPolicyFactoryRegistry
{
    private readonly IReadOnlyDictionary<ContentId, IShopStockPolicyFactory> _factories;

    public ShopStockPolicyFactoryRegistry(
        IEnumerable<IShopStockPolicyFactory>? factories = null)
    {
        var result = new Dictionary<ContentId, IShopStockPolicyFactory>();
        foreach (IShopStockPolicyFactory factory in factories ?? [])
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (!factory.PolicyId.IsValid || factory.PolicyId.IsQualified)
            {
                throw new ArgumentException(
                    "Shop stock policy factory IDs must be valid unqualified IDs.",
                    nameof(factories));
            }
            if (!result.TryAdd(factory.PolicyId, factory))
            {
                throw new ArgumentException(
                    $"Duplicate shop stock policy factory ID '{factory.PolicyId}'.",
                    nameof(factories));
            }
        }

        _factories = result;
    }

    public IReadOnlyCollection<ContentId> PolicyIds =>
        Array.AsReadOnly(
            _factories.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray());

    public static ShopStockPolicyFactoryRegistry CreateStandard(
        IEnumerable<IShopStockPolicyFactory>? additionalFactories = null) =>
        new(
            new IShopStockPolicyFactory[]
            {
                new StandardShopStockPolicyFactory()
            }.Concat(additionalFactories ?? []));

    public ShopStockPolicyBindingResult Bind(
        ContentId policyId,
        IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!policyId.IsValid ||
            policyId.IsQualified ||
            !_factories.TryGetValue(policyId, out IShopStockPolicyFactory? factory))
        {
            return new ShopStockPolicyBindingResult(
                null,
                [new ShopStockPolicyDiagnostic(
                    ShopStockPolicyDiagnosticCode.UnsupportedPolicy,
                    $"Shop stock policy '{policyId}' is not registered.",
                    PolicyId: policyId)]);
        }

        try
        {
            ShopStockPolicyBindingResult result = factory.Create(parameters) ??
                throw new InvalidOperationException("Shop stock policy factory returned null.");
            if (result.IsSuccess && result.Policy?.PolicyId != policyId)
            {
                return FactoryFailure(
                    policyId,
                    $"Shop stock policy factory '{policyId}' returned policy '{result.Policy?.PolicyId}'.");
            }
            if (!result.IsSuccess && result.Diagnostics.Count == 0)
            {
                return FactoryFailure(
                    policyId,
                    $"Shop stock policy factory '{policyId}' rejected configuration without a diagnostic.");
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
                $"Shop stock policy factory '{policyId}' failed: {exception.Message}");
        }
    }

    private static ShopStockPolicyBindingResult FactoryFailure(
        ContentId policyId,
        string message) =>
        new(
            null,
            [new ShopStockPolicyDiagnostic(
                ShopStockPolicyDiagnosticCode.PolicyFactoryFailure,
                message,
                PolicyId: policyId)]);
}

/// <summary>Stable runtime identity for one offer nested inside one shop.</summary>
public sealed record RuntimeShopOfferIdentity
{
    public RuntimeShopOfferIdentity(ContentId shopId, ContentId offerId)
    {
        if (!shopId.IsValid || !shopId.IsQualified)
        {
            throw new ArgumentException(
                "Runtime shop IDs must be valid qualified content IDs.",
                nameof(shopId));
        }
        if (!offerId.IsValid || offerId.IsQualified)
        {
            throw new ArgumentException(
                "Runtime shop offer IDs must be valid shop-local content IDs.",
                nameof(offerId));
        }

        ShopId = shopId;
        OfferId = offerId;
    }

    public ContentId ShopId { get; }
    public ContentId OfferId { get; }
}

/// <summary>Resolved stock authority carried by one runtime shop offer.</summary>
public sealed record RuntimeShopStockProfile
{
    public RuntimeShopStockProfile(
        int? initialQuantity,
        BoundShopStockPolicy? policy)
    {
        if ((initialQuantity is null) != (policy is null))
        {
            throw new ArgumentException(
                "Tracked shop stock requires both an initial quantity and a policy.",
                nameof(policy));
        }
        if (initialQuantity is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialQuantity),
                initialQuantity,
                "Tracked shop stock requires a positive initial quantity.");
        }

        InitialQuantity = initialQuantity;
        Policy = policy;
    }

    public int? InitialQuantity { get; }
    public BoundShopStockPolicy? Policy { get; }
    public bool IsTracked => InitialQuantity.HasValue;

    public static RuntimeShopStockProfile Unlimited { get; } = new(null, null);

    public ShopStockTransitionResult Apply(
        ShopStockOperation operation,
        int currentQuantity)
    {
        if (!IsTracked || Policy is null)
        {
            throw new InvalidOperationException(
                "Unlimited shop stock does not execute a stock transition policy.");
        }

        var request = new ShopStockTransitionRequest(operation, currentQuantity);
        try
        {
            return Policy.Policy.Apply(request) ??
                ShopStockTransitionResult.Rejected(
                    ShopStockTransitionCode.PolicyRejected,
                    Math.Max(0, currentQuantity),
                    $"Shop stock policy '{Policy.PolicyId}' returned no result.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ShopStockTransitionResult.Rejected(
                ShopStockTransitionCode.PolicyRejected,
                Math.Max(0, currentQuantity),
                $"Shop stock policy '{Policy.PolicyId}' failed: {exception.Message}");
        }
    }
}

public sealed record RuntimeShopStockEntrySnapshot(
    RuntimeShopOfferIdentity OfferIdentity,
    int RemainingQuantity);

/// <summary>Immutable durable remaining quantities for tracked shop offers.</summary>
public sealed record RuntimeShopStockSnapshot
{
    public RuntimeShopStockSnapshot(
        IEnumerable<RuntimeShopStockEntrySnapshot>? entries = null)
    {
        RuntimeShopStockEntrySnapshot[] copy = (entries ?? []).ToArray();
        if (copy.Any(entry => entry is null))
        {
            throw new ArgumentException(
                "Shop stock entries cannot contain null values.",
                nameof(entries));
        }

        Entries = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<RuntimeShopStockEntrySnapshot> Entries { get; }

    public static RuntimeShopStockSnapshot CreateInitial(
        IEnumerable<RuntimeShopOfferSnapshot> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        RuntimeShopOfferSnapshot[] copy = offers.ToArray();
        var seen = new HashSet<RuntimeShopOfferIdentity>();
        var entries = new List<RuntimeShopStockEntrySnapshot>();
        foreach (RuntimeShopOfferSnapshot offer in copy)
        {
            ArgumentNullException.ThrowIfNull(offer);
            if (!seen.Add(offer.Identity))
            {
                throw new ArgumentException(
                    $"Runtime shop offer '{offer.Identity.ShopId}/{offer.Identity.OfferId}' appears more than once.",
                    nameof(offers));
            }
            if (offer.Stock.InitialQuantity is int quantity)
            {
                entries.Add(new RuntimeShopStockEntrySnapshot(offer.Identity, quantity));
            }
        }

        return new RuntimeShopStockSnapshot(entries);
    }

    public bool TryGetRemainingQuantity(
        RuntimeShopOfferIdentity identity,
        out int remainingQuantity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        RuntimeShopStockEntrySnapshot[] matches = Entries
            .Where(entry => entry.OfferIdentity == identity)
            .Take(2)
            .ToArray();
        if (matches.Length == 1 && matches[0].RemainingQuantity >= 0)
        {
            remainingQuantity = matches[0].RemainingQuantity;
            return true;
        }

        remainingQuantity = 0;
        return false;
    }

    internal RuntimeShopStockSnapshot WithRemainingQuantity(
        RuntimeShopOfferIdentity identity,
        int remainingQuantity)
    {
        if (remainingQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remainingQuantity),
                remainingQuantity,
                "Remaining shop stock cannot be negative.");
        }

        int matches = 0;
        RuntimeShopStockEntrySnapshot[] next = Entries
            .Select(entry =>
            {
                if (entry.OfferIdentity != identity)
                {
                    return entry;
                }

                matches++;
                return new RuntimeShopStockEntrySnapshot(identity, remainingQuantity);
            })
            .ToArray();
        if (matches != 1)
        {
            throw new InvalidOperationException(
                $"Shop stock identity '{identity.ShopId}/{identity.OfferId}' must have exactly one entry.");
        }

        return new RuntimeShopStockSnapshot(next);
    }
}
