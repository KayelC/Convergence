using Convergence.Tests.TestSupport;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Host;
using JRPGPrototype.Host.CleanConsole.TrainingAnnex;
using JRPGPrototype.Hosting;
using Xunit;

namespace Convergence.Tests.Host;

public sealed class CleanTrainingAnnexPlayHostTests
{
    [Fact]
    public async Task CleanTrainingAnnexPlay_LoadsCleanContentHydratesActorValidatesSnapshotAndExits()
    {
        var io = new ScriptedGameIO().QueueMenu(0, 1, 2, 3);
        using var output = new StringWriter();
        var source = new RecordingContentPackTextSource(Path.Combine(FindRepositoryRoot(), "Data", "Jsons"));
        var host = new CleanTrainingAnnexPlayHost(
            source,
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(host.LastSummary);
        CleanTrainingAnnexPlaySummary summary = host.LastSummary!;
        Assert.Equal(["training_annex_slice.manifest.json"], source.ManifestRequests);
        Assert.Equal(
            [
                "training_annex_slice.races.json",
                "training_annex_slice.ailments.json",
                "training_annex_slice.skills.json",
                "training_annex_slice.entities.json",
                "training_annex_slice.items.json",
                "training_annex_slice.equipment.json",
                "training_annex_slice.shops.json",
                "training_annex_slice.negotiations.json",
                "training_annex_slice.encounters.json",
                "training_annex_slice.dungeons.json",
                "training_annex_slice.fusion.json",
                "training_annex_slice.rulesets.json"
            ],
            source.DocumentRequests);
        Assert.Equal(source.ManifestRequests, summary.RequestedManifestPaths);
        Assert.Equal(source.DocumentRequests, summary.RequestedDocumentPaths);
        Assert.Equal(Qualified("echo_adept"), summary.PlayerEntityId);
        Assert.Equal(3, summary.PlayerLevel);
        Assert.Equal(2, summary.ActiveSkillCount);
        Assert.Equal(1, summary.PassiveSkillCount);
        Assert.True(summary.StartupSnapshotValidated);
        Assert.Equal(0, summary.StartupSnapshotDiagnosticCount);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.InspectSession,
                CleanTrainingAnnexPlayCommand.InspectActor,
                CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);

        Assert.Equal(4, io.Menus.Count);
        foreach (GameIoMenuCall menu in io.Menus)
        {
            Assert.Equal("Training Annex Clean Session", menu.Header);
            Assert.Equal(
                ["Inspect Session", "Inspect Actor", "Validate Startup Snapshot", "Exit"],
                menu.Options);
        }
        io.AssertConsumed();

        string text = output.ToString();
        Assert.Contains("Clean Training Annex session booted.", text, StringComparison.Ordinal);
        Assert.Contains("without legacy Database startup", text, StringComparison.Ordinal);
        Assert.Contains("Hydrated Echo Adept at level 3.", text, StringComparison.Ordinal);
        Assert.Contains("Session: convergence.training_annex_slice; 5 entities, 10 skills, 5 items, 3 encounters, 1 dungeons.", text, StringComparison.Ordinal);
        Assert.Contains("Actor: Echo Adept; level 3; resources:", text, StringComparison.Ordinal);
        Assert.Contains("Active skills: Frost Tip, Echo Strike.", text, StringComparison.Ordinal);
        Assert.Contains("Passive skills: Steady Breath.", text, StringComparison.Ordinal);
        Assert.Contains("Startup snapshot validation: 0 diagnostic(s).", text, StringComparison.Ordinal);
        Assert.Contains("Clean Training Annex session exited.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_MissingContentReportsFailureWithoutReadingCommands()
    {
        var io = new ScriptedGameIO();
        using var output = new StringWriter();
        var source = new FailingContentSource();
        var host = new CleanTrainingAnnexPlayHost(
            source,
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io));

        int exitCode = await host.RunAsync();

        Assert.Equal(2, exitCode);
        Assert.Null(host.LastSummary);
        Assert.Equal(["training_annex_slice.manifest.json"], source.ManifestRequests);
        Assert.Empty(io.Menus);
        Assert.Contains("Content read failed", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("training_annex_slice.manifest.json", output.ToString(), StringComparison.Ordinal);
    }

    private static ContentId Qualified(string localId) =>
        ContentId.Parse($"convergence.training_annex_slice:{localId}");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JRPG.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find JRPG.sln.");
    }

    private sealed class RecordingContentPackTextSource(string root) : IContentPackTextSource
    {
        private readonly List<string> _manifestRequests = [];
        private readonly List<string> _documentRequests = [];

        public IReadOnlyList<string> ManifestRequests => _manifestRequests;
        public IReadOnlyList<string> DocumentRequests => _documentRequests;

        public async ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            _manifestRequests.Add(request.ManifestPath);
            _documentRequests.AddRange(request.DocumentPaths);
            string manifest = await File.ReadAllTextAsync(Path.Combine(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                documents.Add(new ContentDocumentText(
                    path,
                    path,
                    await File.ReadAllTextAsync(Path.Combine(root, path), cancellationToken)));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }
    }

    private sealed class FailingContentSource : IContentPackTextSource
    {
        private readonly List<string> _manifestRequests = [];

        public IReadOnlyList<string> ManifestRequests => _manifestRequests;

        public ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            _manifestRequests.Add(request.ManifestPath);
            throw new FileNotFoundException("Missing Training Annex content.", request.ManifestPath);
        }
    }
}
