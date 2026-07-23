using Convergence.Content;

namespace Convergence.Framework.Tests.TestSupport;

internal static class TestStatusLifetimes
{
    public static StatusLifetimeDefinition DeploymentLifetime(DurationDefinition expiration) =>
        StandardStatusLifetimes.Deployment(expiration);

    public static StatusLifetimeDefinition EncounterLifetime(DurationDefinition expiration) =>
        StandardStatusLifetimes.Encounter(expiration);

    public static StatusLifetimeDefinition FieldLifetime(DurationDefinition expiration) =>
        StandardStatusLifetimes.Field(expiration);
}
