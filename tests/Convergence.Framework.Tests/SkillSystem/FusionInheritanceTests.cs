using System.Reflection;
using Convergence.Content;
using Convergence.Inheritance;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class FusionInheritanceTests
{
    private readonly FusionInheritanceEvaluator _evaluator = new();
    private readonly FusionInheritancePlanner _planner = new();
    private readonly FusionInheritanceSelectionValidator _selectionValidator = new();

    [Fact]
    public void Evaluator_ReturnsAllowedForUnlistedDenyPolicyGroup()
    {
        EntityDefinition entity = Entity("child", InheritanceGroupPolicyMode.DenyList, [InheritanceGroup.Ice]);
        SkillDefinition skill = Skill("ember_dart", InheritanceGroup.Fire);

        FusionInheritanceDecision decision = _evaluator.Evaluate(entity, skill);

        Assert.True(decision.IsAllowed);
        Assert.Equal(FusionInheritanceDecisionCode.Allowed, decision.Code);
        Assert.Equal("allowed", decision.ReasonCode);
    }

    [Fact]
    public void Decision_RejectsContradictoryAllowedStateAndReasonCode()
    {
        Assert.Throws<ArgumentException>(() => new FusionInheritanceDecision(
            true,
            FusionInheritanceDecisionCode.GroupDenied));
        Assert.Throws<ArgumentException>(() => new FusionInheritanceDecision(
            false,
            FusionInheritanceDecisionCode.ExplicitlyAllowed));
    }

    [Fact]
    public void Evaluator_RejectsNonInheritableSkillBeforeEveryEntityException()
    {
        SkillDefinition skill = Skill("unique_art", InheritanceGroup.Ice, isInheritable: false);
        EntityDefinition entity = Entity(
            "child",
            InheritanceGroupPolicyMode.DenyList,
            [InheritanceGroup.Ice],
            blockedSkillIds: [skill.Id],
            allowedSkillIds: [skill.Id]);

        FusionInheritanceDecision decision = _evaluator.Evaluate(entity, skill);

        Assert.False(decision.IsAllowed);
        Assert.Equal(FusionInheritanceDecisionCode.SkillNotInheritable, decision.Code);
        Assert.Equal("skill_not_inheritable", decision.ReasonCode);
    }

    [Fact]
    public void Evaluator_EnforcesOwnerExclusivityBeforeExplicitLists()
    {
        SkillDefinition skill = Skill(
            "royal_art",
            InheritanceGroup.Support,
            exclusiveOwners: [ContentId.Parse("other_owner")]);
        EntityDefinition entity = Entity(
            "child",
            InheritanceGroupPolicyMode.DenyList,
            [],
            blockedSkillIds: [skill.Id],
            allowedSkillIds: [skill.Id]);

        FusionInheritanceDecision decision = _evaluator.Evaluate(entity, skill);

        Assert.False(decision.IsAllowed);
        Assert.Equal(FusionInheritanceDecisionCode.OwnerExclusive, decision.Code);
        Assert.Equal("owner_exclusive", decision.ReasonCode);
    }

    [Fact]
    public void Evaluator_AllowsExclusiveSkillForItsReceivingOwner()
    {
        ContentId childId = ContentId.Parse("child");
        SkillDefinition skill = Skill(
            "royal_art",
            InheritanceGroup.Support,
            exclusiveOwners: [childId]);
        EntityDefinition entity = Entity(childId.Value, InheritanceGroupPolicyMode.DenyList, []);

        FusionInheritanceDecision decision = _evaluator.Evaluate(entity, skill);

        Assert.True(decision.IsAllowed);
        Assert.Equal(FusionInheritanceDecisionCode.Allowed, decision.Code);
    }

    [Fact]
    public void Evaluator_ExplicitBlockWinsOverExplicitAllow()
    {
        SkillDefinition skill = Skill("frost_shard", InheritanceGroup.Ice);
        EntityDefinition entity = Entity(
            "child",
            InheritanceGroupPolicyMode.DenyList,
            [],
            blockedSkillIds: [skill.Id],
            allowedSkillIds: [skill.Id]);

        FusionInheritanceDecision decision = _evaluator.Evaluate(entity, skill);

        Assert.False(decision.IsAllowed);
        Assert.Equal(FusionInheritanceDecisionCode.ExplicitlyBlocked, decision.Code);
        Assert.Equal("explicitly_blocked", decision.ReasonCode);
    }

    [Fact]
    public void Evaluator_ExplicitAllowOverridesGroupPolicyOnly()
    {
        SkillDefinition skill = Skill("frost_shard", InheritanceGroup.Ice);
        EntityDefinition entity = Entity(
            "child",
            InheritanceGroupPolicyMode.DenyList,
            [InheritanceGroup.Ice],
            allowedSkillIds: [skill.Id]);

        FusionInheritanceDecision decision = _evaluator.Evaluate(entity, skill);

        Assert.True(decision.IsAllowed);
        Assert.Equal(FusionInheritanceDecisionCode.ExplicitlyAllowed, decision.Code);
        Assert.Equal("explicitly_allowed", decision.ReasonCode);
    }

    [Theory]
    [InlineData(InheritanceGroupPolicyMode.DenyList, InheritanceGroup.Ice, FusionInheritanceDecisionCode.GroupDenied, "group_denied")]
    [InlineData(InheritanceGroupPolicyMode.AllowList, InheritanceGroup.Fire, FusionInheritanceDecisionCode.GroupNotAllowed, "group_not_allowed")]
    public void Evaluator_ReportsGroupPolicyRejections(
        InheritanceGroupPolicyMode mode,
        InheritanceGroup listedGroup,
        FusionInheritanceDecisionCode expectedCode,
        string expectedReason)
    {
        EntityDefinition entity = Entity("child", mode, [listedGroup]);
        SkillDefinition skill = Skill("frost_shard", InheritanceGroup.Ice);

        FusionInheritanceDecision decision = _evaluator.Evaluate(entity, skill);

        Assert.False(decision.IsAllowed);
        Assert.Equal(expectedCode, decision.Code);
        Assert.Equal(expectedReason, decision.ReasonCode);
    }

    [Fact]
    public void Evaluator_AllowsListedAllowPolicyGroup()
    {
        EntityDefinition entity = Entity("child", InheritanceGroupPolicyMode.AllowList, [InheritanceGroup.Ice]);
        SkillDefinition skill = Skill("frost_shard", InheritanceGroup.Ice);

        FusionInheritanceDecision decision = _evaluator.Evaluate(entity, skill);

        Assert.True(decision.IsAllowed);
        Assert.Equal(FusionInheritanceDecisionCode.Allowed, decision.Code);
    }

    [Fact]
    public void PassiveFusionFodder_CanCarryIceBoostAcrossTwoGenerations()
    {
        SkillDefinition frostShard = Skill("frost_shard", InheritanceGroup.Ice);
        SkillDefinition iceBoost = Skill(
            "ice_boost",
            InheritanceGroup.Passive,
            activation: SkillActivation.Passive);
        EntityDefinition fodder = Entity(
            "fodder",
            InheritanceGroupPolicyMode.DenyList,
            [InheritanceGroup.Ice]);
        EntityDefinition child = Entity(
            "child",
            InheritanceGroupPolicyMode.AllowList,
            [InheritanceGroup.Passive]);

        FusionInheritancePlan fodderPlan = _planner.CreatePlan(new FusionInheritancePlanRequest(
            fodder,
            [frostShard, iceBoost],
            [],
            1));
        FusionInheritanceCandidate activeIce = Assert.Single(
            fodderPlan.Candidates,
            candidate => candidate.Skill.Id == frostShard.Id);
        FusionInheritanceCandidate passiveBoost = Assert.Single(fodderPlan.Candidates, candidate => candidate.Skill.Id == iceBoost.Id);
        FusionInheritanceSelectionResult fodderSelection = _selectionValidator.Validate(fodderPlan, [iceBoost.Id]);
        FusionInheritancePlan childPlan = _planner.CreatePlan(new FusionInheritancePlanRequest(
            child,
            [iceBoost],
            [],
            1));

        Assert.False(activeIce.IsSelectable);
        Assert.Equal("group_denied", activeIce.AvailabilityReasonCode);
        Assert.True(passiveBoost.IsSelectable);
        Assert.True(fodderSelection.IsValid);
        Assert.True(Assert.Single(childPlan.Candidates).IsSelectable);
        Assert.True(_selectionValidator.Validate(childPlan, [iceBoost.Id]).IsValid);
    }

    [Fact]
    public void Planner_PreservesFirstCandidateOrderDeduplicatesAndMarksKnownSkills()
    {
        var source = new List<SkillDefinition>
        {
            Skill("first", InheritanceGroup.Fire),
            Skill("known", InheritanceGroup.Support),
            Skill("first", InheritanceGroup.Ice),
            Skill("last", InheritanceGroup.Passive, activation: SkillActivation.Passive)
        };
        var known = new List<ContentId> { ContentId.Parse("known") };
        var request = new FusionInheritancePlanRequest(
            Entity("child", InheritanceGroupPolicyMode.DenyList, []),
            source,
            known,
            2);
        source.Clear();
        known.Clear();

        FusionInheritancePlan plan = _planner.CreatePlan(request);

        Assert.Equal(["first", "known", "last"], plan.Candidates.Select(candidate => candidate.Skill.Id.Value));
        FusionInheritanceCandidate knownCandidate = plan.Candidates[1];
        Assert.True(knownCandidate.PolicyDecision.IsAllowed);
        Assert.True(knownCandidate.IsAlreadyKnown);
        Assert.False(knownCandidate.IsSelectable);
        Assert.Equal("already_known", knownCandidate.AvailabilityReasonCode);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FusionInheritanceCandidate>)plan.Candidates).Add(plan.Candidates[0]));
    }

    [Fact]
    public void SelectionValidation_AggregatesLimitDuplicateUnknownKnownAndPolicyFailures()
    {
        SkillDefinition allowed = Skill("allowed", InheritanceGroup.Fire);
        SkillDefinition known = Skill("known", InheritanceGroup.Support);
        SkillDefinition denied = Skill("denied", InheritanceGroup.Ice);
        FusionInheritancePlan plan = _planner.CreatePlan(new FusionInheritancePlanRequest(
            Entity("child", InheritanceGroupPolicyMode.DenyList, [InheritanceGroup.Ice]),
            [allowed, known, denied],
            [known.Id],
            2));

        FusionInheritanceSelectionResult result = _selectionValidator.Validate(
            plan,
            [allowed.Id, allowed.Id, ContentId.Parse("missing"), known.Id, denied.Id]);

        Assert.False(result.IsValid);
        Assert.Null(result.ValidatedSelection);
        Assert.Equal(
            [
                FusionInheritanceSelectionDiagnosticCode.SelectionLimitExceeded,
                FusionInheritanceSelectionDiagnosticCode.SkillDuplicate,
                FusionInheritanceSelectionDiagnosticCode.SkillUnknown,
                FusionInheritanceSelectionDiagnosticCode.SkillAlreadyKnown,
                FusionInheritanceSelectionDiagnosticCode.SkillIneligible
            ],
            result.Diagnostics.Select(diagnostic => diagnostic.Code));
        Assert.Equal(
            FusionInheritanceDecisionCode.GroupDenied,
            result.Diagnostics[^1].InheritanceDecisionCode);
        FusionInheritanceSelectionException exception = Assert.Throws<FusionInheritanceSelectionException>(
            result.RequireValidSelection);
        Assert.Equal(result.Diagnostics, exception.Diagnostics);
    }

    [Fact]
    public void SelectionValidation_PermitsEmptyZeroLimitAndPreservesSelectedOrder()
    {
        EntityDefinition entity = Entity("child", InheritanceGroupPolicyMode.DenyList, []);
        SkillDefinition first = Skill("first", InheritanceGroup.Fire);
        SkillDefinition second = Skill("second", InheritanceGroup.Support);
        FusionInheritancePlan emptyPlan = _planner.CreatePlan(new FusionInheritancePlanRequest(
            entity,
            [first],
            [],
            0));
        FusionInheritancePlan orderedPlan = _planner.CreatePlan(new FusionInheritancePlanRequest(
            entity,
            [first, second],
            [],
            2));

        ValidatedFusionInheritanceSelection empty = _selectionValidator
            .Validate(emptyPlan, [])
            .RequireValidSelection();
        ValidatedFusionInheritanceSelection ordered = _selectionValidator
            .Validate(orderedPlan, [second.Id, first.Id])
            .RequireValidSelection();

        Assert.Empty(empty.SelectedSkills);
        Assert.Equal(0, empty.MaximumSelections);
        Assert.Equal([second.Id, first.Id], ordered.SelectedSkillIds);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SkillDefinition>)ordered.SelectedSkills).Add(first));
    }

    [Fact]
    public void PreviewAndSelectionUseTheSameTypedPolicyRegardlessOfDisplayText()
    {
        EntityDefinition entity = Entity("child", InheritanceGroupPolicyMode.DenyList, [InheritanceGroup.Ice]);
        SkillDefinition original = Skill("frost_shard", InheritanceGroup.Ice, displayName: "Frost Shard");
        SkillDefinition renamed = Skill("frost_shard", InheritanceGroup.Ice, displayName: "A Completely Different Label");

        FusionInheritanceDecision originalDecision = _evaluator.Evaluate(entity, original);
        FusionInheritanceDecision renamedDecision = _evaluator.Evaluate(entity, renamed);
        FusionInheritancePlan plan = _planner.CreatePlan(new FusionInheritancePlanRequest(
            entity,
            [renamed],
            [],
            1));
        FusionInheritanceSelectionResult selection = _selectionValidator.Validate(plan, [renamed.Id]);

        Assert.Equal(originalDecision, renamedDecision);
        Assert.Equal(renamedDecision.Code, Assert.Single(plan.Candidates).PolicyDecision.Code);
        Assert.Equal(
            renamedDecision.Code,
            Assert.Single(selection.Diagnostics).InheritanceDecisionCode);
    }

    [Fact]
    public void FinalSelection_ReusesTheEvaluatorThatCreatedThePlan()
    {
        var evaluator = new CountingInheritanceEvaluator();
        var planner = new FusionInheritancePlanner(evaluator);
        SkillDefinition skill = Skill("ember_dart", InheritanceGroup.Fire);
        FusionInheritancePlan plan = planner.CreatePlan(new FusionInheritancePlanRequest(
            Entity("child", InheritanceGroupPolicyMode.DenyList, []),
            [skill],
            [],
            1));

        FusionInheritanceSelectionResult result = _selectionValidator.Validate(plan, [skill.Id]);

        Assert.True(result.IsValid);
        Assert.Equal(2, evaluator.EvaluationCount);
    }

    [Fact]
    public void PlanningRequest_RejectsNegativeSelectionLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FusionInheritancePlanRequest(
            Entity("child", InheritanceGroupPolicyMode.DenyList, []),
            [],
            [],
            -1));
    }

    [Fact]
    public void PublicFusionInheritanceBoundary_ExposesOnlyCleanFrameworkTypes()
    {
        string[] forbiddenFragments =
        [
            "Newtonsoft",
            "System.Text.Json",
            "Godot",
            "SkillData",
            "PersonaData",
            "Combatant",
            "Database"
        ];
        Type[] publicTypes = typeof(FusionInheritanceEvaluator).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.Namespace == typeof(FusionInheritanceEvaluator).Namespace)
            .ToArray();

        foreach (Type publicType in publicTypes)
        {
            IEnumerable<Type> exposedTypes = publicType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(property => Flatten(property.PropertyType))
                .Concat(publicType.GetConstructors().SelectMany(constructor =>
                    constructor.GetParameters().SelectMany(parameter => Flatten(parameter.ParameterType))))
                .Concat(publicType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(method => Flatten(method.ReturnType).Concat(
                        method.GetParameters().SelectMany(parameter => Flatten(parameter.ParameterType)))));

            Assert.DoesNotContain(exposedTypes, type => forbiddenFragments.Any(fragment =>
                (type.FullName ?? type.Name).Contains(fragment, StringComparison.Ordinal)));
        }
    }

    private static EntityDefinition Entity(
        string id,
        InheritanceGroupPolicyMode mode,
        IEnumerable<InheritanceGroup> groups,
        IEnumerable<ContentId>? blockedSkillIds = null,
        IEnumerable<ContentId>? allowedSkillIds = null)
    {
        return new EntityDefinition(
            ContentId.Parse(id),
            id,
            "Reference fusion result.",
            ContentId.Parse("companion"),
            ContentId.Parse("test_race"),
            1,
            1,
            new EntityCapabilitiesDefinition(true, true, true),
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(mode, groups),
                blockedSkillIds,
                allowedSkillIds),
            []);
    }

    private static SkillDefinition Skill(
        string id,
        InheritanceGroup group,
        bool isInheritable = true,
        IEnumerable<ContentId>? exclusiveOwners = null,
        SkillActivation activation = SkillActivation.Active,
        string? displayName = null)
    {
        return new SkillDefinition(
            ContentId.Parse(id),
            displayName ?? id,
            "Reference inheritance skill.",
            activation,
            activation == SkillActivation.Active ? SkillMenuGroup.Offense : null,
            group,
            new SkillInheritanceDefinition(isInheritable, exclusiveOwners));
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;

        if (type.IsArray)
        {
            foreach (Type nested in Flatten(type.GetElementType()!))
            {
                yield return nested;
            }
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in Flatten(argument))
            {
                yield return nested;
            }
        }
    }

    private sealed class CountingInheritanceEvaluator : IFusionInheritanceEvaluator
    {
        private readonly FusionInheritanceEvaluator _inner = new();

        public int EvaluationCount { get; private set; }

        public FusionInheritanceDecision Evaluate(EntityDefinition receivingEntity, SkillDefinition skill)
        {
            EvaluationCount++;
            return _inner.Evaluate(receivingEntity, skill);
        }
    }
}
