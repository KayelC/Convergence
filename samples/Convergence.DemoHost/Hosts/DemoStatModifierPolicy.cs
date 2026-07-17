using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.DemoHost;

internal static class DemoStatModifierPolicy
{
    internal static IStatModifierPolicyService CreatePersistent() =>
        new StatModifierPolicyService(
            new PersistentStagedStatModifierPolicy(
                ContentId.Parse("demo_persistent_stat_modifiers")));
}
