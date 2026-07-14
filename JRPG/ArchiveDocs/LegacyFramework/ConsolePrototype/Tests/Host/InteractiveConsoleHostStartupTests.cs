using Convergence.Tests.TestSupport;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Host;
using JRPGPrototype.Hosting;
using Xunit;

namespace Convergence.Tests.Host;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class InteractiveConsoleHostStartupTests
{
    [Fact]
    public void OrdinaryStartup_LoadsCleanCatalogSidecarSilently()
    {
        LegacyBaselineSupport.ResetLegacyState();
        var io = new ScriptedGameIO()
            .QueueKey('1', ConsoleKey.D1)
            .QueueMenu(4)
            .QueueKey('\r', ConsoleKey.Enter);
        var host = new ConsoleGameHost(io);

        host.Run([]);

        Assert.NotNull(host.LastStartupContext);
        Assert.True(host.LastStartupContext!.HasCleanCatalog);
        Assert.Empty(host.LastStartupContext.CatalogDiagnostics);
        Assert.NotNull(host.LastStartupContext.CleanCatalog);
        Assert.Contains(
            ContentId.Parse("convergence.clean_battle_demo:frost_duelist_demo"),
            host.LastStartupContext.CleanCatalog!.Entities.Keys);
        Assert.DoesNotContain("[Clean Catalog Warning]", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("[GAME SESSION ENDED]", io.CombinedOutput, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public void OrdinaryStartup_WarnsAndContinuesWhenCleanCatalogSidecarFails()
    {
        LegacyBaselineSupport.ResetLegacyState();
        var io = new ScriptedGameIO()
            .QueueKey('1', ConsoleKey.D1)
            .QueueMenu(4)
            .QueueKey('\r', ConsoleKey.Enter);
        var host = new ConsoleGameHost(io, new FailingContentSource());

        host.Run([]);

        Assert.NotNull(host.LastStartupContext);
        Assert.False(host.LastStartupContext!.HasCleanCatalog);
        Assert.Single(host.LastStartupContext.CatalogDiagnostics);
        Assert.Contains("[Clean Catalog Warning]", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("simulated missing clean pack", io.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("[GAME SESSION ENDED]", io.CombinedOutput, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    private sealed class FailingContentSource : IContentPackTextSource
    {
        public ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default) =>
            throw new FileNotFoundException("simulated missing clean pack", request.ManifestPath);
    }
}
