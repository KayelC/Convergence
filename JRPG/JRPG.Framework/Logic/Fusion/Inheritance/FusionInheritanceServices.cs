using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Fusion.Inheritance;

public sealed class FusionInheritanceEvaluator : IFusionInheritanceEvaluator
{
    public FusionInheritanceDecision Evaluate(EntityDefinition receivingEntity, SkillDefinition skill)
    {
        ArgumentNullException.ThrowIfNull(receivingEntity);
        ArgumentNullException.ThrowIfNull(skill);

        if (!skill.Inheritance.IsInheritable)
        {
            return Denied(FusionInheritanceDecisionCode.SkillNotInheritable);
        }

        if (skill.Inheritance.ExclusiveOwnerEntityIds.Count > 0 &&
            !skill.Inheritance.ExclusiveOwnerEntityIds.Contains(receivingEntity.Id))
        {
            return Denied(FusionInheritanceDecisionCode.OwnerExclusive);
        }

        EntityInheritanceRulesDefinition rules = receivingEntity.InheritanceRules;
        if (rules.BlockedSkillIds.Contains(skill.Id))
        {
            return Denied(FusionInheritanceDecisionCode.ExplicitlyBlocked);
        }

        if (rules.AllowedSkillIds.Contains(skill.Id))
        {
            return Allowed(FusionInheritanceDecisionCode.ExplicitlyAllowed);
        }

        bool groupIsListed = rules.GroupPolicy.GroupIds.Contains(skill.InheritanceGroup);
        return rules.GroupPolicy.Mode switch
        {
            InheritanceGroupPolicyMode.DenyList when groupIsListed =>
                Denied(FusionInheritanceDecisionCode.GroupDenied),
            InheritanceGroupPolicyMode.DenyList =>
                Allowed(FusionInheritanceDecisionCode.Allowed),
            InheritanceGroupPolicyMode.AllowList when groupIsListed =>
                Allowed(FusionInheritanceDecisionCode.Allowed),
            InheritanceGroupPolicyMode.AllowList =>
                Denied(FusionInheritanceDecisionCode.GroupNotAllowed),
            _ => throw new InvalidOperationException(
                $"Unsupported inheritance group policy mode '{rules.GroupPolicy.Mode}'.")
        };
    }

    private static FusionInheritanceDecision Allowed(FusionInheritanceDecisionCode code) => new(true, code);
    private static FusionInheritanceDecision Denied(FusionInheritanceDecisionCode code) => new(false, code);
}

public sealed class FusionInheritancePlanner : IFusionInheritancePlanner
{
    private readonly IFusionInheritanceEvaluator _evaluator;

    public FusionInheritancePlanner()
        : this(new FusionInheritanceEvaluator())
    {
    }

    public FusionInheritancePlanner(IFusionInheritanceEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        _evaluator = evaluator;
    }

    public FusionInheritancePlan CreatePlan(FusionInheritancePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var knownSkillIds = request.KnownSkillIds.ToHashSet();
        var seenCandidateIds = new HashSet<ContentId>();
        var candidates = new List<FusionInheritanceCandidate>();

        foreach (SkillDefinition skill in request.CandidateSkills)
        {
            ArgumentNullException.ThrowIfNull(skill);
            if (!seenCandidateIds.Add(skill.Id))
            {
                continue;
            }

            candidates.Add(new FusionInheritanceCandidate(
                skill,
                _evaluator.Evaluate(request.ReceivingEntity, skill),
                knownSkillIds.Contains(skill.Id)));
        }

        return new FusionInheritancePlan(
            request.ReceivingEntity,
            request.MaximumSelections,
            candidates,
            _evaluator);
    }
}

public sealed class FusionInheritanceSelectionValidator : IFusionInheritanceSelectionValidator
{
    public FusionInheritanceSelectionResult Validate(
        FusionInheritancePlan plan,
        IEnumerable<ContentId> selectedSkillIds)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selectedSkillIds);

        ContentId[] selectedIds = selectedSkillIds.ToArray();
        var diagnostics = new List<FusionInheritanceSelectionDiagnostic>();
        if (selectedIds.Length > plan.MaximumSelections)
        {
            diagnostics.Add(new FusionInheritanceSelectionDiagnostic(
                FusionInheritanceSelectionDiagnosticCode.SelectionLimitExceeded,
                $"Selected {selectedIds.Length} skills, but the fusion allows at most {plan.MaximumSelections}."));
        }

        Dictionary<ContentId, FusionInheritanceCandidate> candidates = plan.Candidates
            .ToDictionary(candidate => candidate.Skill.Id);
        var seenSkillIds = new HashSet<ContentId>();
        var selectedSkills = new List<SkillDefinition>();

        foreach (ContentId skillId in selectedIds)
        {
            if (!seenSkillIds.Add(skillId))
            {
                diagnostics.Add(new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.SkillDuplicate,
                    $"Skill '{skillId}' was selected more than once.",
                    skillId));
                continue;
            }

            if (!candidates.TryGetValue(skillId, out FusionInheritanceCandidate? candidate))
            {
                diagnostics.Add(new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.SkillUnknown,
                    $"Skill '{skillId}' is not part of this fusion inheritance plan.",
                    skillId));
                continue;
            }

            FusionInheritanceDecision decision = plan.Evaluator.Evaluate(
                plan.ReceivingEntity,
                candidate.Skill);
            if (!decision.IsAllowed)
            {
                diagnostics.Add(new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.SkillIneligible,
                    $"Skill '{skillId}' cannot be inherited: {decision.ReasonCode}.",
                    skillId,
                    decision.Code));
                continue;
            }

            if (candidate.IsAlreadyKnown)
            {
                diagnostics.Add(new FusionInheritanceSelectionDiagnostic(
                    FusionInheritanceSelectionDiagnosticCode.SkillAlreadyKnown,
                    $"Skill '{skillId}' is already known by the fusion result.",
                    skillId));
                continue;
            }

            selectedSkills.Add(candidate.Skill);
        }

        ValidatedFusionInheritanceSelection? validatedSelection = diagnostics.Count == 0
            ? new ValidatedFusionInheritanceSelection(
                plan.Authority,
                plan.ReceivingEntityId,
                plan.MaximumSelections,
                selectedSkills)
            : null;

        return new FusionInheritanceSelectionResult(diagnostics, validatedSelection);
    }
}
