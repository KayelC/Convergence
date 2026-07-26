using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Framework.Tests.TestSupport;
using Convergence.Hosting;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class StatusLifecycleCertificationTests
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId PlayerTeam = ContentId.Parse("player_team");
    private static readonly ContentId OwnerTurnEnd = ContentId.Parse("owner_turn_end");
    private static readonly ContentId RoundEnd = ContentId.Parse("round_end");
    private static readonly ContentId UnrelatedRoundEnd = ContentId.Parse("unrelated_round_end");
    private static readonly ContentId PlayerPhase = ContentId.Parse("player_phase");
    private static readonly ContentId PlayerPhaseEnd = ContentId.Parse("player_phase_end");
    private static readonly ContentId AdvancingStatus = ContentId.Parse("advancing_status");
    private static readonly ContentId SuspendedStatus = ContentId.Parse("suspended_status");
    private static readonly AilmentDefinition AdvancingAilment = Ailment(
        "advancing_ailment",
        suspendWhileReserve: false);
    private static readonly AilmentDefinition SuspendedAilment = Ailment(
        "suspended_ailment",
        suspendWhileReserve: true);

    [Fact]
    public void SeededReserveClockSequencesMatchIndependentReferenceModel()
    {
        IStatModifierPolicyService modifiers = TestStatModifierPolicy.CreatePersistent();
        var lifecycle = new BattleDurationLifecycleService(
            new AdvanceReserveOnEncounterClockPolicy(
                BattleLifecycleClockKind.Round,
                RoundEnd));

        for (int seed = 0; seed < 32; seed++)
        {
            var sequence = new CertificationSequence(seed);
            bool isDeployed = sequence.Next(2) == 0;
            RuntimeActorState actor = Actor($"reserve_model_{seed}", isDeployed);
            int? advancingStatus = sequence.Next(1, 7);
            int? suspendedStatus = sequence.Next(1, 7);
            int? advancingAilment = sequence.Next(1, 7);
            int? suspendedAilment = sequence.Next(1, 7);
            ApplyModelState(
                actor,
                advancingStatus.Value,
                suspendedStatus.Value,
                advancingAilment.Value,
                suspendedAilment.Value);

            int boundarySequence = 0;
            for (int step = 0; step < 48; step++)
            {
                switch (sequence.Next(7))
                {
                    case 0:
                        isDeployed = true;
                        actor.SetEncounterPresence(isDeployed);
                        break;
                    case 1:
                        isDeployed = false;
                        actor.SetEncounterPresence(isDeployed);
                        break;
                    case 2:
                    case 3:
                        boundarySequence++;
                        lifecycle.ProcessClock(
                            new BattleLifecycleClockRequest(
                                [actor],
                                new RoundLifecycleClockBoundary(RoundEnd, boundarySequence)),
                            modifiers);
                        // This exact round policy advances unsuspended state in either placement.
                        Decrement(ref advancingStatus, shouldAdvance: true);
                        Decrement(ref advancingAilment, shouldAdvance: true);
                        Decrement(ref suspendedStatus, isDeployed);
                        Decrement(ref suspendedAilment, isDeployed);
                        break;
                    case 4:
                        boundarySequence++;
                        lifecycle.ProcessClock(
                            new BattleLifecycleClockRequest(
                                [actor],
                                new RoundLifecycleClockBoundary(
                                    UnrelatedRoundEnd,
                                    boundarySequence)),
                            modifiers);
                        break;
                    case 5:
                        RefreshRandomState(
                            actor,
                            sequence,
                            ref advancingStatus,
                            ref suspendedStatus,
                            ref advancingAilment,
                            ref suspendedAilment);
                        break;
                    case 6:
                        isDeployed = sequence.Next(2) == 0;
                        actor.SetEncounterPresence(isDeployed);
                        break;
                }

                AssertRemaining(
                    actor,
                    advancingStatus,
                    suspendedStatus,
                    advancingAilment,
                    suspendedAilment,
                    seed,
                    step);
            }
        }
    }

    [Fact]
    public void SupportedRestoreBoundariesMatchUninterruptedExecutionAndRejectActionState()
    {
        IStatModifierPolicyService modifiers = TestStatModifierPolicy.CreatePersistent();
        var durationLifecycle = new BattleDurationLifecycleService();
        var statusLifecycle = new BattleStatusLifecycleService(new MinimumRandomSource());
        AilmentDefinition ailment = TimelineAilment();
        Action<RuntimeActorState>[] timeline =
        [
            actor => statusLifecycle.ProcessTurnStart(new BattleTurnStartLifecycleRequest(actor)),
            actor => durationLifecycle.ProcessActionEnd(
                new BattleActionEndLifecycleRequest([actor]),
                modifiers),
            actor => durationLifecycle.ProcessClock(
                new BattleLifecycleClockRequest(
                    [actor],
                    new ActorTurnLifecycleClockBoundary(OwnerTurnEnd, actor.InstanceId, 1)),
                modifiers),
            actor => actor.SetEncounterPresence(isDeployed: false),
            actor => durationLifecycle.ProcessClock(
                new BattleLifecycleClockRequest(
                    [actor],
                    new ActorTurnLifecycleClockBoundary(OwnerTurnEnd, actor.InstanceId, 2)),
                modifiers),
            actor => actor.SetEncounterPresence(isDeployed: true),
            actor => durationLifecycle.ProcessClock(
                new BattleLifecycleClockRequest(
                    [actor],
                    new TeamPhaseLifecycleClockBoundary(
                        PlayerPhaseEnd,
                        PlayerTeam,
                        PlayerPhase,
                        3)),
                modifiers),
            actor => durationLifecycle.ProcessClock(
                new BattleLifecycleClockRequest(
                    [actor],
                    new ActorTurnLifecycleClockBoundary(OwnerTurnEnd, actor.InstanceId, 4)),
                modifiers),
            actor => durationLifecycle.Cleanup(
                new BattleStatusCleanupRequest(
                    actor,
                    BattleStatusDepartureReason.DeploymentSwap),
                modifiers),
            actor => durationLifecycle.Cleanup(
                new BattleStatusCleanupRequest(
                    actor,
                    BattleStatusDepartureReason.BattleEnd),
                modifiers)
        ];

        RuntimeActorState uninterrupted = TimelineActor(ailment);
        var expected = new List<RuntimeActorSnapshot> { uninterrupted.ToSnapshot() };
        foreach (Action<RuntimeActorState> step in timeline)
        {
            step(uninterrupted);
            expected.Add(uninterrupted.ToSnapshot());
        }

        for (int restoreBoundary = 0; restoreBoundary <= timeline.Length; restoreBoundary++)
        {
            RuntimeActorState candidate = TimelineActor(ailment);
            for (int step = 0; step < restoreBoundary; step++)
            {
                timeline[step](candidate);
            }

            if (restoreBoundary < 2)
            {
                CatalogBattleActorCreationResult rejection = Restore(
                    candidate.ToSnapshot(),
                    ailment,
                    modifiers);
                Assert.False(rejection.IsSuccess);
                CatalogBattleActorDiagnostic diagnostic = Assert.Single(rejection.Diagnostics);
                Assert.Equal(CatalogBattleActorDiagnosticCode.SnapshotInvalid, diagnostic.Code);
                Assert.Contains(
                    ".duration.kind",
                    diagnostic.Message,
                    StringComparison.Ordinal);
                Assert.Contains(
                    "Instant duration state cannot be restored",
                    diagnostic.Message,
                    StringComparison.Ordinal);
                continue;
            }

            RuntimeActorState restored = Restore(candidate.ToSnapshot(), ailment, modifiers)
                .RequireActor()
                .State;
            AssertLifecycleEquivalent(
                expected[restoreBoundary],
                restored.ToSnapshot(),
                $"restore boundary {restoreBoundary}");

            for (int step = restoreBoundary; step < timeline.Length; step++)
            {
                timeline[step](restored);
                AssertLifecycleEquivalent(
                    expected[step + 1],
                    restored.ToSnapshot(),
                    $"restore boundary {restoreBoundary}, timeline step {step}");
            }
        }
    }

    private static CatalogBattleActorCreationResult Restore(
        RuntimeActorSnapshot snapshot,
        AilmentDefinition ailment,
        IStatModifierPolicyService modifiers)
    {
        var catalog = new CertificationCatalog(
            Entity(snapshot.Identity.EntityDefinitionId, snapshot.Identity.ActorKindId),
            ailment);
        var factory = new CatalogBattleActorFactory(
            catalog,
            catalog,
            new CertificationInitializationPolicy(),
            catalog);
        return factory.Restore(new CatalogBattleActorRestoreRequest(
            snapshot,
            RuntimeStatSourceKind.Actor,
            MissingHostedEntityBehavior.UseActorBaseStats,
            statModifierPolicy: modifiers));
    }

    private static void ApplyModelState(
        RuntimeActorState actor,
        int advancingStatus,
        int suspendedStatus,
        int advancingAilment,
        int suspendedAilment)
    {
        actor.AddOtherStatus(
            AdvancingStatus,
            CountedLifetime(advancingStatus, suspendWhileReserve: false));
        actor.AddOtherStatus(
            SuspendedStatus,
            CountedLifetime(suspendedStatus, suspendWhileReserve: true));
        actor.ApplyAilment(
            AdvancingAilment,
            CountedLifetime(advancingAilment, suspendWhileReserve: false));
        actor.ApplyAilment(
            SuspendedAilment,
            CountedLifetime(suspendedAilment, suspendWhileReserve: true));
    }

    private static void RefreshRandomState(
        RuntimeActorState actor,
        CertificationSequence sequence,
        ref int? advancingStatus,
        ref int? suspendedStatus,
        ref int? advancingAilment,
        ref int? suspendedAilment)
    {
        int remaining = sequence.Next(1, 7);
        switch (sequence.Next(4))
        {
            case 0:
                actor.AddOtherStatus(
                    AdvancingStatus,
                    CountedLifetime(remaining, suspendWhileReserve: false));
                advancingStatus = remaining;
                break;
            case 1:
                actor.AddOtherStatus(
                    SuspendedStatus,
                    CountedLifetime(remaining, suspendWhileReserve: true));
                suspendedStatus = remaining;
                break;
            case 2:
                actor.ApplyAilment(
                    AdvancingAilment,
                    CountedLifetime(remaining, suspendWhileReserve: false));
                advancingAilment = remaining;
                break;
            case 3:
                actor.ApplyAilment(
                    SuspendedAilment,
                    CountedLifetime(remaining, suspendWhileReserve: true));
                suspendedAilment = remaining;
                break;
        }
    }

    private static void Decrement(ref int? remaining, bool shouldAdvance)
    {
        if (!shouldAdvance || remaining is null)
        {
            return;
        }

        remaining = remaining.Value == 1 ? null : remaining.Value - 1;
    }

    private static void AssertRemaining(
        RuntimeActorState actor,
        int? advancingStatus,
        int? suspendedStatus,
        int? advancingAilment,
        int? suspendedAilment,
        int seed,
        int step)
    {
        RuntimeBattleStatusSnapshot snapshot = actor.ToSnapshot().BattleStatus;
        AssertRemaining(
            snapshot.Statuses,
            AdvancingStatus,
            advancingStatus,
            seed,
            step);
        AssertRemaining(
            snapshot.Statuses,
            SuspendedStatus,
            suspendedStatus,
            seed,
            step);
        AssertRemaining(
            snapshot.Ailments,
            AdvancingAilment.Id,
            advancingAilment,
            seed,
            step);
        AssertRemaining(
            snapshot.Ailments,
            SuspendedAilment.Id,
            suspendedAilment,
            seed,
            step);
    }

    private static void AssertRemaining(
        IEnumerable<RuntimeTimedStateSnapshot> states,
        ContentId id,
        int? expected,
        int seed,
        int step)
    {
        RuntimeTimedStateSnapshot? state = states.SingleOrDefault(candidate => candidate.Id == id);
        int? actual = state is null
            ? null
            : Assert.IsType<TurnDurationDefinition>(state.Duration).Value;
        Assert.True(
            actual == expected,
            $"Seed {seed}, step {step}, state '{id}' expected {Format(expected)} but found {Format(actual)}.");
    }

    private static string Format(int? value) => value?.ToString() ?? "expired";

    private static RuntimeActorState TimelineActor(AilmentDefinition ailment)
    {
        RuntimeActorState actor = Actor("restore_timeline", isDeployed: true);
        actor.SetGuarding(true);
        actor.ApplyAilment(ailment, ailment.DefaultLifetime);
        actor.AddOtherStatus(
            ContentId.Parse("counted_status"),
            CountedLifetime(4, suspendWhileReserve: true, OwnerTurnEnd));
        actor.AddOtherStatus(
            ContentId.Parse("action_status"),
            StandardStatusLifetimes.Encounter(new InstantDurationDefinition()));
        actor.AddOtherStatus(
            ContentId.Parse("phase_status"),
            StandardStatusLifetimes.Encounter(new PhaseDurationDefinition(PlayerPhase)));
        actor.AddOtherStatus(
            ContentId.Parse("battle_status"),
            StandardStatusLifetimes.Encounter(new BattleDurationDefinition()));
        actor.AddOtherStatus(
            ContentId.Parse("permanent_status"),
            StandardStatusLifetimes.Persistent);
        actor.GrantShield(
            ShieldKind.Physical,
            StandardStatusLifetimes.Deployment(
                new TurnDurationDefinition(3, OwnerTurnEnd, true)));
        actor.OverrideAffinity(
            DamageElement.Ice,
            ElementalAffinity.Resist,
            StandardStatusLifetimes.Encounter(
                new PhaseDurationDefinition(PlayerPhase)));
        actor.BreakAffinity(
            DamageElement.Fire,
            StandardStatusLifetimes.Encounter(new BattleDurationDefinition()));
        return actor;
    }

    private static void AssertLifecycleEquivalent(
        RuntimeActorSnapshot expected,
        RuntimeActorSnapshot actual,
        string context)
    {
        Assert.True(
            expected.EncounterPresence == actual.EncounterPresence,
            $"{context}: encounter presence diverged.");
        Assert.True(
            expected.Resources.OrderBy(value => value.ResourceId.ToString(), StringComparer.Ordinal)
                .SequenceEqual(actual.Resources.OrderBy(
                    value => value.ResourceId.ToString(),
                    StringComparer.Ordinal)),
            $"{context}: resources diverged. Expected {FormatResources(expected.Resources)}; " +
            $"actual {FormatResources(actual.Resources)}.");
        Assert.True(
            expected.BattleStatus.IsGuarding == actual.BattleStatus.IsGuarding,
            $"{context}: Guard state diverged.");
        AssertTimedStatesEqual(
            expected.BattleStatus.Ailments,
            actual.BattleStatus.Ailments,
            context + ": ailments");
        AssertTimedStatesEqual(
            expected.BattleStatus.Statuses,
            actual.BattleStatus.Statuses,
            context + ": statuses");
        Assert.True(
            expected.BattleStatus.Shields.OrderBy(value => value.Kind)
                .SequenceEqual(actual.BattleStatus.Shields.OrderBy(value => value.Kind)),
            $"{context}: shields diverged.");
        Assert.True(
            expected.BattleStatus.AffinityOverrides.OrderBy(value => value.Element)
                .SequenceEqual(actual.BattleStatus.AffinityOverrides.OrderBy(value => value.Element)),
            $"{context}: affinity overrides diverged.");
        Assert.True(
            expected.BattleStatus.AffinityBreaks.OrderBy(value => value.Element)
                .SequenceEqual(actual.BattleStatus.AffinityBreaks.OrderBy(value => value.Element)),
            $"{context}: affinity breaks diverged.");
        Assert.True(
            Equals(expected.BattleStatus.ChargeState, actual.BattleStatus.ChargeState),
            $"{context}: charge state diverged.");
        Assert.True(
            Equals(expected.BattleStatus.StatModifiers, actual.BattleStatus.StatModifiers),
            $"{context}: stat-modifier state diverged.");
        Assert.True(
            expected.BattleActivations.PassiveActivations.SequenceEqual(
                actual.BattleActivations.PassiveActivations),
            $"{context}: passive activations diverged.");
        Assert.True(
            expected.BattleActivations.PassiveSkillStates.SequenceEqual(
                actual.BattleActivations.PassiveSkillStates),
            $"{context}: passive enabled state diverged.");
    }

    private static void AssertTimedStatesEqual(
        IEnumerable<RuntimeTimedStateSnapshot> expected,
        IEnumerable<RuntimeTimedStateSnapshot> actual,
        string context)
    {
        RuntimeTimedStateSnapshot[] expectedOrdered = expected
            .OrderBy(value => value.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        RuntimeTimedStateSnapshot[] actualOrdered = actual
            .OrderBy(value => value.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            expectedOrdered.SequenceEqual(actualOrdered),
            $"{context} diverged.");
    }

    private static string FormatResources(IEnumerable<RuntimeResourceSnapshot> resources) =>
        string.Join(
            ", ",
            resources
                .OrderBy(value => value.ResourceId.ToString(), StringComparer.Ordinal)
                .Select(value => $"{value.ResourceId}={value.Current}/{value.Maximum}"));

    private static RuntimeActorState Actor(string id, bool isDeployed) =>
        new(
            RuntimeInstanceId.Parse(id),
            ContentId.Parse(id + "_entity"),
            PlayerTeam,
            Hp,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(Hp, 100, 100),
                new BattleResourceState(Sp, 40, 40)
            ],
            new RuntimeEncounterPresenceSnapshot(isDeployed),
            new RuntimeActorAffiliationSnapshot(
                ContentId.Parse("certification_controller"),
                PlayerTeam),
            baseResourceValues:
            [
                new KeyValuePair<ContentId, decimal>(Hp, 100m),
                new KeyValuePair<ContentId, decimal>(Sp, 40m)
            ]);

    private static AilmentDefinition TimelineAilment() =>
        new(
            ContentId.Parse("timeline_ailment"),
            "Timeline Ailment",
            "Certification-only lifecycle state.",
            CountedLifetime(3, suspendWhileReserve: true, OwnerTurnEnd),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition());

    private static AilmentDefinition Ailment(string id, bool suspendWhileReserve) =>
        new(
            ContentId.Parse(id),
            id,
            "Certification-only model state.",
            CountedLifetime(3, suspendWhileReserve),
            new NormalAilmentTurnBehaviorDefinition(),
            new AilmentModifiersDefinition(1m, 0, 1m, 1m, false),
            new AilmentRecoveryDefinition());

    private static EntityDefinition Entity(ContentId id, ContentId actorKindId) =>
        new(
            id,
            "Certification Actor",
            "Catalog-backed restore fixture.",
            actorKindId,
            ContentId.Parse("certification_race"),
            rank: 1,
            baseLevel: 1,
            new EntityCapabilitiesDefinition(false, false, false),
            new EntityInheritanceRulesDefinition(
                new InheritanceGroupPolicyDefinition(InheritanceGroupPolicyMode.DenyList)),
            []);

    private static StatusLifetimeDefinition CountedLifetime(
        int remaining,
        bool suspendWhileReserve,
        ContentId? eventId = null) =>
        StandardStatusLifetimes.Field(
            new TurnDurationDefinition(
                remaining,
                eventId ?? RoundEnd,
                suspendWhileReserve));

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt32(int minimumInclusive, int maximumExclusive) => minimumInclusive;

        public decimal NextUnitDecimal() => 0m;
    }

    private sealed class CertificationCatalog(
        EntityDefinition entity,
        AilmentDefinition ailment)
        : IEntityDefinitionRepository,
          ISkillDefinitionRepository,
          IAilmentDefinitionRepository,
          IDurationVocabularyRepository
    {
        public IReadOnlySet<ContentId> RegisteredEventIds { get; } =
            new HashSet<ContentId> { OwnerTurnEnd, PlayerPhaseEnd };

        public IReadOnlySet<ContentId> RegisteredPhaseIds { get; } =
            new HashSet<ContentId> { PlayerPhase };

        public bool TryGetEntity(ContentId id, out EntityDefinition? definition)
        {
            definition = id == entity.Id ? entity : null;
            return definition is not null;
        }

        public EntityDefinition GetRequiredEntity(ContentId id) =>
            TryGetEntity(id, out EntityDefinition? definition)
                ? definition!
                : throw new KeyNotFoundException();

        public bool TryGetSkill(ContentId id, out SkillDefinition? definition)
        {
            definition = null;
            return false;
        }

        public SkillDefinition GetRequiredSkill(ContentId id) => throw new KeyNotFoundException();

        public bool TryGetAilment(ContentId id, out AilmentDefinition? definition)
        {
            definition = id == ailment.Id ? ailment : null;
            return definition is not null;
        }

        public AilmentDefinition GetRequiredAilment(ContentId id) =>
            TryGetAilment(id, out AilmentDefinition? definition)
                ? definition!
                : throw new KeyNotFoundException();
    }

    private sealed class CertificationInitializationPolicy : IBattleActorInitializationPolicy
    {
        public BattleActorInitialization Initialize(EntityDefinition entity, int level) =>
            new(
                Hp,
                [
                    new BattleResourceState(Hp, 100, 100),
                    new BattleResourceState(Sp, 40, 40)
                ],
                [
                    new KeyValuePair<ContentId, decimal>(Hp, 100m),
                    new KeyValuePair<ContentId, decimal>(Sp, 40m)
                ]);
    }

    private sealed class CertificationSequence(int seed)
    {
        private uint _state = unchecked((uint)seed + 1u);

        public int Next(int maximumExclusive) => Next(0, maximumExclusive);

        public int Next(int minimumInclusive, int maximumExclusive)
        {
            if (minimumInclusive >= maximumExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            }

            _state = unchecked((_state * 1_664_525u) + 1_013_904_223u);
            uint range = (uint)(maximumExclusive - minimumInclusive);
            return minimumInclusive + (int)(_state % range);
        }
    }
}
