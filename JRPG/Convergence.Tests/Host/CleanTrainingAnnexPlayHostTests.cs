using Convergence.Tests.TestSupport;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Host;
using JRPGPrototype.Host.CleanConsole.TrainingAnnex;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Runtime;
using Xunit;

namespace Convergence.Tests.Host;

public sealed class CleanTrainingAnnexPlayHostTests
{
    [Fact]
    public async Task CleanTrainingAnnexPlay_LoadsCleanContentHydratesActorValidatesSnapshotAndExits()
    {
        var io = new ScriptedGameIO().QueueMenu(0, 1, 2, 3, 4, 6, 0, 5, 7, 0, 9);
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
        Assert.Equal(4, summary.ActorCount);
        Assert.Equal(3, summary.EnemyActorCount);
        Assert.Equal(
            [
                Qualified("echo_adept"),
                Qualified("ashling"),
                Qualified("bramble_runner"),
                Qualified("ward_shell")
            ],
            summary.ActorEntityIds);
        Assert.Equal(
            [
                ContentId.Parse("echo_adept"),
                ContentId.Parse("enemy_ashling"),
                ContentId.Parse("enemy_bramble_runner"),
                ContentId.Parse("enemy_ward_shell")
            ],
            summary.ActorInstanceIds);
        Assert.Equal(2, summary.ActiveSkillCount);
        Assert.Equal(1, summary.PassiveSkillCount);
        RuntimeResourceSnapshot hp = Assert.Single(summary.PlayerResources, resource =>
            resource.ResourceId == ContentId.Parse("hp"));
        RuntimeResourceSnapshot sp = Assert.Single(summary.PlayerResources, resource =>
            resource.ResourceId == ContentId.Parse("sp"));
        Assert.Equal(70, hp.Current);
        Assert.Equal(80, hp.Maximum);
        Assert.Equal(28, sp.Current);
        Assert.Equal(28, sp.Maximum);
        Assert.Equal(4, summary.PlayerProgression.Level);
        Assert.Equal(0, summary.PlayerProgression.Experience);
        Assert.Equal(40, summary.PlayerProgression.LifetimeExperience);
        Assert.Equal(3, summary.PlayerProgression.UnspentStatPoints);
        Assert.True(summary.StatResolutionPreviewed);
        Assert.Equal(8, Resolved(summary, "strength").FinalValue);
        Assert.Equal(5, Resolved(summary, "magic").FinalValue);
        Assert.Equal(5, Resolved(summary, "vitality").FinalValue);
        Assert.Equal(5, Resolved(summary, "agility").FinalValue);
        Assert.Equal(4, Resolved(summary, "luck").FinalValue);
        Assert.True(summary.ResourceRecalculationApplied);
        Assert.True(summary.GrowthApplied);
        Assert.Equal(1, summary.LevelUpCount);
        Assert.True(summary.StartupSnapshotValidated);
        Assert.Equal(0, summary.StartupSnapshotDiagnosticCount);
        Assert.Equal(Qualified("staging_area"), summary.FinalLocationId);
        Assert.Equal(
            [Qualified("staging_area"), Qualified("training_annex_entrance"), Qualified("staging_area")],
            summary.LocationHistory);
        Assert.Equal(Qualified("training_annex_entrance"), summary.FinalDungeonNodeId);
        Assert.Equal([Qualified("training_annex_entrance")], summary.VisitedDungeonNodeIds);
        Assert.Empty(summary.UnlockedCheckpointIds);
        Assert.False(summary.BarrierRejected);
        Assert.False(summary.EncounterTriggerConsumed);
        Assert.Empty(summary.PreparedEncounterIds);
        Assert.Empty(summary.PreparedEncounterActorInstanceIds);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Empty(summary.ExecutedFieldActionIds);
        Assert.Equal(0, summary.CancelledFieldTargetSelections);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.InspectSession,
                CleanTrainingAnnexPlayCommand.InspectActor,
                CleanTrainingAnnexPlayCommand.ResolveStats,
                CleanTrainingAnnexPlayCommand.RecalculateResources,
                CleanTrainingAnnexPlayCommand.ApplyVictoryExperience,
                CleanTrainingAnnexPlayCommand.EnterTrainingAnnex,
                CleanTrainingAnnexPlayCommand.InspectSession,
                CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot,
                CleanTrainingAnnexPlayCommand.ReturnToStagingArea,
                CleanTrainingAnnexPlayCommand.InspectSession,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);

        Assert.Equal(11, io.Menus.Count);
        foreach (GameIoMenuCall menu in io.Menus.Where((_, index) => index is <= 5 or >= 9))
        {
            Assert.Equal("Training Annex Clean Session - Staging Area", menu.Header);
            Assert.Equal(
            [
                    "Inspect Session",
                    "Inspect Actors",
                    "Resolve Stats",
                "Recalculate Resources",
                "Apply Victory EXP",
                "Validate Startup Snapshot",
                "Enter Training Annex",
                "Inventory",
                "Field Skills",
                "Exit"
            ],
                menu.Options);
        }
        foreach (GameIoMenuCall menu in io.Menus.Skip(6).Take(3))
        {
            Assert.Equal("Training Annex Clean Session - Training Annex Entrance", menu.Header);
            Assert.Equal(
            [
                "Inspect Session",
                "Inspect Actors",
                "Resolve Stats",
                "Recalculate Resources",
                "Apply Victory EXP",
                "Validate Startup Snapshot",
                "Enter Review Hall",
                "Return to Staging Area",
                "Inventory",
                "Field Skills",
                "Exit"
            ],
                menu.Options);
        }
        io.AssertConsumed();

        string text = output.ToString();
        Assert.Contains("Clean Training Annex session booted.", text, StringComparison.Ordinal);
        Assert.Contains("without legacy Database startup", text, StringComparison.Ordinal);
        Assert.Contains("Hydrated Echo Adept at level 3.", text, StringComparison.Ordinal);
        Assert.Contains("Hydrated clean actor roster with 4 actor(s): 3 enemy model(s).", text, StringComparison.Ordinal);
        Assert.Contains("Field location: Staging Area.", text, StringComparison.Ordinal);
        Assert.Contains("Session: convergence.training_annex_slice; 5 entities, 10 skills, 5 items, 3 encounters, 1 dungeons. Location: Staging Area (convergence.training_annex_slice:staging_area); dungeon state: not active.", text, StringComparison.Ordinal);
        Assert.Contains("Field navigation: entered Training Annex; location Training Annex Entrance (convergence.training_annex_slice:training_annex_entrance).", text, StringComparison.Ordinal);
        Assert.Contains("Session: convergence.training_annex_slice; 5 entities, 10 skills, 5 items, 3 encounters, 1 dungeons. Location: Training Annex Entrance (convergence.training_annex_slice:training_annex_entrance); dungeon state: convergence.training_annex_slice:training_annex_entrance.", text, StringComparison.Ordinal);
        Assert.Contains("Field navigation: returned to Staging Area; location Staging Area (convergence.training_annex_slice:staging_area).", text, StringComparison.Ordinal);
        Assert.Contains("Actor roster: 4 actor(s).", text, StringComparison.Ordinal);
        Assert.Contains("Player: Echo Adept; instance echo_adept; level 3; resources: hp 80/80, sp 28/28.", text, StringComparison.Ordinal);
        Assert.Contains("Enemy: Ashling; instance enemy_ashling; level 2; resources: hp 65/65, sp 29/29.", text, StringComparison.Ordinal);
        Assert.Contains("Enemy: Bramble Runner; instance enemy_bramble_runner; level 3; resources: hp 75/75, sp 22/22.", text, StringComparison.Ordinal);
        Assert.Contains("Enemy: Ward Shell; instance enemy_ward_shell; level 4; resources: hp 100/100, sp 27/27.", text, StringComparison.Ordinal);
        Assert.Contains("Base stats: strength 6, magic 4, vitality 5, agility 5, luck 4.", text, StringComparison.Ordinal);
        Assert.Contains("Effective stats: strength 6, magic 4, vitality 5, agility 5, luck 4.", text, StringComparison.Ordinal);
        Assert.Contains("Active skills: Frost Tip, Echo Strike.", text, StringComparison.Ordinal);
        Assert.Contains("Passive skills: Steady Breath.", text, StringComparison.Ordinal);
        Assert.Contains("Stat policy: standard_stat resolved Echo Adept with attack stage +1.", text, StringComparison.Ordinal);
        Assert.Contains("Resolved stats: strength 6->8, magic 4->5, vitality 5->5, agility 5->5, luck 4->4.", text, StringComparison.Ordinal);
        Assert.Contains("Resource recalculation: Echo Adept hp 80/80 -> 70/80.", text, StringComparison.Ordinal);
        Assert.Contains("Resource policy: standard_growth preserved current hp and recalculated maximum 80.", text, StringComparison.Ordinal);
        Assert.Contains("Victory EXP: awarded 40 EXP through standard_growth.", text, StringComparison.Ordinal);
        Assert.Contains("Growth result: Echo Adept level 3->4; exp 0->0; lifetime 0->40; stat points 2->3.", text, StringComparison.Ordinal);
        Assert.Contains("Level-up events: 4.", text, StringComparison.Ordinal);
        Assert.Contains("Startup snapshot validation: 0 diagnostic(s).", text, StringComparison.Ordinal);
        Assert.Contains("Clean Training Annex session exited.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_TraversesGenericDungeonNodesWithoutStartingEncounter()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 8, 6, 6, 7, 7, 7, 9);
        using var output = new StringWriter();
        var source = new RecordingContentPackTextSource(Path.Combine(FindRepositoryRoot(), "Data", "Jsons"));
        var host = new CleanTrainingAnnexPlayHost(
            source,
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(Qualified("staging_area"), summary.FinalLocationId);
        Assert.Equal(Qualified("training_annex_entrance"), summary.FinalDungeonNodeId);
        Assert.Equal(
            [Qualified("training_annex_entrance"), Qualified("review_hall"), Qualified("review_alcove")],
            summary.VisitedDungeonNodeIds);
        Assert.Equal([Qualified("review_checkpoint")], summary.UnlockedCheckpointIds);
        Assert.True(summary.BarrierRejected);
        Assert.False(summary.EncounterTriggerConsumed);
        Assert.Empty(summary.PreparedEncounterIds);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.EnterTrainingAnnex,
                CleanTrainingAnnexPlayCommand.EnterReviewHall,
                CleanTrainingAnnexPlayCommand.InspectTrainingBarrier,
                CleanTrainingAnnexPlayCommand.EnterReviewAlcove,
                CleanTrainingAnnexPlayCommand.UnlockReviewCheckpoint,
                CleanTrainingAnnexPlayCommand.ReturnToReviewHall,
                CleanTrainingAnnexPlayCommand.ReturnToAnnexEntrance,
                CleanTrainingAnnexPlayCommand.ReturnToStagingArea,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);

        string text = output.ToString();
        Assert.Contains("Dungeon traversal: Training Annex Entrance -> Review Hall.", text, StringComparison.Ordinal);
        Assert.Contains("Dungeon traversal rejected: The sample barrier is sealed.", text, StringComparison.Ordinal);
        Assert.Contains("Dungeon traversal: Review Hall -> Review Alcove.", text, StringComparison.Ordinal);
        Assert.Contains("Dungeon checkpoint unlocked: convergence.training_annex_slice:review_checkpoint.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("EncounterRequested", text, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_HostTriggerExplicitlyPreparesCatalogEncounterActors()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 7, 7, 9);
        using var output = new StringWriter();
        var source = new RecordingContentPackTextSource(Path.Combine(FindRepositoryRoot(), "Data", "Jsons"));
        var host = new CleanTrainingAnnexPlayHost(
            source,
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.True(summary.EncounterTriggerConsumed);
        Assert.Equal([Qualified("ashling_drill")], summary.PreparedEncounterIds);
        Assert.Equal(
            [ContentId.Parse("review_hall_trigger_ashling_1")],
            summary.PreparedEncounterActorInstanceIds);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.EnterTrainingAnnex,
                CleanTrainingAnnexPlayCommand.EnterReviewHall,
                CleanTrainingAnnexPlayCommand.ActivateAshlingEncounterTrigger,
                CleanTrainingAnnexPlayCommand.ReturnToAnnexEntrance,
                CleanTrainingAnnexPlayCommand.ReturnToStagingArea,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);

        string text = output.ToString();
        Assert.Contains(
            "Encounter trigger review_hall_ashling_trigger prepared Ashling Drill: Ashling (review_hall_trigger_ashling_1).",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "Encounter actors are ready for a host-owned battle handoff; traversal did not start this encounter.",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Battle started", text, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("Activate Ashling Encounter Trigger", io.Menus[2].Options[9]);
        Assert.Equal("Ashling Encounter Trigger (Resolved)", io.Menus[3].Options[9]);
        Assert.True(io.Menus[3].DisabledOptions[9]);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FieldItemUsesSharedExecutorAndCommitsOneReservedItem()
    {
        var io = new ScriptedGameIO().QueueMenu(3, 7, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal([Qualified("annex_tonic")], summary.ExecutedFieldActionIds);
        Assert.Equal(0, summary.CancelledFieldTargetSelections);
        Assert.Equal(80, Assert.Single(summary.PlayerResources, resource => resource.ResourceId == ContentId.Parse("hp")).Current);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.RecalculateResources,
                CleanTrainingAnnexPlayCommand.OpenInventory,
                CleanTrainingAnnexPlayCommand.UseAnnexTonic,
                CleanTrainingAnnexPlayCommand.TargetPlayer,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);
        Assert.Contains(
            "Field action executed: Annex Tonic; HP 70->80/80; SP 28->28/28; inventory convergence.training_annex_slice:annex_tonic x0.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_NoEffectItemDoesNotConsumeInventory()
    {
        var io = new ScriptedGameIO().QueueMenu(7, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Empty(summary.ExecutedFieldActionIds);
        Assert.Contains("would have no effect", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_TargetCancellationDoesNotMutateOrReserveInventory()
    {
        var io = new ScriptedGameIO().QueueMenu(7, 0, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Empty(summary.ExecutedFieldActionIds);
        Assert.Equal(1, summary.CancelledFieldTargetSelections);
        Assert.Contains(
            "Field item target selection canceled; inventory and actor state are unchanged.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FieldSkillUsesTypedEffectsAndCommitsSkillCost()
    {
        var io = new ScriptedGameIO().QueueMenu(4, 3, 8, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal([Qualified("mend")], summary.ExecutedFieldActionIds);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(80, Assert.Single(summary.PlayerResources, resource => resource.ResourceId == ContentId.Parse("hp")).Current);
        Assert.Equal(26, Assert.Single(summary.PlayerResources, resource => resource.ResourceId == ContentId.Parse("sp")).Current);
        Assert.Contains(
            "Field action executed: Mend; HP 70->80/80; SP 28->26/28; inventory convergence.training_annex_slice:annex_tonic x1.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
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

    private static StatResolutionResult Resolved(CleanTrainingAnnexPlaySummary summary, string statId) =>
        Assert.Single(summary.PlayerResolvedStats, result => result.StatId == ContentId.Parse(statId));

    private static CleanTrainingAnnexPlayHost CreateHost(ScriptedGameIO io, StringWriter output) =>
        new(
            new RecordingContentPackTextSource(Path.Combine(FindRepositoryRoot(), "Data", "Jsons")),
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io));

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
