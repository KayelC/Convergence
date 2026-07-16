using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeActorGrowthCompositionTests
{
    private static readonly ContentId Focus = ContentId.Parse("focus");

    [Fact]
    public void StatComposition_PreservesRegisteredNonCoreEffectiveStats()
    {
        RuntimeActorState hostedEntity = CreateActor("hosted_custom", 20m, customEffectiveStat: 31m);
        RuntimeActorState vessel = CreateActor("vessel_custom", 5m, customEffectiveStat: 17m);
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vessel, hostedEntity);

        RuntimeActorStatCompositionResult result = new RuntimeActorStatCompositionService().Compose(
            new RuntimeActorStatCompositionRequest(
                vessel,
                RuntimeStatSourceKind.ActiveHostedEntity,
                MissingHostedEntityBehavior.RejectStatResolution,
                hostedEntity,
                partyRoster));

        Assert.True(result.Applied);
        Assert.Equal(20m, vessel.Stats[StandardProgressionIds.Strength]);
        Assert.Equal(17m, vessel.Stats[Focus]);
        Assert.DoesNotContain(result.StatResolutions, resolution => resolution.StatId == Focus);
    }

    [Fact]
    public void GrowthComposition_CommitsGrowthAndCanonicalStatsTogether()
    {
        RuntimeActorState hostedEntity = CreateActor("hosted_growth", 20m);
        RuntimeActorState vessel = CreateActor("vessel_growth", 5m, customEffectiveStat: 17m);
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vessel, hostedEntity);
        RuntimeActorSnapshot before = vessel.ToSnapshot();
        LevelGrowthResult growth = AppliedGrowth(before, customEffectiveStat: 19m);

        RuntimeActorGrowthCompositionResult result = new RuntimeActorGrowthCompositionService().Apply(
            new RuntimeActorGrowthCompositionRequest(
                growth,
                new RuntimeActorStatCompositionRequest(
                    vessel,
                    RuntimeStatSourceKind.ActiveHostedEntity,
                    MissingHostedEntityBehavior.RejectStatResolution,
                    hostedEntity,
                    partyRoster)));

        Assert.True(result.Applied);
        Assert.Equal(RuntimeActorGrowthCompositionStatus.Applied, result.Status);
        Assert.True(result.GrowthMutation.Applied);
        Assert.NotNull(result.StatComposition);
        Assert.True(result.StatComposition!.Applied);
        Assert.Equal(2, vessel.Progression.Level);
        Assert.Equal(20m, vessel.Stats[StandardProgressionIds.Strength]);
        Assert.Equal(19m, vessel.Stats[Focus]);
        Assert.Equal(56m, vessel.GetRequiredResource(StandardProgressionIds.Hp).Current);
        Assert.Equal(126m, vessel.GetRequiredResource(StandardProgressionIds.Hp).Maximum);
        Assert.Equal(vessel.ToSnapshot().Progression, result.After.Progression);
    }

    [Fact]
    public void GrowthComposition_CompositionRejectionLeavesLiveActorUnchanged()
    {
        RuntimeActorState hostedEntity = CreateActor("hosted_rejected", 20m);
        RuntimeActorState vessel = CreateActor("vessel_rejected", 5m, customEffectiveStat: 17m);
        RuntimePartyRosterSnapshot partyRoster = PartyRoster(vessel, hostedEntity);
        RuntimeActorSnapshot before = vessel.ToSnapshot();
        LevelGrowthResult growth = AppliedGrowth(before, customEffectiveStat: 19m);
        var service = new RuntimeActorGrowthCompositionService(new RejectingCompositionService());

        RuntimeActorGrowthCompositionResult result = service.Apply(
            new RuntimeActorGrowthCompositionRequest(
                growth,
                new RuntimeActorStatCompositionRequest(
                    vessel,
                    RuntimeStatSourceKind.ActiveHostedEntity,
                    MissingHostedEntityBehavior.RejectStatResolution,
                    hostedEntity,
                    partyRoster)));

        Assert.False(result.Applied);
        Assert.Equal(RuntimeActorGrowthCompositionStatus.StatCompositionRejected, result.Status);
        Assert.True(result.GrowthMutation.Applied);
        Assert.Equal(
            RuntimeActorGrowthCompositionDiagnosticCode.StatCompositionRejected,
            Assert.Single(result.Diagnostics).Code);
        AssertActorStateEqual(before, vessel.ToSnapshot());
        AssertActorStateEqual(before, result.After);
    }

    [Fact]
    public void GrowthComposition_GrowthRejectionSkipsCompositionAndLeavesLiveActorUnchanged()
    {
        RuntimeActorState vessel = CreateActor("vessel_growth_rejected", 5m, customEffectiveStat: 17m);
        RuntimeActorSnapshot before = vessel.ToSnapshot();
        var rejectedGrowth = new LevelGrowthResult(
            ProgressionMutationStatus.Rejected,
            before.Progression,
            before.Stats,
            diagnostics:
            [
                new ProgressionMutationDiagnostic(
                    ProgressionMutationErrorCode.NegativeExperience,
                    "Rejected for the transaction test.")
            ]);
        var composition = new CountingCompositionService();

        RuntimeActorGrowthCompositionResult result = new RuntimeActorGrowthCompositionService(composition).Apply(
            new RuntimeActorGrowthCompositionRequest(
                rejectedGrowth,
                new RuntimeActorStatCompositionRequest(
                    vessel,
                    RuntimeStatSourceKind.Actor,
                    MissingHostedEntityBehavior.UseActorBaseStats)));

        Assert.False(result.Applied);
        Assert.Equal(RuntimeActorGrowthCompositionStatus.GrowthRejected, result.Status);
        Assert.Equal(0, composition.CallCount);
        Assert.Null(result.StatComposition);
        AssertActorStateEqual(before, vessel.ToSnapshot());
    }

    private static LevelGrowthResult AppliedGrowth(
        RuntimeActorSnapshot before,
        decimal customEffectiveStat)
    {
        KeyValuePair<ContentId, decimal>[] effectiveStats = before.Stats.EffectiveStats
            .Select(pair => pair.Key == Focus
                ? new KeyValuePair<ContentId, decimal>(pair.Key, customEffectiveStat)
                : pair)
            .ToArray();
        return new LevelGrowthResult(
            ProgressionMutationStatus.Applied,
            new RuntimeProgressionSnapshot(2, 0, 2, 0),
            new RuntimeStatBlockSnapshot(before.Stats.BaseStats, effectiveStats),
            [
                new RuntimeResourceSnapshot(StandardProgressionIds.Hp, 56m, 106m),
                new RuntimeResourceSnapshot(StandardProgressionIds.Sp, 23m, 39m)
            ],
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 26m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 9m)
            ],
            [new LevelUpEvent(2)]);
    }

    private static RuntimeActorState CreateActor(
        string id,
        decimal coreStat,
        decimal customEffectiveStat = 11m)
    {
        KeyValuePair<ContentId, decimal>[] baseStats =
        [
            .. StandardProgressionIds.CoreStats.Select(statId =>
                new KeyValuePair<ContentId, decimal>(statId, coreStat)),
            new KeyValuePair<ContentId, decimal>(Focus, 7m)
        ];
        KeyValuePair<ContentId, decimal>[] effectiveStats =
        [
            .. StandardProgressionIds.CoreStats.Select(statId =>
                new KeyValuePair<ContentId, decimal>(statId, coreStat)),
            new KeyValuePair<ContentId, decimal>(Focus, customEffectiveStat)
        ];
        RuntimeInstanceId instanceId = RuntimeInstanceId.Parse(id);
        ContentId entityId = ContentId.Parse($"{id}_entity");
        return new RuntimeActorState(
            instanceId,
            entityId,
            ContentId.Parse("player_team"),
            StandardProgressionIds.Hp,
            CombatDefenseProfile.Empty,
            [
                new BattleResourceState(StandardProgressionIds.Hp, 50m, 100m),
                new BattleResourceState(StandardProgressionIds.Sp, 20m, 30m)
            ],
            new RuntimeEncounterPresenceSnapshot(IsDeployed: true),
            new RuntimeActorAffiliationSnapshot(ContentId.Parse("test_host"), ContentId.Parse("player_team")),
            effectiveStats,
            identity: new RuntimeActorIdentitySnapshot(
                instanceId,
                entityId,
                StandardProgressionIds.Vessel,
                id),
            progression: new RuntimeProgressionSnapshot(1, 0, 0, 0),
            baseResourceValues:
            [
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, 20m),
                new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, 6m)
            ],
            baseStats: baseStats);
    }

    private static RuntimePartyRosterSnapshot PartyRoster(
        RuntimeActorState owner,
        RuntimeActorState activeHostedEntity)
    {
        RuntimeActorReferenceSnapshot ownerReference = Reference(owner);
        RuntimeActorReferenceSnapshot activeReference = Reference(activeHostedEntity);
        return new RuntimePartyRosterSnapshot(
            ownerReference,
            owner.Progression.Level,
            activeParty: [ownerReference],
            activeHostedEntity: activeReference,
            hostedEntityRoster: [activeReference]);
    }

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorState actor) =>
        new(actor.InstanceId, actor.EntityId, actor.Identity.DisplayName);

    private static void AssertActorStateEqual(RuntimeActorSnapshot expected, RuntimeActorSnapshot actual)
    {
        Assert.Equal(expected.Progression, actual.Progression);
        Assert.Equal(expected.Resources.ToArray(), actual.Resources.ToArray());
        Assert.Equal(expected.BaseResourceValues.OrderBy(pair => pair.Key.ToString()).ToArray(),
            actual.BaseResourceValues.OrderBy(pair => pair.Key.ToString()).ToArray());
        Assert.Equal(expected.Stats.BaseStats.OrderBy(pair => pair.Key.ToString()).ToArray(),
            actual.Stats.BaseStats.OrderBy(pair => pair.Key.ToString()).ToArray());
        Assert.Equal(expected.Stats.EffectiveStats.OrderBy(pair => pair.Key.ToString()).ToArray(),
            actual.Stats.EffectiveStats.OrderBy(pair => pair.Key.ToString()).ToArray());
    }

    private sealed class RejectingCompositionService : IRuntimeActorStatCompositionService
    {
        public RuntimeActorStatCompositionResult Compose(RuntimeActorStatCompositionRequest request)
        {
            RuntimeActorSnapshot before = request.Actor.ToSnapshot();
            return new RuntimeActorStatCompositionResult(
                RuntimeActorStatCompositionStatus.Rejected,
                before,
                before,
                request.SourceKind,
                diagnostics:
                [
                    new RuntimeActorStatCompositionDiagnostic(
                        RuntimeActorStatCompositionDiagnosticCode.StatResolutionFailed,
                        "Rejected for the transaction test.")
                ]);
        }
    }

    private sealed class CountingCompositionService : IRuntimeActorStatCompositionService
    {
        public int CallCount { get; private set; }

        public RuntimeActorStatCompositionResult Compose(RuntimeActorStatCompositionRequest request)
        {
            CallCount++;
            return new RuntimeActorStatCompositionService().Compose(request);
        }
    }
}
