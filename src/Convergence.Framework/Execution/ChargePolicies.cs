using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Execution;

public static class StandardChargePolicyIds
{
    public static ContentId Disabled { get; } = ContentId.Parse("disabled_charge");
    public static ContentId Split { get; } = ContentId.Parse("split_charge");
    public static ContentId Unified { get; } = ContentId.Parse("unified_charge");
}

public enum ChargePolicyDiagnosticCode
{
    PolicyMismatch,
    UnsupportedChargeKind,
    AlreadyInEffect,
    IncompatibleState,
    InvalidDuration
}

public sealed record ChargePolicyDiagnostic
{
    public ChargePolicyDiagnostic(
        ChargePolicyDiagnosticCode code,
        string message,
        ChargeKind? chargeKind = null)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Charge-policy diagnostic message cannot be empty.", nameof(message));
        }
        if (chargeKind is ChargeKind kind && !Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(chargeKind));
        }

        Code = code;
        Message = message;
        ChargeKind = chargeKind;
    }

    public ChargePolicyDiagnosticCode Code { get; }
    public string Message { get; }
    public ChargeKind? ChargeKind { get; }
}

public sealed record ChargeApplicationRequest
{
    public ChargeApplicationRequest(
        RuntimeActorState target,
        ChargeKind chargeKind,
        decimal multiplier,
        StatusLifetimeDefinition? lifetime = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (!Enum.IsDefined(chargeKind))
        {
            throw new ArgumentOutOfRangeException(nameof(chargeKind));
        }
        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Charge multiplier must be positive.");
        }

        ChargeKind = chargeKind;
        Multiplier = multiplier;
        Lifetime = lifetime ?? StandardStatusLifetimes.DeploymentTransient;
    }

    public RuntimeActorState Target { get; }
    public ChargeKind ChargeKind { get; }
    public decimal Multiplier { get; }
    public StatusLifetimeDefinition Lifetime { get; }
}

public sealed record ChargeApplicationAssessment
{
    public ChargeApplicationAssessment(
        bool canApply,
        ChargeKind? storedChargeKind = null,
        IEnumerable<ChargePolicyDiagnostic>? diagnostics = null)
    {
        ChargePolicyDiagnostic[] snapshot = (diagnostics ?? []).ToArray();
        if (snapshot.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Charge diagnostics cannot contain null entries.", nameof(diagnostics));
        }
        if (canApply != (snapshot.Length == 0) || canApply != storedChargeKind.HasValue)
        {
            throw new ArgumentException(
                "An applicable charge assessment requires one stored kind and no diagnostics.",
                nameof(canApply));
        }

        CanApply = canApply;
        StoredChargeKind = storedChargeKind;
        Diagnostics = Array.AsReadOnly(snapshot);
    }

    public bool CanApply { get; }
    public ChargeKind? StoredChargeKind { get; }
    public IReadOnlyList<ChargePolicyDiagnostic> Diagnostics { get; }
}

public sealed record ChargeApplicationResult
{
    internal ChargeApplicationResult(
        bool applied,
        RuntimeChargeStateSnapshot? before,
        RuntimeChargeStateSnapshot? after,
        IEnumerable<ChargePolicyDiagnostic>? diagnostics = null)
    {
        ChargePolicyDiagnostic[] snapshot = (diagnostics ?? []).ToArray();
        if (snapshot.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Charge diagnostics cannot contain null entries.", nameof(diagnostics));
        }
        if (applied == (snapshot.Length > 0))
        {
            throw new ArgumentException(
                "An applied charge result cannot contain diagnostics and a rejected result must explain its rejection.",
                nameof(diagnostics));
        }
        if (applied && after is null)
        {
            throw new ArgumentException("An applied charge result requires resulting charge state.", nameof(after));
        }
        if (!applied && !ReferenceEquals(before, after))
        {
            throw new ArgumentException("A rejected charge result must preserve the exact prior state.", nameof(after));
        }

        Applied = applied;
        Before = before;
        After = after;
        Diagnostics = Array.AsReadOnly(snapshot);
    }

    public bool Applied { get; }
    public RuntimeChargeStateSnapshot? Before { get; }
    public RuntimeChargeStateSnapshot? After { get; }
    public IReadOnlyList<ChargePolicyDiagnostic> Diagnostics { get; }
}

public sealed record ChargeDamageModifier
{
    public ChargeDamageModifier(decimal multiplier, ChargeKind? chargeKind = null)
        : this(multiplier, chargeKind, sourceState: null)
    {
    }

    internal ChargeDamageModifier(
        decimal multiplier,
        ChargeKind? chargeKind,
        BattleChargeState? sourceState)
    {
        if (multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Charge multiplier must be positive.");
        }
        if (chargeKind is ChargeKind kind && !Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(chargeKind));
        }

        Multiplier = multiplier;
        ChargeKind = chargeKind;
        SourceState = sourceState;
    }

    public decimal Multiplier { get; }
    public ChargeKind? ChargeKind { get; }
    public bool IsCharged => ChargeKind.HasValue;

    internal BattleChargeState? SourceState { get; }
}

public sealed record ChargeConsumptionResult
{
    internal ChargeConsumptionResult(
        RuntimeChargeStateSnapshot? before,
        RuntimeChargeStateSnapshot? after,
        IEnumerable<ChargeKind>? consumedChargeKinds = null)
    {
        Before = before;
        After = after;
        ChargeKind[] consumed = (consumedChargeKinds ?? []).Distinct().Order().ToArray();
        if (consumed.Any(kind => !Enum.IsDefined(kind)))
        {
            throw new ArgumentOutOfRangeException(nameof(consumedChargeKinds));
        }

        ConsumedChargeKinds = Array.AsReadOnly(consumed);
    }

    public RuntimeChargeStateSnapshot? Before { get; }
    public RuntimeChargeStateSnapshot? After { get; }
    public IReadOnlyList<ChargeKind> ConsumedChargeKinds { get; }
    public bool StateChanged => ConsumedChargeKinds.Count > 0;
}

public sealed record ChargePolicyValidationResult
{
    public ChargePolicyValidationResult(IEnumerable<ChargePolicyDiagnostic>? diagnostics = null)
    {
        ChargePolicyDiagnostic[] snapshot = (diagnostics ?? []).ToArray();
        if (snapshot.Any(diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Charge diagnostics cannot contain null entries.", nameof(diagnostics));
        }

        Diagnostics = Array.AsReadOnly(snapshot);
    }

    public bool IsValid => Diagnostics.Count == 0;
    public IReadOnlyList<ChargePolicyDiagnostic> Diagnostics { get; }
    public static ChargePolicyValidationResult Valid { get; } = new();
}

public interface IChargePolicyService
{
    ContentId PolicyId { get; }
    ChargeApplicationAssessment Assess(ChargeApplicationRequest request);
    ChargeApplicationResult Apply(ChargeApplicationRequest request);
    ChargeDamageModifier ResolveDamageModifier(RuntimeActorState actor, DamageElement element);
    ChargeConsumptionResult CompleteAction(
        RuntimeActorState actor,
        IEnumerable<ChargeDamageModifier> participatingCharges);
    ChargePolicyValidationResult ValidateState(RuntimeChargeStateSnapshot state);
}

public interface IChargePolicyResolver
{
    bool TryResolve(ContentId policyId, out IChargePolicyService? service);
}

public sealed class ChargePolicyRegistry : IChargePolicyResolver
{
    private readonly IReadOnlyDictionary<ContentId, IChargePolicyService> _services;

    public ChargePolicyRegistry(IEnumerable<IChargePolicyService> services)
    {
        IChargePolicyService[] snapshot =
            (services ?? throw new ArgumentNullException(nameof(services))).ToArray();
        if (snapshot.Any(service => service is null))
        {
            throw new ArgumentException("Charge policy services cannot contain null entries.", nameof(services));
        }
        if (snapshot.Any(service => !service.PolicyId.IsValid))
        {
            throw new ArgumentException("Charge policy IDs cannot be empty.", nameof(services));
        }

        try
        {
            _services = new System.Collections.ObjectModel.ReadOnlyDictionary<ContentId, IChargePolicyService>(
                snapshot.ToDictionary(service => service.PolicyId));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Charge policy IDs must be unique.", nameof(services), exception);
        }
    }

    public static ChargePolicyRegistry CreateStandard() => new(
        [new DisabledChargePolicy(), new SplitChargePolicy(), new UnifiedChargePolicy()]);

    public bool TryResolve(ContentId policyId, out IChargePolicyService? service) =>
        _services.TryGetValue(policyId, out service);
}

/// <summary>
/// Supplied explicit composition for games that do not use charge gameplay.
/// </summary>
public sealed class DisabledChargePolicy : ChargePolicyServiceBase
{
    public DisabledChargePolicy(ContentId? policyId = null)
        : base(policyId ?? StandardChargePolicyIds.Disabled)
    {
    }

    protected override ChargeKind? Normalize(ChargeKind requested) => null;

    protected override ChargeKind? Match(DamageElement element) => null;
}

public sealed class SplitChargePolicy : ChargePolicyServiceBase
{
    public SplitChargePolicy(ContentId? policyId = null)
        : base(policyId ?? StandardChargePolicyIds.Split)
    {
    }

    protected override ChargeKind? Normalize(ChargeKind requested) => requested switch
    {
        ChargeKind.Physical => ChargeKind.Physical,
        ChargeKind.Magical => ChargeKind.Magical,
        _ => null
    };

    protected override ChargeKind? Match(DamageElement element) =>
        element == DamageElement.Physical ? ChargeKind.Physical : ChargeKind.Magical;
}

public sealed class UnifiedChargePolicy : ChargePolicyServiceBase
{
    public UnifiedChargePolicy(ContentId? policyId = null)
        : base(policyId ?? StandardChargePolicyIds.Unified)
    {
    }

    protected override ChargeKind? Normalize(ChargeKind requested) =>
        requested == ChargeKind.General ? ChargeKind.General : null;

    protected override ChargeKind? Match(DamageElement element) => ChargeKind.General;
}

public abstract class ChargePolicyServiceBase : IChargePolicyService
{
    protected ChargePolicyServiceBase(ContentId policyId)
    {
        if (!policyId.IsValid)
        {
            throw new ArgumentException("Charge policy ID cannot be empty.", nameof(policyId));
        }

        PolicyId = policyId;
    }

    public ContentId PolicyId { get; }

    public ChargeApplicationAssessment Assess(ChargeApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ChargePolicyDiagnostic? compatibility = CompatibilityDiagnostic(request.Target);
        if (compatibility is not null)
        {
            return RejectedAssessment(compatibility);
        }
        if (!RuntimeStatusLifetimeDomain.IsValid(request.Lifetime))
        {
            return RejectedAssessment(new ChargePolicyDiagnostic(
                ChargePolicyDiagnosticCode.InvalidDuration,
                "Charge duration must be a valid turn, phase, battle, permanent, or omitted duration.",
                request.ChargeKind));
        }

        ChargeKind? storedKind = Normalize(request.ChargeKind);
        if (storedKind is null)
        {
            return RejectedAssessment(new ChargePolicyDiagnostic(
                ChargePolicyDiagnosticCode.UnsupportedChargeKind,
                $"Charge kind '{request.ChargeKind}' is unsupported by policy '{PolicyId}'.",
                request.ChargeKind));
        }
        if (request.Target.Charges.ContainsKey(storedKind.Value))
        {
            return RejectedAssessment(new ChargePolicyDiagnostic(
                ChargePolicyDiagnosticCode.AlreadyInEffect,
                $"Charge '{storedKind}' is already in effect.",
                storedKind));
        }

        return new ChargeApplicationAssessment(true, storedKind);
    }

    public ChargeApplicationResult Apply(ChargeApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuntimeChargeStateSnapshot? before = request.Target.CaptureChargeState();
        ChargeApplicationAssessment assessment = Assess(request);
        if (!assessment.CanApply)
        {
            return new ChargeApplicationResult(false, before, before, assessment.Diagnostics);
        }

        request.Target.AddCharge(
            PolicyId,
            assessment.StoredChargeKind!.Value,
            new BattleChargeState(request.Multiplier, request.Lifetime));
        return new ChargeApplicationResult(
            true,
            before,
            request.Target.CaptureChargeState());
    }

    public ChargeDamageModifier ResolveDamageModifier(RuntimeActorState actor, DamageElement element)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!Enum.IsDefined(element))
        {
            throw new ArgumentOutOfRangeException(nameof(element));
        }

        RequireCompatible(actor);
        ChargeKind? kind = Match(element);
        return kind is ChargeKind matched && actor.Charges.TryGetValue(matched, out BattleChargeState? state)
            ? new ChargeDamageModifier(state.Multiplier, matched, state)
            : new ChargeDamageModifier(1m);
    }

    public ChargeConsumptionResult CompleteAction(
        RuntimeActorState actor,
        IEnumerable<ChargeDamageModifier> participatingCharges)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ChargeDamageModifier[] participation =
            (participatingCharges ?? throw new ArgumentNullException(nameof(participatingCharges)))
            .ToArray();
        if (participation.Any(modifier => modifier is null))
        {
            throw new ArgumentException(
                "Participating charge modifiers cannot contain null entries.",
                nameof(participatingCharges));
        }
        if (participation.Any(modifier => modifier.IsCharged && modifier.SourceState is null))
        {
            throw new ArgumentException(
                "Participating charged modifiers must originate from ResolveDamageModifier.",
                nameof(participatingCharges));
        }
        if (participation.Any(modifier =>
                modifier.ChargeKind is ChargeKind kind && Normalize(kind) != kind))
        {
            throw new ArgumentException(
                $"Participating charge modifiers must use kinds supported by policy '{PolicyId}'.",
                nameof(participatingCharges));
        }

        RequireCompatible(actor);
        RuntimeChargeStateSnapshot? before = actor.CaptureChargeState();
        ChargeKind[] candidates = participation
            .Where(modifier => modifier.ChargeKind.HasValue)
            .GroupBy(modifier => modifier.ChargeKind!.Value)
            .Where(group =>
                actor.Charges.TryGetValue(group.Key, out BattleChargeState? current) &&
                group.Any(modifier => ReferenceEquals(modifier.SourceState, current)))
            .Select(group => group.Key)
            .Order()
            .ToArray();
        var consumed = new List<ChargeKind>();
        foreach (ChargeKind kind in candidates)
        {
            if (actor.RemoveCharge(PolicyId, kind, StatusRemovalCause.Consumed))
            {
                consumed.Add(kind);
            }
        }

        return new ChargeConsumptionResult(
            before,
            actor.CaptureChargeState(),
            consumed);
    }

    public ChargePolicyValidationResult ValidateState(RuntimeChargeStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var diagnostics = new List<ChargePolicyDiagnostic>();
        if (state.PolicyId != PolicyId)
        {
            diagnostics.Add(new ChargePolicyDiagnostic(
                ChargePolicyDiagnosticCode.PolicyMismatch,
                $"Charge state belongs to policy '{state.PolicyId}', not selected policy '{PolicyId}'."));
        }

        foreach (RuntimeChargeSnapshot charge in state.Charges)
        {
            if (Normalize(charge.Kind) != charge.Kind)
            {
                diagnostics.Add(new ChargePolicyDiagnostic(
                    ChargePolicyDiagnosticCode.UnsupportedChargeKind,
                    $"Charge kind '{charge.Kind}' is incompatible with policy '{PolicyId}'.",
                    charge.Kind));
            }
            if (!RuntimeStatusLifetimeDomain.IsValid(charge.Lifetime))
            {
                diagnostics.Add(new ChargePolicyDiagnostic(
                    ChargePolicyDiagnosticCode.InvalidDuration,
                    $"Charge '{charge.Kind}' contains an invalid retained duration.",
                    charge.Kind));
            }
        }

        if (state.Charges.Select(charge => charge.Kind).Distinct().Count() != state.Charges.Count)
        {
            diagnostics.Add(new ChargePolicyDiagnostic(
                ChargePolicyDiagnosticCode.IncompatibleState,
                "Charge state contains duplicate charge kinds."));
        }

        return diagnostics.Count == 0
            ? ChargePolicyValidationResult.Valid
            : new ChargePolicyValidationResult(diagnostics);
    }

    protected abstract ChargeKind? Normalize(ChargeKind requested);
    protected abstract ChargeKind? Match(DamageElement element);

    private ChargePolicyDiagnostic? CompatibilityDiagnostic(RuntimeActorState actor) =>
        actor.ChargePolicyId is ContentId active && active != PolicyId
            ? new ChargePolicyDiagnostic(
                ChargePolicyDiagnosticCode.PolicyMismatch,
                $"Actor '{actor.InstanceId}' charge state belongs to policy '{active}', not '{PolicyId}'.")
            : null;

    private void RequireCompatible(RuntimeActorState actor)
    {
        ChargePolicyDiagnostic? diagnostic = CompatibilityDiagnostic(actor);
        if (diagnostic is not null)
        {
            throw new InvalidOperationException(diagnostic.Message);
        }
    }

    private static ChargeApplicationAssessment RejectedAssessment(ChargePolicyDiagnostic diagnostic) =>
        new(false, diagnostics: [diagnostic]);

}
