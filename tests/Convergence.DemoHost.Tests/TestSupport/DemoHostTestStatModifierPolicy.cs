using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.DemoHost.Tests.TestSupport;

internal static class DemoHostTestStatModifierPolicy
{
    private static readonly ContentId PolicyId =
        ContentId.Parse("demo_persistent_stat_modifiers");

    internal static IStatModifierPolicyService CreatePersistent() =>
        new StatModifierPolicyService(new PersistentStagedStatModifierPolicy(PolicyId));

    internal static void ApplyPersistent(
        RuntimeActorState actor,
        ContentId modifierTrackId,
        int stageDelta) =>
        Apply(actor, CreatePersistent(), modifierTrackId, stageDelta);

    internal static void Apply(
        RuntimeActorState actor,
        IStatModifierPolicyService service,
        ContentId modifierTrackId,
        int stageDelta)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(service);
        RuntimeStatModifierStateSnapshot before = actor.ResolveStatModifierState(service);
        StatModifierTransitionResult result = service.Apply(
            new StatModifierApplicationRequest(before, modifierTrackId, stageDelta));
        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                $"DemoHost test modifier setup was rejected: {string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message))}");
        }

        actor.ReplaceStatModifierState(service, result.After);
    }
}
