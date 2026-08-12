using System.Collections.ObjectModel;
using Convergence.Content;
using Convergence.Execution;

namespace Convergence.Runtime;

/// <summary>Stable IDs for the recovery policies supplied with Convergence.</summary>
public static class StandardRecoveryPolicyIds
{
    public static ContentId Hospital { get; } = ContentId.Parse("standard_hospital_recovery");
}

/// <summary>Typed temporary-state categories that a recovery plan may clear.</summary>
public enum RecoveryTemporaryStateKind
{
    Guard,
    StatModifiers,
    Charges,
    Shields,
    AffinityOverrides,
    AffinityBreaks,
    OtherStatuses
}

public enum RecoveryPolicyDiagnosticCode
{
    MissingResource,
    InvalidActorState,
    NumericOverflow,
    PolicyRejected,
    PolicyFaulted,
    InvalidPolicyResult
}

public sealed record RecoveryPolicyDiagnostic(
    RecoveryPolicyDiagnosticCode Code,
    string Message,
    ContentId? ResourceId = null);

public sealed record RecoveryPolicyRequest
{
    public RecoveryPolicyRequest(RuntimeActorSnapshot actor)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    public RuntimeActorSnapshot Actor { get; }
}

/// <summary>Immutable treatment requested by one recovery policy.</summary>
public sealed record RecoveryTreatmentPlan
{
    public RecoveryTreatmentPlan(
        ContentId currencyId,
        int cost,
        IEnumerable<ContentId> resourceIds,
        bool removeAilments,
        IEnumerable<RecoveryTemporaryStateKind>? temporaryStateKinds = null)
    {
        if (!currencyId.IsValid)
        {
            throw new ArgumentException("Recovery currency ID cannot be empty.", nameof(currencyId));
        }
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), cost, "Recovery cost cannot be negative.");
        }

        ContentId[] resources = (resourceIds ?? throw new ArgumentNullException(nameof(resourceIds))).ToArray();
        if (resources.Any(id => !id.IsValid))
        {
            throw new ArgumentException("Recovery resource IDs cannot be empty.", nameof(resourceIds));
        }
        if (resources.Distinct().Count() != resources.Length)
        {
            throw new ArgumentException("Recovery resource IDs must be unique.", nameof(resourceIds));
        }

        RecoveryTemporaryStateKind[] temporary = (temporaryStateKinds ?? [])
            .ToArray();
        if (temporary.Any(kind => !Enum.IsDefined(kind)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(temporaryStateKinds),
                "Recovery temporary-state kinds must be defined.");
        }
        if (temporary.Distinct().Count() != temporary.Length)
        {
            throw new ArgumentException(
                "Recovery temporary-state kinds must be unique.",
                nameof(temporaryStateKinds));
        }
        if (resources.Length == 0 && !removeAilments && temporary.Length == 0)
        {
            throw new ArgumentException("A recovery treatment must request at least one state change.");
        }

        CurrencyId = currencyId;
        Cost = cost;
        ResourceIds = Array.AsReadOnly(resources);
        RemoveAilments = removeAilments;
        TemporaryStateKinds = Array.AsReadOnly(temporary);
    }

    public ContentId CurrencyId { get; }
    public int Cost { get; }
    public IReadOnlyList<ContentId> ResourceIds { get; }
    public bool RemoveAilments { get; }
    public IReadOnlyList<RecoveryTemporaryStateKind> TemporaryStateKinds { get; }
}

public sealed record RecoveryPolicyDecision
{
    public RecoveryPolicyDecision(
        RecoveryTreatmentPlan? plan,
        IEnumerable<RecoveryPolicyDiagnostic>? diagnostics = null)
    {
        RecoveryPolicyDiagnostic[] copy = (diagnostics ?? []).ToArray();
        if (copy.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Recovery policy diagnostics cannot contain null entries.", nameof(diagnostics));
        }
        if (copy.Any(diagnostic =>
                !Enum.IsDefined(diagnostic.Code) || string.IsNullOrWhiteSpace(diagnostic.Message)))
        {
            throw new ArgumentException(
                "Recovery policy diagnostics must have defined codes and nonempty messages.",
                nameof(diagnostics));
        }
        if ((plan is null) == (copy.Length == 0))
        {
            throw new ArgumentException(
                "A recovery policy decision must contain either one plan or one or more diagnostics.");
        }

        Plan = plan;
        Diagnostics = Array.AsReadOnly(copy);
    }

    public RecoveryTreatmentPlan? Plan { get; }
    public IReadOnlyList<RecoveryPolicyDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Plan is not null && Diagnostics.Count == 0;

    public static RecoveryPolicyDecision Planned(RecoveryTreatmentPlan plan) => new(plan);

    public static RecoveryPolicyDecision Rejected(
        RecoveryPolicyDiagnosticCode code,
        string message,
        ContentId? resourceId = null) =>
        new(null, [new RecoveryPolicyDiagnostic(code, message, resourceId)]);
}

/// <summary>Plans recovery from immutable actor state without mutating host state.</summary>
public interface IRecoveryPolicy
{
    RecoveryPolicyDecision Plan(RecoveryPolicyRequest request);
}

/// <summary>
/// Fully restores configured resources, optionally cures legally removable ailments,
/// and requests explicit temporary-state cleanup categories.
/// </summary>
public sealed class StandardHospitalRecoveryPolicy : IRecoveryPolicy
{
    private readonly IReadOnlyDictionary<ContentId, decimal> _resourceCosts;

    public StandardHospitalRecoveryPolicy(
        ContentId currencyId,
        IEnumerable<KeyValuePair<ContentId, decimal>> resourceCosts,
        bool removeAilments,
        IEnumerable<RecoveryTemporaryStateKind>? temporaryStateKinds = null)
    {
        if (!currencyId.IsValid)
        {
            throw new ArgumentException("Recovery currency ID cannot be empty.", nameof(currencyId));
        }

        var costs = new Dictionary<ContentId, decimal>();
        foreach ((ContentId resourceId, decimal unitCost) in
                 resourceCosts ?? throw new ArgumentNullException(nameof(resourceCosts)))
        {
            if (!resourceId.IsValid)
            {
                throw new ArgumentException("Recovery resource IDs cannot be empty.", nameof(resourceCosts));
            }
            if (unitCost < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resourceCosts),
                    unitCost,
                    "Recovery resource unit costs cannot be negative.");
            }
            if (!costs.TryAdd(resourceId, unitCost))
            {
                throw new ArgumentException(
                    $"Recovery resource '{resourceId}' is configured more than once.",
                    nameof(resourceCosts));
            }
        }
        if (costs.Count == 0)
        {
            throw new ArgumentException(
                "Standard hospital recovery requires at least one configured resource.",
                nameof(resourceCosts));
        }

        RecoveryTemporaryStateKind[] temporary = (temporaryStateKinds ?? []).ToArray();
        if (temporary.Any(kind => !Enum.IsDefined(kind)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(temporaryStateKinds),
                "Recovery temporary-state kinds must be defined.");
        }
        if (temporary.Distinct().Count() != temporary.Length)
        {
            throw new ArgumentException(
                "Recovery temporary-state kinds must be unique.",
                nameof(temporaryStateKinds));
        }

        CurrencyId = currencyId;
        _resourceCosts = new ReadOnlyDictionary<ContentId, decimal>(costs);
        RemoveAilments = removeAilments;
        TemporaryStateKinds = Array.AsReadOnly(temporary);
    }

    public ContentId CurrencyId { get; }
    public IReadOnlyDictionary<ContentId, decimal> ResourceCosts => _resourceCosts;
    public bool RemoveAilments { get; }
    public IReadOnlyList<RecoveryTemporaryStateKind> TemporaryStateKinds { get; }

    public RecoveryPolicyDecision Plan(RecoveryPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        decimal total = 0m;
        foreach ((ContentId resourceId, decimal unitCost) in _resourceCosts)
        {
            RuntimeResourceSnapshot[] matches = request.Actor.Resources
                .Where(resource => resource.ResourceId == resourceId)
                .Take(2)
                .ToArray();
            if (matches.Length == 0)
            {
                return RecoveryPolicyDecision.Rejected(
                    RecoveryPolicyDiagnosticCode.MissingResource,
                    $"Actor '{request.Actor.Identity.InstanceId}' has no configured recovery resource '{resourceId}'.",
                    resourceId);
            }
            if (matches.Length != 1)
            {
                return RecoveryPolicyDecision.Rejected(
                    RecoveryPolicyDiagnosticCode.InvalidActorState,
                    $"Actor '{request.Actor.Identity.InstanceId}' contains duplicate recovery resource '{resourceId}'.",
                    resourceId);
            }

            try
            {
                decimal missing = checked(matches[0].Maximum - matches[0].Current);
                total = checked(total + checked(missing * unitCost));
            }
            catch (OverflowException)
            {
                return RecoveryPolicyDecision.Rejected(
                    RecoveryPolicyDiagnosticCode.NumericOverflow,
                    $"Recovery cost for actor '{request.Actor.Identity.InstanceId}' exceeds the supported decimal range.",
                    resourceId);
            }
        }

        decimal truncated = decimal.Truncate(total);
        if (truncated is < 0m or > int.MaxValue)
        {
            return RecoveryPolicyDecision.Rejected(
                RecoveryPolicyDiagnosticCode.NumericOverflow,
                $"Recovery cost for actor '{request.Actor.Identity.InstanceId}' exceeds the supported currency range.");
        }

        return RecoveryPolicyDecision.Planned(new RecoveryTreatmentPlan(
            CurrencyId,
            decimal.ToInt32(truncated),
            _resourceCosts.Keys,
            RemoveAilments,
            TemporaryStateKinds));
    }
}

public enum RecoveryPolicyFactoryDiagnosticCode
{
    UnsupportedPolicy,
    MissingParameter,
    UnknownParameter,
    InvalidParameterType,
    InvalidParameterValue,
    PolicyFactoryFailure
}

public sealed record RecoveryPolicyFactoryDiagnostic(
    RecoveryPolicyFactoryDiagnosticCode Code,
    string Message,
    string? ParameterName = null,
    ContentId? PolicyId = null);

public sealed record BoundRecoveryPolicy
{
    public BoundRecoveryPolicy(ContentId policyId, IRecoveryPolicy policy)
    {
        if (!policyId.IsValid || policyId.IsQualified)
        {
            throw new ArgumentException(
                "Recovery policy IDs must be valid unqualified IDs.",
                nameof(policyId));
        }

        PolicyId = policyId;
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public ContentId PolicyId { get; }
    public IRecoveryPolicy Policy { get; }
}

public sealed record RecoveryPolicyBindingResult
{
    public RecoveryPolicyBindingResult(
        BoundRecoveryPolicy? policy,
        IEnumerable<RecoveryPolicyFactoryDiagnostic>? diagnostics = null)
    {
        RecoveryPolicyFactoryDiagnostic[] copy = (diagnostics ?? []).ToArray();
        if (copy.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException(
                "Recovery policy factory diagnostics cannot contain null entries.",
                nameof(diagnostics));
        }
        if (copy.Any(diagnostic =>
                !Enum.IsDefined(diagnostic.Code) || string.IsNullOrWhiteSpace(diagnostic.Message)))
        {
            throw new ArgumentException(
                "Recovery policy factory diagnostics must have defined codes and nonempty messages.",
                nameof(diagnostics));
        }
        if ((policy is null) == (copy.Length == 0))
        {
            throw new ArgumentException(
                "A recovery policy binding must contain either one policy or one or more diagnostics.");
        }

        Policy = policy;
        Diagnostics = Array.AsReadOnly(copy);
    }

    public BoundRecoveryPolicy? Policy { get; }
    public IReadOnlyList<RecoveryPolicyFactoryDiagnostic> Diagnostics { get; }
    public bool IsSuccess => Policy is not null && Diagnostics.Count == 0;

    public BoundRecoveryPolicy RequirePolicy() =>
        IsSuccess && Policy is not null
            ? Policy
            : throw new InvalidOperationException(
                "Recovery policy binding failed: " +
                string.Join("; ", Diagnostics.Select(diagnostic => diagnostic.Message)));
}

public interface IRecoveryPolicyFactory
{
    ContentId PolicyId { get; }

    RecoveryPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters);
}

internal sealed class StandardHospitalRecoveryPolicyFactory : IRecoveryPolicyFactory
{
    public ContentId PolicyId => StandardRecoveryPolicyIds.Hospital;

    public RecoveryPolicyBindingResult Create(IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var diagnostics = new List<RecoveryPolicyFactoryDiagnostic>();
        ContentId currencyId = default;
        var resourceCosts = new Dictionary<ContentId, decimal>();
        bool removeAilments = false;
        var temporaryKinds = new List<RecoveryTemporaryStateKind>();

        foreach ((string key, object? value) in parameters)
        {
            switch (key)
            {
                case "currencyId":
                    if (value is not string currencyText || !ContentId.TryParse(currencyText, out currencyId))
                    {
                        InvalidTypeOrValue(key, "a valid content ID string", diagnostics);
                    }
                    break;
                case "resourceCosts":
                    ReadResourceCosts(value, key, resourceCosts, diagnostics);
                    break;
                case "removeAilments":
                    if (value is bool remove)
                    {
                        removeAilments = remove;
                    }
                    else
                    {
                        InvalidTypeOrValue(key, "a Boolean", diagnostics);
                    }
                    break;
                case "temporaryStateKinds":
                    ReadTemporaryKinds(value, key, temporaryKinds, diagnostics);
                    break;
                default:
                    diagnostics.Add(new RecoveryPolicyFactoryDiagnostic(
                        RecoveryPolicyFactoryDiagnosticCode.UnknownParameter,
                        $"Recovery policy '{PolicyId}' does not support parameter '{key}'.",
                        key,
                        PolicyId));
                    break;
            }
        }

        Require(parameters, "currencyId", diagnostics);
        Require(parameters, "resourceCosts", diagnostics);
        Require(parameters, "removeAilments", diagnostics);
        Require(parameters, "temporaryStateKinds", diagnostics);

        if (diagnostics.Count > 0)
        {
            return new RecoveryPolicyBindingResult(null, diagnostics);
        }

        try
        {
            return new RecoveryPolicyBindingResult(new BoundRecoveryPolicy(
                PolicyId,
                new StandardHospitalRecoveryPolicy(
                    currencyId,
                    resourceCosts,
                    removeAilments,
                    temporaryKinds)));
        }
        catch (ArgumentException exception)
        {
            return new RecoveryPolicyBindingResult(
                null,
                [new RecoveryPolicyFactoryDiagnostic(
                    RecoveryPolicyFactoryDiagnosticCode.InvalidParameterValue,
                    exception.Message,
                    PolicyId: PolicyId)]);
        }
    }

    private void Require(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        ICollection<RecoveryPolicyFactoryDiagnostic> diagnostics)
    {
        if (!parameters.ContainsKey(key))
        {
            diagnostics.Add(new RecoveryPolicyFactoryDiagnostic(
                RecoveryPolicyFactoryDiagnosticCode.MissingParameter,
                $"Recovery policy '{PolicyId}' requires parameter '{key}'.",
                key,
                PolicyId));
        }
    }

    private void InvalidTypeOrValue(
        string key,
        string expectation,
        ICollection<RecoveryPolicyFactoryDiagnostic> diagnostics) =>
        diagnostics.Add(new RecoveryPolicyFactoryDiagnostic(
            RecoveryPolicyFactoryDiagnosticCode.InvalidParameterType,
            $"Recovery policy '{PolicyId}' parameter '{key}' must be {expectation}.",
            key,
            PolicyId));

    private void ReadResourceCosts(
        object? value,
        string key,
        IDictionary<ContentId, decimal> destination,
        ICollection<RecoveryPolicyFactoryDiagnostic> diagnostics)
    {
        if (value is not IReadOnlyDictionary<string, object?> authored || authored.Count == 0)
        {
            InvalidTypeOrValue(key, "a nonempty object of resource IDs to nonnegative decimal costs", diagnostics);
            return;
        }

        foreach ((string rawId, object? rawCost) in authored)
        {
            string path = $"{key}.{rawId}";
            if (!ContentId.TryParse(rawId, out ContentId resourceId))
            {
                diagnostics.Add(new RecoveryPolicyFactoryDiagnostic(
                    RecoveryPolicyFactoryDiagnosticCode.InvalidParameterValue,
                    $"Recovery policy '{PolicyId}' resource cost key '{rawId}' is not a valid content ID.",
                    path,
                    PolicyId));
                continue;
            }
            if (!RulesetPolicyFactoryParameters.TryReadDecimal(rawCost, out decimal cost))
            {
                InvalidTypeOrValue(path, "a decimal number", diagnostics);
                continue;
            }
            if (cost < 0m)
            {
                diagnostics.Add(new RecoveryPolicyFactoryDiagnostic(
                    RecoveryPolicyFactoryDiagnosticCode.InvalidParameterValue,
                    $"Recovery policy '{PolicyId}' resource cost '{path}' cannot be negative.",
                    path,
                    PolicyId));
                continue;
            }

            if (!destination.TryAdd(resourceId, cost))
            {
                diagnostics.Add(new RecoveryPolicyFactoryDiagnostic(
                    RecoveryPolicyFactoryDiagnosticCode.InvalidParameterValue,
                    $"Recovery policy '{PolicyId}' resource '{resourceId}' is configured more than once.",
                    path,
                    PolicyId));
            }
        }
    }

    private void ReadTemporaryKinds(
        object? value,
        string key,
        ICollection<RecoveryTemporaryStateKind> destination,
        ICollection<RecoveryPolicyFactoryDiagnostic> diagnostics)
    {
        if (value is not IReadOnlyList<object?> authored)
        {
            InvalidTypeOrValue(key, "an array of supported temporary-state kind strings", diagnostics);
            return;
        }

        foreach ((object? raw, int index) in authored.Select((item, index) => (item, index)))
        {
            string path = $"{key}[{index}]";
            if (raw is not string text || !TryParseTemporaryKind(text, out RecoveryTemporaryStateKind kind))
            {
                diagnostics.Add(new RecoveryPolicyFactoryDiagnostic(
                    RecoveryPolicyFactoryDiagnosticCode.InvalidParameterValue,
                    $"Recovery policy '{PolicyId}' parameter '{path}' is not a supported temporary-state kind.",
                    path,
                    PolicyId));
                continue;
            }
            if (destination.Contains(kind))
            {
                diagnostics.Add(new RecoveryPolicyFactoryDiagnostic(
                    RecoveryPolicyFactoryDiagnosticCode.InvalidParameterValue,
                    $"Recovery policy '{PolicyId}' temporary-state kind '{text}' is duplicated.",
                    path,
                    PolicyId));
                continue;
            }

            destination.Add(kind);
        }
    }

    private static bool TryParseTemporaryKind(
        string text,
        out RecoveryTemporaryStateKind kind)
    {
        kind = text switch
        {
            "guard" => RecoveryTemporaryStateKind.Guard,
            "stat_modifiers" => RecoveryTemporaryStateKind.StatModifiers,
            "charges" => RecoveryTemporaryStateKind.Charges,
            "shields" => RecoveryTemporaryStateKind.Shields,
            "affinity_overrides" => RecoveryTemporaryStateKind.AffinityOverrides,
            "affinity_breaks" => RecoveryTemporaryStateKind.AffinityBreaks,
            "other_statuses" => RecoveryTemporaryStateKind.OtherStatuses,
            _ => default
        };
        return text is "guard" or "stat_modifiers" or "charges" or "shields" or
            "affinity_overrides" or "affinity_breaks" or "other_statuses";
    }
}

/// <summary>Resolves authored recovery-policy IDs through supplied typed factories.</summary>
public sealed class RecoveryPolicyFactoryRegistry
{
    private readonly IReadOnlyDictionary<ContentId, IRecoveryPolicyFactory> _factories;

    public RecoveryPolicyFactoryRegistry(IEnumerable<IRecoveryPolicyFactory>? factories = null)
    {
        var result = new Dictionary<ContentId, IRecoveryPolicyFactory>();
        foreach (IRecoveryPolicyFactory factory in factories ?? [])
        {
            ArgumentNullException.ThrowIfNull(factory);
            if (!factory.PolicyId.IsValid || factory.PolicyId.IsQualified)
            {
                throw new ArgumentException(
                    "Recovery policy factory IDs must be valid unqualified IDs.",
                    nameof(factories));
            }
            if (!result.TryAdd(factory.PolicyId, factory))
            {
                throw new ArgumentException(
                    $"Duplicate recovery policy factory ID '{factory.PolicyId}'.",
                    nameof(factories));
            }
        }

        _factories = new ReadOnlyDictionary<ContentId, IRecoveryPolicyFactory>(result);
    }

    public IReadOnlyCollection<ContentId> PolicyIds =>
        Array.AsReadOnly(_factories.Keys.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray());

    public static RecoveryPolicyFactoryRegistry CreateStandard(
        IEnumerable<IRecoveryPolicyFactory>? additionalFactories = null) =>
        new(new IRecoveryPolicyFactory[] { new StandardHospitalRecoveryPolicyFactory() }
            .Concat(additionalFactories ?? []));

    public RecoveryPolicyBindingResult Bind(
        ContentId policyId,
        IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!policyId.IsValid || policyId.IsQualified || !_factories.TryGetValue(policyId, out IRecoveryPolicyFactory? factory))
        {
            return new RecoveryPolicyBindingResult(
                null,
                [new RecoveryPolicyFactoryDiagnostic(
                    RecoveryPolicyFactoryDiagnosticCode.UnsupportedPolicy,
                    $"Recovery policy '{policyId}' is not registered.",
                    PolicyId: policyId)]);
        }

        try
        {
            RecoveryPolicyBindingResult result = factory.Create(parameters) ??
                throw new InvalidOperationException("Recovery policy factory returned null.");
            if (result.IsSuccess && result.Policy?.PolicyId != policyId)
            {
                return FactoryFailure(
                    policyId,
                    $"Recovery policy factory '{policyId}' returned policy '{result.Policy?.PolicyId}'.");
            }
            if (!result.IsSuccess && result.Diagnostics.Count == 0)
            {
                return FactoryFailure(
                    policyId,
                    $"Recovery policy factory '{policyId}' rejected configuration without a diagnostic.");
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
                $"Recovery policy factory '{policyId}' failed: {exception.Message}");
        }
    }

    private static RecoveryPolicyBindingResult FactoryFailure(ContentId policyId, string message) =>
        new(
            null,
            [new RecoveryPolicyFactoryDiagnostic(
                RecoveryPolicyFactoryDiagnosticCode.PolicyFactoryFailure,
                message,
                PolicyId: policyId)]);
}

public enum RecoveryOperation
{
    Assessment,
    Execution
}

public enum RecoveryTransactionCode
{
    Applied,
    NoRecoveryNeeded,
    InsufficientCurrency,
    CurrencyNotFound,
    PolicyRejected,
    PolicyFaulted,
    InvalidPolicyResult,
    InvalidActorState,
    MissingResource,
    NumericOverflow,
    MissingStatModifierPolicy,
    StatModifierCleanupRejected,
    StateMutationRejected,
    CurrencyRejected
}

public sealed record RecoveryTransactionDiagnostic(
    RecoveryTransactionCode Code,
    string Message,
    ContentId? ResourceId = null,
    ContentId? CurrencyId = null,
    RecoveryTemporaryStateKind? TemporaryStateKind = null);

public sealed record RecoveryTransactionResult
{
    public RecoveryTransactionResult(
        RecoveryOperation operation,
        RecoveryTransactionCode code,
        RuntimeActorSnapshot beforeActor,
        RuntimeActorSnapshot afterActor,
        RuntimeCurrencyLedgerSnapshot beforeCurrencyLedger,
        RuntimeCurrencyLedgerSnapshot afterCurrencyLedger,
        ContentId? currencyId,
        int cost,
        IEnumerable<RecoveryTransactionDiagnostic>? diagnostics = null)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost));
        }
        if (currencyId is ContentId id && !id.IsValid)
        {
            throw new ArgumentException("Recovery result currency ID cannot be empty.", nameof(currencyId));
        }

        Operation = operation;
        Code = code;
        BeforeActor = beforeActor ?? throw new ArgumentNullException(nameof(beforeActor));
        AfterActor = afterActor ?? throw new ArgumentNullException(nameof(afterActor));
        BeforeCurrencyLedger = beforeCurrencyLedger ?? throw new ArgumentNullException(nameof(beforeCurrencyLedger));
        AfterCurrencyLedger = afterCurrencyLedger ?? throw new ArgumentNullException(nameof(afterCurrencyLedger));
        CurrencyId = currencyId;
        Cost = cost;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
        if (Diagnostics.Any(diagnostic =>
                diagnostic is null ||
                !Enum.IsDefined(diagnostic.Code) ||
                string.IsNullOrWhiteSpace(diagnostic.Message)))
        {
            throw new ArgumentException(
                "Recovery transaction diagnostics must have defined codes and nonempty messages.",
                nameof(diagnostics));
        }
    }

    public RecoveryOperation Operation { get; }
    public RecoveryTransactionCode Code { get; }
    public bool Applied => Code == RecoveryTransactionCode.Applied;
    public RuntimeActorSnapshot BeforeActor { get; }
    public RuntimeActorSnapshot AfterActor { get; }
    public RuntimeCurrencyLedgerSnapshot BeforeCurrencyLedger { get; }
    public RuntimeCurrencyLedgerSnapshot AfterCurrencyLedger { get; }
    public ContentId? CurrencyId { get; }
    public int Cost { get; }
    public IReadOnlyList<RecoveryTransactionDiagnostic> Diagnostics { get; }
}

public interface IRecoveryService
{
    RecoveryTransactionResult Assess(
        RuntimeActorState actor,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        IStatModifierPolicyService? statModifiers = null);

    RecoveryTransactionResult Recover(
        RuntimeActorState actor,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        IStatModifierPolicyService? statModifiers = null);
}

/// <summary>Stages actor recovery and a named currency debit as one transaction.</summary>
public sealed class RecoveryService : IRecoveryService
{
    private readonly BoundRecoveryPolicy _policy;
    private readonly IEconomyTransactionService _economy;

    public RecoveryService(
        BoundRecoveryPolicy policy,
        IEconomyTransactionService? economy = null)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _economy = economy ?? new EconomyTransactionService();
    }

    public RecoveryTransactionResult Assess(
        RuntimeActorState actor,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        IStatModifierPolicyService? statModifiers = null) =>
        Evaluate(RecoveryOperation.Assessment, actor, currencyLedger, statModifiers);

    public RecoveryTransactionResult Recover(
        RuntimeActorState actor,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        IStatModifierPolicyService? statModifiers = null) =>
        Evaluate(RecoveryOperation.Execution, actor, currencyLedger, statModifiers);

    private RecoveryTransactionResult Evaluate(
        RecoveryOperation operation,
        RuntimeActorState actor,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        IStatModifierPolicyService? statModifiers)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(currencyLedger);
        RuntimeActorSnapshot beforeActor = actor.ToSnapshot();
        RecoveryPolicyDecision decision;
        try
        {
            decision = _policy.Policy.Plan(new RecoveryPolicyRequest(beforeActor));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Rejected(
                operation,
                RecoveryTransactionCode.PolicyFaulted,
                beforeActor,
                currencyLedger,
                currencyId: null,
                cost: 0,
                $"Recovery policy '{_policy.PolicyId}' failed: {exception.Message}");
        }

        if (decision is null)
        {
            return Rejected(
                operation,
                RecoveryTransactionCode.InvalidPolicyResult,
                beforeActor,
                currencyLedger,
                currencyId: null,
                cost: 0,
                $"Recovery policy '{_policy.PolicyId}' returned no decision.");
        }

        if (!decision.IsSuccess || decision.Plan is null)
        {
            RecoveryTransactionDiagnostic[] diagnostics = decision.Diagnostics
                .Select(MapPolicyDiagnostic)
                .ToArray();
            RecoveryTransactionCode code = diagnostics.Length > 0
                ? diagnostics[0].Code
                : RecoveryTransactionCode.InvalidPolicyResult;
            return new RecoveryTransactionResult(
                operation,
                code,
                beforeActor,
                beforeActor,
                currencyLedger,
                currencyLedger,
                currencyId: null,
                cost: 0,
                diagnostics);
        }

        RecoveryTreatmentPlan plan = decision.Plan;
        var transaction = new RuntimeActorExecutionTransaction(actor, [actor]);
        RuntimeActorState staged = transaction.Actor;
        RecoveryTransactionDiagnostic? mutationFailure;
        bool changed;
        try
        {
            mutationFailure = ApplyPlan(staged, plan, statModifiers, out changed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Rejected(
                operation,
                RecoveryTransactionCode.StateMutationRejected,
                beforeActor,
                currencyLedger,
                plan.CurrencyId,
                plan.Cost,
                $"Recovery could not stage actor state: {exception.Message}");
        }
        if (mutationFailure is not null)
        {
            return new RecoveryTransactionResult(
                operation,
                mutationFailure.Code,
                beforeActor,
                beforeActor,
                currencyLedger,
                currencyLedger,
                plan.CurrencyId,
                plan.Cost,
                [mutationFailure]);
        }
        if (!changed)
        {
            return Rejected(
                operation,
                RecoveryTransactionCode.NoRecoveryNeeded,
                beforeActor,
                currencyLedger,
                plan.CurrencyId,
                plan.Cost,
                "The actor does not need recovery.");
        }

        CurrencyTransactionResult debit = _economy.Debit(
            currencyLedger,
            plan.CurrencyId,
            plan.Cost);
        if (!debit.Applied)
        {
            RecoveryTransactionCode code = debit.Code switch
            {
                ResourceTransactionCode.InsufficientCurrency => RecoveryTransactionCode.InsufficientCurrency,
                ResourceTransactionCode.CurrencyNotFound => RecoveryTransactionCode.CurrencyNotFound,
                ResourceTransactionCode.NumericOverflow => RecoveryTransactionCode.NumericOverflow,
                _ => RecoveryTransactionCode.CurrencyRejected
            };
            return new RecoveryTransactionResult(
                operation,
                code,
                beforeActor,
                beforeActor,
                currencyLedger,
                currencyLedger,
                plan.CurrencyId,
                plan.Cost,
                debit.Diagnostics.Select(diagnostic => new RecoveryTransactionDiagnostic(
                    code,
                    diagnostic.Message,
                    CurrencyId: diagnostic.CurrencyId)));
        }

        RuntimeActorSnapshot afterActor = staged.ToSnapshot();
        if (operation == RecoveryOperation.Execution)
        {
            try
            {
                transaction.Commit();
                afterActor = actor.ToSnapshot();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return Rejected(
                    operation,
                    RecoveryTransactionCode.StateMutationRejected,
                    beforeActor,
                    currencyLedger,
                    plan.CurrencyId,
                    plan.Cost,
                    $"Recovery could not commit actor state: {exception.Message}");
            }
        }

        return new RecoveryTransactionResult(
            operation,
            RecoveryTransactionCode.Applied,
            beforeActor,
            afterActor,
            currencyLedger,
            debit.After,
            plan.CurrencyId,
            plan.Cost);
    }

    private static RecoveryTransactionDiagnostic? ApplyPlan(
        RuntimeActorState staged,
        RecoveryTreatmentPlan plan,
        IStatModifierPolicyService? statModifiers,
        out bool changed)
    {
        changed = false;
        foreach (ContentId resourceId in plan.ResourceIds)
        {
            if (!staged.TryGetResource(resourceId, out BattleResourceState? resource) || resource is null)
            {
                return new RecoveryTransactionDiagnostic(
                    RecoveryTransactionCode.MissingResource,
                    $"Actor '{staged.InstanceId}' has no recovery resource '{resourceId}'.",
                    resourceId,
                    plan.CurrencyId);
            }
            if (resource.Current != resource.Maximum)
            {
                staged.SetResource(resourceId, resource.Maximum);
                changed = true;
            }
        }

        if (plan.RemoveAilments)
        {
            changed |= staged.RemoveAilments(StatusRemovalCause.RecoveryEvent, _ => true).Count > 0;
        }

        HashSet<RecoveryTemporaryStateKind> temporary = plan.TemporaryStateKinds.ToHashSet();
        if (temporary.Contains(RecoveryTemporaryStateKind.Guard) && staged.IsGuarding)
        {
            staged.SetGuarding(false);
            changed = true;
        }

        if (temporary.Contains(RecoveryTemporaryStateKind.StatModifiers) &&
            staged.StatModifierState is RuntimeStatModifierStateSnapshot modifierState &&
            modifierState.Tracks.Count > 0)
        {
            if (statModifiers is null)
            {
                return new RecoveryTransactionDiagnostic(
                    RecoveryTransactionCode.MissingStatModifierPolicy,
                    $"Actor '{staged.InstanceId}' has stat modifiers, but no matching policy service was supplied.",
                    CurrencyId: plan.CurrencyId,
                    TemporaryStateKind: RecoveryTemporaryStateKind.StatModifiers);
            }

            StatModifierTransitionResult cleanup = statModifiers.Cleanup(
                new StatModifierCleanupRequest(modifierState, StatModifierCleanupScope.RecoveryEvent));
            if (!cleanup.Accepted)
            {
                return new RecoveryTransactionDiagnostic(
                    RecoveryTransactionCode.StatModifierCleanupRejected,
                    $"Stat-modifier recovery cleanup was rejected: " +
                    string.Join("; ", cleanup.Diagnostics.Select(diagnostic => diagnostic.Message)),
                    CurrencyId: plan.CurrencyId,
                    TemporaryStateKind: RecoveryTemporaryStateKind.StatModifiers);
            }
            if (cleanup.StateChanged)
            {
                staged.ReplaceStatModifierState(statModifiers, cleanup.After);
                changed = true;
            }
        }

        HashSet<StatusEffectKind> statusKinds = MapStatusKinds(temporary);
        if (statusKinds.Count > 0)
        {
            changed |= staged.RemoveNonModifierStatuses(
                statusKinds,
                staged.OtherStatuses.ToArray(),
                StatusRemovalCause.RecoveryEvent).Count > 0;
        }

        return null;
    }

    private static HashSet<StatusEffectKind> MapStatusKinds(
        IReadOnlySet<RecoveryTemporaryStateKind> temporary)
    {
        var kinds = new HashSet<StatusEffectKind>();
        if (temporary.Contains(RecoveryTemporaryStateKind.Charges))
        {
            kinds.Add(StatusEffectKind.Charge);
        }
        if (temporary.Contains(RecoveryTemporaryStateKind.Shields))
        {
            kinds.Add(StatusEffectKind.Shield);
        }
        if (temporary.Contains(RecoveryTemporaryStateKind.AffinityOverrides))
        {
            kinds.Add(StatusEffectKind.AffinityOverride);
        }
        if (temporary.Contains(RecoveryTemporaryStateKind.AffinityBreaks))
        {
            kinds.Add(StatusEffectKind.AffinityBreak);
        }
        if (temporary.Contains(RecoveryTemporaryStateKind.OtherStatuses))
        {
            kinds.Add(StatusEffectKind.Other);
        }

        return kinds;
    }

    private static RecoveryTransactionDiagnostic MapPolicyDiagnostic(
        RecoveryPolicyDiagnostic diagnostic) =>
        new(
            diagnostic.Code switch
            {
                RecoveryPolicyDiagnosticCode.MissingResource => RecoveryTransactionCode.MissingResource,
                RecoveryPolicyDiagnosticCode.InvalidActorState => RecoveryTransactionCode.InvalidActorState,
                RecoveryPolicyDiagnosticCode.NumericOverflow => RecoveryTransactionCode.NumericOverflow,
                RecoveryPolicyDiagnosticCode.PolicyFaulted => RecoveryTransactionCode.PolicyFaulted,
                RecoveryPolicyDiagnosticCode.InvalidPolicyResult => RecoveryTransactionCode.InvalidPolicyResult,
                _ => RecoveryTransactionCode.PolicyRejected
            },
            diagnostic.Message,
            diagnostic.ResourceId);

    private static RecoveryTransactionResult Rejected(
        RecoveryOperation operation,
        RecoveryTransactionCode code,
        RuntimeActorSnapshot actor,
        RuntimeCurrencyLedgerSnapshot currencyLedger,
        ContentId? currencyId,
        int cost,
        string message) =>
        new(
            operation,
            code,
            actor,
            actor,
            currencyLedger,
            currencyLedger,
            currencyId,
            cost,
            [new RecoveryTransactionDiagnostic(code, message, CurrencyId: currencyId)]);
}
