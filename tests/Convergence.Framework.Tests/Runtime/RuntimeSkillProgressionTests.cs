using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeSkillProgressionTests
{
    [Fact]
    public void UnlockPlanner_DiscoversCrossedUnlocksInAuthoredOrderOnce()
    {
        SkillDefinition known = Skill("known");
        SkillDefinition first = Skill("first");
        SkillDefinition second = Skill("second", SkillActivation.Passive);
        var skills = new SkillRepository(known, first, second);
        RuntimeActorState actor = Actor(
            "source",
            "source_entity",
            level: 4,
            StandardProgressionIds.Companion,
            skills,
            [known.Id]);
        EntityDefinition entity = Entity(
            actor.EntityId,
            baseLevel: 1,
            baseSkills: [known.Id],
            unlocks:
            [
                new SkillUnlockDefinition(3, second.Id),
                new SkillUnlockDefinition(2, first.Id),
                new SkillUnlockDefinition(4, first.Id),
                new SkillUnlockDefinition(4, known.Id)
            ]);

        RuntimeSkillUnlockPlanResult result = new RuntimeSkillUnlockPlanner(skills).Plan(
            new RuntimeSkillUnlockPlanRequest(
                actor.ToSnapshot(),
                entity,
                previousLevel: 1,
                new SharedRuntimeMoveListCapacityPolicy()));

        Assert.True(result.Planned);
        Assert.Equal(
            [second.Id, first.Id],
            result.Entries.Select(entry => entry.SkillId));
        Assert.All(result.Entries, entry =>
            Assert.Equal(RuntimeSkillUnlockDisposition.AutomaticallyEquipped, entry.Disposition));
        Assert.Equal([known.Id, second.Id, first.Id], result.After.LearnedSkillIds);
        Assert.Equal([known.Id, second.Id, first.Id], result.After.EquippedSkillIds);
        Assert.Empty(result.After.PendingChoices);
        Assert.Equal(1, result.After.Revision);
    }

    [Fact]
    public void UnlockPlanner_AutoFillsThenPersistsPendingChoiceWhenSharedListIsFull()
    {
        SkillDefinition[] existing = Enumerable.Range(1, 7)
            .Select(index => Skill($"existing_{index}"))
            .ToArray();
        SkillDefinition auto = Skill("auto");
        SkillDefinition pending = Skill("pending", SkillActivation.Passive);
        var skills = new SkillRepository([.. existing, auto, pending]);
        RuntimeActorState actor = Actor(
            "source",
            "source_entity",
            level: 3,
            StandardProgressionIds.Companion,
            skills,
            existing.Select(skill => skill.Id));
        EntityDefinition entity = Entity(
            actor.EntityId,
            baseLevel: 1,
            baseSkills: existing.Select(skill => skill.Id),
            unlocks:
            [
                new SkillUnlockDefinition(2, auto.Id),
                new SkillUnlockDefinition(3, pending.Id)
            ]);

        RuntimeSkillUnlockPlanResult result = new RuntimeSkillUnlockPlanner(skills).Plan(
            new RuntimeSkillUnlockPlanRequest(
                actor.ToSnapshot(),
                entity,
                previousLevel: 1,
                new SharedRuntimeMoveListCapacityPolicy()));

        Assert.True(result.Planned);
        Assert.Equal(RuntimeSkillUnlockDisposition.AutomaticallyEquipped, result.Entries[0].Disposition);
        Assert.Equal(RuntimeSkillUnlockDisposition.PendingChoice, result.Entries[1].Disposition);
        Assert.Equal(8, result.After.EquippedSkillIds.Count);
        Assert.Contains(auto.Id, result.After.LearnedSkillIds);
        Assert.DoesNotContain(pending.Id, result.After.LearnedSkillIds);
        RuntimePendingSkillChoiceSnapshot choice = Assert.Single(result.After.PendingChoices);
        Assert.Equal(pending.Id, choice.SkillId);
        Assert.Equal(3, choice.UnlockLevel);
        Assert.True(choice.Token.IsValid);
    }

    [Fact]
    public void UnlockPlanner_SupportsSeparateActiveAndPassiveCapacity()
    {
        SkillDefinition active = Skill("active");
        SkillDefinition passive = Skill("passive", SkillActivation.Passive);
        SkillDefinition pendingPassive = Skill("pending_passive", SkillActivation.Passive);
        var skills = new SkillRepository(active, passive, pendingPassive);
        RuntimeActorState actor = Actor(
            "source",
            "source_entity",
            level: 2,
            StandardProgressionIds.Companion,
            skills,
            [active.Id, passive.Id]);
        EntityDefinition entity = Entity(
            actor.EntityId,
            baseLevel: 1,
            baseSkills: [active.Id, passive.Id],
            unlocks: [new SkillUnlockDefinition(2, pendingPassive.Id)]);
        var planner = new RuntimeSkillUnlockPlanner(skills);

        RuntimeSkillUnlockPlanResult shared = planner.Plan(new RuntimeSkillUnlockPlanRequest(
            actor.ToSnapshot(),
            entity,
            previousLevel: 1,
            new SharedRuntimeMoveListCapacityPolicy(capacity: 2)));
        RuntimeSkillUnlockPlanResult separated = planner.Plan(new RuntimeSkillUnlockPlanRequest(
            actor.ToSnapshot(),
            entity,
            previousLevel: 1,
            new SeparatedRuntimeMoveListCapacityPolicy(
                activeCapacity: 1,
                passiveCapacity: 2)));

        Assert.Equal(
            RuntimeSkillUnlockDisposition.PendingChoice,
            Assert.Single(shared.Entries).Disposition);
        Assert.Equal(
            RuntimeSkillUnlockDisposition.AutomaticallyEquipped,
            Assert.Single(separated.Entries).Disposition);
    }

    [Theory]
    [InlineData("independent_actor", 1, RuntimeSkillUnlockDisposition.PendingChoice)]
    [InlineData("vessel", 2, RuntimeSkillUnlockDisposition.AutomaticallyEquipped)]
    [InlineData("companion", 3, RuntimeSkillUnlockDisposition.AutomaticallyEquipped)]
    public void UnlockPlanner_CapacityPolicyMayVaryByActorKind(
        string actorKind,
        int capacity,
        RuntimeSkillUnlockDisposition expected)
    {
        SkillDefinition existing = Skill("existing");
        SkillDefinition next = Skill("next");
        var skills = new SkillRepository(existing, next);
        RuntimeActorState actor = Actor(
            actorKind,
            $"{actorKind}_entity",
            level: 2,
            ContentId.Parse(actorKind),
            skills,
            [existing.Id]);
        EntityDefinition entity = Entity(
            actor.EntityId,
            baseLevel: 1,
            baseSkills: [existing.Id],
            unlocks: [new SkillUnlockDefinition(2, next.Id)]);

        RuntimeSkillUnlockPlanResult result = new RuntimeSkillUnlockPlanner(skills).Plan(
            new RuntimeSkillUnlockPlanRequest(
                actor.ToSnapshot(),
                entity,
                previousLevel: 1,
                new ActorKindCapacityPolicy(
                    new Dictionary<ContentId, int>
                    {
                        [ContentId.Parse(actorKind)] = capacity
                    })));

        Assert.Equal(expected, Assert.Single(result.Entries).Disposition);
    }

    [Fact]
    public void GrowthTransaction_LevelsHostedEntityAndRecomposesVesselAtomically()
    {
        SkillDefinition old = Skill("old");
        SkillDefinition unlocked = Skill("unlocked", SkillActivation.Passive);
        var skills = new SkillRepository(old, unlocked);
        RuntimeActorState source = Actor(
            "hosted",
            "hosted_entity",
            level: 1,
            StandardProgressionIds.Companion,
            skills,
            [old.Id],
            strength: 7m);
        RuntimeActorState vessel = Actor(
            "vessel",
            "vessel_entity",
            level: 3,
            StandardProgressionIds.Vessel,
            skills,
            [old.Id],
            strength: 2m);
        RuntimePartyRosterSnapshot party = PartyRoster(vessel, source);
        EntityDefinition sourceEntity = Entity(
            source.EntityId,
            baseLevel: 1,
            baseSkills: [old.Id],
            unlocks: [new SkillUnlockDefinition(2, unlocked.Id)]);
        LevelGrowthResult growth = Growth(source.ToSnapshot(), level: 2, strength: 9m);
        var composition = new RuntimeActorCombatProfileCompositionService(skills);

        RuntimeActorGrowthCompositionResult result =
            new RuntimeActorGrowthCompositionService(composition, skills).Apply(
                new RuntimeActorGrowthCompositionRequest(
                    source,
                    sourceEntity,
                    growth,
                    new SharedRuntimeMoveListCapacityPolicy(),
                    new RuntimeActorCombatProfileCompositionRequest(
                        vessel,
                        RuntimeStatSourceKind.ActiveHostedEntity,
                        MissingHostedEntityBehavior.RejectStatResolution,
                        party,
                        [source])));

        Assert.True(result.Applied);
        Assert.Equal(2, source.Progression.Level);
        Assert.Equal([old.Id, unlocked.Id], source.Skills.EquippedSkillIds);
        Assert.Equal(3, vessel.Progression.Level);
        Assert.Equal(9m, vessel.Stats[StandardProgressionIds.Strength]);
        Assert.Equal([old.Id, unlocked.Id], vessel.Skills.EquippedSkillIds);
        Assert.Contains(
            vessel.Passives.Entries,
            entry => entry.Skill.Id == unlocked.Id && entry.IsEnabled);
        Assert.Equal(source.InstanceId, result.CombatProfileComposition?.SourceActorId);
        Assert.Equal(source.ToSnapshot().Skills.EquippedSkillIds, result.GrowthActorAfter.Skills.EquippedSkillIds);
        Assert.Equal(vessel.ToSnapshot().Skills.EquippedSkillIds, result.ComposedActorAfter.Skills.EquippedSkillIds);
    }

    [Fact]
    public void SkillChoice_ReplaceRemovesOldSkillAndImmediatelyRecomposesVessel()
    {
        SkillDefinition old = Skill("old");
        SkillDefinition retained = Skill("retained");
        SkillDefinition learned = Skill("learned", SkillActivation.Passive);
        var skills = new SkillRepository(old, retained, learned);
        var choice = new RuntimePendingSkillChoiceSnapshot(
            new RuntimeSkillChoiceToken(1),
            unlockLevel: 2,
            learned.Id);
        RuntimeActorState source = Actor(
            "hosted",
            "hosted_entity",
            level: 2,
            StandardProgressionIds.Companion,
            skills,
            [old.Id, retained.Id],
            skillState: new RuntimeSkillStateSnapshot(
                [old.Id, retained.Id],
                [old.Id, retained.Id],
                [choice],
                revision: 4));
        RuntimeActorState vessel = Actor(
            "vessel",
            "vessel_entity",
            level: 3,
            StandardProgressionIds.Vessel,
            skills,
            [old.Id, retained.Id]);
        RuntimePartyRosterSnapshot party = PartyRoster(vessel, source);
        var composition = new RuntimeActorCombatProfileCompositionService(skills);
        var service = new RuntimeSkillChoiceTransactionService(skills, composition);

        RuntimeSkillChoiceTransactionResult result = service.Apply(
            new RuntimeSkillChoiceTransactionRequest(
                source,
                new ReplacePendingSkillCommand(
                    choice.Token,
                    expectedSourceLevel: 2,
                    expectedSkillRevision: 4,
                    old.Id),
                new RuntimeActorCombatProfileCompositionRequest(
                    vessel,
                    RuntimeStatSourceKind.ActiveHostedEntity,
                    MissingHostedEntityBehavior.RejectStatResolution,
                    party,
                    [source])));

        Assert.True(result.Applied);
        Assert.Equal([retained.Id, learned.Id], source.Skills.LearnedSkillIds);
        Assert.Equal([learned.Id, retained.Id], source.Skills.EquippedSkillIds);
        Assert.Empty(source.Skills.PendingChoices);
        Assert.Equal(5, source.Skills.Revision);
        Assert.Equal(source.Skills.EquippedSkillIds, vessel.Skills.EquippedSkillIds);
        Assert.Empty(vessel.Skills.PendingChoices);
        Assert.Equal(source.Skills.Revision, vessel.Skills.Revision);
        Assert.Contains(
            vessel.Passives.Entries,
            entry => entry.Skill.Id == learned.Id && entry.IsEnabled);
    }

    [Fact]
    public void SkillChoice_ForgetNewRetainsMoveListAndConsumesPendingChoice()
    {
        SkillDefinition old = Skill("old");
        SkillDefinition pendingSkill = Skill("pending");
        var skills = new SkillRepository(old, pendingSkill);
        var choice = new RuntimePendingSkillChoiceSnapshot(
            new RuntimeSkillChoiceToken(9),
            unlockLevel: 2,
            pendingSkill.Id);
        RuntimeActorState actor = Actor(
            "source",
            "source_entity",
            level: 2,
            StandardProgressionIds.Companion,
            skills,
            [old.Id],
            skillState: new RuntimeSkillStateSnapshot(
                [old.Id],
                [old.Id],
                [choice],
                revision: 6));

        RuntimeSkillChoiceTransactionResult result =
            new RuntimeSkillChoiceTransactionService(
                skills,
                new RuntimeActorCombatProfileCompositionService(skills)).Apply(
                new RuntimeSkillChoiceTransactionRequest(
                    actor,
                    new ForgetPendingSkillCommand(
                        choice.Token,
                        expectedSourceLevel: 2,
                        expectedSkillRevision: 6)));

        Assert.True(result.Applied);
        Assert.Equal([old.Id], actor.Skills.LearnedSkillIds);
        Assert.Equal([old.Id], actor.Skills.EquippedSkillIds);
        Assert.Empty(actor.Skills.PendingChoices);
        Assert.Equal(7, actor.Skills.Revision);
    }

    [Fact]
    public void SkillChoice_CustomRetentionPolicyKeepsReplacedSkillLearned()
    {
        SkillDefinition old = Skill("old");
        SkillDefinition pendingSkill = Skill("pending");
        var skills = new SkillRepository(old, pendingSkill);
        var choice = new RuntimePendingSkillChoiceSnapshot(
            new RuntimeSkillChoiceToken(2),
            unlockLevel: 2,
            pendingSkill.Id);
        RuntimeActorState actor = Actor(
            "source",
            "source_entity",
            level: 2,
            StandardProgressionIds.Companion,
            skills,
            [old.Id],
            skillState: new RuntimeSkillStateSnapshot(
                [old.Id],
                [old.Id],
                [choice],
                revision: 1));

        RuntimeSkillChoiceTransactionResult result =
            new RuntimeSkillChoiceTransactionService(
                skills,
                new RuntimeActorCombatProfileCompositionService(skills),
                new RetainLearnedRuntimeSkillPolicy()).Apply(
                new RuntimeSkillChoiceTransactionRequest(
                    actor,
                    new ReplacePendingSkillCommand(
                        choice.Token,
                        expectedSourceLevel: 2,
                        expectedSkillRevision: 1,
                        old.Id)));

        Assert.True(result.Applied);
        Assert.Equal([old.Id, pendingSkill.Id], actor.Skills.LearnedSkillIds);
        Assert.Equal([pendingSkill.Id], actor.Skills.EquippedSkillIds);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SkillChoice_StaleCommandsRejectWithoutMutation(
        bool staleLevel,
        bool staleRevision)
    {
        SkillDefinition old = Skill("old");
        SkillDefinition pendingSkill = Skill("pending");
        var skills = new SkillRepository(old, pendingSkill);
        var choice = new RuntimePendingSkillChoiceSnapshot(
            new RuntimeSkillChoiceToken(4),
            unlockLevel: 2,
            pendingSkill.Id);
        RuntimeActorState actor = Actor(
            "source",
            "source_entity",
            level: 2,
            StandardProgressionIds.Companion,
            skills,
            [old.Id],
            skillState: new RuntimeSkillStateSnapshot(
                [old.Id],
                [old.Id],
                [choice],
                revision: 3));
        RuntimeActorSnapshot before = actor.ToSnapshot();

        RuntimeSkillChoiceTransactionResult result =
            new RuntimeSkillChoiceTransactionService(
                skills,
                new RuntimeActorCombatProfileCompositionService(skills)).Apply(
                new RuntimeSkillChoiceTransactionRequest(
                    actor,
                    new ReplacePendingSkillCommand(
                        choice.Token,
                        expectedSourceLevel: staleLevel ? 1 : 2,
                        expectedSkillRevision: staleRevision ? 2 : 3,
                        old.Id)));

        Assert.False(result.Applied);
        Assert.Equal(
            staleLevel
                ? RuntimeSkillChoiceDiagnosticCode.StaleSourceLevel
                : RuntimeSkillChoiceDiagnosticCode.StaleSkillRevision,
            Assert.Single(result.Diagnostics).Code);
        AssertSkillStateEqual(before.Skills, actor.Skills);
    }

    [Fact]
    public void SkillChoice_InvalidReplacementRejectsWithoutMutation()
    {
        SkillDefinition equipped = Skill("equipped");
        SkillDefinition notEquipped = Skill("not_equipped");
        SkillDefinition pendingSkill = Skill("pending");
        var skills = new SkillRepository(equipped, notEquipped, pendingSkill);
        var choice = new RuntimePendingSkillChoiceSnapshot(
            new RuntimeSkillChoiceToken(7),
            unlockLevel: 2,
            pendingSkill.Id);
        RuntimeActorState actor = Actor(
            "source",
            "source_entity",
            level: 2,
            StandardProgressionIds.Companion,
            skills,
            [equipped.Id],
            skillState: new RuntimeSkillStateSnapshot(
                [equipped.Id],
                [equipped.Id],
                [choice],
                revision: 2));
        RuntimeActorSnapshot before = actor.ToSnapshot();

        RuntimeSkillChoiceTransactionResult result =
            new RuntimeSkillChoiceTransactionService(
                skills,
                new RuntimeActorCombatProfileCompositionService(skills)).Apply(
                new RuntimeSkillChoiceTransactionRequest(
                    actor,
                    new ReplacePendingSkillCommand(
                        choice.Token,
                        expectedSourceLevel: 2,
                        expectedSkillRevision: 2,
                        notEquipped.Id)));

        Assert.False(result.Applied);
        Assert.Equal(
            RuntimeSkillChoiceDiagnosticCode.ReplacementSkillMissing,
            Assert.Single(result.Diagnostics).Code);
        AssertSkillStateEqual(before.Skills, actor.Skills);
    }

    [Fact]
    public void SkillChoice_DuplicateResolutionRejectsSecondCommandWithoutMutation()
    {
        SkillDefinition old = Skill("old");
        SkillDefinition pendingSkill = Skill("pending");
        var skills = new SkillRepository(old, pendingSkill);
        var choice = new RuntimePendingSkillChoiceSnapshot(
            new RuntimeSkillChoiceToken(5),
            unlockLevel: 2,
            pendingSkill.Id);
        RuntimeActorState actor = Actor(
            "source",
            "source_entity",
            level: 2,
            StandardProgressionIds.Companion,
            skills,
            [old.Id],
            skillState: new RuntimeSkillStateSnapshot(
                [old.Id],
                [old.Id],
                [choice],
                revision: 1));
        var service = new RuntimeSkillChoiceTransactionService(
            skills,
            new RuntimeActorCombatProfileCompositionService(skills));
        var command = new ForgetPendingSkillCommand(
            choice.Token,
            expectedSourceLevel: 2,
            expectedSkillRevision: 1);

        Assert.True(service.Apply(new RuntimeSkillChoiceTransactionRequest(actor, command)).Applied);
        RuntimeActorSnapshot afterFirst = actor.ToSnapshot();
        RuntimeSkillChoiceTransactionResult second =
            service.Apply(new RuntimeSkillChoiceTransactionRequest(actor, command));

        Assert.False(second.Applied);
        Assert.Equal(
            RuntimeSkillChoiceDiagnosticCode.PendingChoiceMissing,
            Assert.Single(second.Diagnostics).Code);
        AssertSkillStateEqual(afterFirst.Skills, actor.Skills);
    }

    [Fact]
    public void SkillChoice_DependentCompositionFailureLeavesSourceAndVesselUnchanged()
    {
        SkillDefinition old = Skill("old");
        SkillDefinition pendingSkill = Skill("pending");
        var skills = new SkillRepository(old, pendingSkill);
        var choice = new RuntimePendingSkillChoiceSnapshot(
            new RuntimeSkillChoiceToken(6),
            unlockLevel: 2,
            pendingSkill.Id);
        RuntimeActorState source = Actor(
            "hosted",
            "hosted_entity",
            level: 2,
            StandardProgressionIds.Companion,
            skills,
            [old.Id],
            skillState: new RuntimeSkillStateSnapshot(
                [old.Id],
                [old.Id],
                [choice],
                revision: 2));
        RuntimeActorState vessel = Actor(
            "vessel",
            "vessel_entity",
            level: 3,
            StandardProgressionIds.Vessel,
            skills,
            [old.Id]);
        RuntimePartyRosterSnapshot party = PartyRoster(vessel, source);
        RuntimeActorSnapshot sourceBefore = source.ToSnapshot();
        RuntimeActorSnapshot vesselBefore = vessel.ToSnapshot();

        RuntimeSkillChoiceTransactionResult result =
            new RuntimeSkillChoiceTransactionService(
                skills,
                new RejectingCompositionService()).Apply(
                new RuntimeSkillChoiceTransactionRequest(
                    source,
                    new ForgetPendingSkillCommand(
                        choice.Token,
                        expectedSourceLevel: 2,
                        expectedSkillRevision: 2),
                    new RuntimeActorCombatProfileCompositionRequest(
                        vessel,
                        RuntimeStatSourceKind.ActiveHostedEntity,
                        MissingHostedEntityBehavior.RejectStatResolution,
                        party,
                        [source])));

        Assert.False(result.Applied);
        Assert.Equal(
            RuntimeSkillChoiceTransactionStatus.CombatProfileCompositionRejected,
            result.Status);
        AssertSkillStateEqual(sourceBefore.Skills, source.Skills);
        AssertSkillStateEqual(vesselBefore.Skills, vessel.Skills);
    }

    private static RuntimeActorState Actor(
        string instanceId,
        string entityId,
        int level,
        ContentId actorKind,
        SkillRepository skills,
        IEnumerable<ContentId> equippedSkills,
        decimal strength = 5m,
        RuntimeSkillStateSnapshot? skillState = null)
    {
        ContentId[] equipped = equippedSkills.ToArray();
        SkillDefinition[] definitions = equipped.Select(skills.GetRequiredSkill).ToArray();
        RuntimeInstanceId runtimeId = RuntimeInstanceId.Parse(instanceId);
        ContentId definitionId = ContentId.Parse(entityId);
        KeyValuePair<ContentId, decimal>[] stats = StandardProgressionIds.CoreStats
            .Select(stat => new KeyValuePair<ContentId, decimal>(
                stat,
                stat == StandardProgressionIds.Strength ? strength : 5m))
            .ToArray();
        return new RuntimeActorState(
            runtimeId,
            definitionId,
            ContentId.Parse("player_team"),
            StandardProgressionIds.Hp,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(StandardProgressionIds.Hp, 30m, 50m),
                new BattleResourceState(StandardProgressionIds.Sp, 10m, 20m)
            ],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: false),
            new RuntimeActorAffiliationSnapshot(
                ContentId.Parse("test_authority"),
                ContentId.Parse("player_team")),
            stats,
            skillIds: equipped,
            passiveSkills: definitions.Where(skill =>
                skill.Activation == SkillActivation.Passive),
            identity: new RuntimeActorIdentitySnapshot(
                runtimeId,
                definitionId,
                actorKind,
                instanceId),
            progression: new RuntimeProgressionSnapshot(level, 0, 0, 0),
            baseResourceValues:
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 20m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 5m)
            ],
            baseStats: stats,
            skillState: skillState ?? new RuntimeSkillStateSnapshot(equipped, equipped));
    }

    private static EntityDefinition Entity(
        ContentId id,
        int baseLevel,
        IEnumerable<ContentId> baseSkills,
        IEnumerable<SkillUnlockDefinition> unlocks) =>
        new(
            id,
            id.ToString(),
            "Skill progression test entity.",
            StandardProgressionIds.Companion,
            ContentId.Parse("test_race"),
            rank: 1,
            baseLevel,
            new EntityCapabilitiesDefinition(false, false, false),
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
            StandardProgressionIds.CoreStats.Select(stat =>
                new KeyValuePair<ContentId, int>(stat, 5)),
            baseSkillIds: baseSkills,
            skillUnlocks: unlocks);

    private static LevelGrowthResult Growth(
        RuntimeActorSnapshot before,
        int level,
        decimal strength)
    {
        KeyValuePair<ContentId, decimal>[] baseStats = before.Stats.BaseStats
            .Select(pair => pair.Key == StandardProgressionIds.Strength
                ? new KeyValuePair<ContentId, decimal>(pair.Key, strength)
                : pair)
            .ToArray();
        return new LevelGrowthResult(
            ProgressionMutationStatus.Applied,
            new LevelGrowthSourceSnapshot(
                before.Progression,
                before.Stats,
                before.Resources,
                before.BaseResourceValues),
            new RuntimeProgressionSnapshot(level, 0, level, 0),
            new RuntimeStatBlockSnapshot(baseStats, baseStats),
            before.Resources,
            before.BaseResourceValues,
            [new LevelUpEvent(level)]);
    }

    private static RuntimePartyRosterSnapshot PartyRoster(
        RuntimeActorState vessel,
        RuntimeActorState activeHostedEntity)
    {
        RuntimeActorReferenceSnapshot vesselReference = Reference(vessel);
        RuntimeActorReferenceSnapshot sourceReference = Reference(activeHostedEntity);
        return new RuntimePartyRosterSnapshot(
            vesselReference,
            activeParty: [vesselReference],
            activeHostedEntity: sourceReference,
            hostedEntityRoster: [sourceReference]);
    }

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorState actor) =>
        new(actor.InstanceId, actor.EntityId, actor.Identity.DisplayName);

    private static SkillDefinition Skill(
        string id,
        SkillActivation activation = SkillActivation.Active) =>
        new(
            ContentId.Parse(id),
            id,
            "Skill progression fixture.",
            activation,
            menuGroup: null,
            activation == SkillActivation.Passive
                ? InheritanceGroup.Passive
                : InheritanceGroup.Support,
            new SkillInheritanceDefinition(true));

    private static void AssertSkillStateEqual(
        RuntimeSkillStateSnapshot expected,
        RuntimeSkillStateSnapshot actual)
    {
        Assert.Equal(expected.LearnedSkillIds, actual.LearnedSkillIds);
        Assert.Equal(expected.EquippedSkillIds, actual.EquippedSkillIds);
        Assert.Equal(expected.PendingChoices, actual.PendingChoices);
        Assert.Equal(expected.Revision, actual.Revision);
    }

    private sealed class SkillRepository : ISkillDefinitionRepository
    {
        private readonly IReadOnlyDictionary<ContentId, SkillDefinition> _skills;

        public SkillRepository(params SkillDefinition[] skills)
            : this((IEnumerable<SkillDefinition>)skills)
        {
        }

        public SkillRepository(IEnumerable<SkillDefinition> skills)
        {
            _skills = skills.ToDictionary(skill => skill.Id);
        }

        public bool TryGetSkill(ContentId id, out SkillDefinition? definition) =>
            _skills.TryGetValue(id, out definition);

        public SkillDefinition GetRequiredSkill(ContentId id) =>
            _skills.TryGetValue(id, out SkillDefinition? definition)
                ? definition
                : throw new KeyNotFoundException(id.ToString());
    }

    private sealed class ActorKindCapacityPolicy : IRuntimeMoveListCapacityPolicy
    {
        private readonly IReadOnlyDictionary<ContentId, int> _capacities;

        public ActorKindCapacityPolicy(IReadOnlyDictionary<ContentId, int> capacities)
        {
            _capacities = capacities;
        }

        public RuntimeMoveListCapacityAssessment Assess(
            RuntimeMoveListCapacityRequest request) =>
            new(
                ContentId.Parse("actor_kind"),
                _capacities[request.Actor.ActorKindId],
                request.EquippedSkills.Count);
    }

    private sealed class RejectingCompositionService :
        IRuntimeActorCombatProfileCompositionService
    {
        public RuntimeActorCombatProfileCompositionResult Compose(
            RuntimeActorCombatProfileCompositionRequest request)
        {
            RuntimeActorSnapshot before = request.Actor.ToSnapshot();
            return new RuntimeActorCombatProfileCompositionResult(
                RuntimeActorCombatProfileCompositionStatus.Rejected,
                before,
                before,
                request.SourceKind,
                request.Actor.InstanceId,
                diagnostics:
                [
                    new RuntimeActorCombatProfileCompositionDiagnostic(
                        RuntimeActorCombatProfileCompositionDiagnosticCode.CommitFailed,
                        "Rejected for atomic skill-choice testing.")
                ]);
        }
    }
}
