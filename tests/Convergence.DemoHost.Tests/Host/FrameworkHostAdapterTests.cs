using Convergence.DemoHost.Tests.TestSupport;
using Convergence.DemoHost;
using Convergence.Catalog;
using Convergence.Hosting;
using Convergence.Content;
using Xunit;

namespace Convergence.DemoHost.Tests.Host;

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
    public async Task FileContentSource_LoadsConfinedNestedPathsAndPreservesLogicalPaths()
    {
        string root = CreateTempDirectory();
        try
        {
            string nested = Path.Combine(root, "packs", "nested");
            Directory.CreateDirectory(nested);
            await File.WriteAllTextAsync(Path.Combine(nested, "pack.manifest.json"), "{\"manifest\":true}");
            await File.WriteAllTextAsync(Path.Combine(nested, "document.json"), "{\"document\":true}");
            var source = new FileContentPackSource(root);
            const string manifestPath = "packs/nested/pack.manifest.json";
            const string mixedDocumentPath = "document.json";

            ContentPackTextBundle bundle = await source.ReadAsync(new ContentPackTextRequest(
                manifestPath,
                [mixedDocumentPath]));

            Assert.Equal(Path.GetFullPath(Path.Combine(nested, "pack.manifest.json")), bundle.ManifestSourceName);
            ContentDocumentText document = Assert.Single(bundle.Documents);
            Assert.Equal(mixedDocumentPath, document.Path);
            Assert.Equal(Path.GetFullPath(Path.Combine(nested, "document.json")), document.SourceName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FileContentSource_KeepsIdenticalDocumentNamesIsolatedByManifestDirectory()
    {
        string root = CreateTempDirectory();
        try
        {
            string firstPack = Path.Combine(root, "packs", "first");
            string secondPack = Path.Combine(root, "packs", "second");
            Directory.CreateDirectory(firstPack);
            Directory.CreateDirectory(secondPack);
            await File.WriteAllTextAsync(Path.Combine(firstPack, "pack.manifest.json"), "{\"pack\":1}");
            await File.WriteAllTextAsync(Path.Combine(secondPack, "pack.manifest.json"), "{\"pack\":2}");
            await File.WriteAllTextAsync(Path.Combine(firstPack, "skills.json"), "{\"skills\":1}");
            await File.WriteAllTextAsync(Path.Combine(secondPack, "skills.json"), "{\"skills\":2}");
            var source = new FileContentPackSource(root);

            ContentPackTextBundle first = await source.ReadAsync(new ContentPackTextRequest(
                "packs/first/pack.manifest.json",
                ["skills.json"]));
            ContentPackTextBundle second = await source.ReadAsync(new ContentPackTextRequest(
                "packs/second/pack.manifest.json",
                ["skills.json"]));

            Assert.Equal("{\"skills\":1}", Assert.Single(first.Documents).Json);
            Assert.Equal("{\"skills\":2}", Assert.Single(second.Documents).Json);
            Assert.Equal(Path.Combine(firstPack, "skills.json"), first.Documents[0].SourceName);
            Assert.Equal(Path.Combine(secondPack, "skills.json"), second.Documents[0].SourceName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DemoHostContentOutput_PreservesPackRelativeDirectories()
    {
        string contentRoot = Path.Combine(AppContext.BaseDirectory, "Content");

        Assert.True(File.Exists(Path.Combine(
            contentRoot,
            "original",
            "training-annex",
            "training_annex_slice.manifest.json")));
        Assert.True(File.Exists(Path.Combine(
            contentRoot,
            "demos",
            "clean-battle",
            "clean_battle_demo.manifest.json")));
        Assert.False(File.Exists(Path.Combine(contentRoot, "training_annex_slice.manifest.json")));
        Assert.False(File.Exists(Path.Combine(contentRoot, "clean_battle_demo.manifest.json")));
    }

    [Fact]
    public async Task FileContentSource_RejectsRootedManifestAndDocumentPathsEvenWhenContained()
    {
        string root = CreateTempDirectory();
        try
        {
            string manifest = Path.Combine(root, "pack.manifest.json");
            string document = Path.Combine(root, "document.json");
            await File.WriteAllTextAsync(manifest, "{\"manifest\":true}");
            await File.WriteAllTextAsync(document, "{\"document\":true}");
            var source = new FileContentPackSource(root);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await source.ReadAsync(new ContentPackTextRequest(manifest, [])));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await source.ReadAsync(new ContentPackTextRequest("pack.manifest.json", [document])));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("..\\outside.json")]
    [InlineData("nested/..\\../outside.json")]
    [InlineData("../content-other/outside.json")]
    public async Task FileContentSource_RejectsPathsThatResolveOutsideTheContentRoot(string traversalPath)
    {
        string parent = CreateTempDirectory();
        string root = Path.Combine(parent, "content");
        Directory.CreateDirectory(root);
        try
        {
            var source = new FileContentPackSource(root);

            UnauthorizedAccessException exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await source.ReadAsync(new ContentPackTextRequest(traversalPath, [])));

            Assert.Contains(traversalPath, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task FileContentSource_ValidatesEveryPathBeforeReadingAnyFile()
    {
        string root = CreateTempDirectory();
        try
        {
            var source = new FileContentPackSource(root);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await source.ReadAsync(new ContentPackTextRequest(
                    "missing.manifest.json",
                    ["../outside.json"])));
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
                new HostCommandOption<string>(
                    "two",
                    "Second",
                    SelectionIdentity: HostCommandSelectionIdentity.ForContent(
                        ContentId.Parse("selected_skill"))),
                new HostCommandOption<string>("three", "Third", IsEnabled: false)
            ],
            initialIndex: 1);

        HostCommandReadResult<string> result = await source.ReadAsync(request);

        Assert.True(result.IsSelected);
        Assert.Equal("two", result.Command);
        Assert.Equal(ContentId.Parse("selected_skill"), result.SelectionIdentity?.ContentId);
        Assert.Null(result.SelectionIdentity?.RuntimeInstanceId);
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
