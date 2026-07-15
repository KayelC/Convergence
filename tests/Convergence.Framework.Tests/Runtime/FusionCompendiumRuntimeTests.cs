using Convergence.Content;
using Convergence.Hosting;
using Convergence.Fusion;
using Convergence.Inheritance;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class FusionCompendiumRuntimeTests
{
    [Fact]
    public void ResultResolver_UsesExplicitCreateRankAccidentAndCatalystPolicies()
    {
        var catalystPolicy = new CatalystStatBoostFusionPolicy(
            Id("stat_boost"),
            [
                new FusionCatalystStatBoostRule(
                    Id("catalyst"),
                    [new KeyValuePair<ContentId, int>(Id("strength"), 2)])
            ]);
        var repository = new TestFusionRepository(
            entities:
            [
                Entity("glow_wisp", "fairy", rank: 1, level: 2),
                Entity("greater_glow_wisp", "fairy", rank: 2, level: 10),
                Entity("mire_blob", "foul", rank: 1, level: 4),
                Entity("gale_catalyst", "element", rank: 1, level: 8),
                Entity("catalyst", "material", rank: 1, level: 12),
                Entity("direct_child", "beast", rank: 1, level: 6)
            ],
            recipes:
            [
                new FusionRecipeSnapshot(
                    EntityParent("glow_wisp"),
                    EntityParent("mire_blob"),
                    new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("direct_child")),
                    AccidentPolicyId: Id("accident")),
                new FusionRecipeSnapshot(
                    RaceParent("fairy"),
                    RaceParent("element"),
                    new FusionRecipeResultSnapshot(
                        FusionResultOperationKind.RankOffset,
                        ResultRaceId: Id("fairy"),
                        RankOffset: 1)),
                new FusionRecipeSnapshot(
                    EntityParent("catalyst"),
                    EntityParent("glow_wisp"),
                    new FusionRecipeResultSnapshot(
                        FusionResultOperationKind.StatBoost,
                        PolicyId: Id("stat_boost")))
            ],
            skills: []);
        FusionPolicyRegistry policies = Policies(
            accidentPolicies:
            [
                new ContextualPercentageFusionAccidentPolicy(
                    Id("accident"),
                    defaultChancePercent: 0,
                    Id("danger_level"),
                    matchingValue: 1,
                    matchingChancePercent: 100)
            ],
            resultPolicies: [catalystPolicy]);
        var resolver = new FusionResultResolver(repository, new SequenceRandomSource(), policies);

        FusionResolvedResult direct = resolver.Resolve(new FusionResultRequest(
            Participant("glow_wisp", "fairy", rank: 1, level: 2),
            Participant("mire_blob", "foul", rank: 1, level: 4)));
        Assert.Equal(FusionRuntimeOperation.CreateNewEntity, direct.Operation);
        Assert.Equal(Id("direct_child"), direct.ResultEntityId);
        Assert.False(direct.IsAccident);

        FusionResolvedResult rank = resolver.Resolve(new FusionResultRequest(
            Participant("glow_wisp", "fairy", rank: 1, level: 2),
            Participant("gale_catalyst", "element", rank: 1, level: 8)));
        Assert.Equal(FusionRuntimeOperation.RankUpParent, rank.Operation);
        Assert.Equal(Id("greater_glow_wisp"), rank.ResultEntityId);

        FusionResolvedResult accident = resolver.Resolve(new FusionResultRequest(
            Participant("glow_wisp", "fairy", rank: 1, level: 2),
            Participant("mire_blob", "foul", rank: 1, level: 4),
            new FusionPolicyContext(numericValues:
            [
                new KeyValuePair<ContentId, decimal>(Id("danger_level"), 1)
            ])));
        Assert.True(accident.IsAccident);

        FusionResolvedResult statBoost = resolver.Resolve(new FusionResultRequest(
            Participant("catalyst", "material", rank: 1, level: 12),
            Participant("glow_wisp", "fairy", rank: 1, level: 2)));
        Assert.Equal(FusionRuntimeOperation.StatBoost, statBoost.Operation);
        Assert.Equal(Id("glow_wisp"), statBoost.ResultEntityId);
        Assert.Equal(Id("stat_boost"), statBoost.ResultPolicyId);
        Assert.Equal(2, statBoost.ResultStats[Id("strength")]);
    }

    [Fact]
    public void Planning_UsesTypedInheritanceAndPassiveFusionFodder()
    {
        SkillDefinition frostLance = Skill("frost_lance", InheritanceGroup.Ice);
        SkillDefinition iceBoost = Skill("ice_boost", InheritanceGroup.Passive, SkillActivation.Passive);
        FusionEntitySnapshot child = Entity(
            "child",
            "foul",
            rank: 1,
            level: 5,
            inheritanceRules: new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList, [InheritanceGroup.Ice])));
        var repository = new TestFusionRepository(
            entities: [Entity("glow_wisp", "fairy", 1, 2), Entity("mire_blob", "foul", 1, 4), child],
            recipes:
            [
                new FusionRecipeSnapshot(
                    EntityParent("glow_wisp"),
                    EntityParent("mire_blob"),
                    new FusionRecipeResultSnapshot(FusionResultOperationKind.CreateEntity, Id("child")))
            ],
            skills: [frostLance, iceBoost]);
        var random = new SequenceRandomSource(ints: [50]);
        FusionPolicyRegistry policies = Policies();
        var resolver = new FusionResultResolver(repository, random, policies);
        var planner = new FusionPlanningService(repository, resolver, random, policies, new FusionInheritancePlanner());

        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("glow_wisp", "fairy", 1, 2, ["frost_lance", "ice_boost"]),
            Participant("mire_blob", "foul", 1, 4),
            Sacrifice: null,
            IsSacrificial: false));

        Assert.True(plan.IsSuccessful);
        Assert.Contains(Id("ice_boost"), plan.PickableSkillIds);
        Assert.DoesNotContain(Id("frost_lance"), plan.PickableSkillIds);
        Assert.Contains(plan.DisplaySkills, entry => entry.SkillId == Id("frost_lance") && entry.ReasonCode == "group_denied");
    }

    [Fact]
    public void MutationAndSlots_PreserveEstablishedPolicies()
    {
        SkillDefinition emberDart = Skill("ember_dart", InheritanceGroup.Fire, mutationFamily: "fire", mutationTier: 1);
        SkillDefinition emberWave = Skill("ember_wave", InheritanceGroup.Fire, mutationFamily: "fire", mutationTier: 2);
        var repository = new TestFusionRepository(
            entities: [Entity("glow_wisp", "fairy", 1, 2)],
            recipes: [],
            skills:
            [
                emberDart,
                emberWave,
                Skill("s1", InheritanceGroup.Physical),
                Skill("s2", InheritanceGroup.Physical),
                Skill("s3", InheritanceGroup.Physical),
                Skill("s4", InheritanceGroup.Physical),
                Skill("s5", InheritanceGroup.Physical),
                Skill("s6", InheritanceGroup.Physical),
                Skill("s7", InheritanceGroup.Physical)
            ]);
        FusionPolicyRegistry policies = Policies(
            mutationPolicies: [new AdjacentTierFusionMutationPolicy(Id("mutation"), chancePercent: 100)]);
        var planner = new FusionPlanningService(
            repository,
            new FusionResultResolver(repository, new SequenceRandomSource(), policies),
            new SequenceRandomSource(ints: [0]),
            policies);

        Assert.Equal(2, planner.GetInheritanceSlotCount(repository.GetSkills().Take(7)));
        Assert.Equal(Id("ember_wave"), planner.MutateSkill(Id("ember_dart"), Id("mutation")));
    }

    [Fact]
    public void CompendiumService_RegistersOverwritesPricesAndRejectsRecallBeforeMutation()
    {
        var service = new CompendiumService(new LinearCompendiumRecallPricingPolicy(
            defaultBasePrice: 2000,
            levelFactor: 100,
            statPointFactor: 50,
            skillFactor: 200));
        var empty = new CompendiumStateSnapshot();
        var glowWisp = new CompendiumEntrySnapshot(
            Id("glow_wisp"),
            "Glow Wisp",
            level: 10,
            stats: [new KeyValuePair<ContentId, int>(Id("magic"), 7)],
            skillIds: [Id("recovery_pulse"), Id("ember_dart")]);

        CompendiumRegistrationResult added = service.Register(empty, glowWisp);
        CompendiumRegistrationResult updated = service.Register(added.After, glowWisp with { });

        Assert.Equal(CompendiumRegistrationCode.Added, added.Code);
        Assert.Equal(CompendiumRegistrationCode.Updated, updated.Code);
        Assert.Equal(2000 + 1000 + 350 + 400, service.GetRecallPricing(glowWisp).Cost);

        var customPricing = new LinearCompendiumRecallPricingPolicy(
            defaultBasePrice: 17,
            levelFactor: 3,
            statPointFactor: 5,
            skillFactor: 7);
        Assert.Equal(17 + 30 + 35 + 14, customPricing.GetPricing(new(glowWisp)).Cost);
        Assert.Equal(23 + 30 + 35 + 14, customPricing.GetPricing(new(glowWisp, basePrice: 23)).Cost);

        CompendiumRecallAssessment duplicate = service.AssessRecall(
            updated.After,
            Id("glow_wisp"),
            availableCurrency: 99999,
            alreadyOwned: true,
            hasOpenRosterSlot: true);
        Assert.Equal(CompendiumRecallCode.DuplicateOwned, duplicate.Code);

        CompendiumRecallAssessment valid = service.AssessRecall(
            updated.After,
            Id("glow_wisp"),
            availableCurrency: 99999,
            alreadyOwned: false,
            hasOpenRosterSlot: true);
        Assert.True(valid.CanRecall);
    }

    [Fact]
    public void CompendiumService_RecallPricingIsExplicitAndCanBeFreeOrUnavailable()
    {
        var entry = new CompendiumEntrySnapshot(Id("sample"), "Sample", level: 1);
        var registrationOnly = new CompendiumService();
        var freeRecall = new CompendiumService(new FixedCompendiumRecallPricingPolicy(0));
        var gatedRecall = new CompendiumService(new UnavailableRecallPricingPolicy());
        CompendiumRegistrationResult registration = registrationOnly.Register(
            new CompendiumStateSnapshot(),
            entry);
        CompendiumStateSnapshot state = registration.After;

        CompendiumRecallPricingDecision unavailablePricing = registrationOnly.GetRecallPricing(entry);
        CompendiumRecallAssessment unavailable = registrationOnly.AssessRecall(
            state,
            entry.EntityId,
            availableCurrency: 0,
            alreadyOwned: false,
            hasOpenRosterSlot: true);
        CompendiumRecallAssessment free = freeRecall.AssessRecall(
            state,
            entry.EntityId,
            availableCurrency: 0,
            alreadyOwned: false,
            hasOpenRosterSlot: true);
        CompendiumRecallAssessment gated = gatedRecall.AssessRecall(
            state,
            entry.EntityId,
            availableCurrency: 0,
            alreadyOwned: false,
            hasOpenRosterSlot: true);

        Assert.Equal(CompendiumRegistrationCode.Added, registration.Code);
        Assert.False(unavailablePricing.IsAvailable);
        Assert.Equal(CompendiumRecallCode.RecallUnavailable, unavailable.Code);
        Assert.DoesNotContain("Credits", Assert.Single(unavailable.Diagnostics).Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(free.CanRecall);
        Assert.Equal(0, free.Cost);
        Assert.Equal(CompendiumRecallCode.RecallUnavailable, gated.Code);
        Assert.Equal("Recall has not been unlocked.", Assert.Single(gated.Diagnostics).Message);
    }

    [Fact]
    public void CompendiumService_AcquisitionIsIdempotentAndDoesNotReplaceAnExistingRecord()
    {
        var service = new CompendiumService();
        var original = new CompendiumEntrySnapshot(Id("sample"), "Original Record", level: 3);
        var laterAcquisition = new CompendiumEntrySnapshot(Id("sample"), "Later Acquisition", level: 9);

        CompendiumRegistrationResult first = service.RecordAcquisition(
            new CompendiumStateSnapshot(),
            original);
        CompendiumRegistrationResult repeated = service.RecordAcquisition(first.After, laterAcquisition);

        Assert.Equal(CompendiumRegistrationCode.Added, first.Code);
        Assert.Equal(CompendiumRegistrationCode.AlreadyRegistered, repeated.Code);
        Assert.Same(first.After, repeated.Before);
        Assert.Same(first.After, repeated.After);
        Assert.Same(first.Entry, repeated.Entry);
        Assert.Equal("Original Record", Assert.Single(repeated.After.Entries).DisplayName);
        Assert.Equal(3, repeated.Entry!.Level);
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

    private sealed class UnavailableRecallPricingPolicy : ICompendiumRecallPricingPolicy
    {
        public CompendiumRecallPricingDecision GetPricing(CompendiumRecallPricingRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return CompendiumRecallPricingDecision.Unavailable("Recall has not been unlocked.");
        }
    }

    private static FusionRecipeParentSelectorSnapshot EntityParent(string id) =>
        new(FusionParentSelectorKind.Entity, Id(id));

    private static FusionRecipeParentSelectorSnapshot RaceParent(string id) =>
        new(FusionParentSelectorKind.Race, Id(id));

    private static FusionEntitySnapshot Entity(
        string id,
        string race,
        int rank,
        int level,
        EntityInheritanceRulesDefinition? inheritanceRules = null) =>
        new(new EntityDefinition(
            Id(id),
            id,
            string.Empty,
            Id("companion"),
            Id(race),
            rank,
            level,
            new EntityCapabilitiesDefinition(true, true, true),
            inheritanceRules ?? new EntityInheritanceRulesDefinition(new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
            []));

    private static FusionParticipantSnapshot Participant(
        string id,
        string race,
        int rank,
        int level,
        IEnumerable<string>? skills = null) =>
        new(
            RuntimeInstanceId.Parse($"test:{id}"),
            Id(id),
            id,
            Id(race),
            rank,
            level,
            skills?.Select(Id));

    private static SkillDefinition Skill(
        string id,
        InheritanceGroup group,
        SkillActivation activation = SkillActivation.Active,
        string? mutationFamily = null,
        int? mutationTier = null) =>
        new(
            Id(id),
            id,
            string.Empty,
            activation,
            activation == SkillActivation.Active ? SkillMenuGroup.Offense : null,
            group,
            new SkillInheritanceDefinition(true),
            mutationFamily is null || mutationTier is null
                ? null
                : new SkillMutationDefinition(Id(mutationFamily), mutationTier.Value));

    private static FusionPolicyRegistry Policies(
        IEnumerable<IFusionAccidentPolicy>? accidentPolicies = null,
        IEnumerable<IFusionMutationPolicy>? mutationPolicies = null,
        IEnumerable<IFusionResultPolicy>? resultPolicies = null,
        IFusionSacrificePolicy? sacrificePolicy = null) =>
        new(
            new TieredFusionInheritanceSlotPolicy(
                [
                    new FusionInheritanceSlotTier(0, 1),
                    new FusionInheritanceSlotTier(7, 2),
                    new FusionInheritanceSlotTier(10, 3),
                    new FusionInheritanceSlotTier(14, 4),
                    new FusionInheritanceSlotTier(19, 5),
                    new FusionInheritanceSlotTier(24, 6)
                ],
                maximumSlots: 8),
            sacrificePolicy ?? new FixedFusionSacrificePolicy(true, 2),
            accidentPolicies,
            mutationPolicies,
            resultPolicies);

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

    private sealed class SequenceRandomSource : IRandomSource
    {
        private readonly Queue<int> _ints;

        public SequenceRandomSource(IEnumerable<int>? ints = null)
        {
            _ints = new Queue<int>(ints ?? []);
        }

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            int value = _ints.Count == 0 ? minimumInclusive : _ints.Dequeue();
            Assert.InRange(value, minimumInclusive, maximumExclusive - 1);
            return value;
        }

        public decimal NextUnitDecimal() => 0.5m;
    }
}
