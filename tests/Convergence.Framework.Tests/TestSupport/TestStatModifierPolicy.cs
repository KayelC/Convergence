using Convergence.Content;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Framework.Tests.TestSupport;

internal static class TestStatModifierPolicy
{
    private static readonly ContentId PersistentPolicyId =
        ContentId.Parse("test_persistent_stat_modifiers");

    internal static IStatModifierPolicyService CreatePersistent() =>
        new StatModifierPolicyService(
            new PersistentStagedStatModifierPolicy(PersistentPolicyId));

    internal static void ApplyPersistent(
        RuntimeActorState actor,
        ContentId modifierTrackId,
        int stageDelta)
    {
        ArgumentNullException.ThrowIfNull(actor);
        IStatModifierPolicyService service = CreatePersistent();
        RuntimeStatModifierStateSnapshot before = actor.ResolveStatModifierState(service);
        StatModifierTransitionResult result = service.Apply(
            new StatModifierApplicationRequest(before, modifierTrackId, stageDelta));
        if (!result.Accepted)
        {
            throw new InvalidOperationException(
                $"Test modifier setup was rejected: {string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message))}");
        }

        actor.ReplaceStatModifierState(service, result.After);
    }
}
