using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Fusion.Inheritance;

public enum FusionInheritanceDecisionCode
{
    Allowed,
    SkillNotInheritable,
    OwnerExclusive,
    ExplicitlyBlocked,
    ExplicitlyAllowed,
    GroupDenied,
    GroupNotAllowed
}

public sealed record FusionInheritanceDecision
{
    public FusionInheritanceDecision(bool isAllowed, FusionInheritanceDecisionCode code)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        bool codeAllowsInheritance = code is
            FusionInheritanceDecisionCode.Allowed or
            FusionInheritanceDecisionCode.ExplicitlyAllowed;
        if (isAllowed != codeAllowsInheritance)
        {
            throw new ArgumentException(
                $"Decision code '{code}' is inconsistent with allowed value '{isAllowed}'.",
                nameof(isAllowed));
        }

        IsAllowed = isAllowed;
        Code = code;
    }

    public bool IsAllowed { get; }
    public FusionInheritanceDecisionCode Code { get; }
    public string ReasonCode => Code switch
    {
        FusionInheritanceDecisionCode.Allowed => "allowed",
        FusionInheritanceDecisionCode.SkillNotInheritable => "skill_not_inheritable",
        FusionInheritanceDecisionCode.OwnerExclusive => "owner_exclusive",
        FusionInheritanceDecisionCode.ExplicitlyBlocked => "explicitly_blocked",
        FusionInheritanceDecisionCode.ExplicitlyAllowed => "explicitly_allowed",
        FusionInheritanceDecisionCode.GroupDenied => "group_denied",
        FusionInheritanceDecisionCode.GroupNotAllowed => "group_not_allowed",
        _ => throw new InvalidOperationException($"Unsupported inheritance decision code '{Code}'.")
    };
}

public interface IFusionInheritanceEvaluator
{
    FusionInheritanceDecision Evaluate(EntityDefinition receivingEntity, SkillDefinition skill);
}

public sealed record FusionInheritancePlanRequest
{
    public FusionInheritancePlanRequest(
        EntityDefinition receivingEntity,
        IEnumerable<SkillDefinition> candidateSkills,
        IEnumerable<ContentId>? knownSkillIds,
        int maximumSelections)
    {
        ArgumentNullException.ThrowIfNull(receivingEntity);
        ArgumentNullException.ThrowIfNull(candidateSkills);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSelections);

        ReceivingEntity = receivingEntity;
        CandidateSkills = Array.AsReadOnly(candidateSkills.ToArray());
        KnownSkillIds = Array.AsReadOnly(knownSkillIds?.ToArray() ?? []);
        MaximumSelections = maximumSelections;
    }

    public EntityDefinition ReceivingEntity { get; }
    public IReadOnlyList<SkillDefinition> CandidateSkills { get; }
    public IReadOnlyList<ContentId> KnownSkillIds { get; }
    public int MaximumSelections { get; }
}

public sealed record FusionInheritanceCandidate
{
    internal FusionInheritanceCandidate(
        SkillDefinition skill,
        FusionInheritanceDecision policyDecision,
        bool isAlreadyKnown)
    {
        Skill = skill;
        PolicyDecision = policyDecision;
        IsAlreadyKnown = isAlreadyKnown;
    }

    public SkillDefinition Skill { get; }
    public FusionInheritanceDecision PolicyDecision { get; }
    public bool IsAlreadyKnown { get; }
    public bool IsSelectable => PolicyDecision.IsAllowed && !IsAlreadyKnown;
    public string AvailabilityReasonCode => !PolicyDecision.IsAllowed
        ? PolicyDecision.ReasonCode
        : IsAlreadyKnown
            ? "already_known"
            : PolicyDecision.ReasonCode;
}

public sealed record FusionInheritancePlan
{
    internal FusionInheritancePlan(
        EntityDefinition receivingEntity,
        int maximumSelections,
        IEnumerable<FusionInheritanceCandidate> candidates,
        IFusionInheritanceEvaluator evaluator)
    {
        ReceivingEntity = receivingEntity;
        MaximumSelections = maximumSelections;
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Evaluator = evaluator;
    }

    internal EntityDefinition ReceivingEntity { get; }
    internal IFusionInheritanceEvaluator Evaluator { get; }
    public ContentId ReceivingEntityId => ReceivingEntity.Id;
    public int MaximumSelections { get; }
    public IReadOnlyList<FusionInheritanceCandidate> Candidates { get; }
}

public interface IFusionInheritancePlanner
{
    FusionInheritancePlan CreatePlan(FusionInheritancePlanRequest request);
}

public enum FusionInheritanceSelectionDiagnosticCode
{
    SelectionLimitExceeded,
    SkillDuplicate,
    SkillUnknown,
    SkillAlreadyKnown,
    SkillIneligible
}

public sealed record FusionInheritanceSelectionDiagnostic(
    FusionInheritanceSelectionDiagnosticCode Code,
    string Message,
    ContentId? SkillId = null,
    FusionInheritanceDecisionCode? InheritanceDecisionCode = null);

public sealed record FusionInheritanceSelectionResult
{
    internal FusionInheritanceSelectionResult(
        IEnumerable<FusionInheritanceSelectionDiagnostic> diagnostics,
        ValidatedFusionInheritanceSelection? validatedSelection)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        ValidatedSelection = validatedSelection;
    }

    public IReadOnlyList<FusionInheritanceSelectionDiagnostic> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0;
    public ValidatedFusionInheritanceSelection? ValidatedSelection { get; }

    public ValidatedFusionInheritanceSelection RequireValidSelection() =>
        ValidatedSelection ?? throw new FusionInheritanceSelectionException(Diagnostics);
}

public sealed class FusionInheritanceSelectionException : Exception
{
    public FusionInheritanceSelectionException(IEnumerable<FusionInheritanceSelectionDiagnostic> diagnostics)
        : this(Array.AsReadOnly(diagnostics.ToArray()))
    {
    }

    private FusionInheritanceSelectionException(
        IReadOnlyList<FusionInheritanceSelectionDiagnostic> diagnostics)
        : base($"Fusion inheritance selection failed with {diagnostics.Count} error(s).")
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<FusionInheritanceSelectionDiagnostic> Diagnostics { get; }
}

public sealed record ValidatedFusionInheritanceSelection
{
    internal ValidatedFusionInheritanceSelection(
        ContentId receivingEntityId,
        int maximumSelections,
        IEnumerable<SkillDefinition> selectedSkills)
    {
        ReceivingEntityId = receivingEntityId;
        MaximumSelections = maximumSelections;
        SelectedSkills = Array.AsReadOnly(selectedSkills.ToArray());
        SelectedSkillIds = Array.AsReadOnly(SelectedSkills.Select(skill => skill.Id).ToArray());
    }

    public ContentId ReceivingEntityId { get; }
    public int MaximumSelections { get; }
    public IReadOnlyList<SkillDefinition> SelectedSkills { get; }
    public IReadOnlyList<ContentId> SelectedSkillIds { get; }
}

public interface IFusionInheritanceSelectionValidator
{
    FusionInheritanceSelectionResult Validate(
        FusionInheritancePlan plan,
        IEnumerable<ContentId> selectedSkillIds);
}
