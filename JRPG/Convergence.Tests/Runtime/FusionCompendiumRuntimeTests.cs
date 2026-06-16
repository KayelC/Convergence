using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Inheritance;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Runtime;

public sealed class FusionCompendiumRuntimeTests
{
    [Fact]
    public void ResultResolver_PreservesSpecificRankAccidentAndMitamaRules()
    {
        var repository = new TestFusionRepository(
            entities:
            [
                Entity("pixie", "fairy", rank: 1, level: 2),
                Entity("high_pixie", "fairy", rank: 2, level: 10),
                Entity("slime", "foul", rank: 1, level: 4),
                Entity("aeros", "element", rank: 1, level: 8),
                Entity("ara_mitama", "mitama", rank: 1, level: 12),
                Entity("direct_child", "beast", rank: 1, level: 6)
            ],
            recipes:
            [
                new FusionRecipeSnapshot(Id("pixie"), Id("slime"), "direct_child"),
                new FusionRecipeSnapshot(Id("fairy"), Id("foul"), "fairy"),
                new FusionRecipeSnapshot(Id("fairy"), Id("element"), "1")
            ],
            skills: []);
        var resolver = new FusionResultResolver(repository, new SequenceRandomSource(ints: [50, 3, 0]));

        FusionResolvedResult direct = resolver.Resolve(new FusionResultRequest(
            Participant("pixie", "fairy", rank: 1, level: 2),
            Participant("slime", "foul", rank: 1, level: 4),
            MoonPhase: 0));
        Assert.Equal(FusionRuntimeOperation.CreateNewEntity, direct.Operation);
        Assert.Equal(Id("direct_child"), direct.ResultEntityId);

        FusionResolvedResult rank = resolver.Resolve(new FusionResultRequest(
            Participant("pixie", "fairy", rank: 1, level: 2),
            Participant("aeros", "element", rank: 1, level: 8),
            MoonPhase: 0));
        Assert.Equal(FusionRuntimeOperation.RankUpParent, rank.Operation);
        Assert.Equal(Id("high_pixie"), rank.ResultEntityId);

        FusionResolvedResult accident = resolver.Resolve(new FusionResultRequest(
            Participant("pixie", "fairy", rank: 1, level: 2),
            Participant("slime", "foul", rank: 1, level: 4),
            MoonPhase: 8));
        Assert.True(accident.IsAccident);

        FusionResolvedResult mitama = resolver.Resolve(new FusionResultRequest(
            Participant("ara_mitama", "mitama", rank: 1, level: 12),
            Participant("pixie", "fairy", rank: 1, level: 2),
            MoonPhase: 0));
        Assert.Equal(FusionRuntimeOperation.StatBoost, mitama.Operation);
        Assert.Equal(Id("pixie"), mitama.ResultEntityId);
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
            entities: [Entity("pixie", "fairy", 1, 2), Entity("slime", "foul", 1, 4), child],
            recipes: [new FusionRecipeSnapshot(Id("pixie"), Id("slime"), "child")],
            skills: [frostLance, iceBoost]);
        var random = new SequenceRandomSource(ints: [50]);
        var resolver = new FusionResultResolver(repository, random);
        var planner = new FusionPlanningService(repository, resolver, random, new FusionInheritancePlanner());

        FusionPlanningResult plan = planner.CreatePlan(new FusionPlanningRequest(
            Participant("pixie", "fairy", 1, 2, ["frost_lance", "ice_boost"]),
            Participant("slime", "foul", 1, 4),
            Sacrifice: null,
            IsSacrificial: false,
            MoonPhase: 0));

        Assert.True(plan.IsSuccessful);
        Assert.Contains(Id("ice_boost"), plan.PickableSkillIds);
        Assert.DoesNotContain(Id("frost_lance"), plan.PickableSkillIds);
        Assert.Contains(plan.DisplaySkills, entry => entry.SkillId == Id("frost_lance") && entry.ReasonCode == "group_denied");
    }

    [Fact]
    public void MutationAndSlots_PreserveLegacyPolicies()
    {
        SkillDefinition agi = Skill("agi", InheritanceGroup.Fire, mutationFamily: "fire", mutationTier: 1);
        SkillDefinition maragi = Skill("maragi", InheritanceGroup.Fire, mutationFamily: "fire", mutationTier: 2);
        var repository = new TestFusionRepository(
            entities: [Entity("pixie", "fairy", 1, 2)],
            recipes: [],
            skills:
            [
                agi,
                maragi,
                Skill("s1", InheritanceGroup.Physical),
                Skill("s2", InheritanceGroup.Physical),
                Skill("s3", InheritanceGroup.Physical),
                Skill("s4", InheritanceGroup.Physical),
                Skill("s5", InheritanceGroup.Physical),
                Skill("s6", InheritanceGroup.Physical),
                Skill("s7", InheritanceGroup.Physical)
            ]);
        var planner = new FusionPlanningService(
            repository,
            new FusionResultResolver(repository, new SequenceRandomSource()),
            new SequenceRandomSource(ints: [1, 0]));

        Assert.Equal(2, planner.GetInheritanceSlotCount(repository.GetSkills().Take(7)));
        Assert.Equal(Id("maragi"), planner.MutateSkill(Id("agi")));
    }

    [Fact]
    public void CompendiumService_RegistersOverwritesPricesAndRejectsRecallBeforeMutation()
    {
        var service = new CompendiumService();
        var empty = new CompendiumStateSnapshot();
        var pixie = new CompendiumEntrySnapshot(
            Id("pixie"),
            "Pixie",
            level: 10,
            stats: [new KeyValuePair<ContentId, int>(Id("magic"), 7)],
            skillIds: [Id("dia"), Id("agi")]);

        CompendiumRegistrationResult added = service.Register(empty, pixie);
        CompendiumRegistrationResult updated = service.Register(added.After, pixie with { });

        Assert.Equal(CompendiumRegistrationCode.Added, added.Code);
        Assert.Equal(CompendiumRegistrationCode.Updated, updated.Code);
        Assert.Equal(2000 + 1000 + 350 + 400, service.CalculateRecallCost(pixie));

        CompendiumRecallAssessment duplicate = service.AssessRecall(
            updated.After,
            Id("pixie"),
            currentMacca: 99999,
            alreadyOwned: true,
            hasOpenStockSlot: true);
        Assert.Equal(CompendiumRecallCode.DuplicateOwned, duplicate.Code);

        CompendiumRecallAssessment valid = service.AssessRecall(
            updated.After,
            Id("pixie"),
            currentMacca: 99999,
            alreadyOwned: false,
            hasOpenStockSlot: true);
        Assert.True(valid.CanRecall);
    }

    private static ContentId Id(string value) => ContentId.Parse(value);

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
            Id("demon"),
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
