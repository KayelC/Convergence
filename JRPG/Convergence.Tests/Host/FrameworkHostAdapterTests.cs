using Convergence.Tests.TestSupport;
using JRPGPrototype.Host;
using JRPGPrototype.Hosting;
using Xunit;

namespace Convergence.Tests.Host;

public sealed class FrameworkHostAdapterTests
{
    [Fact]
    public async Task FileContentSource_PreservesDocumentOrderAndDiagnosticPaths()
    {
        string root = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "pack.manifest.json"), "{\"manifest\":true}");
            await File.WriteAllTextAsync(Path.Combine(root, "second.json"), "{\"order\":2}");
            await File.WriteAllTextAsync(Path.Combine(root, "first.json"), "{\"order\":1}");
            var source = new FileContentPackSource(root);

            var bundle = await source.ReadAsync(new ContentPackTextRequest(
                "pack.manifest.json",
                ["second.json", "first.json"]));

            Assert.Equal(Path.Combine(root, "pack.manifest.json"), bundle.ManifestSourceName);
            Assert.Equal(["second.json", "first.json"], bundle.Documents.Select(document => document.Path));
            Assert.Equal(
                [Path.Combine(root, "second.json"), Path.Combine(root, "first.json")],
                bundle.Documents.Select(document => document.SourceName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FileContentSource_ReportsMissingFilesAndHonorsCancellation()
    {
        string root = CreateTempDirectory();
        try
        {
            var source = new FileContentPackSource(root);
            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
                await source.ReadAsync(new ContentPackTextRequest("missing.json", [])));

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await source.ReadAsync(new ContentPackTextRequest("missing.json", []), cancellation.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TextWriterEventSink_PreservesOrderAndHonorsCancellation()
    {
        var output = new StringWriter();
        var sink = new TextWriterEventSink(output);

        await sink.PublishAsync("first");
        await sink.PublishAsync("second");

        Assert.Equal($"first{Environment.NewLine}second{Environment.NewLine}", output.ToString());

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await sink.PublishAsync("third", cancellation.Token));
    }

    [Fact]
    public async Task ConsoleCommandSource_PreservesOptionsAndReturnsSelection()
    {
        var io = new ScriptedGameIO().QueueMenu(1);
        var source = new ConsoleHostCommandSource<string>(io);
        var request = new HostCommandRequest<string>(
            "Choose",
            [
                new HostCommandOption<string>("one", "First"),
                new HostCommandOption<string>("two", "Second"),
                new HostCommandOption<string>("three", "Third", IsEnabled: false)
            ],
            initialIndex: 1);

        HostCommandReadResult<string> result = await source.ReadAsync(request);

        Assert.True(result.IsSelected);
        Assert.Equal("two", result.Command);
        GameIoMenuCall menu = Assert.Single(io.Menus);
        Assert.Equal("Choose", menu.Header);
        Assert.Equal(["First", "Second", "Third"], menu.Options);
        Assert.Equal([false, false, true], menu.DisabledOptions);
        Assert.Equal(1, menu.InitialIndex);
        io.AssertConsumed();
    }

    [Fact]
    public async Task ConsoleCommandSource_DistinguishesMenuCancellationAndTokenCancellation()
    {
        var io = new ScriptedGameIO().QueueMenu(-1);
        var source = new ConsoleHostCommandSource<int>(io);
        var request = new HostCommandRequest<int>("Choose", [new HostCommandOption<int>(7, "Seven")]);

        HostCommandReadResult<int> result = await source.ReadAsync(request);
        Assert.Equal(HostCommandReadStatus.Cancelled, result.Status);
        Assert.False(result.IsSelected);
        io.AssertConsumed();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await source.ReadAsync(request, cancellation.Token));
    }

    [Fact]
    public void SystemRandomSource_IsSeedableAndKeepsValuesWithinContractBounds()
    {
        var first = new SystemRandomSource(1729);
        var second = new SystemRandomSource(1729);

        for (int index = 0; index < 100; index++)
        {
            int firstInteger = first.NextInt32(-4, 9);
            int secondInteger = second.NextInt32(-4, 9);
            decimal firstUnit = first.NextUnitDecimal();
            decimal secondUnit = second.NextUnitDecimal();

            Assert.Equal(firstInteger, secondInteger);
            Assert.InRange(firstInteger, -4, 8);
            Assert.Equal(firstUnit, secondUnit);
            Assert.True(firstUnit >= 0m && firstUnit < 1m);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"convergence-track-b-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
