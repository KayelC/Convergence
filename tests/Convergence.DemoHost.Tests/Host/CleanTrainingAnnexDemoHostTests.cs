using Convergence.Content;
using Convergence.Catalog;
using Convergence.DemoHost;
using Convergence.Hosting;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Runtime;
using Xunit;

namespace Convergence.DemoHost.Tests.Host;

public sealed class CleanTrainingAnnexDemoHostTests
{
    [Fact]
    public async Task CleanTrainingAnnexDemo_RunsOriginalCleanRuntimeSliceEndToEnd()
    {
        using var output = new StringWriter();
        var source = new RecordingContentPackTextSource(Path.Combine(AppContext.BaseDirectory, "Content"));
        var host = new CleanTrainingAnnexDemoHost(source, new TextWriterEventSink(output));

        int exitCode = await host.RunAsync();

        Assert.NotNull(host.LastSummary);
        CleanTrainingAnnexDemoSummary summary = host.LastSummary!;
        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["original/training-annex/training_annex_slice.manifest.json"],
            source.ManifestRequests);
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

        Assert.Equal([RuntimeDungeonTraversalEventKind.TransitionApplied], summary.DungeonEventKinds);
        Assert.Equal(Qualified("ashling_drill"), summary.EncounterId);
        Assert.Equal(Qualified("ashling"), summary.EnemyEntityId);

        Assert.Equal(BattleActionExecutionStatus.Executed, summary.ItemStatus);
        Assert.Equal(ItemConsumptionDecision.ConsumeOne, summary.ItemConsumption);
        Assert.True(summary.ItemConsumptionCommitted);
        Assert.Equal(0, summary.InventoryRemaining);

        Assert.Equal(AutomatedBattleOutcome.Victory, summary.BattleOutcome);
        Assert.Equal(ContentId.Parse("player_team"), summary.WinningTeamId);
        Assert.True(summary.RewardExperience > 0);
        Assert.True(summary.RewardCredits > 0);
        Assert.True(summary.LifetimeExperienceAfter > 0);
        Assert.True(summary.LevelAfter >= 3);
        Assert.True(summary.SaveValid);
        Assert.Equal(0, summary.SaveDiagnosticCount);

        string text = output.ToString();
        Assert.Contains("[catalog] Loaded Training Annex slice.", text, StringComparison.Ordinal);
        Assert.Contains("[catalog] Sample counts: 4 races, 7 entities, 10 skills, 5 items, 3 encounters.", text, StringComparison.Ordinal);
        Assert.Contains("[ruleset] Bound standard Training Annex rulesets.", text, StringComparison.Ordinal);
        Assert.Contains("[dungeon] TransitionApplied:", text, StringComparison.Ordinal);
        Assert.Contains("[encounter] Host trigger annex_scene_trigger selected Ashling Drill.", text, StringComparison.Ordinal);
        Assert.Contains("[encounter] Resolved Ashling Drill: Ashling.", text, StringComparison.Ordinal);
        Assert.Contains("[item] Annex Tonic:", text, StringComparison.Ordinal);
        Assert.Contains("SkillSelected: echo_adept selected Frost Tip.", text, StringComparison.Ordinal);
        Assert.Contains("[battle] Outcome Victory; winner player_team.", text, StringComparison.Ordinal);
        Assert.Contains("[reward] Awarded", text, StringComparison.Ordinal);
        Assert.Contains("[save] Validated Training Annex save snapshot with 0 diagnostic(s).", text, StringComparison.Ordinal);
        Assert.Contains("[outcome] Training Annex runtime slice completed successfully.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanTrainingAnnexDemo_MissingContentReportsReadFailureWithoutFallback()
    {
        using var output = new StringWriter();
        var source = new FailingContentSource();
        var host = new CleanTrainingAnnexDemoHost(source, new TextWriterEventSink(output));

        int exitCode = await host.RunAsync();

        Assert.Equal(2, exitCode);
        Assert.Null(host.LastSummary);
        Assert.Equal(
            ["original/training-annex/training_annex_slice.manifest.json"],
            source.ManifestRequests);
        Assert.Contains("Content read failed", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("training_annex_slice.manifest.json", output.ToString(), StringComparison.Ordinal);
    }

    private static ContentId Qualified(string localId) =>
        ContentId.Parse($"convergence.training_annex_slice:{localId}");

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
            string manifest = await File.ReadAllTextAsync(TestContentPath.ResolveManifest(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                documents.Add(new ContentDocumentText(
                    path,
                    path,
                    await File.ReadAllTextAsync(TestContentPath.ResolveDocument(root, request.ManifestPath, path), cancellationToken)));
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
