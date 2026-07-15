using Convergence.Battle;
using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class RuntimeActorRosterInvariantIntegrationTests
{
    [Fact]
    public void Restore_RejectsActiveHostedEntityDuplicatedInInactiveRoster()
    {
        var activeHostedEntity = new RuntimeActorReferenceSnapshot(
            RuntimeInstanceId.Parse("hosted_entity:active_1"),
            ContentId.Parse("test.pack:hosted_entity"),
            "Active Hosted Entity");
        var snapshot = new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                RuntimeInstanceId.Parse("actor:vessel_1"),
                ContentId.Parse("test.pack:vessel"),
                ContentId.Parse("vessel"),
                "Vessel"),
            new RuntimeActorOwnershipSnapshot(
                ContentId.Parse("player_controller"),
                ContentId.Parse("player_team")),
            new RuntimeActorDeploymentSnapshot(RuntimeActorDeployment.Deployed, IsActive: true),
            new RuntimeProgressionSnapshot(level: 1, experience: 0, lifetimeExperience: 0, unspentStatPoints: 0),
            [new RuntimeResourceSnapshot(ContentId.Parse("hp"), current: 10m, maximum: 10m)],
            new RuntimeStatBlockSnapshot(),
            new RuntimeSkillStateSnapshot(),
            new RuntimeActorRosterSnapshot(
                activeHostedEntity,
                hostedEntityRoster: [activeHostedEntity]),
            new RuntimeEquipmentSnapshot(),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            [new KeyValuePair<ContentId, decimal>(ContentId.Parse("hp"), 10m)],
            ContentId.Parse("hp"));

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            RuntimeActorState.Restore(snapshot, CombatDefenseProfile.Empty));

        Assert.Equal("rosters", exception.ParamName);
        Assert.Contains("$.hostedEntityRoster[0]", exception.Message, StringComparison.Ordinal);
        Assert.Contains("cannot also appear", exception.Message, StringComparison.Ordinal);
    }
}
