using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class FusionStrategyPolicyTests
{
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
                    Id("catalyst_core"),
                    Id("target"),
                    "stat_boost",
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
        FusionPreviewSnapshot preview = Assert.IsType<FusionPreviewSnapshot>(
            new FusionPreviewService().CreatePreview(new FusionPreviewRequest(plan, [])));

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
    public void Resolver_RejectsUnstructuredTokensWithoutACompatibilityPolicy()
    {
        TestFusionRepository repository = Repository(
            recipes:
            [
                new FusionRecipeSnapshot(Id("parent_a"), Id("parent_b"), "legacy_race_token")
            ]);
        var resolver = new FusionResultResolver(repository, new ThrowingRandomSource(), Policies());

        FusionResolvedResult result = resolver.Resolve(new FusionResultRequest(
            Participant("parent_a", "race_a"),
            Participant("parent_b", "race_b")));

        Assert.False(result.IsSuccessful);
        Assert.Equal(FusionRuntimeDiagnosticCode.UnsupportedRecipeFormat, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolver_ValidatesRegisteredCustomPolicyResults()
    {
        var policy = new UnknownEntityResultPolicy();
        TestFusionRepository repository = Repository(
            recipes:
            [
                new FusionRecipeSnapshot(
                    Id("parent_a"),
                    Id("parent_b"),
                    "special",
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
            Id(parentA),
            Id(parentB),
            child,
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

    private static SkillDefinition Skill(string id) =>
        new(
            Id(id),
            id,
            string.Empty,
            SkillActivation.Active,
            SkillMenuGroup.Offense,
            InheritanceGroup.Physical,
            new SkillInheritanceDefinition(true));

    private static ContentId Id(string value) => ContentId.Parse(value);

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

    private sealed class UnknownEntityResultPolicy : IFusionResultPolicy
    {
        public ContentId Id { get; } = ContentId.Parse("unknown_entity_result");

        public FusionPolicyResolution Resolve(FusionResultPolicyRequest request) =>
            new(FusionRuntimeOperation.CreateNewEntity, ContentId.Parse("not_in_catalog"));
    }
}
