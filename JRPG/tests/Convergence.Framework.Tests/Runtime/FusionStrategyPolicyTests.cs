using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Inheritance;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class FusionStrategyPolicyTests
{
    [Fact]
    public void RecipeResultParameters_AreRecursivelyImmutableForDirectRuntimeCallers()
    {
        var nested = new Dictionary<string, object?> { ["enabled"] = true };
        var values = new List<object?> { 1, nested };
        var result = new FusionRecipeResultSnapshot(
            FusionResultOperationKind.CreateEntity,
            ResultEntityId: Id("child"),
            Parameters: [new KeyValuePair<string, object?>("values", values)]);

        values[0] = 99;
        nested["enabled"] = false;

        IReadOnlyList<object?> frozen = Assert.IsAssignableFrom<IReadOnlyList<object?>>(
            result.Parameters["values"]);
        Assert.Equal(1L, frozen[0]);
        Assert.Equal(
            true,
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(frozen[1])["enabled"]);
    }

    [Fact]
    public void Resolver_RequiresRegisteredAccidentPolicyAndDoesNotInventOne()
    {
        TestFusionRepository missingPolicyRepository = Repository(
            recipes:
            [
                CreateRecipe("parent_a", "parent_b", "child", accidentPolicyId: "missing_accident")
            ]);
        FusionPolicyRegistry policies = Policies();
        var missingPolicyResolver = new FusionResultResolver(
            missingPolicyRepository,
            new ThrowingRandomSource(),
            policies);

        FusionResolvedResult rejected = missingPolicyResolver.Resolve(new FusionResultRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b")));

        Assert.False(rejected.IsSuccessful);
        FusionRuntimeDiagnostic diagnostic = Assert.Single(rejected.Diagnostics);
        Assert.Equal(FusionRuntimeDiagnosticCode.PolicyNotRegistered, diagnostic.Code);

        TestFusionRepository noAccidentRepository = Repository(
            recipes: [CreateRecipe("parent_a", "parent_b", "child")]);
        var noAccidentResolver = new FusionResultResolver(
            noAccidentRepository,
            new ThrowingRandomSource(),
            policies);
        FusionResolvedResult resolved = noAccidentResolver.Resolve(new FusionResultRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b")));

        Assert.True(resolved.IsSuccessful);
        Assert.False(resolved.IsAccident);
    }

    [Fact]
    public void Planner_UsesInjectedSlotAndSacrificePolicies()
    {
        SkillDefinition[] skills =
        [
            Skill("skill_1"),
            Skill("skill_2"),
            Skill("skill_3")
        ];
        TestFusionRepository repository = Repository(
            recipes: [CreateRecipe("parent_a", "parent_b", "child")],
            skills: skills);
        FusionPolicyRegistry policies = new(
            new TieredFusionInheritanceSlotPolicy(
                [
                    new FusionInheritanceSlotTier(0, 0),
                    new FusionInheritanceSlotTier(3, 4)
                ],
                maximumSlots: 10),
            new FixedFusionSacrificePolicy(true, additionalInheritanceSlots: 3));
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), policies);
        var planner = new FusionPlanningService(repository, resolver, new ThrowingRandomSource(), policies);
        FusionParticipantSnapshot first = Participant("parent_a", "race_a", skills: ["skill_1", "skill_2"]);
        FusionParticipantSnapshot second = Participant("parent_b", "race_b", skills: ["skill_3"]);
        FusionParticipantSnapshot sacrifice = Participant("sacrifice", "race_c");

        FusionPlanningResult ordinary = planner.CreatePlan(new FusionPlanningRequest(
            first,
            second,
            Sacrifice: null,
            IsSacrificial: false));
        FusionPlanningResult sacrificial = planner.CreatePlan(new FusionPlanningRequest(
            first,
            second,
            sacrifice,
            IsSacrificial: true));

        Assert.Equal(4, ordinary.MaximumInheritanceSlots);
        Assert.Equal(7, sacrificial.MaximumInheritanceSlots);
        Assert.Equal(3, sacrificial.SacrificeDecision?.AdditionalInheritanceSlots);
    }

    [Fact]
    public void InheritanceSlotHelper_UsesExplicitContextWhileTheCompatibilityOverloadIsContextFree()
    {
        var slotPolicy = new RecordingInheritanceSlotPolicy();
        var policies = new FusionPolicyRegistry(
            slotPolicy,
            new FixedFusionSacrificePolicy(true));
        var planner = new FusionPlanningService(
            Repository(recipes: []),
            new ThrowingFusionResultResolver(),
            new ThrowingRandomSource(),
            policies);
        var context = new FusionPolicyContext(
            [Id("advanced_slots")],
            [new KeyValuePair<ContentId, decimal>(Id("progress"), 4)]);

        int contextualCount = planner.GetInheritanceSlotCount([Skill("skill_1")], context);
        int contextFreeCount = planner.GetInheritanceSlotCount([Skill("skill_1")]);

        Assert.Equal(1, contextualCount);
        Assert.Equal(1, contextFreeCount);
        Assert.Collection(
            slotPolicy.Contexts,
            received => Assert.Same(context, received),
            received => Assert.Same(FusionPolicyContext.Empty, received));
    }

    [Fact]
    public void Planner_RejectsDisabledSacrificeBeforeResolvingOrUsingRandomness()
    {
        TestFusionRepository repository = Repository(recipes: []);
        FusionPolicyRegistry policies = Policies(
            sacrificePolicy: new FixedFusionSacrificePolicy(
                false,
                rejectionMessage: "Requires an authored progression unlock."));
        var planner = new FusionPlanningService(
            repository,
            new ThrowingFusionResultResolver(),
            new ThrowingRandomSource(),
            policies);

        FusionPlanningResult result = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b"),
            Participant("sacrifice", "race_c"),
            IsSacrificial: true));

        Assert.False(result.IsSuccessful);
        FusionRuntimeDiagnostic diagnostic = Assert.Single(result.Result.Diagnostics);
        Assert.Equal(FusionRuntimeDiagnosticCode.SacrificeNotAllowed, diagnostic.Code);
        Assert.Contains("progression unlock", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalystStatBoostPolicy_UsesTypedIdsAndSuppliesPreviewStats()
    {
        var catalystPolicy = new CatalystStatBoostFusionPolicy(
            Id("training_catalyst_boost"),
            [
                new FusionCatalystStatBoostRule(
                    Id("catalyst_core"),
                    [new KeyValuePair<ContentId, int>(Id("strength"), 2)])
            ]);
        TestFusionRepository repository = Repository(
            recipes:
            [
                new FusionRecipeSnapshot(
                    EntityParent("catalyst_core"),
                    EntityParent("target"),
                    new FusionRecipeResultSnapshot(
                        FusionResultOperationKind.StatBoost,
                        PolicyId: catalystPolicy.Id))
            ]);
        FusionPolicyRegistry policies = Policies(resultPolicies: [catalystPolicy]);
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), policies);
        var planner = new FusionPlanningService(repository, resolver, new ThrowingRandomSource(), policies);
        FusionParticipantSnapshot catalyst = Participant(
            "catalyst_core",
            "material",
            displayName: "A Renamed Material");
        FusionParticipantSnapshot target = Participant(
            "target",
            "construct",
            displayName: "A Renamed Target",
            stats: [("strength", 5)]);

        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            catalyst,
            target,
            Sacrifice: null,
            IsSacrificial: false));
        ValidatedFusionInheritanceSelection selection = planner
            .ValidateInheritanceSelection(plan, [])
            .RequireValidSelection();
        FusionPreviewSnapshot preview = Assert.IsType<FusionPreviewSnapshot>(
            new FusionPreviewService().CreatePreview(new FusionPreviewRequest(plan, selection)));

        Assert.Equal(FusionRuntimeOperation.StatBoost, plan.Result.Operation);
        Assert.Equal(Id("training_catalyst_boost"), plan.Result.ResultPolicyId);
        Assert.Equal(7, plan.Result.ResultStats[Id("strength")]);
        Assert.Equal(7, preview.Stats[Id("strength")]);
        Assert.Equal("A Renamed Material", plan.Result.CatalystParent?.DisplayName);
    }

    [Fact]
    public void Resolver_DoesNotInferLegacyCatalystRacesWithoutAnExplicitPolicy()
    {
        TestFusionRepository repository = Repository(recipes: []);
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), Policies());

        FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(
            Participant("ara_mitama", "mitama"),
            Participant("target", "fairy")));

        Assert.False(result.IsSuccessful);
        Assert.Equal(FusionRuntimeDiagnosticCode.NoRecipe, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Planner_RejectsAnAuthoredUnregisteredMutationPolicy()
    {
        TestFusionRepository repository = Repository(
            recipes:
            [
                CreateRecipe(
                    "parent_a",
                    "parent_b",
                    "child",
                    mutationPolicyId: "missing_mutation")
            ]);
        FusionPolicyRegistry policies = Policies();
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), policies);
        var planner = new FusionPlanningService(repository, resolver, new ThrowingRandomSource(), policies);

        FusionPlanningResult result = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: false));

        Assert.False(result.IsSuccessful);
        Assert.Equal(FusionRuntimeDiagnosticCode.PolicyNotRegistered, Assert.Single(result.Result.Diagnostics).Code);
        Assert.Throws<InvalidOperationException>(() => planner.MutateSkill(Id("skill_1"), Id("missing_mutation")));
    }

    [Fact]
    public void AccidentInheritance_PreservesThePlanningContextForEveryMutation()
    {
        var mutationPolicy = new RecordingMutationPolicy(Id("contextual_mutation"));
        TestFusionRepository repository = Repository(
            recipes:
            [
                CreateRecipe(
                    "parent_a",
                    "parent_b",
                    "child",
                    mutationPolicyId: mutationPolicy.Id.ToString())
            ],
            skills: [Skill("skill_1"), Skill("skill_2")]);
        FusionPolicyRegistry policies = Policies(mutationPolicies: [mutationPolicy]);
        var random = new MinimumRandomSource();
        var resolver = new FusionResultResolver(repository, random, policies);
        var planner = new FusionPlanningService(repository, resolver, random, policies);
        var context = new FusionPolicyContext(
            [Id("mutation_unlocked")],
            [new KeyValuePair<ContentId, decimal>(Id("story_progress"), 7)]);
        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a", skills: ["skill_1", "skill_2"]),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: false,
            context));

        FusionAccidentInheritanceResult accident = planner.CreateAccidentInheritance(plan);
        IReadOnlyList<ContentId> inherited = accident.RequireValidSelection().SelectedSkillIds;

        Assert.True(plan.IsSuccessful);
        Assert.True(accident.IsValid);
        Assert.Same(context, plan.PolicyContext);
        Assert.Equal(1, plan.MaximumInheritanceSlots);
        Assert.Equal([Id("skill_1")], inherited);
        Assert.Equal(
            [
                new FusionAccidentInheritanceMutation(Id("skill_1"), Id("skill_1"))
            ],
            accident.Mutations);
        Assert.Collection(
            mutationPolicy.Requests,
            request => Assert.Same(context, request.Context));
        Assert.All(mutationPolicy.Requests, request =>
        {
            Assert.True(request.Context.HasFlag(Id("mutation_unlocked")));
            Assert.True(request.Context.TryGetNumericValue(Id("story_progress"), out decimal progress));
            Assert.Equal(7, progress);
        });
    }

    [Fact]
    public void Resolver_RejectsUnstructuredTokensWithoutACompatibilityPolicy()
    {
        TestFusionRepository repository = Repository(
            recipes:
            [
                new FusionRecipeSnapshot(
                    EntityParent("parent_a"),
                    EntityParent("parent_b"),
                    CompatibilityResultToken: "legacy_race_token")
            ]);
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), Policies());

        FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b")));

        Assert.False(result.IsSuccessful);
        Assert.Equal(FusionRuntimeDiagnosticCode.UnsupportedRecipeFormat, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolver_MatchesMixedSelectorsInEitherParticipantOrder()
    {
        FusionRecipeSnapshot mixedRecipe = new(
            EntityParent("parent_a"),
            RaceParent("race_b"),
            new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("child")));
        TestFusionRepository repository = Repository(recipes: [mixedRecipe]);
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), Policies());
        FusionParticipantSnapshot first = Participant("parent_a", "race_a");
        FusionParticipantSnapshot second = Participant("parent_b", "race_b");

        FusionResolvedResult forward = resolver.Resolve(new FusionResultRequest(first, second));
        FusionResolvedResult reversed = resolver.Resolve(new FusionResultRequest(second, first));

        Assert.Equal(Id("child"), forward.ResultEntityId);
        Assert.Equal(Id("child"), reversed.ResultEntityId);
        Assert.Same(mixedRecipe, forward.MatchedRecipe);
        Assert.Same(mixedRecipe, reversed.MatchedRecipe);
    }

    [Fact]
    public void Resolver_UsesSelectorKindsToDisambiguateCollidingIds()
    {
        FusionRecipeSnapshot entityRecipe = new(
            EntityParent("shared_a"),
            EntityParent("shared_b"),
            new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("entity_child")));
        FusionRecipeSnapshot raceRecipe = new(
            RaceParent("shared_a"),
            RaceParent("shared_b"),
            new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("race_child")));
        var repository = new TestFusionRepository(
            [
                Entity("actual_a", "shared_a"),
                Entity("actual_b", "shared_b"),
                Entity("entity_child", "result"),
                Entity("race_child", "result")
            ],
            [entityRecipe, raceRecipe],
            []);
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), Policies());

        FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(
            Participant("actual_a", "shared_a"),
            Participant("actual_b", "shared_b")));

        Assert.Equal(Id("race_child"), result.ResultEntityId);
        Assert.Same(raceRecipe, result.MatchedRecipe);
    }

    [Fact]
    public void Resolver_PrefersTheMostSpecificMatchingTypedRecipe()
    {
        FusionRecipeSnapshot raceRecipe = new(
            RaceParent("race_a"),
            RaceParent("race_b"),
            new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("race_child")));
        FusionRecipeSnapshot entityRecipe = new(
            EntityParent("parent_a"),
            EntityParent("parent_b"),
            new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("entity_child")));
        var repository = new TestFusionRepository(
            [
                Entity("parent_a", "race_a"),
                Entity("parent_b", "race_b"),
                Entity("race_child", "result"),
                Entity("entity_child", "result")
            ],
            [raceRecipe, entityRecipe],
            []);
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), Policies());

        FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b")));

        Assert.Equal(Id("entity_child"), result.ResultEntityId);
        Assert.Same(entityRecipe, result.MatchedRecipe);
    }

    [Fact]
    public void Resolver_RejectsEqualSpecificityMatchesRegardlessOfRepositoryOrder()
    {
        FusionRecipeSnapshot firstRecipe = new(
            EntityParent("parent_a"),
            RaceParent("race_b"),
            new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("child")));
        FusionRecipeSnapshot secondRecipe = new(
            EntityParent("parent_b"),
            RaceParent("race_a"),
            new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("target")));
        FusionParticipantSnapshot first = Participant("parent_a", "race_a");
        FusionParticipantSnapshot second = Participant("parent_b", "race_b");

        foreach (FusionRecipeSnapshot[] recipes in new[]
                 {
                     new[] { firstRecipe, secondRecipe },
                     new[] { secondRecipe, firstRecipe }
                 })
        {
            TestFusionRepository repository = Repository(recipes: recipes);
            var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), Policies());

            FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(first, second));

            Assert.False(result.IsSuccessful);
            Assert.Null(result.ResultEntityId);
            Assert.Null(result.MatchedRecipe);
            Assert.Equal(
                FusionRuntimeDiagnosticCode.AmbiguousRecipe,
                Assert.Single(result.Diagnostics).Code);
            Assert.Null(resolver.TryResolveDirectCreateResult(
                first.EntityId,
                first.RaceId,
                second.EntityId,
                second.RaceId));
        }
    }

    [Fact]
    public void Resolver_TreatsStructuredResultsAsAuthoritativeOverCompatibilityTokens()
    {
        FusionRecipeSnapshot recipe = new(
            EntityParent("parent_a"),
            EntityParent("parent_b"),
            new FusionRecipeResultSnapshot(
                FusionResultOperationKind.RankOffset,
                ResultRaceId: Id("race_child"),
                RankOffset: 1),
            CompatibilityResultToken: "target");
        TestFusionRepository repository = Repository(recipes: [recipe]);
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), Policies());

        ContentId? direct = resolver.TryResolveDirectCreateResult(
            Id("parent_a"),
            Id("race_a"),
            Id("parent_b"),
            Id("race_b"));
        FusionResolvedResult resolved = resolver.Resolve(new FusionResultRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b")));

        Assert.Null(direct);
        Assert.Equal(FusionRuntimeOperation.RankUpParent, resolved.Operation);
        Assert.Equal(Id("child"), resolved.ResultEntityId);
    }

    [Fact]
    public void Resolver_ValidatesRegisteredCustomPolicyResults()
    {
        var policy = new UnknownEntityResultPolicy();
        TestFusionRepository repository = Repository(
            recipes:
            [
                new FusionRecipeSnapshot(
                    EntityParent("parent_a"),
                    EntityParent("parent_b"),
                    new FusionRecipeResultSnapshot(
                        FusionResultOperationKind.Special,
                        PolicyId: policy.Id))
            ]);
        var resolver = new FusionResultResolver(
            repository,
            new ThrowingRandomSource(),
            Policies(resultPolicies: [policy]));

        FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b")));

        Assert.False(result.IsSuccessful);
        Assert.Equal(FusionRuntimeDiagnosticCode.MissingEntity, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Planner_PreservesParentStateOnlyWhenRankPolicyIdentifiesTransformedParent()
    {
        var policy = new ExplicitRankResultPolicy();
        FusionRecipeSnapshot recipe = new(
            EntityParent("parent_a"),
            EntityParent("parent_b"),
            new FusionRecipeResultSnapshot(
                FusionResultOperationKind.Special,
                PolicyId: policy.Id));
        TestFusionRepository repository = Repository(recipes: [recipe]);
        FusionPolicyRegistry policies = Policies(resultPolicies: [policy]);
        var random = new ThrowingRandomSource();
        var planner = new FusionPlanningService(
            repository,
            new FusionResultResolver(repository, random, policies),
            random,
            policies);
        FusionParticipantSnapshot first = Participant(
            "parent_a",
            "race_a",
            stats: [("strength", 2)]);
        FusionParticipantSnapshot transformed = Participant(
            "parent_b",
            "race_b",
            stats: [("strength", 9)]);

        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            first,
            transformed,
            Sacrifice: null,
            IsSacrificial: false));
        ValidatedFusionInheritanceSelection selection = planner
            .ValidateInheritanceSelection(plan, [])
            .RequireValidSelection();
        FusionPreviewSnapshot preview = Assert.IsType<FusionPreviewSnapshot>(
            new FusionPreviewService().CreatePreview(new FusionPreviewRequest(plan, selection)));

        Assert.Same(transformed, plan.PreviewBaseline);
        Assert.Equal(9, preview.Stats[Id("strength")]);
    }

    [Fact]
    public void PreviewRequest_RequiresValidatedSelectionAndRejectsAnotherPlansSelection()
    {
        SkillDefinition firstSkill = Skill("skill_1");
        SkillDefinition secondSkill = Skill("skill_2");
        TestFusionRepository repository = Repository(
            recipes: [CreateRecipe("parent_a", "parent_b", "child")],
            skills: [firstSkill, secondSkill]);
        FusionPolicyRegistry policies = Policies();
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), policies);
        var planner = new FusionPlanningService(
            repository,
            resolver,
            new ThrowingRandomSource(),
            policies);
        FusionPlanningResult firstPlan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a", skills: ["skill_1"]),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: false));
        FusionPlanningResult secondPlan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a", skills: ["skill_2"]),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: false));
        FusionInheritanceSelectionResult impossibleSelection = planner
            .ValidateInheritanceSelection(firstPlan, [secondSkill.Id]);
        ValidatedFusionInheritanceSelection secondSelection = planner
            .ValidateInheritanceSelection(secondPlan, [secondSkill.Id])
            .RequireValidSelection();

        FusionPreviewSnapshot? preview = new FusionPreviewService().CreatePreview(
            new FusionPreviewRequest(firstPlan, secondSelection));

        Assert.Null(preview);
        Assert.False(impossibleSelection.IsValid);
        Assert.Null(impossibleSelection.ValidatedSelection);
        Assert.Equal(
            FusionInheritanceSelectionDiagnosticCode.SkillUnknown,
            Assert.Single(impossibleSelection.Diagnostics).Code);
        var constructor = Assert.Single(typeof(FusionPreviewRequest).GetConstructors());
        Assert.Equal(
            [typeof(FusionPlanningResult), typeof(ValidatedFusionInheritanceSelection)],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void AccidentInheritance_DerivesCandidatesAndLimitFromTheExactPlan()
    {
        SkillDefinition firstSkill = Skill("skill_1");
        SkillDefinition secondSkill = Skill("skill_2");
        TestFusionRepository repository = Repository(
            recipes: [CreateRecipe("parent_a", "parent_b", "child")],
            skills: [firstSkill, secondSkill]);
        FusionPolicyRegistry policies = Policies();
        var planner = new FusionPlanningService(
            repository,
            new FusionResultResolver(repository, new ThrowingRandomSource(), policies),
            new MinimumRandomSource(),
            policies);
        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a", skills: ["skill_1", "skill_2"]),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: false));

        FusionAccidentInheritanceResult accident = planner.CreateAccidentInheritance(plan);
        ValidatedFusionInheritanceSelection selection = accident.RequireValidSelection();
        FusionPreviewSnapshot? preview = new FusionPreviewService().CreatePreview(
            new FusionPreviewRequest(plan, selection));
        FusionPlanningResult equivalentButDistinctPlan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a", skills: ["skill_1", "skill_2"]),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: false));
        FusionPreviewSnapshot? wrongPlanPreview = new FusionPreviewService().CreatePreview(
            new FusionPreviewRequest(equivalentButDistinctPlan, selection));

        Assert.True(accident.IsValid);
        Assert.Equal(1, plan.MaximumInheritanceSlots);
        Assert.Equal([firstSkill.Id], selection.SelectedSkillIds);
        Assert.Equal(
            [new FusionAccidentInheritanceMutation(firstSkill.Id, firstSkill.Id)],
            accident.Mutations);
        Assert.NotNull(preview);
        Assert.Equal(selection.SelectedSkillIds, preview!.InheritedSkillIds);
        Assert.Null(wrongPlanPreview);

        var method = Assert.Single(typeof(IFusionPlanningService).GetMethods(), candidate =>
            candidate.Name == nameof(IFusionPlanningService.CreateAccidentInheritance));
        Assert.Equal(typeof(FusionAccidentInheritanceResult), method.ReturnType);
        Assert.Equal(
            [typeof(FusionPlanningResult)],
            method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void AccidentInheritance_RejectsAnIneligibleMutationResult()
    {
        SkillDefinition sourceSkill = Skill("skill_1");
        SkillDefinition blockedSkill = Skill("blocked_skill", isInheritable: false);
        var mutationPolicy = new RedirectMutationPolicy(Id("redirect_mutation"), blockedSkill.Id);
        TestFusionRepository repository = Repository(
            recipes:
            [
                CreateRecipe(
                    "parent_a",
                    "parent_b",
                    "child",
                    mutationPolicyId: mutationPolicy.Id.ToString())
            ],
            skills: [sourceSkill, blockedSkill]);
        FusionPolicyRegistry policies = Policies(mutationPolicies: [mutationPolicy]);
        var planner = new FusionPlanningService(
            repository,
            new FusionResultResolver(repository, new ThrowingRandomSource(), policies),
            new MinimumRandomSource(),
            policies);
        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a", skills: ["skill_1"]),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: false));

        FusionAccidentInheritanceResult accident = planner.CreateAccidentInheritance(plan);

        Assert.False(accident.IsValid);
        Assert.Null(accident.ValidatedSelection);
        Assert.Equal(
            new FusionAccidentInheritanceMutation(sourceSkill.Id, blockedSkill.Id),
            Assert.Single(accident.Mutations));
        FusionInheritanceSelectionDiagnostic diagnostic = Assert.Single(accident.Diagnostics);
        Assert.Equal(FusionInheritanceSelectionDiagnosticCode.SkillIneligible, diagnostic.Code);
        Assert.Equal(blockedSkill.Id, diagnostic.SkillId);
        Assert.Equal(FusionInheritanceDecisionCode.SkillNotInheritable, diagnostic.InheritanceDecisionCode);
    }

    [Fact]
    public void PlannerValidation_UsesFinalSlotLimitAndRejectsDuplicateSelections()
    {
        SkillDefinition firstSkill = Skill("skill_1");
        SkillDefinition secondSkill = Skill("skill_2");
        TestFusionRepository repository = Repository(
            recipes: [CreateRecipe("parent_a", "parent_b", "child")],
            skills: [firstSkill, secondSkill]);
        FusionPolicyRegistry policies = Policies();
        var planner = new FusionPlanningService(
            repository,
            new FusionResultResolver(repository, new ThrowingRandomSource(), policies),
            new ThrowingRandomSource(),
            policies);
        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a", skills: ["skill_1", "skill_2"]),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: false));

        FusionInheritanceSelectionResult result = planner.ValidateInheritanceSelection(
            plan,
            [firstSkill.Id, firstSkill.Id, secondSkill.Id]);

        Assert.Equal(1, plan.MaximumInheritanceSlots);
        Assert.False(result.IsValid);
        Assert.Null(result.ValidatedSelection);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == FusionInheritanceSelectionDiagnosticCode.SelectionLimitExceeded);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == FusionInheritanceSelectionDiagnosticCode.SkillDuplicate);
    }

    [Fact]
    public void Planner_RejectsSelectionsAgainstPlansWithoutAuthoritativeInheritanceState()
    {
        TestFusionRepository repository = Repository(recipes: []);
        FusionPolicyRegistry policies = Policies();
        var planner = new FusionPlanningService(
            repository,
            new ThrowingFusionResultResolver(),
            new ThrowingRandomSource(),
            policies);
        FusionPlanningResult failedPlan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b"),
            Sacrifice: null,
            IsSacrificial: true));

        FusionInheritanceSelectionResult result = planner.ValidateInheritanceSelection(failedPlan, []);

        Assert.False(result.IsValid);
        Assert.Null(result.ValidatedSelection);
        Assert.Equal(
            FusionInheritanceSelectionDiagnosticCode.PlanUnavailable,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void PolicyContext_DefensivelySnapshotsFlagsAndNumericValues()
    {
        var flags = new List<ContentId> { Id("feature_enabled") };
        var values = new List<KeyValuePair<ContentId, decimal>>
        {
            new(Id("progress"), 2)
        };
        var context = new FusionPolicyContext(flags, values);

        flags.Clear();
        values.Clear();

        Assert.True(context.HasFlag(Id("feature_enabled")));
        Assert.True(context.TryGetNumericValue(Id("progress"), out decimal progress));
        Assert.Equal(2, progress);
        Assert.IsAssignableFrom<IReadOnlyList<ContentId>>(context.Flags);
        Assert.IsAssignableFrom<IReadOnlyDictionary<ContentId, decimal>>(context.NumericValues);
    }

    private static FusionPolicyRegistry Policies(
        IFusionSacrificePolicy? sacrificePolicy = null,
        IEnumerable<IFusionAccidentPolicy>? accidentPolicies = null,
        IEnumerable<IFusionMutationPolicy>? mutationPolicies = null,
        IEnumerable<IFusionResultPolicy>? resultPolicies = null) =>
        new(
            new TieredFusionInheritanceSlotPolicy(
                [new FusionInheritanceSlotTier(0, 1)],
                maximumSlots: 8),
            sacrificePolicy ?? new FixedFusionSacrificePolicy(true, 2),
            accidentPolicies,
            mutationPolicies,
            resultPolicies);

    private static FusionRecipeSnapshot CreateRecipe(
        string parentA,
        string parentB,
        string child,
        string? accidentPolicyId = null,
        string? mutationPolicyId = null) =>
        new(
            EntityParent(parentA),
            EntityParent(parentB),
            new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id(child)),
            accidentPolicyId is null ? null : Id(accidentPolicyId),
            mutationPolicyId is null ? null : Id(mutationPolicyId));

    private static TestFusionRepository Repository(
        IEnumerable<FusionRecipeSnapshot> recipes,
        IEnumerable<SkillDefinition>? skills = null) =>
        new(
            [
                Entity("parent_a", "race_a"),
                Entity("parent_b", "race_b"),
                Entity("sacrifice", "race_c"),
                Entity("child", "race_child"),
                Entity("target", "construct"),
                Entity("catalyst_core", "material"),
                Entity("ara_mitama", "mitama")
            ],
            recipes,
            skills ?? []);

    private static FusionEntitySnapshot Entity(string id, string race) =>
        new(new EntityDefinition(
            Id(id),
            id,
            string.Empty,
            Id("demon"),
            Id(race),
            rank: 1,
            baseLevel: 1,
            new EntityCapabilitiesDefinition(true, true, true),
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
            []));

    private static FusionParticipantSnapshot Participant(
        string id,
        string race,
        string? displayName = null,
        IEnumerable<string>? skills = null,
        IEnumerable<(string StatId, int Value)>? stats = null) =>
        new(
            RuntimeInstanceId.Parse($"instance_{id}"),
            Id(id),
            displayName ?? id,
            Id(race),
            rank: 1,
            level: 1,
            skills?.Select(Id),
            stats?.Select(pair => new KeyValuePair<ContentId, int>(Id(pair.StatId), pair.Value)));

    private static SkillDefinition Skill(string id, bool isInheritable = true) =>
        new(
            Id(id),
            id,
            string.Empty,
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Physical,
            new SkillInheritanceDefinition(isInheritable));

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static FusionRecipeParentSelectorSnapshot EntityParent(string id) =>
        new(FusionParentSelectorKind.Entity, Id(id));

    private static FusionRecipeParentSelectorSnapshot RaceParent(string id) =>
        new(FusionParentSelectorKind.Race, Id(id));

    private sealed class TestFusionRepository : IFusionContentRepository
    {
        private readonly IReadOnlyList<FusionEntitySnapshot> _entities;
        private readonly IReadOnlyList<FusionRecipeSnapshot> _recipes;
        private readonly IReadOnlyList<SkillDefinition> _skills;

        public TestFusionRepository(
            IEnumerable<FusionEntitySnapshot> entities,
            IEnumerable<FusionRecipeSnapshot> recipes,
            IEnumerable<SkillDefinition> skills)
        {
            _entities = entities.ToArray();
            _recipes = recipes.ToArray();
            _skills = skills.ToArray();
        }

        public IEnumerable<FusionRecipeSnapshot> GetRecipes() => _recipes;

        public bool TryGetEntity(ContentId entityId, out FusionEntitySnapshot? entity)
        {
            entity = _entities.FirstOrDefault(candidate => candidate.Id == entityId);
            return entity is not null;
        }

        public IReadOnlyList<FusionEntitySnapshot> GetEntitiesByRace(ContentId raceId) =>
            _entities.Where(entity => entity.RaceId == raceId).ToArray();

        public bool TryGetSkill(ContentId skillId, out SkillDefinition? skill)
        {
            skill = _skills.FirstOrDefault(candidate => candidate.Id == skillId);
            return skill is not null;
        }

        public IReadOnlyList<SkillDefinition> GetSkills() => _skills;
    }

    private sealed class ThrowingRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) =>
            throw new InvalidOperationException("Randomness was not expected for this test path.");

        public decimal NextUnitDecimal() =>
            throw new InvalidOperationException("Randomness was not expected for this test path.");
    }

    private sealed class RedirectMutationPolicy : IFusionMutationPolicy
    {
        private readonly ContentId _resultSkillId;

        public RedirectMutationPolicy(ContentId id, ContentId resultSkillId)
        {
            Id = id;
            _resultSkillId = resultSkillId;
        }

        public ContentId Id { get; }

        public ContentId Mutate(FusionMutationPolicyRequest request, IRandomSource random) =>
            _resultSkillId;
    }

    private sealed class ThrowingFusionResultResolver : IFusionResultResolver
    {
        public FusionResolvedResult Resolve(FusionResultRequest request) =>
            throw new InvalidOperationException("Resolution must not run for a rejected sacrifice request.");

        public ContentId? TryResolveDirectCreateResult(
            ContentId firstParentId,
            ContentId firstRaceId,
            ContentId secondParentId,
            ContentId secondRaceId) =>
            throw new InvalidOperationException("Resolution must not run for a rejected sacrifice request.");
    }

    private sealed class RecordingInheritanceSlotPolicy : IFusionInheritanceSlotPolicy
    {
        private readonly List<FusionPolicyContext> _contexts = [];

        public IReadOnlyList<FusionPolicyContext> Contexts => _contexts;

        public int GetMaximumSlots(FusionInheritanceSlotPolicyRequest request)
        {
            _contexts.Add(request.Context);
            return request.LegalSkills.Count;
        }
    }

    private sealed class RecordingMutationPolicy(ContentId id) : IFusionMutationPolicy
    {
        private readonly List<FusionMutationPolicyRequest> _requests = [];

        public ContentId Id { get; } = id;
        public IReadOnlyList<FusionMutationPolicyRequest> Requests => _requests;

        public ContentId Mutate(FusionMutationPolicyRequest request, IRandomSource random)
        {
            _requests.Add(request);
            return request.SkillId;
        }
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class UnknownEntityResultPolicy : IFusionResultPolicy
    {
        public ContentId Id { get; } = ContentId.Parse("unknown_entity_result");

        public FusionPolicyResolution Resolve(FusionResultPolicyRequest request) =>
            new(FusionRuntimeOperation.CreateNewEntity, ContentId.Parse("not_in_catalog"));
    }

    private sealed class ExplicitRankResultPolicy : IFusionResultPolicy
    {
        public ContentId Id { get; } = ContentId.Parse("explicit_rank_result");

        public FusionPolicyResolution Resolve(FusionResultPolicyRequest request) =>
            new(
                FusionRuntimeOperation.RankUpParent,
                ContentId.Parse("child"),
                transformedParent: request.SecondParent);
    }
}
