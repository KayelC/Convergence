using Convergence.Tests.TestSupport;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Data.SkillSystem.Catalog;
using JRPGPrototype.Host;
using JRPGPrototype.Host.CleanConsole.TrainingAnnex;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Battle;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Battle.Runtime;
using JRPGPrototype.Logic.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        Assert.Null(summary.PreparedBattleRewardPreview);
        Assert.Null(summary.AppliedBattleReward);
        Assert.Equal(0, summary.Wallet.Macca);
        Assert.Empty(summary.SessionProgress.Counters);
        Assert.Empty(summary.SessionProgress.Flags);
        Assert.Equal(0, summary.ManualSaveCount);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.Equal(0, summary.SuspendSaveCount);
        Assert.Equal(0, summary.SuspendLoadCount);
        Assert.False(summary.SuspendSaveConsumed);
        Assert.False(summary.HasManualSave);
        Assert.False(summary.HasSuspendSave);
        Assert.Equal(0, summary.SaveDiagnosticCount);
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
                "Exit",
                "Save / Load"
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
                "Exit",
                "Save / Load"
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
    public async Task CleanTrainingAnnexPlay_StartPreparedBattle_UsesManualSkillActionsAndReachesVictory()
    {
        var io = new ScriptedGameIO().QueueMenu(
            6, 6, 9, 10,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            13);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.True(summary.EncounterTriggerConsumed);
        Assert.True(summary.PreparedBattleStarted);
        Assert.Equal(BattleEncounterOutcome.Victory, summary.PreparedBattleOutcome);
        Assert.Equal(ContentId.Parse("player_team"), summary.PreparedBattleWinningTeamId);
        Assert.Equal(3, summary.ExecutedBattleActionIds.Count(id => id == Qualified("frost_tip")));
        Assert.Equal(1, summary.ExecutedBattleActionIds.Count(id => id == Qualified("ash_spark")));
        Assert.Equal(3, summary.ExecutedBattleEffectEvidence.Count(effect =>
            IsDamage(effect, Qualified("frost_tip"), DamageElement.Ice)));
        Assert.Equal(1, summary.ExecutedBattleEffectEvidence.Count(effect =>
            IsDamage(effect, Qualified("ash_spark"), DamageElement.Fire)));
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(70, Resource(summary, "hp").Current);
        Assert.Equal(25, Resource(summary, "sp").Current);
        Assert.Equal(3, summary.LifecycleEvidence.Count(evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.PassiveTriggered &&
            evidence.RelatedContentId == Qualified("steady_breath") &&
            evidence.Detail == "owner_turn_end"));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.ResourceChanged &&
            evidence.RelatedContentId == ContentId.Parse("hp") &&
            evidence.Value == 3m);
        Assert.True(summary.PreparedBattleEventCount > 0);
        TrainingAnnexCombatResolutionEvidence frost = summary.CombatResolutionEvidence.First(
            evidence => evidence.SourceActionId == Qualified("frost_tip") && evidence.Value == 23m);
        Assert.Equal(DamageElement.Ice, frost.DamageElement);
        Assert.Equal(8, frost.Power);
        Assert.Equal(100, frost.Accuracy);
        Assert.Equal(CriticalMode.Never, frost.CriticalMode);
        Assert.True(frost.Hit);
        Assert.False(frost.IsCritical);
        Assert.Equal(ElementalAffinity.Weak, frost.ResolvedAffinity);
        Assert.Equal(PressTurnOutcome.Weakness, frost.PressTurnOutcome);
        Assert.Contains(summary.BattleKnowledgeEvidence, evidence =>
            IsElementalKnowledge(
                evidence,
                Qualified("frost_tip"),
                Qualified("ashling"),
                DamageElement.Ice,
                ElementalAffinity.Weak));
        Assert.Contains(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("ashling") &&
            knowledge.Element == DamageElement.Ice &&
            knowledge.Affinity == ElementalAffinity.Weak);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.ActionId == Qualified("frost_tip") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.PressTurn &&
            evidence.PressTurnOutcome == PressTurnOutcome.Weakness &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 1);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.ActionId == Qualified("frost_tip") &&
            evidence.BeforeFullIcons == 0 &&
            evidence.BeforeBlinkingIcons == 1 &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("review_hall_trigger_ashling_1") &&
            evidence.ActionId == Qualified("ash_spark") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.PressTurn &&
            evidence.PressTurnOutcome == PressTurnOutcome.Normal &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.NotNull(summary.PreparedBattleRewardPreview);
        Assert.Equal(1, summary.PreparedBattleRewardPreview!.TotalExperience);
        Assert.Equal(14, summary.PreparedBattleRewardPreview.TotalMacca);
        Assert.NotNull(summary.AppliedBattleReward);
        Assert.Equal(1, summary.AppliedBattleReward!.TotalExperience);
        Assert.Equal(14, summary.AppliedBattleReward.TotalMacca);
        Assert.Equal(0, summary.AppliedBattleRewardLevelUpCount);
        Assert.True(summary.GrowthApplied);
        Assert.Equal(0, summary.LevelUpCount);
        Assert.Equal(1, summary.PlayerProgression.Experience);
        Assert.Equal(1, summary.PlayerProgression.LifetimeExperience);
        Assert.Equal(14, summary.Wallet.Macca);
        Assert.Equal(1, summary.SessionProgress.Counters[ContentId.Parse("training_annex_victories")]);
        Assert.Equal(1, summary.SessionProgress.Counters[ContentId.Parse("training_annex_exp")]);
        Assert.Equal(14, summary.SessionProgress.Counters[ContentId.Parse("training_annex_macca")]);
        Assert.Contains(ContentId.Parse("ashling_drill_cleared"), summary.SessionProgress.Flags);

        string text = output.ToString();
        Assert.Contains("Clean battle started: Ashling Drill.", text, StringComparison.Ordinal);
        Assert.Contains("Press Turn before command: 1 full, 0 blinking.", text, StringComparison.Ordinal);
        Assert.Contains("Press Turn updated: 0 full, 1 blinking.", text, StringComparison.Ordinal);
        Assert.Contains("Battle action executed: Echo Adept used Frost Tip.", text, StringComparison.Ordinal);
        Assert.Contains("Battle knowledge updated: 1 discovery.", text, StringComparison.Ordinal);
        Assert.Contains("Battle action executed: Ashling used Ash Spark.", text, StringComparison.Ordinal);
        Assert.Contains("Clean battle ended: Victory; winner player_team.", text, StringComparison.Ordinal);
        Assert.Contains("Battle rewards applied: +1 EXP, +14 Macca.", text, StringComparison.Ordinal);
        Assert.Contains("Reward progression: Echo Adept level 3->3; exp 0->1; lifetime 0->1; wallet 0->14.", text, StringComparison.Ordinal);
        Assert.Equal("Start Prepared Battle", io.Menus[3].Options[10]);
        Assert.Equal("Prepared Battle (Resolved)", io.Menus[^1].Options[10]);
        Assert.True(io.Menus[^1].DisabledOptions[10]);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_BasicAttackUsesPracticeBlade()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.True(summary.PreparedBattleStarted);
        Assert.Equal(BattleEncounterOutcome.Cancelled, summary.PreparedBattleOutcome);
        Assert.Null(summary.AppliedBattleReward);
        Assert.Equal(0, summary.Wallet.Macca);
        Assert.Empty(summary.SessionProgress.Counters);
        Assert.Contains(Qualified("practice_blade"), summary.ExecutedBattleActionIds);
        Assert.Contains(Qualified("ash_spark"), summary.ExecutedBattleActionIds);
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            IsDamage(effect, Qualified("practice_blade"), DamageElement.Physical));
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            IsDamage(effect, Qualified("ash_spark"), DamageElement.Fire));
        Assert.Equal(1, summary.CancelledBattleCommandSelections);
        Assert.Equal(67, Resource(summary, "hp").Current);
        TrainingAnnexAiDecisionEvidence ai = Assert.Single(summary.AiDecisionEvidence);
        Assert.Equal(ContentId.Parse("review_hall_trigger_ashling_1"), ai.ActorInstanceId);
        Assert.Equal(Qualified("ashling"), ai.ActorEntityId);
        Assert.Equal(BattleActionSelectionStatus.Selected, ai.Status);
        Assert.Equal(Qualified("ash_spark"), ai.SelectedActionId);
        Assert.Equal([ContentId.Parse("echo_adept")], ai.TargetIds);
        Assert.True(ai.AssessmentCanExecute);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ContentId>)ai.TargetIds).Add(ContentId.Parse("unexpected")));
        TrainingAnnexCombatResolutionEvidence attack = Assert.Single(
            summary.CombatResolutionEvidence,
            evidence => evidence.SourceActionId == Qualified("practice_blade"));
        Assert.Equal(DamageElement.Physical, attack.DamageElement);
        Assert.Equal(12, attack.Power);
        Assert.Equal(95, attack.Accuracy);
        Assert.Equal(23, attack.Value);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.ActionId == Qualified("practice_blade") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.PressTurn &&
            evidence.PressTurnOutcome == PressTurnOutcome.Normal &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.Contains(
            "Battle action executed: Echo Adept used Practice Blade.",
            output.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "Framework AI selected: Ashling -> Ash Spark.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FrameworkAiPreservesAuthoredSkillOrderForEqualScores()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                ashlingBaseSkillIds: ["toxin_touch", "ash_spark"]));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexAiDecisionEvidence ai = Assert.Single(summary.AiDecisionEvidence);
        Assert.Equal(BattleActionSelectionStatus.Selected, ai.Status);
        Assert.Equal(Qualified("toxin_touch"), ai.SelectedActionId);
        Assert.True(ai.AssessmentCanExecute);
        Assert.Contains(Qualified("toxin_touch"), summary.ExecutedBattleActionIds);
        Assert.DoesNotContain(Qualified("ash_spark"), summary.ExecutedBattleActionIds);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FrameworkAiSkipsUnaffordableSkillUsingSharedAssessment()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                ashlingBaseSkillIds: ["toxin_touch", "ash_spark"],
                unaffordableSkillId: "toxin_touch"));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexAiDecisionEvidence ai = Assert.Single(summary.AiDecisionEvidence);
        Assert.Equal(BattleActionSelectionStatus.Selected, ai.Status);
        Assert.Equal(Qualified("ash_spark"), ai.SelectedActionId);
        Assert.True(ai.AssessmentCanExecute);
        Assert.DoesNotContain(Qualified("toxin_touch"), summary.ExecutedBattleActionIds);
        Assert.Contains(Qualified("ash_spark"), summary.ExecutedBattleActionIds);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FrameworkAiPassesWhenNoAuthoredSkillIsExecutable()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                ashlingBaseSkillIds: ["toxin_touch"],
                unaffordableSkillId: "toxin_touch"));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(2, summary.AiDecisionEvidence.Count);
        Assert.All(summary.AiDecisionEvidence, ai =>
        {
            Assert.Equal(BattleActionSelectionStatus.Pass, ai.Status);
            Assert.Equal(ContentId.Parse("pass"), ai.SelectedActionId);
            Assert.Empty(ai.TargetIds);
            Assert.Null(ai.AssessmentCanExecute);
        });
        Assert.Equal(2, summary.ExecutedBattleActionIds.Count(id => id == ContentId.Parse("pass")));
        Assert.DoesNotContain(Qualified("toxin_touch"), summary.ExecutedBattleActionIds);
        Assert.Contains("Framework AI selected: Ashling -> Pass.", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_AnalyzeLearnsAllDefenseChannelsAndValidatesSaveKnowledge()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 5, 0, -1, 5, 13);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.True(summary.StartupSnapshotValidated);
        Assert.Equal(0, summary.StartupSnapshotDiagnosticCount);
        Assert.Contains(ContentId.Parse("analyze"), summary.ExecutedBattleActionIds);
        Assert.Contains(summary.BattleKnowledgeEvidence, evidence =>
            IsElementalKnowledge(
                evidence,
                ContentId.Parse("analyze"),
                Qualified("ashling"),
                DamageElement.Fire,
                ElementalAffinity.Resist));
        Assert.Contains(summary.BattleKnowledgeEvidence, evidence =>
            IsAilmentKnowledge(
                evidence,
                ContentId.Parse("analyze"),
                Qualified("ashling"),
                Qualified("sample_poison"),
                ResistanceLevel.Normal));
        Assert.Contains(summary.BattleKnowledgeEvidence, evidence =>
            IsInstantDeathKnowledge(
                evidence,
                ContentId.Parse("analyze"),
                Qualified("ashling"),
                InstantDeathChannel.Light,
                ResistanceLevel.Normal));
        Assert.Contains(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("ashling") &&
            knowledge.Element == DamageElement.Ice &&
            knowledge.Affinity == ElementalAffinity.Weak);
        Assert.Contains(summary.BattleKnowledge.AilmentResistances, knowledge =>
            knowledge.EntityId == Qualified("ashling") &&
            knowledge.AilmentId == Qualified("sample_poison") &&
            knowledge.Resistance == ResistanceLevel.Normal);
        Assert.Contains(summary.BattleKnowledge.InstantDeathResistances, knowledge =>
            knowledge.EntityId == Qualified("ashling") &&
            knowledge.Channel == InstantDeathChannel.Dark &&
            knowledge.Resistance == ResistanceLevel.Normal);
        Assert.Contains("Battle knowledge updated:", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FrameworkAiReusesDiscoveredElementalResistance()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 4, 4, 4, 4, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                ashlingBaseSkillIds: ["ash_spark", "echo_strike"],
                affinityEntityId: "echo_adept",
                affinityElementId: "fire",
                affinity: "resist"));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(
            [Qualified("ash_spark"), Qualified("echo_strike")],
            summary.AiDecisionEvidence.Select(evidence => evidence.SelectedActionId).Take(2));
        Assert.DoesNotContain(summary.BattleKnowledgeEvidence, evidence =>
            IsElementalKnowledge(
                evidence,
                Qualified("ash_spark"),
                Qualified("echo_adept"),
                DamageElement.Fire,
                ElementalAffinity.Resist));
        Assert.Contains(summary.EncounterAiKnowledgeEvidence, evidence =>
            IsElementalKnowledge(
                evidence,
                Qualified("ash_spark"),
                Qualified("echo_adept"),
                DamageElement.Fire,
                ElementalAffinity.Resist));
        Assert.DoesNotContain(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("echo_adept") &&
            knowledge.Element == DamageElement.Fire);
        Assert.Contains(summary.EncounterAiKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("echo_adept") &&
            knowledge.Element == DamageElement.Fire &&
            knowledge.Affinity == ElementalAffinity.Resist);
        Assert.Contains("Framework AI selected: Ashling -> Ash Spark.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Framework AI selected: Ashling -> Echo Strike.", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Battle knowledge updated:", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_BattleItemCommitsOneReservedTonicAfterMeaningfulSuccess()
    {
        var io = new ScriptedGameIO().QueueMenu(3, 6, 6, 9, 10, 2, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.True(summary.PreparedBattleStarted);
        Assert.Contains(Qualified("annex_tonic"), summary.ExecutedBattleActionIds);
        Assert.Contains(Qualified("ash_spark"), summary.ExecutedBattleActionIds);
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            IsResourceEffect(effect, Qualified("annex_tonic"), "restore_resource", "hp"));
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            IsDamage(effect, Qualified("ash_spark"), DamageElement.Fire));
        Assert.Equal(0, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(67, Resource(summary, "hp").Current);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.ActionId == Qualified("annex_tonic") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.Normal &&
            evidence.PressTurnOutcome is null &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.Contains(
            "Battle action executed: Echo Adept used Annex Tonic.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_BattleGuardPassAndAnalyzeUseFrameworkCommands()
    {
        var io = new ScriptedGameIO().QueueMenu(
            6, 6, 9, 10,
            5, 0,
            3,
            4, 4,
            -1,
            13);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.True(summary.PreparedBattleStarted);
        Assert.Contains(ContentId.Parse("analyze"), summary.ExecutedBattleActionIds);
        Assert.Contains(ContentId.Parse("guard"), summary.ExecutedBattleActionIds);
        Assert.Equal(2, summary.ExecutedBattleActionIds.Count(id => id == ContentId.Parse("pass")));
        Assert.Equal(3, summary.ExecutedBattleActionIds.Count(id => id == Qualified("ash_spark")));
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActionId == ContentId.Parse("analyze") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.Normal &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActionId == ContentId.Parse("guard") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.Normal &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActionId == ContentId.Parse("pass") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.Pass &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 1);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActionId == ContentId.Parse("pass") &&
            evidence.BeforeFullIcons == 0 &&
            evidence.BeforeBlinkingIcons == 1 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.Pass &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            effect.SourceActionId == ContentId.Parse("analyze") &&
            effect.EffectIndex == 0 &&
            effect.EffectKind == "analyze");
        Assert.DoesNotContain(summary.ExecutedBattleEffectEvidence, effect =>
            effect.SourceActionId == ContentId.Parse("guard"));
        Assert.DoesNotContain(summary.ExecutedBattleEffectEvidence, effect =>
            effect.SourceActionId == ContentId.Parse("pass"));
        Assert.Equal(57, Resource(summary, "hp").Current);
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.GuardCleared);

        string text = output.ToString();
        Assert.Contains("Battle action executed: Echo Adept used Analyze.", text, StringComparison.Ordinal);
        Assert.Contains("Battle action executed: Echo Adept used Guard.", text, StringComparison.Ordinal);
        Assert.Contains("Battle action executed: Echo Adept used Pass.", text, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_BattleBackSelectionDoesNotExecuteSkillOrConsumeItem()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 1, 2, 4, 4, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.True(summary.PreparedBattleStarted);
        Assert.DoesNotContain(Qualified("frost_tip"), summary.ExecutedBattleActionIds);
        Assert.DoesNotContain(Qualified("echo_strike"), summary.ExecutedBattleActionIds);
        Assert.DoesNotContain(summary.ExecutedBattleEffectEvidence, effect =>
            effect.SourceActionId == Qualified("frost_tip"));
        Assert.DoesNotContain(summary.ExecutedBattleEffectEvidence, effect =>
            effect.SourceActionId == Qualified("echo_strike"));
        Assert.DoesNotContain(summary.ExecutedBattleEffectEvidence, effect =>
            effect.SourceActionId == Qualified("annex_tonic"));
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(2, summary.CancelledBattleCommandSelections);
        Assert.Equal(2, summary.ExecutedBattleActionIds.Count(id => id == ContentId.Parse("pass")));
        Assert.Equal(2, summary.LifecycleEvidence.Count(evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.PassiveTriggered &&
            evidence.RelatedContentId == Qualified("steady_breath")));
        Assert.DoesNotContain(summary.PressTurnEvidence, evidence =>
            evidence.ActionId == Qualified("frost_tip") ||
            evidence.ActionId == Qualified("echo_strike") ||
            evidence.ActionId == Qualified("annex_tonic"));
        Assert.Contains(
            "Battle action executed: Echo Adept used Pass.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_DisplayTextChangesDoNotChangeTypedBattleBehavior()
    {
        var io = new ScriptedGameIO().QueueMenu(
            6, 6, 9, 10,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            13);
        using var output = new StringWriter();
        string root = Path.Combine(FindRepositoryRoot(), "Data", "Jsons");
        var host = new CleanTrainingAnnexPlayHost(
            new DisplayTextMutatingContentPackTextSource(root),
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(BattleEncounterOutcome.Victory, summary.PreparedBattleOutcome);
        Assert.Equal(ContentId.Parse("player_team"), summary.PreparedBattleWinningTeamId);
        Assert.Equal(3, summary.ExecutedBattleActionIds.Count(id => id == Qualified("frost_tip")));
        Assert.Equal(1, summary.ExecutedBattleActionIds.Count(id => id == Qualified("ash_spark")));
        Assert.Equal(3, summary.ExecutedBattleEffectEvidence.Count(effect =>
            IsDamage(effect, Qualified("frost_tip"), DamageElement.Ice)));
        Assert.Equal(1, summary.ExecutedBattleEffectEvidence.Count(effect =>
            IsDamage(effect, Qualified("ash_spark"), DamageElement.Fire)));
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(70, Resource(summary, "hp").Current);
        Assert.Equal(25, Resource(summary, "sp").Current);
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.PassiveTriggered &&
            evidence.RelatedContentId == Qualified("steady_breath"));
        Assert.Contains("Renamed Frost Tip", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ShellFacingContentUsesConcreteTypedEffects()
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();

        var echo = Assert.IsType<DamageEffectDefinition>(
            Assert.Single(catalog.GetRequiredSkill(Qualified("echo_strike")).Effects));
        Assert.Equal(DamageElement.Physical, echo.Element);

        var frost = Assert.IsType<DamageEffectDefinition>(
            Assert.Single(catalog.GetRequiredSkill(Qualified("frost_tip")).Effects));
        Assert.Equal(DamageElement.Ice, frost.Element);

        var ash = Assert.IsType<DamageEffectDefinition>(
            Assert.Single(catalog.GetRequiredSkill(Qualified("ash_spark")).Effects));
        Assert.Equal(DamageElement.Fire, ash.Element);

        var mend = Assert.IsType<RestoreResourceEffectDefinition>(
            Assert.Single(catalog.GetRequiredSkill(Qualified("mend")).Effects));
        Assert.Equal(ContentId.Parse("hp"), mend.ResourceId);

        var item = Assert.IsType<RestoreResourceEffectDefinition>(
            Assert.Single(catalog.GetRequiredItem(Qualified("annex_tonic")).Usage!.Effects));
        Assert.Equal(ContentId.Parse("hp"), item.ResourceId);

        var clear = Assert.IsType<RemoveAilmentEffectDefinition>(
            Assert.Single(catalog.GetRequiredSkill(Qualified("clear_toxin")).Effects));
        Assert.Equal([Qualified("sample_poison")], clear.AilmentIds);

        Assert.IsType<ModifyStatStageEffectDefinition>(
            Assert.Single(catalog.GetRequiredSkill(Qualified("focus_call")).Effects));
        Assert.IsType<ModifyStatStageEffectDefinition>(
            Assert.Single(catalog.GetRequiredSkill(Qualified("soften_guard")).Effects));

        var toxin = Assert.IsType<ApplyAilmentEffectDefinition>(
            Assert.Single(catalog.GetRequiredSkill(Qualified("toxin_touch")).Effects));
        Assert.Equal(Qualified("sample_poison"), toxin.AilmentId);

        var passive = Assert.Single(catalog.GetRequiredSkill(Qualified("steady_breath")).Triggers);
        var passiveRestore = Assert.IsType<RestoreResourceEffectDefinition>(Assert.Single(passive.Effects));
        Assert.Equal(ContentId.Parse("hp"), passiveRestore.ResourceId);

        IEnumerable<EffectDefinition> allShellFacingEffects = catalog.Skills.Values
            .SelectMany(skill => skill.Effects.Concat(skill.Triggers.SelectMany(trigger => trigger.Effects)))
            .Concat(catalog.Items.Values.SelectMany(item => item.Usage?.Effects ?? []));
        Assert.DoesNotContain(allShellFacingEffects, effect => effect is CustomEffectDefinition);
    }

    [Fact]
    public async Task TrainingAnnexExecutionServices_UseOneCatalogBoundProductionCombatRuleset()
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();
        ProductionCombatRuleset ruleset = new RuntimeRulesetBindingResolver()
            .BindProductionCombatRuleset(
                catalog,
                Qualified("standard_damage"),
                new SequenceRandomSource())
            .RequireService();

        BattleExecutionServices services =
            TrainingAnnexHostSupport.CreateExecutionServices(catalog, ruleset);

        Assert.Same(ruleset, services.DamagePolicy);
        Assert.Same(ruleset, services.InstantDeathPolicy);
        Assert.Same(ruleset, services.AilmentPolicy);
        Assert.Same(ruleset, services.ChancePolicy);
        Assert.Same(ruleset, services.PowerAmountPolicy);
        Assert.Equal(1.5m, ruleset.Config.WeakDamageMultiplier);
        Assert.Equal(0.5m, ruleset.Config.ResistDamageMultiplier);
    }

    [Fact]
    public async Task TrainingAnnexBoundRuleset_UsesStrengthForPhysicalAndMagicForMagicalDamage()
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();
        ProductionCombatRuleset ruleset = new RuntimeRulesetBindingResolver()
            .BindProductionCombatRuleset(
                catalog,
                Qualified("standard_damage"),
                new SequenceRandomSource())
            .RequireService();
        EntityDefinition attacker = catalog.GetRequiredEntity(Qualified("echo_adept"));
        EntityDefinition target = catalog.GetRequiredEntity(Qualified("bramble_runner"));

        ProductionDamageResolutionHit physical = Assert.Single(ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                CombatProfile(attacker),
                CombatProfile(target),
                DamageElement.Physical,
                ElementalAffinity.Normal,
                Power: 10,
                Accuracy: 100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))).Hits);
        ProductionDamageResolutionHit magical = Assert.Single(ruleset.ResolveDamage(
            new ProductionDamageResolutionRequest(
                CombatProfile(attacker),
                CombatProfile(target),
                DamageElement.Ice,
                ElementalAffinity.Normal,
                Power: 8,
                Accuracy: 100,
                new NeverCriticalDefinition(),
                new HitCountDefinition(1, 1))).Hits);

        Assert.Equal(18, physical.Damage);
        Assert.Equal(13, magical.Damage);
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_AuthoredAccuracyCanMissThroughBoundCombatRuleset()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 1, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new CombatEffectMutatingContentPackTextSource(
                ContentRoot(),
                "frost_tip",
                accuracy: 5),
            new SequenceRandomSource(0.99m, 0m, 0m));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexCombatResolutionEvidence miss = Assert.Single(
            summary.CombatResolutionEvidence,
            evidence => evidence.SourceActionId == Qualified("frost_tip"));
        Assert.Equal(5, miss.Accuracy);
        Assert.False(miss.Hit);
        Assert.Equal(EffectExecutionOutcome.Failure, miss.Outcome);
        Assert.Equal(PressTurnOutcome.Miss, miss.PressTurnOutcome);
        Assert.Null(miss.Value);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.ActionId == Qualified("frost_tip") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.PressTurnOutcome == PressTurnOutcome.Miss &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.Equal(BattleEncounterOutcome.Cancelled, summary.PreparedBattleOutcome);
        io.AssertConsumed();
    }

    [Theory]
    [InlineData("echo_strike", 1, DamageElement.Physical, true, PressTurnOutcome.Critical)]
    [InlineData("frost_tip", 0, DamageElement.Ice, false, PressTurnOutcome.Weakness)]
    public async Task CleanTrainingAnnexPlay_CriticalPolicyUsesTypedDamageElement(
        string skillId,
        int skillMenuIndex,
        DamageElement element,
        bool expectedCritical,
        PressTurnOutcome expectedOutcome)
    {
        var io = new ScriptedGameIO().QueueMenu(
            6, 6, 9, 10,
            1, skillMenuIndex, 0,
            -1,
            13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new CombatEffectMutatingContentPackTextSource(
                ContentRoot(),
                skillId,
                criticalChance: 100),
            new SequenceRandomSource(0m, 0m, 0m, 0m, 0m));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexCombatResolutionEvidence resolution = Assert.Single(
            summary.CombatResolutionEvidence,
            evidence => evidence.SourceActionId == Qualified(skillId));
        Assert.Equal(element, resolution.DamageElement);
        Assert.Equal(CriticalMode.Chance, resolution.CriticalMode);
        Assert.Equal(expectedCritical, resolution.IsCritical);
        Assert.Equal(expectedOutcome, resolution.PressTurnOutcome);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.ActionId == Qualified(skillId) &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.PressTurnOutcome == expectedOutcome &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 1);
        io.AssertConsumed();
    }

    [Theory]
    [InlineData("null", ElementalAffinity.Null, PressTurnOutcome.Null)]
    [InlineData("repel", ElementalAffinity.Repel, PressTurnOutcome.Repel)]
    [InlineData("absorb", ElementalAffinity.Absorb, PressTurnOutcome.Absorb)]
    public async Task CleanTrainingAnnexPlay_DefensivePressTurnOutcomesTerminatePlayerPhase(
        string affinity,
        ElementalAffinity expectedAffinity,
        PressTurnOutcome expectedOutcome)
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 1, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new EntityAffinityMutatingContentPackTextSource(ContentRoot(), "ashling", "ice", affinity));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexCombatResolutionEvidence resolution = Assert.Single(
            summary.CombatResolutionEvidence,
            evidence => evidence.SourceActionId == Qualified("frost_tip"));
        Assert.Equal(expectedAffinity, resolution.ResolvedAffinity);
        Assert.Equal(expectedOutcome, resolution.PressTurnOutcome);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.ActionId == Qualified("frost_tip") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.PressTurnOutcome == expectedOutcome &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_PoisonAppliesAndTicksThroughFrameworkLifecycle()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 1, 2, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                playerBaseSkillIds:
                [
                    "frost_tip",
                    "echo_strike",
                    "steady_breath",
                    "toxin_touch"
                ]),
            new SequenceRandomSource(
                units: [0m, 0m, 0m, 0m],
                ints: [99]));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Contains(Qualified("toxin_touch"), summary.ExecutedBattleActionIds);
        Assert.Contains(Qualified("ash_spark"), summary.ExecutedBattleActionIds);
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            effect.SourceActionId == Qualified("toxin_touch") &&
            effect.EffectKind == "apply_ailment" &&
            effect.RelatedContentId == Qualified("sample_poison"));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            IsLifecycle(
                evidence,
                ContentId.Parse("review_hall_trigger_ashling_1"),
                BattleStatusLifecycleEventKind.AilmentApplied,
                Qualified("sample_poison"),
                Qualified("toxin_touch")));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("review_hall_trigger_ashling_1") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.ResourceChanged &&
            evidence.RelatedContentId == ContentId.Parse("hp") &&
            evidence.Value == -2m);
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.PassiveTriggered &&
            evidence.RelatedContentId == Qualified("steady_breath") &&
            evidence.Detail == "owner_turn_end");
        Assert.Contains(
            "Lifecycle resource changed: hp -2.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_StunSkipsEnemyTurnAndExpiresThroughFrameworkLifecycle()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 1, 2, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                playerBaseSkillIds:
                [
                    "frost_tip",
                    "echo_strike",
                    "steady_breath",
                    "toxin_touch"
                ],
                toxinAilmentId: "sample_stun"),
            new SequenceRandomSource(units: [0m], ints: [99]));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            IsLifecycle(
                evidence,
                ContentId.Parse("review_hall_trigger_ashling_1"),
                BattleStatusLifecycleEventKind.AilmentApplied,
                Qualified("sample_stun"),
                Qualified("toxin_touch")));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("review_hall_trigger_ashling_1") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.TurnRestricted &&
            evidence.RelatedContentId == Qualified("sample_stun") &&
            evidence.TurnStartOutcome == BattleTurnStartOutcome.Skip);
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("review_hall_trigger_ashling_1") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.AilmentRemoved &&
            evidence.RelatedContentId == Qualified("sample_stun"));
        Assert.DoesNotContain(summary.AiDecisionEvidence, evidence =>
            evidence.ActorInstanceId == ContentId.Parse("review_hall_trigger_ashling_1"));
        Assert.DoesNotContain(Qualified("ash_spark"), summary.ExecutedBattleActionIds);
        Assert.Contains("Ashling turn restriction: Skip.", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ClearToxinRemovesPoisonBeforeTurnEndTick()
    {
        var io = new ScriptedGameIO().QueueMenu(
            6, 6, 9, 10,
            4, 4,
            1, 2, 0,
            -1,
            13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                playerBaseSkillIds:
                [
                    "frost_tip",
                    "echo_strike",
                    "steady_breath",
                    "clear_toxin"
                ],
                ashlingBaseSkillIds: ["toxin_touch"]),
            new SequenceRandomSource(units: [0m, 0m], ints: [99]));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            IsLifecycle(
                evidence,
                ContentId.Parse("echo_adept"),
                BattleStatusLifecycleEventKind.AilmentApplied,
                Qualified("sample_poison"),
                Qualified("toxin_touch")));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            IsLifecycle(
                evidence,
                ContentId.Parse("echo_adept"),
                BattleStatusLifecycleEventKind.AilmentRemoved,
                Qualified("sample_poison"),
                Qualified("clear_toxin")));
        Assert.DoesNotContain(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.ResourceChanged &&
            evidence.RelatedContentId == ContentId.Parse("hp") &&
            evidence.Value < 0);
        Assert.Equal(Resource(summary, "hp").Maximum, Resource(summary, "hp").Current);
        Assert.Contains(Qualified("clear_toxin"), summary.ExecutedBattleActionIds);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_BattleStartPassiveDispatchesOnceThroughFrameworkLifecycle()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                steadyBreathEventId: "battle_start"));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexLifecycleEvidence activation = Assert.Single(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == ContentId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.PassiveTriggered &&
            evidence.RelatedContentId == Qualified("steady_breath"));
        Assert.Equal("battle_start", activation.Detail);
        Assert.DoesNotContain(summary.ExecutedBattleActionIds, id => id == ContentId.Parse("pass"));
        Assert.Contains(
            $"Lifecycle passive triggered: {Qualified("steady_breath")}.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_PassiveRuleModifierUsesTypedElementNotDisplayText()
    {
        var io = new ScriptedGameIO().QueueMenu(1, 6, 6, 9, 10, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new TrainingAnnexLifecycleContentPackTextSource(
                ContentRoot(),
                steadyBreathDisplayName: "Unrelated Label",
                steadyBreathPhysicalDamageMultiplier: 2m));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexCombatResolutionEvidence attack = Assert.Single(
            summary.CombatResolutionEvidence,
            evidence => evidence.SourceActionId == Qualified("practice_blade"));
        Assert.Equal(DamageElement.Physical, attack.DamageElement);
        Assert.Equal(46m, attack.Value);
        Assert.Contains(Qualified("practice_blade"), summary.ExecutedBattleActionIds);
        Assert.Contains("Passive skills: Unrelated Label.", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Theory]
    [InlineData("standard_damage", "standard_reward", "damage")]
    [InlineData("standard_reward", "standard_damage", "reward")]
    [InlineData("standard_press_turn", "standard_damage", "press_turn")]
    public async Task CleanTrainingAnnexPlay_InvalidCombatBindingFailsWithoutFallback(
        string rulesetId,
        string policyId,
        string diagnosticCategory)
    {
        var io = new ScriptedGameIO();
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new RulesetPolicyMutatingContentPackTextSource(ContentRoot(), rulesetId, policyId));

        int exitCode = await host.RunAsync();

        Assert.Equal(4, exitCode);
        Assert.Null(host.LastSummary);
        Assert.Empty(io.Menus);
        Assert.Contains($"[{diagnosticCategory}:UnsupportedPolicy]", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, RulesetBindingDiagnosticCode.MissingRuleset)]
    [InlineData(false, RulesetBindingDiagnosticCode.CategoryMismatch)]
    public async Task CleanTrainingAnnexPlay_InvalidPressTurnBindingFailsBeforeSession(
        bool removeRuleset,
        RulesetBindingDiagnosticCode expectedCode)
    {
        var io = new ScriptedGameIO();
        using var output = new StringWriter();
        IContentPackTextSource source = removeRuleset
            ? new RulesetRemovingContentPackTextSource(ContentRoot(), "standard_press_turn")
            : new RulesetCategoryMutatingContentPackTextSource(ContentRoot(), "standard_press_turn", "damage");
        var host = CreateHost(io, output, source);

        int exitCode = await host.RunAsync();

        Assert.Equal(4, exitCode);
        Assert.Null(host.LastSummary);
        Assert.Empty(io.Menus);
        Assert.Contains($"[press_turn:{expectedCode}]", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CleanTrainingAnnexShell_DoesNotReferenceLegacyEffectInputs()
    {
        string root = Path.Combine(FindRepositoryRoot(), "Host", "CleanConsole", "TrainingAnnex");
        string[] banned =
        [
            "SkillData",
            "ItemData",
            "JRPGPrototype.Data.Database",
            "Database.",
            "Database[",
            "Database.LoadData",
            "ActionProcessor",
            "DemoDamageExecutionPolicy",
            "DemoInstantDeathPolicy",
            "DemoAilmentPolicy",
            "DemoChancePolicy",
            "DemoPowerAmountPolicy",
            "EffectText",
            "effect string"
        ];

        foreach (string file in Directory.EnumerateFiles(root, "*.cs"))
        {
            string text = File.ReadAllText(file);
            foreach (string term in banned)
            {
                Assert.DoesNotContain(term, text, StringComparison.Ordinal);
            }
        }
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
    public async Task CleanTrainingAnnexPlay_ManualSaveLoadRestoresProgressionInventoryWalletFieldAndKnowledge()
    {
        var io = new ScriptedGameIO().QueueMenu(10, 0, 4, 10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(1, summary.ManualSaveCount);
        Assert.Equal(1, summary.ManualLoadCount);
        Assert.Equal(0, summary.SuspendSaveCount);
        Assert.Equal(0, summary.SuspendLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.False(summary.HasSuspendSave);
        Assert.Equal(0, summary.SaveDiagnosticCount);
        Assert.Equal(3, summary.PlayerProgression.Level);
        Assert.Equal(0, summary.PlayerProgression.Experience);
        Assert.Equal(0, summary.PlayerProgression.LifetimeExperience);
        Assert.Equal(2, summary.PlayerProgression.UnspentStatPoints);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(0, summary.Wallet.Macca);
        Assert.Equal(Qualified("staging_area"), summary.FinalLocationId);
        Assert.Empty(summary.BattleKnowledge.ElementalAffinities);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.OpenSaveLoad,
                CleanTrainingAnnexPlayCommand.ManualSave,
                CleanTrainingAnnexPlayCommand.ApplyVictoryExperience,
                CleanTrainingAnnexPlayCommand.OpenSaveLoad,
                CleanTrainingAnnexPlayCommand.ManualLoad,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);

        string text = output.ToString();
        Assert.Contains("Manual save created in field_menu (sequence 0).", text, StringComparison.Ordinal);
        Assert.Contains("Manual save restored from field_menu (sequence 0).", text, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_SuspendLoadConsumesSlotOnlyAfterSuccessfulRestore()
    {
        var io = new ScriptedGameIO().QueueMenu(10, 2, 10, 3, 10, 4, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.ManualSaveCount);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.Equal(1, summary.SuspendSaveCount);
        Assert.Equal(1, summary.SuspendLoadCount);
        Assert.True(summary.SuspendSaveConsumed);
        Assert.False(summary.HasSuspendSave);
        Assert.Equal(0, summary.SaveDiagnosticCount);

        GameIoMenuCall finalSaveMenu = Assert.Single(io.Menus.Where(menu =>
            menu.Header == "Clean Save / Load").Skip(2));
        Assert.True(finalSaveMenu.DisabledOptions[3]);
        Assert.Contains(
            "Suspend save consumed after successful restore.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_SaveWhilePreparedEncounterPendingIsRejectedWithoutSlotMutation()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 14, 0, 13);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.True(summary.EncounterTriggerConsumed);
        Assert.False(summary.PreparedBattleStarted);
        Assert.Equal(0, summary.ManualSaveCount);
        Assert.False(summary.HasManualSave);
        Assert.Equal(1, summary.SaveDiagnosticCount);
        Assert.Contains(
            "Manual save rejected [PendingHostAction]",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_MalformedManualSaveJsonDoesNotMutateCurrentSession()
    {
        var slots = new TrainingAnnexSaveSlotStore();
        slots.SetRaw(RuntimeSaveKind.Manual, "{");
        var io = new ScriptedGameIO().QueueMenu(10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, saveSlots: slots);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.Equal(1, summary.SaveDiagnosticCount);
        Assert.Equal(3, summary.PlayerProgression.Level);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Contains(
            "Manual load rejected: save JSON could not be read",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_PostVictoryRewardStateSurvivesManualSaveLoad()
    {
        var io = new ScriptedGameIO().QueueMenu(
            6, 6, 9, 10,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            1, 0, 0,
            14, 0,
            11, 0, 0,
            14, 1,
            13);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(1, summary.ManualSaveCount);
        Assert.Equal(1, summary.ManualLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.Equal(BattleEncounterOutcome.Victory, summary.PreparedBattleOutcome);
        Assert.Equal(1, summary.PlayerProgression.Experience);
        Assert.Equal(1, summary.PlayerProgression.LifetimeExperience);
        Assert.Equal(14, summary.Wallet.Macca);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(70, Resource(summary, "hp").Current);
        Assert.Equal(1, summary.SessionProgress.Counters[ContentId.Parse("training_annex_victories")]);
        Assert.Contains(ContentId.Parse("ashling_drill_cleared"), summary.SessionProgress.Flags);
        Assert.Contains(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("ashling") &&
            knowledge.Element == DamageElement.Ice &&
            knowledge.Affinity == ElementalAffinity.Weak);
        Assert.Contains(
            "Manual save restored from dungeon_menu",
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

    private static RuntimeResourceSnapshot Resource(CleanTrainingAnnexPlaySummary summary, string resourceId) =>
        Assert.Single(summary.PlayerResources, resource => resource.ResourceId == ContentId.Parse(resourceId));

    private static ProductionCombatantProfile CombatProfile(EntityDefinition entity) =>
        new(
            entity.BaseLevel,
            new ProductionCombatStats(
                entity.Stats.GetValueOrDefault(ContentId.Parse("strength")),
                entity.Stats.GetValueOrDefault(ContentId.Parse("magic")),
                entity.Stats.GetValueOrDefault(ContentId.Parse("vitality")),
                entity.Stats.GetValueOrDefault(ContentId.Parse("agility")),
                entity.Stats.GetValueOrDefault(ContentId.Parse("luck"))));

    private static bool IsDamage(
        TrainingAnnexTypedEffectEvidence effect,
        ContentId sourceActionId,
        DamageElement element) =>
        effect.SourceActionId == sourceActionId &&
        effect.EffectIndex == 0 &&
        effect.EffectKind == "damage" &&
        effect.DamageElement == element;

    private static bool IsResourceEffect(
        TrainingAnnexTypedEffectEvidence effect,
        ContentId sourceActionId,
        string effectKind,
        string resourceId) =>
        effect.SourceActionId == sourceActionId &&
        effect.EffectIndex == 0 &&
        effect.EffectKind == effectKind &&
        effect.ResourceId == ContentId.Parse(resourceId);

    private static bool IsElementalKnowledge(
        TrainingAnnexBattleKnowledgeEvidence evidence,
        ContentId sourceActionId,
        ContentId targetEntityId,
        DamageElement element,
        ElementalAffinity affinity) =>
        evidence.SourceActionId == sourceActionId &&
        evidence.TargetEntityId == targetEntityId &&
        evidence.Channel == TrainingAnnexBattleKnowledgeChannel.ElementalAffinity &&
        evidence.Element == element &&
        evidence.Affinity == affinity;

    private static bool IsAilmentKnowledge(
        TrainingAnnexBattleKnowledgeEvidence evidence,
        ContentId sourceActionId,
        ContentId targetEntityId,
        ContentId ailmentId,
        ResistanceLevel resistance) =>
        evidence.SourceActionId == sourceActionId &&
        evidence.TargetEntityId == targetEntityId &&
        evidence.Channel == TrainingAnnexBattleKnowledgeChannel.AilmentResistance &&
        evidence.AilmentId == ailmentId &&
        evidence.Resistance == resistance;

    private static bool IsInstantDeathKnowledge(
        TrainingAnnexBattleKnowledgeEvidence evidence,
        ContentId sourceActionId,
        ContentId targetEntityId,
        InstantDeathChannel channel,
        ResistanceLevel resistance) =>
        evidence.SourceActionId == sourceActionId &&
        evidence.TargetEntityId == targetEntityId &&
        evidence.Channel == TrainingAnnexBattleKnowledgeChannel.InstantDeathResistance &&
        evidence.InstantDeathChannel == channel &&
        evidence.Resistance == resistance;

    private static bool IsLifecycle(
        TrainingAnnexLifecycleEvidence evidence,
        ContentId actorId,
        BattleStatusLifecycleEventKind kind,
        ContentId relatedId,
        ContentId sourceActionId) =>
        evidence.ActorId == actorId &&
        evidence.EventKind == kind &&
        evidence.RelatedContentId == relatedId &&
        evidence.SourceActionId == sourceActionId;

    private static async Task<GameDataCatalog> LoadTrainingAnnexCatalogAsync()
    {
        string root = Path.Combine(FindRepositoryRoot(), "Data", "Jsons");
        var source = new RecordingContentPackTextSource(root);
        ContentPackTextBundle bundle = await source.ReadAsync(TrainingAnnexHostSupport.CreateContentRequest());
        CatalogLoadResult load = new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(TrainingAnnexHostSupport.BuildRegistrations(), [bundle]));
        return load.RequireCatalog();
    }

    private static CleanTrainingAnnexPlayHost CreateHost(
        ScriptedGameIO io,
        StringWriter output,
        IContentPackTextSource? source = null,
        IRandomSource? randomSource = null,
        TrainingAnnexSaveSlotStore? saveSlots = null) =>
        new(
            source ?? new RecordingContentPackTextSource(ContentRoot()),
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io),
            randomSource,
            saveSlots);

    private static string ContentRoot() => Path.Combine(FindRepositoryRoot(), "Data", "Jsons");

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

    private sealed class DisplayTextMutatingContentPackTextSource(string root) : IContentPackTextSource
    {
        public async ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            string manifest = await File.ReadAllTextAsync(Path.Combine(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                string text = await File.ReadAllTextAsync(Path.Combine(root, path), cancellationToken);
                documents.Add(new ContentDocumentText(path, path, MutateDisplayText(text)));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }

        private static string MutateDisplayText(string json)
        {
            JsonNode node = JsonNode.Parse(json) ??
                throw new InvalidOperationException("Training Annex JSON could not be parsed.");
            Mutate(node);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static void Mutate(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                foreach (string key in obj.Select(pair => pair.Key).ToArray())
                {
                    if (key == "displayName" && obj[key]?.GetValue<string>() is string displayName)
                    {
                        obj[key] = $"Renamed {displayName}";
                    }
                    else if (key == "description" && obj[key]?.GetValue<string>() is not null)
                    {
                        obj[key] = "Renamed description.";
                    }
                    else if (obj[key] is JsonNode child)
                    {
                        Mutate(child);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (JsonNode? child in array)
                {
                    if (child is not null)
                    {
                        Mutate(child);
                    }
                }
            }
        }
    }

    private sealed class CombatEffectMutatingContentPackTextSource(
        string root,
        string skillId,
        int? accuracy = null,
        int? criticalChance = null) : IContentPackTextSource
    {
        public async ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            string manifest = await File.ReadAllTextAsync(Path.Combine(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                string text = await File.ReadAllTextAsync(Path.Combine(root, path), cancellationToken);
                if (path.EndsWith(".skills.json", StringComparison.Ordinal))
                {
                    text = MutateSkill(text);
                }

                documents.Add(new ContentDocumentText(path, path, text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }

        private string MutateSkill(string json)
        {
            JsonObject rootNode = JsonNode.Parse(json)?.AsObject() ??
                throw new InvalidOperationException("Training Annex skills JSON could not be parsed.");
            JsonArray skills = rootNode["skills"]?.AsArray() ??
                throw new InvalidOperationException("Training Annex skills document has no skills array.");
            JsonObject skill = skills
                .Select(node => node?.AsObject())
                .Single(node => node?["id"]?.GetValue<string>() == skillId) ??
                throw new InvalidOperationException($"Training Annex skill '{skillId}' was not found.");
            JsonObject effect = skill["effects"]?.AsArray()[0]?.AsObject() ??
                throw new InvalidOperationException($"Training Annex skill '{skillId}' has no first effect.");
            if (accuracy is int authoredAccuracy)
            {
                effect["accuracy"] = authoredAccuracy;
            }
            if (criticalChance is int authoredCriticalChance)
            {
                effect["critical"] = new JsonObject
                {
                    ["mode"] = "chance",
                    ["chance"] = authoredCriticalChance
                };
            }

            return rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private sealed class EntityAffinityMutatingContentPackTextSource(
        string root,
        string entityId,
        string elementId,
        string affinity) : IContentPackTextSource
    {
        public async ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            string manifest = await File.ReadAllTextAsync(Path.Combine(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                string text = await File.ReadAllTextAsync(Path.Combine(root, path), cancellationToken);
                if (path.EndsWith(".entities.json", StringComparison.Ordinal))
                {
                    JsonObject rootNode = JsonNode.Parse(text)?.AsObject() ??
                        throw new InvalidOperationException("Training Annex entities JSON could not be parsed.");
                    JsonObject entity = rootNode["entities"]?.AsArray()
                        .Select(node => node?.AsObject())
                        .Single(node => node?["id"]?.GetValue<string>() == entityId) ??
                        throw new InvalidOperationException($"Training Annex entity '{entityId}' was not found.");
                    JsonObject affinities = entity["elementalAffinities"]?.AsObject() ??
                        throw new InvalidOperationException($"Training Annex entity '{entityId}' has no affinity map.");
                    affinities[elementId] = affinity;
                    text = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }

                documents.Add(new ContentDocumentText(path, path, text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }
    }

    private sealed class TrainingAnnexLifecycleContentPackTextSource(
        string root,
        IReadOnlyList<string>? playerBaseSkillIds = null,
        IReadOnlyList<string>? ashlingBaseSkillIds = null,
        string? toxinAilmentId = null,
        string? steadyBreathEventId = null,
        string? steadyBreathDisplayName = null,
        decimal? steadyBreathPhysicalDamageMultiplier = null,
        string? unaffordableSkillId = null,
        string? affinityEntityId = null,
        string? affinityElementId = null,
        string? affinity = null) : IContentPackTextSource
    {
        public async ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            string manifest = await File.ReadAllTextAsync(Path.Combine(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                string text = await File.ReadAllTextAsync(Path.Combine(root, path), cancellationToken);
                if (path.EndsWith(".entities.json", StringComparison.Ordinal))
                {
                    text = MutateEntities(text);
                }
                else if (path.EndsWith(".skills.json", StringComparison.Ordinal))
                {
                    if (toxinAilmentId is not null)
                    {
                        text = MutateToxinTouch(text);
                    }
                    if (steadyBreathEventId is not null ||
                        steadyBreathDisplayName is not null ||
                        steadyBreathPhysicalDamageMultiplier is not null)
                    {
                        text = MutateSteadyBreath(text);
                    }
                    if (unaffordableSkillId is not null)
                    {
                        text = MakeSkillUnaffordable(text, unaffordableSkillId);
                    }
                }

                documents.Add(new ContentDocumentText(path, path, text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }

        private string MutateEntities(string json)
        {
            JsonObject rootNode = JsonNode.Parse(json)?.AsObject() ??
                throw new InvalidOperationException("Training Annex entities JSON could not be parsed.");
            JsonArray entities = rootNode["entities"]?.AsArray() ??
                throw new InvalidOperationException("Training Annex entities document has no entities array.");
            if (playerBaseSkillIds is not null)
            {
                ReplaceBaseSkills(entities, "echo_adept", playerBaseSkillIds);
            }
            if (ashlingBaseSkillIds is not null)
            {
                ReplaceBaseSkills(entities, "ashling", ashlingBaseSkillIds);
            }
            if (affinityEntityId is not null &&
                affinityElementId is not null &&
                affinity is not null)
            {
                ReplaceElementalAffinity(entities, affinityEntityId, affinityElementId, affinity);
            }

            return rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private string MutateToxinTouch(string json)
        {
            JsonObject rootNode = JsonNode.Parse(json)?.AsObject() ??
                throw new InvalidOperationException("Training Annex skills JSON could not be parsed.");
            JsonObject skill = rootNode["skills"]?.AsArray()
                .Select(node => node?.AsObject())
                .Single(node => node?["id"]?.GetValue<string>() == "toxin_touch") ??
                throw new InvalidOperationException("Training Annex skill 'toxin_touch' was not found.");
            JsonObject effect = skill["effects"]?.AsArray()[0]?.AsObject() ??
                throw new InvalidOperationException("Training Annex skill 'toxin_touch' has no first effect.");
            effect["ailmentId"] = toxinAilmentId;
            return rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private string MutateSteadyBreath(string json)
        {
            JsonObject rootNode = JsonNode.Parse(json)?.AsObject() ??
                throw new InvalidOperationException("Training Annex skills JSON could not be parsed.");
            JsonObject skill = rootNode["skills"]?.AsArray()
                .Select(node => node?.AsObject())
                .Single(node => node?["id"]?.GetValue<string>() == "steady_breath") ??
                throw new InvalidOperationException("Training Annex skill 'steady_breath' was not found.");
            if (steadyBreathEventId is not null)
            {
                JsonObject trigger = skill["triggers"]?.AsArray()[0]?.AsObject() ??
                    throw new InvalidOperationException("Training Annex skill 'steady_breath' has no first trigger.");
                trigger["event"] = steadyBreathEventId;
            }
            if (steadyBreathDisplayName is not null)
            {
                skill["displayName"] = steadyBreathDisplayName;
                skill["description"] = "Text deliberately unrelated to its typed modifier.";
            }
            if (steadyBreathPhysicalDamageMultiplier is decimal multiplier)
            {
                skill["modifiers"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "damage_dealt",
                        ["operation"] = "multiply",
                        ["value"] = multiplier,
                        ["when"] = new JsonObject
                        {
                            ["type"] = "effect_element_is",
                            ["elementId"] = "physical"
                        }
                    });
            }

            return rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static string MakeSkillUnaffordable(string json, string skillId)
        {
            JsonObject rootNode = JsonNode.Parse(json)?.AsObject() ??
                throw new InvalidOperationException("Training Annex skills JSON could not be parsed.");
            JsonObject skill = rootNode["skills"]?.AsArray()
                .Select(node => node?.AsObject())
                .Single(node => node?["id"]?.GetValue<string>() == skillId) ??
                throw new InvalidOperationException($"Training Annex skill '{skillId}' was not found.");
            JsonObject amount = skill["costs"]?.AsArray()[0]?["amount"]?.AsObject() ??
                throw new InvalidOperationException($"Training Annex skill '{skillId}' has no first cost amount.");
            amount["value"] = 999;
            return rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static void ReplaceBaseSkills(
            JsonArray entities,
            string entityId,
            IReadOnlyList<string> baseSkillIds)
        {
            JsonObject entity = entities
                .Select(node => node?.AsObject())
                .Single(node => node?["id"]?.GetValue<string>() == entityId) ??
                throw new InvalidOperationException($"Training Annex entity '{entityId}' was not found.");
            entity["baseSkillIds"] = new JsonArray(baseSkillIds.Select(skillId => JsonValue.Create(skillId)).ToArray());
            if (entity["skillUnlocks"] is JsonArray unlocks)
            {
                var promoted = new HashSet<string>(baseSkillIds, StringComparer.Ordinal);
                for (int index = unlocks.Count - 1; index >= 0; index--)
                {
                    if (unlocks[index]?.AsObject()["skillId"]?.GetValue<string>() is string skillId &&
                        promoted.Contains(skillId))
                    {
                        unlocks.RemoveAt(index);
                    }
                }
            }
        }

        private static void ReplaceElementalAffinity(
            JsonArray entities,
            string entityId,
            string elementId,
            string affinity)
        {
            JsonObject entity = entities
                .Select(node => node?.AsObject())
                .Single(node => node?["id"]?.GetValue<string>() == entityId) ??
                throw new InvalidOperationException($"Training Annex entity '{entityId}' was not found.");
            JsonObject affinities = entity["elementalAffinities"]?.AsObject() ??
                throw new InvalidOperationException($"Training Annex entity '{entityId}' has no elemental affinities.");
            affinities[elementId] = affinity;
        }
    }

    private sealed class RulesetPolicyMutatingContentPackTextSource(
        string root,
        string rulesetId,
        string policyId) : IContentPackTextSource
    {
        public async ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            string manifest = await File.ReadAllTextAsync(Path.Combine(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                string text = await File.ReadAllTextAsync(Path.Combine(root, path), cancellationToken);
                if (path.EndsWith(".rulesets.json", StringComparison.Ordinal))
                {
                    JsonObject rootNode = JsonNode.Parse(text)?.AsObject() ??
                        throw new InvalidOperationException("Training Annex rulesets JSON could not be parsed.");
                    JsonObject ruleset = rootNode["rulesets"]?.AsArray()
                        .Select(node => node?.AsObject())
                        .Single(node => node?["id"]?.GetValue<string>() == rulesetId) ??
                        throw new InvalidOperationException($"Training Annex ruleset '{rulesetId}' was not found.");
                    ruleset["policyId"] = policyId;
                    text = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }

                documents.Add(new ContentDocumentText(path, path, text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }
    }

    private sealed class RulesetCategoryMutatingContentPackTextSource(
        string root,
        string rulesetId,
        string category) : IContentPackTextSource
    {
        public async ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            string manifest = await File.ReadAllTextAsync(Path.Combine(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                string text = await File.ReadAllTextAsync(Path.Combine(root, path), cancellationToken);
                if (path.EndsWith(".rulesets.json", StringComparison.Ordinal))
                {
                    JsonObject rootNode = JsonNode.Parse(text)?.AsObject() ??
                        throw new InvalidOperationException("Training Annex rulesets JSON could not be parsed.");
                    JsonObject ruleset = rootNode["rulesets"]?.AsArray()
                        .Select(node => node?.AsObject())
                        .Single(node => node?["id"]?.GetValue<string>() == rulesetId) ??
                        throw new InvalidOperationException($"Training Annex ruleset '{rulesetId}' was not found.");
                    ruleset["category"] = category;
                    text = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }

                documents.Add(new ContentDocumentText(path, path, text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }
    }

    private sealed class RulesetRemovingContentPackTextSource(
        string root,
        string rulesetId) : IContentPackTextSource
    {
        public async ValueTask<ContentPackTextBundle> ReadAsync(
            ContentPackTextRequest request,
            CancellationToken cancellationToken = default)
        {
            string manifest = await File.ReadAllTextAsync(Path.Combine(root, request.ManifestPath), cancellationToken);
            var documents = new List<ContentDocumentText>();
            foreach (string path in request.DocumentPaths)
            {
                string text = await File.ReadAllTextAsync(Path.Combine(root, path), cancellationToken);
                if (path.EndsWith(".rulesets.json", StringComparison.Ordinal))
                {
                    JsonObject rootNode = JsonNode.Parse(text)?.AsObject() ??
                        throw new InvalidOperationException("Training Annex rulesets JSON could not be parsed.");
                    JsonArray rulesets = rootNode["rulesets"]?.AsArray() ??
                        throw new InvalidOperationException("Training Annex rulesets document has no rulesets array.");
                    JsonNode? ruleset = rulesets.SingleOrDefault(node =>
                        node?["id"]?.GetValue<string>() == rulesetId);
                    if (ruleset is not null)
                    {
                        rulesets.Remove(ruleset);
                    }

                    text = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }

                documents.Add(new ContentDocumentText(path, path, text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }
    }

    private sealed class SequenceRandomSource : IRandomSource
    {
        private readonly Queue<decimal> _units;
        private readonly Queue<int> _ints;

        public SequenceRandomSource(params decimal[] values)
            : this(values, [])
        {
        }

        public SequenceRandomSource(IEnumerable<decimal> units, IEnumerable<int> ints)
        {
            _units = new Queue<decimal>(units);
            _ints = new Queue<int>(ints);
        }

        public int NextInt32(int minimumInclusive, int maximumExclusive)
        {
            int value = _ints.Count == 0 ? minimumInclusive : _ints.Dequeue();
            return Math.Clamp(value, minimumInclusive, maximumExclusive - 1);
        }

        public decimal NextUnitDecimal() => _units.Count == 0 ? 0m : _units.Dequeue();
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
