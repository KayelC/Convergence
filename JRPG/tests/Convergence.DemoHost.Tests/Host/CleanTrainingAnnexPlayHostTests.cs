using Convergence.DemoHost.Tests.TestSupport;
using Convergence.Content;
using Convergence.Catalog;
using Convergence.DemoHost;
using Convergence.DemoHost.TrainingAnnex;
using Convergence.Hosting;
using Convergence.Battle;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Execution;
using Convergence.Encounters;
using Convergence.Fusion;
using Convergence.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Convergence.DemoHost.Tests.Host;

public sealed class CleanTrainingAnnexPlayHostTests
{
    [Fact]
    public async Task CleanTrainingAnnexPlay_LoadsCleanContentHydratesActorValidatesSnapshotAndExits()
    {
        var io = new ScriptedGameIO().QueueMenu(0, 1, 2, 3, 4, 6, 0, 5, 7, 0, 9);
        using var output = new StringWriter();
        var source = new RecordingContentPackTextSource(ContentRoot());
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
        Assert.Equal(4, summary.PlayerLevel);
        Assert.Equal(10, summary.ActorCount);
        Assert.Equal(3, summary.EnemyActorCount);
        Assert.Equal(
            [
                Qualified("echo_adept"),
                Qualified("annex_mentor"),
                Qualified("annex_mentor"),
                Qualified("bramble_runner"),
                Qualified("ashling"),
                Qualified("ward_shell"),
                Qualified("bramble_runner"),
                Qualified("ashling"),
                Qualified("bramble_runner"),
                Qualified("ward_shell")
            ],
            summary.ActorEntityIds);
        Assert.Equal(
            [
                RuntimeInstanceId.Parse("echo_adept"),
                RuntimeInstanceId.Parse("support_annex_mentor"),
                RuntimeInstanceId.Parse("form_annex_mentor"),
                RuntimeInstanceId.Parse("persona_bramble_runner"),
                RuntimeInstanceId.Parse("demon_ashling"),
                RuntimeInstanceId.Parse("demon_ward_shell"),
                RuntimeInstanceId.Parse("replacement_bramble_runner"),
                RuntimeInstanceId.Parse("enemy_ashling"),
                RuntimeInstanceId.Parse("enemy_bramble_runner"),
                RuntimeInstanceId.Parse("enemy_ward_shell")
            ],
            summary.ActorInstanceIds);
        Assert.Equal(
            [RuntimeInstanceId.Parse("echo_adept")],
            summary.PartyStock.ActiveParty.Select(actor => actor.InstanceId));
        Assert.Equal(
            [RuntimeInstanceId.Parse("support_annex_mentor")],
            summary.PartyStock.ReserveMembers.Select(actor => actor.InstanceId));
        Assert.NotNull(summary.PartyStock.ActiveForm);
        RuntimeActorReferenceSnapshot activeForm = summary.PartyStock.ActiveForm!;
        Assert.Equal(RuntimeInstanceId.Parse("form_annex_mentor"), activeForm.InstanceId);
        Assert.Equal(
            [RuntimeInstanceId.Parse("persona_bramble_runner")],
            summary.PartyStock.PersonaStock.Select(actor => actor.InstanceId));
        Assert.Equal(
            [RuntimeInstanceId.Parse("demon_ashling"), RuntimeInstanceId.Parse("demon_ward_shell")],
            summary.PartyStock.DemonStock.Select(actor => actor.InstanceId));
        Assert.Empty(summary.PartyTransitions);
        Assert.Empty(summary.FusionResults);
        Assert.Empty(summary.FusionPlanning);
        Assert.Empty(summary.FusionPreviews);
        Assert.Empty(summary.FusionTransactions);
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
        Assert.Null(summary.AppliedWalletTransaction);
        Assert.Empty(summary.ShopTransactions);
        Assert.Empty(summary.ShopEquipmentChanges);
        Assert.Empty(summary.HospitalRestorations);
        Assert.Equal(0, summary.Wallet.Balance);
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
        Assert.Contains(Qualified("practice_blade"), summary.Inventory.GetEquipmentIds(EquipmentSlot.Weapon));
        Assert.Contains(Qualified("focus_charm"), summary.Inventory.GetEquipmentIds(EquipmentSlot.Accessory));
        Assert.Equal(Qualified("practice_blade"), summary.Equipment.EquippedItemIds[EquipmentSlot.Weapon]);
        Assert.Equal(Qualified("focus_charm"), summary.Equipment.EquippedItemIds[EquipmentSlot.Accessory]);
        Assert.Equal(Qualified("practice_blade"), summary.EquipmentProfile.BasicAttack?.EquipmentId);
        Assert.Equal(1, summary.EquipmentProfile.StatModifiers[ContentId.Parse("magic")]);
        Assert.Empty(summary.EquipmentProfile.Diagnostics);
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
                "Save / Load",
                "Training Supply",
                "Recovery Facility",
                "Inspect Party",
                "Inspect Stock",
                "Party / Stock Operations",
                "Negotiate / Recruit",
                "Calculate Fusion Results",
                "Preview Fusion Result",
                "Commit Fusion Transaction",
                "Compendium"
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
                "Save / Load",
                "Training Supply",
                "Recovery Facility",
                "Inspect Party",
                "Inspect Stock",
                "Party / Stock Operations",
                "Negotiate / Recruit",
                "Calculate Fusion Results",
                "Preview Fusion Result",
                "Commit Fusion Transaction",
                "Compendium"
            ],
                menu.Options);
        }
        io.AssertConsumed();

        string text = output.ToString();
        Assert.Contains("Clean Training Annex session booted.", text, StringComparison.Ordinal);
        Assert.Contains("through the clean catalog pipeline", text, StringComparison.Ordinal);
        Assert.Contains("Hydrated Echo Adept at level 3.", text, StringComparison.Ordinal);
        Assert.Contains("Hydrated clean actor roster with 10 actor(s): 3 enemy model(s).", text, StringComparison.Ordinal);
        Assert.Contains("Party setup: 1 active, 1 reserve.", text, StringComparison.Ordinal);
        Assert.Contains("Stock setup: active form 1, Persona stock 1, Demon stock 2.", text, StringComparison.Ordinal);
        Assert.Contains("Field location: Staging Area.", text, StringComparison.Ordinal);
        Assert.Contains("Session: convergence.training_annex_slice; 5 entities, 10 skills, 5 items, 3 encounters, 1 dungeons. Location: Staging Area (convergence.training_annex_slice:staging_area); dungeon state: not active.", text, StringComparison.Ordinal);
        Assert.Contains("Field navigation: entered Training Annex; location Training Annex Entrance (convergence.training_annex_slice:training_annex_entrance).", text, StringComparison.Ordinal);
        Assert.Contains("Session: convergence.training_annex_slice; 5 entities, 10 skills, 5 items, 3 encounters, 1 dungeons. Location: Training Annex Entrance (convergence.training_annex_slice:training_annex_entrance); dungeon state: convergence.training_annex_slice:training_annex_entrance.", text, StringComparison.Ordinal);
        Assert.Contains("Field navigation: returned to Staging Area; location Staging Area (convergence.training_annex_slice:staging_area).", text, StringComparison.Ordinal);
        Assert.Contains("Actor roster: 10 actor(s).", text, StringComparison.Ordinal);
        Assert.Contains("Player: Echo Adept; instance echo_adept; level 3; resources: hp 80/80, sp 28/28.", text, StringComparison.Ordinal);
        Assert.Contains("Reserve: Annex Mentor; instance support_annex_mentor; level 5;", text, StringComparison.Ordinal);
        Assert.Contains("Active Form: Annex Mentor; instance form_annex_mentor; level 5;", text, StringComparison.Ordinal);
        Assert.Contains("Persona Stock: Bramble Runner; instance persona_bramble_runner; level 3;", text, StringComparison.Ordinal);
        Assert.Contains("Demon Stock: Ashling; instance demon_ashling; level 2;", text, StringComparison.Ordinal);
        Assert.Contains("Demon Stock: Ward Shell; instance demon_ward_shell; level 4;", text, StringComparison.Ordinal);
        Assert.Contains("Demon Replacement Candidate: Bramble Runner; instance replacement_bramble_runner; level 3;", text, StringComparison.Ordinal);
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
    public async Task CleanTrainingAnnexPlay_InspectPartyUsesFrameworkPartySnapshot()
    {
        var io = new ScriptedGameIO().QueueMenu(13, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(
            [RuntimeInstanceId.Parse("echo_adept")],
            summary.PartyStock.ActiveParty.Select(actor => actor.InstanceId));
        Assert.Equal(
            [RuntimeInstanceId.Parse("support_annex_mentor")],
            summary.PartyStock.ReserveMembers.Select(actor => actor.InstanceId));
        Assert.Empty(summary.PartyTransitions);

        string text = output.ToString();
        Assert.Contains(
            "Party: active [Echo Adept (echo_adept)]; reserve [Annex Mentor (support_annex_mentor)].",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_InspectStockUsesFrameworkPartyStockSnapshot()
    {
        var io = new ScriptedGameIO().QueueMenu(14, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.NotNull(summary.PartyStock.ActiveForm);
        RuntimeActorReferenceSnapshot activeForm = summary.PartyStock.ActiveForm!;
        Assert.Equal(RuntimeInstanceId.Parse("form_annex_mentor"), activeForm.InstanceId);
        Assert.Equal(
            [RuntimeInstanceId.Parse("persona_bramble_runner")],
            summary.PartyStock.PersonaStock.Select(actor => actor.InstanceId));
        Assert.Equal(
            [RuntimeInstanceId.Parse("demon_ashling"), RuntimeInstanceId.Parse("demon_ward_shell")],
            summary.PartyStock.DemonStock.Select(actor => actor.InstanceId));

        string text = output.ToString();
        Assert.Contains(
            "Stock: active form [Annex Mentor (form_annex_mentor)]; Persona stock [Bramble Runner (persona_bramble_runner)]; Demon stock [Ashling (demon_ashling), Ward Shell (demon_ward_shell)].",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_PartyStockOperationsUseFrameworkTransitions()
    {
        var io = new ScriptedGameIO().QueueMenu(
            15, 0,
            15, 1,
            15, 2,
            15, 3,
            15, 4,
            15, 5,
            15, 6,
            9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(
            [
                "swap_active_form",
                "summon_demon",
                "swap_active_demon",
                "return_active_demon",
                "replace_demon",
                "dismiss_demon",
                "consume_demon"
            ],
            summary.PartyTransitions.Select(transition => transition.Operation));
        Assert.All(summary.PartyTransitions, transition =>
            Assert.Equal(PartyStockTransitionCode.Applied, transition.Code));
        Assert.Equal(
            [RuntimeInstanceId.Parse("echo_adept")],
            summary.PartyStock.ActiveParty.Select(actor => actor.InstanceId));
        Assert.Equal(
            [RuntimeInstanceId.Parse("support_annex_mentor")],
            summary.PartyStock.ReserveMembers.Select(actor => actor.InstanceId));
        Assert.NotNull(summary.PartyStock.ActiveForm);
        Assert.Equal(RuntimeInstanceId.Parse("persona_bramble_runner"), summary.PartyStock.ActiveForm!.InstanceId);
        Assert.Equal(
            [RuntimeInstanceId.Parse("form_annex_mentor")],
            summary.PartyStock.PersonaStock.Select(actor => actor.InstanceId));
        Assert.Empty(summary.PartyStock.DemonStock);

        TrainingAnnexPartyTransitionEvidence summon = summary.PartyTransitions[1];
        Assert.Equal(1, summon.ActiveCountBefore);
        Assert.Equal(2, summon.ActiveCountAfter);
        Assert.Equal(2, summon.DemonStockCountBefore);
        Assert.Equal(2, summon.DemonStockCountAfter);
        TrainingAnnexPartyTransitionEvidence returned = summary.PartyTransitions[3];
        Assert.Equal(2, returned.ActiveCountBefore);
        Assert.Equal(1, returned.ActiveCountAfter);
        TrainingAnnexPartyTransitionEvidence consumed = summary.PartyTransitions[6];
        Assert.Equal(1, consumed.DemonStockCountBefore);
        Assert.Equal(0, consumed.DemonStockCountAfter);

        string text = output.ToString();
        Assert.Contains("Party stock operation applied: swap_active_form", text, StringComparison.Ordinal);
        Assert.Contains("Party stock operation applied: summon_demon; active 1->2", text, StringComparison.Ordinal);
        Assert.Contains("Party stock operation applied: swap_active_demon", text, StringComparison.Ordinal);
        Assert.Contains("Party stock operation applied: return_active_demon; active 2->1", text, StringComparison.Ordinal);
        Assert.Contains("Party stock operation applied: replace_demon", text, StringComparison.Ordinal);
        Assert.Contains("Party stock operation applied: dismiss_demon", text, StringComparison.Ordinal);
        Assert.Contains("Party stock operation applied: consume_demon", text, StringComparison.Ordinal);
        Assert.Contains("Clean Party / Stock Operations", io.Menus.Select(menu => menu.Header));
        io.AssertConsumed();
    }

    [Fact]
    public async Task TrainingAnnexPartyController_RejectedOperationsDoNotMutateSnapshots()
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();
        TrainingAnnexActorRoster roster = TrainingAnnexHostSupport.CreateActorRoster(catalog).RequireRoster();
        var controller = new TrainingAnnexPartyController();
        RuntimePartyStockSnapshot initial = controller.CreateInitialParty(roster).Snapshot;

        PartyStockTransitionResult returned = controller.ExecuteOperation(
            TrainingAnnexPartyOperation.ReturnActiveDemon,
            initial,
            roster);

        Assert.False(returned.Applied);
        Assert.Equal(PartyStockTransitionCode.NotActive, returned.Code);
        Assert.Same(initial, returned.After);
        Assert.Empty(returned.AffectedInstanceIds);
        PartyStockTransitionDiagnostic diagnostic = Assert.Single(returned.Diagnostics);
        Assert.Equal(PartyStockTransitionCode.NotActive, diagnostic.Code);
        Assert.Equal("No active demon is in the party.", diagnostic.Message);
        Assert.Null(diagnostic.SubjectInstanceId);

        PartyStockTransitionResult summoned = controller.ExecuteOperation(
            TrainingAnnexPartyOperation.SummonAshling,
            initial,
            roster);
        Assert.True(summoned.Applied);

        PartyStockTransitionResult duplicate = controller.ExecuteOperation(
            TrainingAnnexPartyOperation.SummonAshling,
            summoned.After,
            roster);

        Assert.False(duplicate.Applied);
        Assert.Equal(PartyStockTransitionCode.AlreadyActive, duplicate.Code);
        Assert.Same(summoned.After, duplicate.After);
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_NegotiationRecruitmentAddsDemonThroughFrameworkTransitions()
    {
        var io = new ScriptedGameIO().QueueMenu(16, 0, 0, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexNegotiationEvidence negotiation = Assert.Single(summary.Negotiations);
        Assert.Equal(Qualified("bramble_runner"), negotiation.TargetEntityId);
        Assert.Equal(RuntimeInstanceId.Parse("replacement_bramble_runner"), negotiation.TargetInstanceId);
        Assert.Equal(NegotiationOutcomeKind.Success, negotiation.Outcome);
        Assert.Equal(NegotiationOutcomeReason.None, negotiation.Reason);
        Assert.Equal(4, negotiation.MoodScore);
        Assert.Equal(50, negotiation.MaccaSpent);
        Assert.Equal(RecruitmentTransactionStatus.Applied, negotiation.RecruitmentStatus);
        Assert.Equal(RecruitmentTransactionErrorCode.None, negotiation.RecruitmentErrorCode);
        Assert.Equal(PartyStockTransitionCode.Applied, negotiation.StockTransitionCode);
        Assert.True(negotiation.Recruited);
        Assert.Equal(100, negotiation.WalletBefore);
        Assert.Equal(50, negotiation.WalletAfter);
        Assert.Equal(2, negotiation.DemonStockCountBefore);
        Assert.Equal(3, negotiation.DemonStockCountAfter);
        Assert.Equal(50, summary.Wallet.Balance);
        Assert.Contains(
            summary.PartyStock.DemonStock,
            actor => actor.InstanceId == RuntimeInstanceId.Parse("replacement_bramble_runner"));
        Assert.Contains(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("bramble_runner") &&
            knowledge.Element == DamageElement.Fire &&
            knowledge.Affinity == ElementalAffinity.Weak);
        Assert.Empty(summary.EncounterAiKnowledge.ElementalAffinities);
        CompendiumEntrySnapshot entry = Assert.Single(summary.Compendium.Entries);
        Assert.Equal(Qualified("bramble_runner"), entry.EntityId);
        TrainingAnnexCompendiumEvidence acquisition = Assert.Single(summary.CompendiumEvidence);
        Assert.Equal(TrainingAnnexCompendiumAction.Acquisition, acquisition.Action);
        Assert.True(acquisition.Applied);
        Assert.Equal(CompendiumRegistrationCode.Added, acquisition.RegistrationCode);
        Assert.Equal(TrainingAnnexHostSupport.NegotiationAcquisitionSource, acquisition.AcquisitionSourceId);

        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.OpenNegotiation,
                CleanTrainingAnnexPlayCommand.SelectNegotiationTarget,
                CleanTrainingAnnexPlayCommand.SelectNegotiationAnswer,
                CleanTrainingAnnexPlayCommand.SelectNegotiationAnswer,
                CleanTrainingAnnexPlayCommand.SelectNegotiationDemand,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);
        string text = output.ToString();
        Assert.Contains("Negotiation opened: Steady Sample; target Bramble Runner; wallet 100 M.", text, StringComparison.Ordinal);
        Assert.Contains("Negotiation event: MoodPositive; Bramble Runner seems pleased with your answers.", text, StringComparison.Ordinal);
        Assert.Contains("Recruitment applied: Bramble Runner joined Demon stock; wallet 100->50 M; Demon stock 2->3.", text, StringComparison.Ordinal);
        Assert.Contains(
            "Compendium first-acquisition record added: Bramble Runner (negotiation).",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_CalculatesFusionResultsThroughCleanCatalogRepository()
    {
        var io = new ScriptedGameIO().QueueMenu(17, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(
            [CleanTrainingAnnexPlayCommand.CalculateFusionResults, CleanTrainingAnnexPlayCommand.Exit],
            summary.Commands);
        Assert.Equal(2, summary.FusionResults.Count);
        TrainingAnnexFusionPlanningEvidence planning = Assert.Single(summary.FusionPlanning);
        Assert.Empty(summary.FusionPreviews);

        TrainingAnnexFusionResultEvidence direct = summary.FusionResults[0];
        Assert.Equal("direct_entity_result", direct.ScenarioId);
        Assert.Equal(RuntimeInstanceId.Parse("demon_ashling"), direct.FirstParentInstanceId);
        Assert.Equal(Qualified("ashling"), direct.FirstParentEntityId);
        Assert.Equal(RuntimeInstanceId.Parse("replacement_bramble_runner"), direct.SecondParentInstanceId);
        Assert.Equal(Qualified("bramble_runner"), direct.SecondParentEntityId);
        Assert.Equal(FusionRuntimeOperation.CreateNewEntity, direct.Operation);
        Assert.Equal(Qualified("ward_shell"), direct.ResultEntityId);
        Assert.False(direct.IsAccident);
        Assert.Equal(Id("standard_accident"), direct.AccidentPolicyId);
        Assert.Null(direct.ResultPolicyId);
        Assert.Empty(direct.Diagnostics);

        TrainingAnnexFusionResultEvidence rank = summary.FusionResults[1];
        Assert.Equal("race_rank_offset_result", rank.ScenarioId);
        Assert.Equal(RuntimeInstanceId.Parse("echo_adept"), rank.FirstParentInstanceId);
        Assert.Equal(Qualified("echo_adept"), rank.FirstParentEntityId);
        Assert.Equal(RuntimeInstanceId.Parse("replacement_bramble_runner"), rank.SecondParentInstanceId);
        Assert.Equal(Qualified("bramble_runner"), rank.SecondParentEntityId);
        Assert.Equal(FusionRuntimeOperation.RankUpParent, rank.Operation);
        Assert.Equal(Qualified("ward_shell"), rank.ResultEntityId);
        Assert.False(rank.IsAccident);
        Assert.Equal(Id("standard_accident"), rank.AccidentPolicyId);
        Assert.Null(rank.ResultPolicyId);
        Assert.Empty(rank.Diagnostics);

        Assert.Equal("inheritance_slots_mutation_accident", planning.ScenarioId);
        Assert.Equal(Qualified("ward_shell"), planning.ResultEntityId);
        Assert.Equal(1, planning.MaximumInheritanceSlots);
        Assert.Equal(3, planning.SacrificialMaximumInheritanceSlots);
        Assert.Equal([Qualified("shell_bash"), Qualified("soften_guard")], planning.NaturalSkillIds);
        Assert.Equal(
            [Qualified("frost_tip"), Qualified("echo_strike"), Qualified("steady_breath")],
            planning.PickableSkillIds);
        Assert.Contains(planning.DisplaySkills, entry =>
            entry.SkillId == Qualified("shell_bash") &&
            !entry.IsSelectable &&
            entry.ReasonCode == "already_known");
        Assert.Contains(planning.DisplaySkills, entry =>
            entry.SkillId == Qualified("toxin_touch") &&
            !entry.IsSelectable &&
            entry.ReasonCode == "group_not_allowed");
        Assert.Equal([Qualified("shell_bash")], planning.AccidentInheritedSkillIds);
        Assert.Equal(Qualified("echo_strike"), planning.MutationSourceSkillId);
        Assert.Equal(Qualified("shell_bash"), planning.MutationResultSkillId);
        Assert.Equal(Id("standard_accident"), planning.AccidentPolicyId);
        Assert.Equal(Id("standard_mutation"), planning.MutationPolicyId);
        Assert.Equal(2, planning.SacrificeAdditionalSlots);

        string text = output.ToString();
        Assert.Contains(
            "Fusion result: Ashling + Bramble Runner -> Ward Shell (create_entity; direct_entity_result).",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "Fusion result: Echo Adept + Bramble Runner -> Ward Shell (rank_up; race_rank_offset_result).",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "Fusion planning: Ward Shell; slots 1, sacrificial slots 3;",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "accident policy standard_accident; mutation policy standard_mutation; sacrifice bonus 2; accident sample Echo Strike -> Shell Bash.",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RegistersOwnedActorAndImportsOnlyPlayerFamiliarKnowledge()
    {
        var io = new ScriptedGameIO().QueueMenu(20, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        CompendiumEntrySnapshot entry = Assert.Single(summary.Compendium.Entries);
        Assert.Equal(Qualified("ashling"), entry.EntityId);
        Assert.Equal(RuntimeInstanceId.Parse("demon_ashling"), summary.PartyStock.DemonStock[0].InstanceId);

        TrainingAnnexCompendiumEvidence evidence = Assert.Single(summary.CompendiumEvidence);
        Assert.Equal(TrainingAnnexCompendiumAction.Register, evidence.Action);
        Assert.True(evidence.Applied);
        Assert.Equal(CompendiumRegistrationCode.Added, evidence.RegistrationCode);
        Assert.Equal(7, evidence.ImportedElementalAffinities);
        Assert.True(evidence.ImportedAilmentResistances > 0);
        Assert.Equal(2, evidence.ImportedInstantDeathResistances);
        Assert.Contains(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("ashling") &&
            knowledge.Element == DamageElement.Ice &&
            knowledge.Affinity == ElementalAffinity.Weak);
        Assert.Empty(summary.EncounterAiKnowledge.ElementalAffinities);
        Assert.Empty(summary.EncounterAiKnowledge.AilmentResistances);
        Assert.Empty(summary.EncounterAiKnowledge.InstantDeathResistances);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.OpenCompendium,
                CleanTrainingAnnexPlayCommand.CompendiumRegister,
                CleanTrainingAnnexPlayCommand.SelectCompendiumActor,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);
        Assert.Contains(
            "Compendium added: Ashling; familiar defense knowledge imported for the player only.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RecallsRegisteredActorAtomicallyThroughStockAndWallet()
    {
        var io = new ScriptedGameIO().QueueMenu(20, 0, 3, 15, 4, 20, 1, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, initialWallet: new RuntimeWalletSnapshot(5_000));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(Qualified("ward_shell"), Assert.Single(summary.Compendium.Entries).EntityId);
        Assert.Equal(2, summary.CompendiumEvidence.Count);
        TrainingAnnexCompendiumEvidence recall = summary.CompendiumEvidence[1];
        Assert.Equal(TrainingAnnexCompendiumAction.Recall, recall.Action);
        Assert.True(recall.Applied);
        Assert.Equal(CompendiumRecallTransactionCode.Applied, recall.RecallCode);
        Assert.Equal(3_850, recall.Cost);
        Assert.Equal(5_000, recall.WalletBefore);
        Assert.Equal(1_150, recall.WalletAfter);
        Assert.Equal(2, recall.DemonStockBefore);
        Assert.Equal(3, recall.DemonStockAfter);
        Assert.Equal(1_150, summary.Wallet.Balance);
        Assert.Contains(summary.PartyStock.DemonStock, actor =>
            actor.InstanceId == RuntimeInstanceId.Parse("recall_ward_shell_1") &&
            actor.EntityDefinitionId == Qualified("ward_shell"));
        Assert.Contains(summary.ActorInstanceIds, id => id == RuntimeInstanceId.Parse("recall_ward_shell_1"));
        Assert.Contains(
            "Compendium recall applied: Ward Shell; wallet 5000->1150 M; Demon stock 2->3.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RecallRejectionDoesNotAddStockOrSpendWallet()
    {
        var io = new ScriptedGameIO().QueueMenu(20, 0, 3, 15, 4, 20, 1, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, initialWallet: new RuntimeWalletSnapshot(0));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexCompendiumEvidence recall = summary.CompendiumEvidence[1];
        Assert.False(recall.Applied);
        Assert.Equal(CompendiumRecallTransactionCode.InsufficientCurrency, recall.RecallCode);
        Assert.Equal(0, summary.Wallet.Balance);
        Assert.Equal(2, summary.PartyStock.DemonStock.Count);
        Assert.DoesNotContain(summary.PartyStock.DemonStock, actor =>
            actor.InstanceId == RuntimeInstanceId.Parse("recall_ward_shell_1"));
        Assert.Contains(
            "Compendium rejected [InsufficientCurrency]",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_SaveLoadRestoresCompendiumAndImportedPlayerKnowledge()
    {
        var io = new ScriptedGameIO().QueueMenu(20, 0, 0, 10, 0, 20, 0, 3, 10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        CompendiumEntrySnapshot entry = Assert.Single(summary.Compendium.Entries);
        Assert.Equal(Qualified("ashling"), entry.EntityId);
        Assert.Equal(1, summary.ManualSaveCount);
        Assert.Equal(1, summary.ManualLoadCount);
        Assert.Contains(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("ashling") &&
            knowledge.Element == DamageElement.Ice &&
            knowledge.Affinity == ElementalAffinity.Weak);
        Assert.DoesNotContain(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("ward_shell"));
        Assert.Contains("Manual save restored", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_SaveLoadRestoresRecalledCatalogActor()
    {
        var io = new ScriptedGameIO().QueueMenu(20, 0, 3, 15, 4, 20, 1, 0, 10, 0, 10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, initialWallet: new RuntimeWalletSnapshot(5_000));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(1, summary.ManualSaveCount);
        Assert.Equal(1, summary.ManualLoadCount);
        Assert.Equal(1_150, summary.Wallet.Balance);
        Assert.Contains(summary.PartyStock.DemonStock, actor =>
            actor.InstanceId == RuntimeInstanceId.Parse("recall_ward_shell_1") &&
            actor.EntityDefinitionId == Qualified("ward_shell"));
        Assert.Contains(summary.ActorInstanceIds, id => id == RuntimeInstanceId.Parse("recall_ward_shell_1"));
        Assert.Equal(Qualified("ward_shell"), Assert.Single(summary.Compendium.Entries).EntityId);
        Assert.Equal(0, summary.SaveDiagnosticCount);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_PreviewsFusionThroughValidatedSelectionWithoutMutation()
    {
        var io = new ScriptedGameIO().QueueMenu(18, 0, 1, 2, 6, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.PreviewFusionResult,
                CleanTrainingAnnexPlayCommand.SelectFusionInheritedSkill,
                CleanTrainingAnnexPlayCommand.SelectFusionInheritedSkill,
                CleanTrainingAnnexPlayCommand.SelectFusionInheritedSkill,
                CleanTrainingAnnexPlayCommand.BuildFusionPreview,
                CleanTrainingAnnexPlayCommand.ConfirmFusionPreview,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);
        TrainingAnnexFusionPreviewEvidence previewEvidence = Assert.Single(summary.FusionPreviews);
        Assert.Equal("sacrificial_preview_confirmation", previewEvidence.ScenarioId);
        Assert.Equal(Qualified("ward_shell"), previewEvidence.ResultEntityId);
        Assert.Equal(
            [Qualified("frost_tip"), Qualified("echo_strike"), Qualified("steady_breath")],
            previewEvidence.SelectedSkillIds);
        Assert.Empty(previewEvidence.SelectionDiagnostics);
        Assert.True(previewEvidence.Confirmed);
        Assert.False(previewEvidence.MutatedRuntimeState);

        Assert.NotNull(previewEvidence.Preview);
        FusionPreviewSnapshot preview = previewEvidence.Preview!;
        Assert.Equal(Qualified("ward_shell"), preview.EntityId);
        Assert.Equal([Qualified("shell_bash"), Qualified("soften_guard")], preview.NaturalSkillIds);
        Assert.Equal(previewEvidence.SelectedSkillIds, preview.InheritedSkillIds);

        Assert.Contains(io.Menus, menu =>
            menu.Header == "Select Inherited Skills" &&
            menu.Options.Contains("Toxin Touch [group_not_allowed]"));
        GameIoMenuCall inheritanceMenu = io.Menus.First(menu =>
            menu.Header == "Select Inherited Skills" &&
            menu.Options.Contains("Toxin Touch [group_not_allowed]"));
        Assert.Equal("Frost Tip", inheritanceMenu.Options[0]);
        Assert.Equal("Echo Strike", inheritanceMenu.Options[1]);
        Assert.Equal("Steady Breath", inheritanceMenu.Options[2]);
        Assert.Equal("Shell Bash [already_known]", inheritanceMenu.Options[3]);

        string text = output.ToString();
        Assert.Contains(
            "Fusion preview: Ward Shell; level 4; natural Shell Bash, Soften Guard; inherited Frost Tip, Echo Strike, Steady Breath;",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "Fusion preview confirmed: Ward Shell with inherited Frost Tip, Echo Strike, Steady Breath. No runtime state was mutated.",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RejectsFusionTransactionWhenResultIsAlreadyOwned()
    {
        var io = new ScriptedGameIO().QueueMenu(19, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(
            [CleanTrainingAnnexPlayCommand.CommitFusionTransaction, CleanTrainingAnnexPlayCommand.Exit],
            summary.Commands);
        TrainingAnnexFusionTransactionEvidence transaction = Assert.Single(summary.FusionTransactions);
        Assert.Equal("direct_transaction_commit", transaction.ScenarioId);
        Assert.Equal(Qualified("ward_shell"), transaction.ResultEntityId);
        Assert.Null(transaction.ResultInstanceId);
        Assert.Empty(transaction.SelectedSkillIds);
        Assert.Empty(transaction.ResultSkillIds);
        Assert.NotNull(transaction.Assessment);
        Assert.False(transaction.Assessment!.CanCommit);
        Assert.Equal(
            FusionRuntimeDiagnosticCode.DuplicateResult,
            Assert.Single(transaction.Assessment.Diagnostics).Code);
        Assert.False(transaction.Confirmed);
        Assert.False(transaction.Committed);
        Assert.False(transaction.MutatedRuntimeState);
        Assert.Null(transaction.CommitResult);
        Assert.Equal(2, transaction.DemonStockCountBefore);
        Assert.Equal(2, transaction.DemonStockCountAfter);
        Assert.Empty(transaction.StockTransitions);
        Assert.Equal(10, summary.ActorCount);
        Assert.Equal(
            [RuntimeInstanceId.Parse("demon_ashling"), RuntimeInstanceId.Parse("demon_ward_shell")],
            summary.PartyStock.DemonStock.Select(actor => actor.InstanceId));

        string text = output.ToString();
        Assert.Contains(
            "Fusion transaction rejected [DuplicateResult]: The fusion result is already owned.",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Fusion transaction committed:", text, StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_CommitsFusionTransactionAtomicallyAfterResultSlotIsFreed()
    {
        var io = new ScriptedGameIO().QueueMenu(15, 4, 19, 3, 0, 5, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.OpenPartyStockOperations,
                CleanTrainingAnnexPlayCommand.PartyReplaceWardShell,
                CleanTrainingAnnexPlayCommand.CommitFusionTransaction,
                CleanTrainingAnnexPlayCommand.BuildFusionPreview,
                CleanTrainingAnnexPlayCommand.ConfirmFusionTransaction,
                CleanTrainingAnnexPlayCommand.ValidateStartupSnapshot,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);

        TrainingAnnexPartyTransitionEvidence replacement = Assert.Single(summary.PartyTransitions);
        Assert.Equal("replace_demon", replacement.Operation);
        Assert.Equal(PartyStockTransitionCode.Applied, replacement.Code);
        Assert.Equal(2, replacement.DemonStockCountBefore);
        Assert.Equal(2, replacement.DemonStockCountAfter);
        Assert.Equal(
            [TrainingAnnexHostSupport.DemonWardShellInstance, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance],
            replacement.AffectedInstanceIds);

        TrainingAnnexFusionTransactionEvidence transaction = Assert.Single(summary.FusionTransactions);
        Assert.Equal("direct_transaction_commit", transaction.ScenarioId);
        Assert.Equal(Qualified("ward_shell"), transaction.ResultEntityId);
        Assert.Equal(RuntimeInstanceId.Parse("fusion_ward_shell_1"), transaction.ResultInstanceId);
        Assert.Empty(transaction.SelectedSkillIds);
        Assert.Equal([Qualified("shell_bash"), Qualified("soften_guard")], transaction.ResultSkillIds);
        Assert.NotNull(transaction.Assessment);
        Assert.True(transaction.Assessment!.CanCommit);
        Assert.Empty(transaction.Assessment.Diagnostics);
        Assert.Equal(
            [TrainingAnnexHostSupport.DemonAshlingInstance, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance],
            transaction.Assessment.ConsumedParticipantIds);
        Assert.True(transaction.Confirmed);
        Assert.True(transaction.Committed);
        Assert.True(transaction.MutatedRuntimeState);
        FusionTransactionCommitResult commit = Assert.IsType<FusionTransactionCommitResult>(transaction.CommitResult);
        Assert.True(commit.Applied);
        Assert.Equal(FusionTransactionCommitCode.Applied, commit.Code);
        Assert.Same(commit.PreparedTransaction.AfterPartyStock, commit.AfterPartyStock);
        Assert.Equal(RuntimeInstanceId.Parse("fusion_ward_shell_1"), commit.ResultActorSnapshot?.Identity.InstanceId);
        Assert.Equal(2, transaction.DemonStockCountBefore);
        Assert.Equal(1, transaction.DemonStockCountAfter);
        Assert.Equal(3, transaction.StockTransitions.Count);
        Assert.All(transaction.StockTransitions, transition => Assert.Equal(PartyStockTransitionCode.Applied, transition.Code));

        RuntimeActorReferenceSnapshot fusedDemon = Assert.Single(summary.PartyStock.DemonStock);
        Assert.Equal(RuntimeInstanceId.Parse("fusion_ward_shell_1"), fusedDemon.InstanceId);
        Assert.Equal(Qualified("ward_shell"), fusedDemon.EntityDefinitionId);
        Assert.Equal(11, summary.ActorCount);
        Assert.Contains(RuntimeInstanceId.Parse("fusion_ward_shell_1"), summary.ActorInstanceIds);
        Assert.Contains(summary.BattleKnowledge.ElementalAffinities, knowledge =>
            knowledge.EntityId == Qualified("ward_shell") &&
            knowledge.Element == DamageElement.Electric &&
            knowledge.Affinity == ElementalAffinity.Weak);
        Assert.Empty(summary.EncounterAiKnowledge.ElementalAffinities);
        CompendiumEntrySnapshot compendiumEntry = Assert.Single(summary.Compendium.Entries);
        Assert.Equal(Qualified("ward_shell"), compendiumEntry.EntityId);
        TrainingAnnexCompendiumEvidence acquisition = Assert.Single(summary.CompendiumEvidence);
        Assert.Equal(TrainingAnnexCompendiumAction.Acquisition, acquisition.Action);
        Assert.True(acquisition.Applied);
        Assert.Equal(CompendiumRegistrationCode.Added, acquisition.RegistrationCode);
        Assert.Equal(TrainingAnnexHostSupport.FusionAcquisitionSource, acquisition.AcquisitionSourceId);
        Assert.True(summary.StartupSnapshotValidated);
        Assert.Equal(0, summary.StartupSnapshotDiagnosticCount);

        Assert.Contains(io.Menus, menu =>
            menu.Header == "Select Inherited Skills" &&
            menu.Options.SequenceEqual([
                "Ash Spark [group_not_allowed]",
                "Shell Bash [already_known]",
                "Toxin Touch [group_not_allowed]",
                "Build Preview (0/1)",
                "Back"
            ]));

        string text = output.ToString();
        Assert.Contains(
            "Fusion transaction committed: Ashling (demon_ashling) + Bramble Runner (replacement_bramble_runner) -> Ward Shell; consumed demon_ashling, replacement_bramble_runner; added fusion_ward_shell_1; Demon stock 2->1.",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "Compendium first-acquisition record added: Ward Shell (fusion).",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "Startup snapshot validation: 0 diagnostic(s).",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_NegotiationAcquisitionPreservesAnExistingCompendiumRecord()
    {
        var io = new ScriptedGameIO().QueueMenu(20, 0, 1, 16, 0, 0, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        CompendiumEntrySnapshot entry = Assert.Single(summary.Compendium.Entries);
        Assert.Equal(Qualified("bramble_runner"), entry.EntityId);
        Assert.Equal(2, summary.CompendiumEvidence.Count);
        Assert.Equal(CompendiumRegistrationCode.Added, summary.CompendiumEvidence[0].RegistrationCode);
        TrainingAnnexCompendiumEvidence acquisition = summary.CompendiumEvidence[1];
        Assert.Equal(TrainingAnnexCompendiumAction.Acquisition, acquisition.Action);
        Assert.False(acquisition.Applied);
        Assert.Equal(CompendiumRegistrationCode.AlreadyRegistered, acquisition.RegistrationCode);
        Assert.Equal(TrainingAnnexHostSupport.NegotiationAcquisitionSource, acquisition.AcquisitionSourceId);
        Assert.Contains(
            "Compendium record preserved: Bramble Runner was already registered; negotiation did not overwrite it.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ExplicitRegistrationCanStillUpdateAnExistingRecord()
    {
        var io = new ScriptedGameIO().QueueMenu(20, 0, 0, 20, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Single(summary.Compendium.Entries);
        Assert.Equal(2, summary.CompendiumEvidence.Count);
        Assert.Equal(CompendiumRegistrationCode.Added, summary.CompendiumEvidence[0].RegistrationCode);
        Assert.Equal(CompendiumRegistrationCode.Updated, summary.CompendiumEvidence[1].RegistrationCode);
        Assert.All(summary.CompendiumEvidence, evidence =>
            Assert.Equal(TrainingAnnexCompendiumAction.Register, evidence.Action));
        Assert.Contains("Compendium updated: Ashling", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FusionAcquisitionPreservesAnExistingCompendiumRecord()
    {
        var io = new ScriptedGameIO().QueueMenu(20, 0, 3, 15, 4, 19, 3, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        CompendiumEntrySnapshot entry = Assert.Single(summary.Compendium.Entries);
        Assert.Equal(Qualified("ward_shell"), entry.EntityId);
        Assert.Equal(2, summary.CompendiumEvidence.Count);
        TrainingAnnexCompendiumEvidence explicitRegistration = summary.CompendiumEvidence[0];
        TrainingAnnexCompendiumEvidence acquisition = summary.CompendiumEvidence[1];
        Assert.Equal(TrainingAnnexCompendiumAction.Register, explicitRegistration.Action);
        Assert.Equal(CompendiumRegistrationCode.Added, explicitRegistration.RegistrationCode);
        Assert.Equal(TrainingAnnexCompendiumAction.Acquisition, acquisition.Action);
        Assert.False(acquisition.Applied);
        Assert.Equal(CompendiumRegistrationCode.AlreadyRegistered, acquisition.RegistrationCode);
        Assert.Equal(TrainingAnnexHostSupport.FusionAcquisitionSource, acquisition.AcquisitionSourceId);
        Assert.Contains(
            "Compendium record preserved: Ward Shell was already registered; fusion did not overwrite it.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FusionConfirmationBackDoesNotCommitPreparedTransaction()
    {
        var io = new ScriptedGameIO().QueueMenu(15, 4, 19, 3, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexFusionTransactionEvidence transaction = Assert.Single(summary.FusionTransactions);
        Assert.NotNull(transaction.Assessment);
        Assert.True(transaction.Assessment!.CanCommit);
        Assert.False(transaction.Confirmed);
        Assert.False(transaction.Committed);
        Assert.False(transaction.MutatedRuntimeState);
        Assert.Null(transaction.CommitResult);
        Assert.Empty(transaction.StockTransitions);
        Assert.Equal(
            [TrainingAnnexHostSupport.DemonAshlingInstance, TrainingAnnexHostSupport.ReplacementBrambleRunnerInstance],
            summary.PartyStock.DemonStock.Select(actor => actor.InstanceId));
        Assert.DoesNotContain(RuntimeInstanceId.Parse("fusion_ward_shell_1"), summary.ActorInstanceIds);
        Assert.Contains(
            "Fusion transaction canceled at confirmation. No runtime state was mutated.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task TrainingAnnexNegotiationController_UsesSelectedRuntimeTargetRatherThanFixedSample()
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();
        TrainingAnnexActorRoster originalRoster = TrainingAnnexHostSupport.CreateActorRoster(catalog).RequireRoster();
        RuntimePartyStockSnapshot party = new TrainingAnnexPartyController()
            .CreateInitialParty(originalRoster)
            .Snapshot;
        TrainingAnnexRuntimeActor selectedCandidate = originalRoster.Enemies
            .Single(actor => actor.Actor.State.InstanceId == RuntimeInstanceId.Parse("enemy_bramble_runner"))
            with
            {
                Role = "Demon Replacement Candidate"
            };
        var roster = new TrainingAnnexActorRoster(
            originalRoster.Player,
            originalRoster.SupportMembers,
            [.. originalRoster.StockMembers, selectedCandidate],
            originalRoster.Enemies);
        var io = new ScriptedGameIO().QueueMenu(1, 0, 0, 0);
        using var output = new StringWriter();
        var commands = new List<CleanTrainingAnnexPlayCommand>();
        var controller = new TrainingAnnexNegotiationController(
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io),
            new TrainingAnnexMinimumRandomSource());

        TrainingAnnexNegotiationInteractionResult result = await controller.OpenAsync(
            catalog,
            roster,
            party,
            new RuntimeWalletSnapshot(100),
            new EconomyTransactionService(),
            new HashSet<ContentId>(),
            commands,
            CancellationToken.None);

        TrainingAnnexNegotiationEvidence evidence = Assert.Single(result.Evidence);
        Assert.Equal(Qualified("bramble_runner"), evidence.TargetEntityId);
        Assert.Equal(RuntimeInstanceId.Parse("enemy_bramble_runner"), evidence.TargetInstanceId);
        Assert.True(evidence.Recruited);
        Assert.Equal(50, evidence.WalletAfter);
        Assert.Contains(
            result.PartyStock.DemonStock,
            actor => actor.InstanceId == RuntimeInstanceId.Parse("enemy_bramble_runner"));
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.SelectNegotiationTarget,
                CleanTrainingAnnexPlayCommand.SelectNegotiationAnswer,
                CleanTrainingAnnexPlayCommand.SelectNegotiationAnswer,
                CleanTrainingAnnexPlayCommand.SelectNegotiationDemand
            ],
            commands);
        Assert.Contains(
            "Negotiation opened: Steady Sample; 2 targets; wallet 100 M.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_NegotiationUsesAuthoredDemandAmountFromContent()
    {
        var io = new ScriptedGameIO().QueueMenu(16, 0, 0, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new NegotiationDemandAmountContentPackTextSource(ContentRoot(), 30),
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexNegotiationEvidence negotiation = Assert.Single(summary.Negotiations);
        Assert.Equal(NegotiationOutcomeKind.Success, negotiation.Outcome);
        Assert.Equal(30, negotiation.MaccaSpent);
        Assert.Equal(100, negotiation.WalletBefore);
        Assert.Equal(70, negotiation.WalletAfter);
        Assert.Equal(70, summary.Wallet.Balance);
        Assert.Contains(
            "Recruitment applied: Bramble Runner joined Demon stock; wallet 100->70 M; Demon stock 2->3.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_NegotiationRefusalDoesNotSpendOrMutateStock()
    {
        var io = new ScriptedGameIO().QueueMenu(16, 0, 0, 0, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexNegotiationEvidence negotiation = Assert.Single(summary.Negotiations);
        Assert.Equal(NegotiationOutcomeKind.Failure, negotiation.Outcome);
        Assert.Equal(NegotiationOutcomeReason.CurrencyRefused, negotiation.Reason);
        Assert.False(negotiation.Recruited);
        Assert.Null(negotiation.RecruitmentStatus);
        Assert.Null(negotiation.StockTransitionCode);
        Assert.Equal(100, negotiation.WalletBefore);
        Assert.Equal(100, negotiation.WalletAfter);
        Assert.Equal(2, negotiation.DemonStockCountBefore);
        Assert.Equal(2, negotiation.DemonStockCountAfter);
        Assert.Equal(100, summary.Wallet.Balance);
        Assert.DoesNotContain(
            summary.PartyStock.DemonStock,
            actor => actor.InstanceId == RuntimeInstanceId.Parse("replacement_bramble_runner"));
        Assert.Contains("Negotiation ended: Failure (MaccaRefused); wallet and Demon stock are unchanged.", output.ToString(), StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_NegotiationInsufficientAuthoredDemandDoesNotSpendOrMutateStock()
    {
        var io = new ScriptedGameIO().QueueMenu(16, 0, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(40));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexNegotiationEvidence negotiation = Assert.Single(summary.Negotiations);
        Assert.Equal(NegotiationOutcomeKind.Failure, negotiation.Outcome);
        Assert.Equal(NegotiationOutcomeReason.InsufficientCurrency, negotiation.Reason);
        Assert.Equal(0, negotiation.MaccaSpent);
        Assert.False(negotiation.Recruited);
        Assert.Null(negotiation.RecruitmentStatus);
        Assert.Null(negotiation.StockTransitionCode);
        Assert.Equal(40, negotiation.WalletBefore);
        Assert.Equal(40, negotiation.WalletAfter);
        Assert.Equal(2, negotiation.DemonStockCountBefore);
        Assert.Equal(2, negotiation.DemonStockCountAfter);
        Assert.Equal(40, summary.Wallet.Balance);
        Assert.DoesNotContain(
            summary.Commands,
            command => command == CleanTrainingAnnexPlayCommand.SelectNegotiationDemand);
        Assert.DoesNotContain(
            summary.PartyStock.DemonStock,
            actor => actor.InstanceId == RuntimeInstanceId.Parse("replacement_bramble_runner"));
        Assert.Contains(
            "Negotiation ended: Failure (InsufficientMacca); wallet and Demon stock are unchanged.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RepeatedNegotiationUsesFamiliarPathWithoutDuplicateRecruitment()
    {
        var io = new ScriptedGameIO().QueueMenu(16, 0, 0, 0, 0, 16, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(2, summary.Negotiations.Count);
        TrainingAnnexNegotiationEvidence first = summary.Negotiations[0];
        TrainingAnnexNegotiationEvidence second = summary.Negotiations[1];
        Assert.True(first.Recruited);
        Assert.Equal(NegotiationOutcomeKind.FamiliarFlee, second.Outcome);
        Assert.Equal(NegotiationOutcomeReason.FamiliarTarget, second.Reason);
        Assert.False(second.Recruited);
        Assert.Equal(3, second.DemonStockCountBefore);
        Assert.Equal(3, second.DemonStockCountAfter);
        Assert.Equal(50, second.WalletBefore);
        Assert.Equal(50, second.WalletAfter);
        Assert.Single(
            summary.PartyStock.DemonStock,
            actor => actor.EntityDefinitionId == Qualified("bramble_runner"));
        Assert.Contains(
            io.Menus.SelectMany(menu => menu.Options),
            option => option.Contains("[Familiar]", StringComparison.Ordinal));
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_TraversesGenericDungeonNodesWithoutStartingEncounter()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 8, 6, 6, 7, 7, 7, 9);
        using var output = new StringWriter();
        var source = new RecordingContentPackTextSource(ContentRoot());
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
        var source = new RecordingContentPackTextSource(ContentRoot());
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
            [RuntimeInstanceId.Parse("review_hall_trigger_ashling_1")],
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.PassiveTriggered &&
            evidence.RelatedContentId == Qualified("steady_breath") &&
            evidence.Detail == "owner_turn_end"));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
            evidence.ActionId == Qualified("frost_tip") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.PressTurn &&
            evidence.PressTurnOutcome == PressTurnOutcome.Weakness &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 1);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
            evidence.ActionId == Qualified("frost_tip") &&
            evidence.BeforeFullIcons == 0 &&
            evidence.BeforeBlinkingIcons == 1 &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("review_hall_trigger_ashling_1") &&
            evidence.ActionId == Qualified("ash_spark") &&
            evidence.BeforeFullIcons == 1 &&
            evidence.BeforeBlinkingIcons == 0 &&
            evidence.TurnConsumptionKind == ActionTurnConsumptionKind.PressTurn &&
            evidence.PressTurnOutcome == PressTurnOutcome.Normal &&
            evidence.AfterFullIcons == 0 &&
            evidence.AfterBlinkingIcons == 0);
        Assert.NotNull(summary.PreparedBattleRewardPreview);
        Assert.Equal(1, summary.PreparedBattleRewardPreview!.TotalExperience);
        Assert.Equal(14, summary.PreparedBattleRewardPreview.TotalCurrency);
        Assert.NotNull(summary.AppliedBattleReward);
        Assert.Equal(1, summary.AppliedBattleReward!.TotalExperience);
        Assert.Equal(14, summary.AppliedBattleReward.TotalCurrency);
        Assert.Equal(0, summary.AppliedBattleRewardLevelUpCount);
        WalletTransactionResult walletTransaction = Assert.IsType<WalletTransactionResult>(
            summary.AppliedWalletTransaction);
        Assert.True(walletTransaction.Applied);
        Assert.Equal(0, walletTransaction.Before.Balance);
        Assert.Equal(14, walletTransaction.After.Balance);
        Assert.True(summary.GrowthApplied);
        Assert.Equal(0, summary.LevelUpCount);
        Assert.Equal(1, summary.PlayerProgression.Experience);
        Assert.Equal(1, summary.PlayerProgression.LifetimeExperience);
        Assert.Equal(14, summary.Wallet.Balance);
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
        Assert.Null(summary.AppliedWalletTransaction);
        Assert.Equal(0, summary.Wallet.Balance);
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
        Assert.Equal(RuntimeInstanceId.Parse("review_hall_trigger_ashling_1"), ai.ActorInstanceId);
        Assert.Equal(Qualified("ashling"), ai.ActorEntityId);
        Assert.Equal(BattleActionSelectionStatus.Selected, ai.Status);
        Assert.Equal(Qualified("ash_spark"), ai.SelectedActionId);
        Assert.Equal([RuntimeInstanceId.Parse("echo_adept")], ai.TargetIds);
        Assert.True(ai.AssessmentCanExecute);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RuntimeInstanceId>)ai.TargetIds).Add(RuntimeInstanceId.Parse("unexpected")));
        TrainingAnnexCombatResolutionEvidence attack = Assert.Single(
            summary.CombatResolutionEvidence,
            evidence => evidence.SourceActionId == Qualified("practice_blade"));
        Assert.Equal(DamageElement.Physical, attack.DamageElement);
        Assert.Equal(12, attack.Power);
        Assert.Equal(95, attack.Accuracy);
        Assert.Equal(23, attack.Value);
        Assert.Contains(summary.PressTurnEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
    public async Task CleanTrainingAnnexPlay_BasicAttackUsesEquippedWeaponProfile()
    {
        ContentId weightedClub = Qualified("weighted_club");
        var inventory = new RuntimeInventorySnapshot(
            itemQuantities:
            [
                new KeyValuePair<ContentId, int>(Qualified("annex_tonic"), 1)
            ],
            ownedEquipmentIds:
            [
                new KeyValuePair<EquipmentSlot, IEnumerable<ContentId>>(EquipmentSlot.Weapon, [weightedClub])
            ]);
        var equipment = new RuntimeEquipmentSnapshot(
            [new KeyValuePair<EquipmentSlot, ContentId>(EquipmentSlot.Weapon, weightedClub)]);
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 0, 0, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new EquipmentAddingContentPackTextSource(ContentRoot()),
            initialInventory: inventory,
            initialEquipment: equipment);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(weightedClub, summary.Equipment.EquippedItemIds[EquipmentSlot.Weapon]);
        Assert.Equal(weightedClub, summary.EquipmentProfile.BasicAttack?.EquipmentId);
        Assert.Contains(weightedClub, summary.ExecutedBattleActionIds);
        Assert.DoesNotContain(Qualified("practice_blade"), summary.ExecutedBattleActionIds);
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            IsDamage(effect, weightedClub, DamageElement.Physical));
        TrainingAnnexCombatResolutionEvidence attack = Assert.Single(
            summary.CombatResolutionEvidence,
            evidence => evidence.SourceActionId == weightedClub);
        Assert.Equal(4, attack.Power);
        Assert.Equal(88, attack.Accuracy);
        Assert.Contains(
            "Battle action executed: Echo Adept used Weighted Club.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_TargetMenuSelectsTheAuthoredSecondEnemy()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 0, 1, -1, 13);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new MultiEnemyAshlingDrillContentSource(ContentRoot()));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexCombatResolutionEvidence attack = Assert.Single(
            summary.CombatResolutionEvidence,
            evidence => evidence.SourceActionId == Qualified("practice_blade"));
        Assert.Equal(RuntimeInstanceId.Parse("review_hall_trigger_bramble_runner_2"), attack.TargetId);
        GameIoMenuCall targetMenu = Assert.Single(io.Menus, menu => menu.Header == "Select Battle Target");
        Assert.Equal(["Ashling", "Bramble Runner", "Back"], targetMenu.Options);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_BattleSkillMenuExecutesContentWithoutAnEnumCase()
    {
        var io = new ScriptedGameIO().QueueMenu(6, 6, 9, 10, 1, 2, 0, -1, 13);
        using var output = new StringWriter();
        var source = new TrainingAnnexLifecycleContentPackTextSource(
            ContentRoot(),
            playerBaseSkillIds: ["frost_tip", "echo_strike", "steady_breath", "focus_call"]);
        var host = CreateHost(io, output, source);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Contains(Qualified("focus_call"), summary.ExecutedBattleActionIds);
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            effect.SourceActionId == Qualified("focus_call") &&
            effect.EffectKind == "modify_stat_stage");
        GameIoMenuCall skillMenu = Assert.Single(io.Menus, menu => menu.Header == "Clean Battle Skills");
        Assert.Equal(["Frost Tip", "Echo Strike", "Focus Call", "Back"], skillMenu.Options);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_BattleItemMenuExecutesSecondOwnedCatalogItem()
    {
        ContentId focusTea = Qualified("focus_tea");
        var io = new ScriptedGameIO().QueueMenu(
            6, 6, 9, 10,
            1, 0, 0,
            2, 1, 0,
            -1,
            13);
        using var output = new StringWriter();
        var initialInventory = new RuntimeInventorySnapshot(
        [
            KeyValuePair.Create(Qualified("annex_tonic"), 1),
            KeyValuePair.Create(focusTea, 1)
        ]);
        var host = CreateHost(io, output, initialInventory: initialInventory);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Contains(focusTea, summary.ExecutedBattleActionIds);
        Assert.Equal(0, summary.Inventory.GetQuantity(focusTea));
        Assert.Contains(summary.ExecutedBattleEffectEvidence, effect =>
            IsResourceEffect(effect, focusTea, "restore_resource", "sp"));
        GameIoMenuCall itemMenu = Assert.Single(io.Menus, menu => menu.Header == "Clean Battle Items");
        Assert.Equal(["Annex Tonic x1", "Focus Tea x1", "Back"], itemMenu.Options);
        io.AssertConsumed();
    }

    [Fact]
    public async Task PressTurnEventSink_UsesTypedStateAndIgnoresDisplayMessageWording()
    {
        using var output = new StringWriter();
        var tracker = new TrainingAnnexPressTurnTracker();
        RuntimeInstanceId actorId = RuntimeInstanceId.Parse("echo_adept");
        tracker.RecordBefore(
            actorId,
            Qualified("frost_tip"),
            1,
            0,
            ActionTurnConsumption.FromPressTurn(
                new PressTurnResolution(PressTurnOutcome.Weakness, false, false)));
        var sink = new TrainingAnnexPressTurnEventSink(new TextWriterEventSink(output), tracker);

        await sink.PublishAsync(new BattleEncounterEvent(
            1,
            BattleEncounterEventKind.TurnEconomyChanged,
            "Localized presentation text with no parseable icon counts.",
            actorId,
            TurnEconomyState: new PressTurnEconomySnapshot(0, 1)));

        TrainingAnnexPressTurnEvidence evidence = Assert.Single(tracker.Evidence);
        Assert.Equal(0, evidence.AfterFullIcons);
        Assert.Equal(1, evidence.AfterBlinkingIcons);
        Assert.Contains("Press Turn updated: 0 full, 1 blinking.", output.ToString(), StringComparison.Ordinal);
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
        string root = ContentRoot();
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
                RuntimeInstanceId.Parse("review_hall_trigger_ashling_1"),
                BattleStatusLifecycleEventKind.AilmentApplied,
                Qualified("sample_poison"),
                Qualified("toxin_touch")));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("review_hall_trigger_ashling_1") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.ResourceChanged &&
            evidence.RelatedContentId == ContentId.Parse("hp") &&
            evidence.Value == -2m);
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
                RuntimeInstanceId.Parse("review_hall_trigger_ashling_1"),
                BattleStatusLifecycleEventKind.AilmentApplied,
                Qualified("sample_stun"),
                Qualified("toxin_touch")));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("review_hall_trigger_ashling_1") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.TurnRestricted &&
            evidence.RelatedContentId == Qualified("sample_stun") &&
            evidence.TurnStartOutcome == BattleTurnStartOutcome.Skip);
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("review_hall_trigger_ashling_1") &&
            evidence.EventKind == BattleStatusLifecycleEventKind.AilmentRemoved &&
            evidence.RelatedContentId == Qualified("sample_stun"));
        Assert.DoesNotContain(summary.AiDecisionEvidence, evidence =>
            evidence.ActorInstanceId == RuntimeInstanceId.Parse("review_hall_trigger_ashling_1"));
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
                RuntimeInstanceId.Parse("echo_adept"),
                BattleStatusLifecycleEventKind.AilmentApplied,
                Qualified("sample_poison"),
                Qualified("toxin_touch")));
        Assert.Contains(summary.LifecycleEvidence, evidence =>
            IsLifecycle(
                evidence,
                RuntimeInstanceId.Parse("echo_adept"),
                BattleStatusLifecycleEventKind.AilmentRemoved,
                Qualified("sample_poison"),
                Qualified("clear_toxin")));
        Assert.DoesNotContain(summary.LifecycleEvidence, evidence =>
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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
            evidence.ActorId == RuntimeInstanceId.Parse("echo_adept") &&
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

    [Theory]
    [InlineData(true, RulesetBindingDiagnosticCode.MissingRuleset)]
    [InlineData(false, RulesetBindingDiagnosticCode.CategoryMismatch)]
    public async Task CleanTrainingAnnexPlay_InvalidEconomyBindingFailsBeforeSession(
        bool removeRuleset,
        RulesetBindingDiagnosticCode expectedCode)
    {
        var io = new ScriptedGameIO();
        using var output = new StringWriter();
        IContentPackTextSource source = removeRuleset
            ? new RulesetRemovingContentPackTextSource(ContentRoot(), "standard_economy")
            : new RulesetCategoryMutatingContentPackTextSource(ContentRoot(), "standard_economy", "damage");
        var host = CreateHost(io, output, source);

        int exitCode = await host.RunAsync();

        Assert.Equal(4, exitCode);
        Assert.Null(host.LastSummary);
        Assert.Empty(io.Menus);
        Assert.Contains($"[economy:{expectedCode}]", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, RulesetBindingDiagnosticCode.MissingRuleset)]
    [InlineData(false, RulesetBindingDiagnosticCode.CategoryMismatch)]
    public async Task CleanTrainingAnnexPlay_InvalidStockCapacityBindingFailsBeforeSession(
        bool removeRuleset,
        RulesetBindingDiagnosticCode expectedCode)
    {
        var io = new ScriptedGameIO();
        using var output = new StringWriter();
        IContentPackTextSource source = removeRuleset
            ? new RulesetRemovingContentPackTextSource(ContentRoot(), "standard_stock_capacity")
            : new RulesetCategoryMutatingContentPackTextSource(ContentRoot(), "standard_stock_capacity", "damage");
        var host = CreateHost(io, output, source);

        int exitCode = await host.RunAsync();

        Assert.Equal(4, exitCode);
        Assert.Null(host.LastSummary);
        Assert.Empty(io.Menus);
        Assert.Contains($"[stock_capacity:{expectedCode}]", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ShopBuysCatalogItemThroughBoundTransactions()
    {
        var io = new ScriptedGameIO().QueueMenu(11, 0, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexShopTransactionEvidence transaction = Assert.Single(summary.ShopTransactions);
        Assert.True(transaction.IsPurchase);
        Assert.Equal(Qualified("training_supply"), transaction.ShopId);
        Assert.Equal(Qualified("annex_tonic"), transaction.OfferId);
        Assert.Equal(ShopContentKind.Item, transaction.ContentKind);
        Assert.Equal(ResourceTransactionCode.Applied, transaction.Code);
        Assert.Equal(48, transaction.Price);
        Assert.Equal(100, transaction.WalletBefore);
        Assert.Equal(52, transaction.WalletAfter);
        Assert.Equal(1, transaction.OwnedCountBefore);
        Assert.Equal(2, transaction.OwnedCountAfter);
        Assert.Empty(summary.ShopEquipmentChanges);
        Assert.Equal(52, summary.Wallet.Balance);
        Assert.Equal(2, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.OpenShop,
                CleanTrainingAnnexPlayCommand.ShopBuy,
                CleanTrainingAnnexPlayCommand.SelectShopOffer,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);
        Assert.Contains(
            "Shop transaction: Bought Annex Tonic for 48 M; wallet 100->52; quantity 1->2.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ShopBuysAndEquipsCatalogEquipment()
    {
        var io = new ScriptedGameIO().QueueMenu(11, 0, 3, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexShopTransactionEvidence transaction = Assert.Single(summary.ShopTransactions);
        TrainingAnnexEquipmentChangeEvidence equipment = Assert.Single(summary.ShopEquipmentChanges);
        Assert.Equal(Qualified("padded_jacket"), transaction.OfferId);
        Assert.Equal(ShopContentKind.Equipment, transaction.ContentKind);
        Assert.Equal(86, transaction.Price);
        Assert.Equal(100, transaction.WalletBefore);
        Assert.Equal(14, transaction.WalletAfter);
        Assert.Equal(0, transaction.OwnedCountBefore);
        Assert.Equal(1, transaction.OwnedCountAfter);
        Assert.Equal(Qualified("padded_jacket"), equipment.EquipmentId);
        Assert.Equal(EquipmentSlot.Armor, equipment.Slot);
        Assert.True(equipment.Applied);
        Assert.Equal(ResourceTransactionCode.Applied, equipment.Code);
        Assert.Equal(14, summary.Wallet.Balance);
        Assert.Contains(Qualified("padded_jacket"), summary.Inventory.GetEquipmentIds(EquipmentSlot.Armor));
        Assert.Equal(Qualified("padded_jacket"), summary.Equipment.EquippedItemIds[EquipmentSlot.Armor]);
        Assert.Contains(EquipmentSlot.Armor, summary.EquipmentProfile.EquippedDefinitions.Keys);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.OpenShop,
                CleanTrainingAnnexPlayCommand.ShopBuy,
                CleanTrainingAnnexPlayCommand.SelectShopOffer,
                CleanTrainingAnnexPlayCommand.EquipPurchasedEquipment,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);

        GameIoMenuCall buyMenu = Assert.Single(io.Menus, menu => menu.Header == "Training Supply - Buy");
        Assert.Equal(
            [
                "Annex Tonic - 48 M",
                "Cleanse Drop - 38 M (stock 5)",
                "Practice Blade - 115 M [Already owned]",
                "Padded Jacket - 86 M",
                "Back"
            ],
            buyMenu.Options);
        Assert.Equal([false, false, true, false, false], buyMenu.DisabledOptions);
        string text = output.ToString();
        Assert.Contains(
            "Shop transaction: Bought Padded Jacket for 86 M; wallet 100->14; owned 0->1.",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "Equipped Padded Jacket in Armor; equipment profile now",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ShopInsufficientFundsAreDisabledWithoutMutation()
    {
        var io = new ScriptedGameIO().QueueMenu(11, 0, 4, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Empty(summary.ShopTransactions);
        Assert.Empty(summary.ShopEquipmentChanges);
        Assert.Equal(0, summary.Wallet.Balance);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.DoesNotContain(Qualified("padded_jacket"), summary.Inventory.GetEquipmentIds(EquipmentSlot.Armor));

        GameIoMenuCall buyMenu = Assert.Single(io.Menus, menu => menu.Header == "Training Supply - Buy");
        Assert.Equal(
            [
                "Annex Tonic - 48 M [Not enough Macca]",
                "Cleanse Drop - 38 M (stock 5) [Not enough Macca]",
                "Practice Blade - 115 M [Already owned]",
                "Padded Jacket - 86 M [Not enough Macca]",
                "Back"
            ],
            buyMenu.Options);
        Assert.Equal([true, true, true, true, false], buyMenu.DisabledOptions);
        Assert.Contains(
            "Shop purchase canceled; wallet and inventory are unchanged.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ShopReportsUnsupportedRuntimeOfferDiagnostics()
    {
        var io = new ScriptedGameIO().QueueMenu(11, 0, 4, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            new RuntimeUnsupportedShopOfferContentPackTextSource(ContentRoot()),
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        RuntimeShopOfferResolutionDiagnostic diagnostic = Assert.Single(summary.ShopOfferDiagnostics);
        Assert.Equal(RuntimeShopOfferResolutionCode.UnsupportedPricePolicy, diagnostic.Code);
        Assert.Equal(Qualified("annex_tonic"), diagnostic.ContentId);
        Assert.Empty(summary.ShopTransactions);
        Assert.Empty(summary.ShopEquipmentChanges);
        Assert.Equal(100, summary.Wallet.Balance);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));

        GameIoMenuCall buyMenu = Assert.Single(io.Menus, menu => menu.Header == "Training Supply - Buy");
        Assert.Equal(
            [
                "Annex Tonic - 48 M",
                "Cleanse Drop - 38 M (stock 5)",
                "Practice Blade - 115 M [Already owned]",
                "Padded Jacket - 86 M",
                "Back"
            ],
            buyMenu.Options);
        string text = output.ToString();
        Assert.Contains("Shop offer diagnostic: [UnsupportedPricePolicy]", text, StringComparison.Ordinal);
        Assert.Contains("annex_tonic", text, StringComparison.Ordinal);
        Assert.Contains(
            "Shop purchase canceled; wallet and inventory are unchanged.",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ShopSellsCatalogItemAndBlocksEquippedSale()
    {
        var io = new ScriptedGameIO().QueueMenu(11, 1, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexShopTransactionEvidence transaction = Assert.Single(summary.ShopTransactions);
        Assert.False(transaction.IsPurchase);
        Assert.Equal(Qualified("annex_tonic"), transaction.OfferId);
        Assert.Equal(ResourceTransactionCode.Applied, transaction.Code);
        Assert.Equal(27, transaction.Price);
        Assert.Equal(0, transaction.WalletBefore);
        Assert.Equal(27, transaction.WalletAfter);
        Assert.Equal(1, transaction.OwnedCountBefore);
        Assert.Equal(0, transaction.OwnedCountAfter);
        Assert.Equal(27, summary.Wallet.Balance);
        Assert.Equal(0, summary.Inventory.GetQuantity(Qualified("annex_tonic")));

        GameIoMenuCall sellMenu = Assert.Single(io.Menus, menu => menu.Header == "Training Supply - Sell");
        Assert.Equal(
            [
                "Annex Tonic - 27 M (owned 1)",
                "Practice Blade - 64 M (owned) [Equipped]",
                "Back"
            ],
            sellMenu.Options);
        Assert.Equal([false, true, false], sellMenu.DisabledOptions);
        Assert.Contains(
            "Shop transaction: Sold Annex Tonic for 27 M; wallet 0->27; quantity 1->0.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RecoveryFacilityRestoresResourcesAndSpendsWallet()
    {
        var io = new ScriptedGameIO().QueueMenu(3, 12, 0, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(20));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        TrainingAnnexHospitalRestorationEvidence restoration = Assert.Single(summary.HospitalRestorations);
        Assert.Equal(RuntimeInstanceId.Parse("echo_adept"), restoration.PatientId);
        Assert.Equal(ResourceTransactionCode.Applied, restoration.Code);
        Assert.Equal(10, restoration.Cost);
        Assert.Equal(20, restoration.WalletBefore);
        Assert.Equal(10, restoration.WalletAfter);
        Assert.Equal(70, restoration.HpBefore);
        Assert.Equal(80, restoration.HpAfter);
        Assert.Equal(80, restoration.MaxHp);
        Assert.Equal(28, restoration.SpBefore);
        Assert.Equal(28, restoration.SpAfter);
        Assert.Equal(28, restoration.MaxSp);
        Assert.False(restoration.HadAilmentBefore);
        Assert.False(restoration.HasAilmentAfter);
        Assert.False(restoration.HadEncounterPersistenceBefore);
        Assert.False(restoration.HasEncounterPersistenceAfter);
        Assert.Equal(10, summary.Wallet.Balance);
        Assert.Equal(80, Resource(summary, "hp").Current);
        Assert.Equal(
            [
                CleanTrainingAnnexPlayCommand.RecalculateResources,
                CleanTrainingAnnexPlayCommand.OpenRecoveryFacility,
                CleanTrainingAnnexPlayCommand.RecoveryTreat,
                CleanTrainingAnnexPlayCommand.Exit
            ],
            summary.Commands);

        GameIoMenuCall recoveryMenu = Assert.Single(io.Menus, menu => menu.Header == "Recovery Facility");
        Assert.Equal(
            [
                "Treat Echo Adept - 10 M",
                "Back"
            ],
            recoveryMenu.Options);
        Assert.Equal([false, false], recoveryMenu.DisabledOptions);
        Assert.Contains(
            "Recovery complete: Echo Adept; HP 70->80/80; SP 28->28/28; wallet 20->10.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task TrainingAnnexRecoveryFacility_CleansLiveAilmentsAndEncounterPersistence()
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();
        TrainingAnnexRuntimeActor player = TrainingAnnexHostSupport.CreateActorRoster(catalog)
            .RequireRoster()
            .Player;
        RuntimeActorState state = player.Actor.State;
        state.SetResource(StandardProgressionIds.Hp, 70);
        state.ApplyAilment(catalog.GetRequiredAilment(Qualified("sample_poison")), Turns(3));
        state.SetGuarding(true);
        state.ChangeStatStage(ContentId.Parse("attack"), 1, Turns(1));
        state.GrantCharge(ChargeKind.Physical, 2m, Turns(1));
        state.GrantShield(ShieldKind.Physical, Turns(1));
        state.OverrideAffinity(DamageElement.Fire, ElementalAffinity.Null, Turns(1));
        state.AddOtherStatus(ContentId.Parse("training_annex_recovery_mark"), Turns(1));
        var commands = new List<CleanTrainingAnnexPlayCommand>();
        var io = new ScriptedGameIO().QueueMenu(0);
        using var output = new StringWriter();
        var controller = new TrainingAnnexRecoveryFacilityController(
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io));

        TrainingAnnexRecoveryFacilityResult result = await controller.OpenAsync(
            new HospitalRestorationService(),
            player,
            new RuntimeWalletSnapshot(20),
            commands,
            CancellationToken.None);

        TrainingAnnexHospitalRestorationEvidence restoration = Assert.Single(result.Restorations);
        Assert.Equal(ResourceTransactionCode.Applied, restoration.Code);
        Assert.True(restoration.HadAilmentBefore);
        Assert.False(restoration.HasAilmentAfter);
        Assert.True(restoration.HadEncounterPersistenceBefore);
        Assert.False(restoration.HasEncounterPersistenceAfter);
        Assert.Equal(10, result.Wallet.Balance);
        Assert.Equal(80, state.GetRequiredResource(StandardProgressionIds.Hp).Current);
        Assert.Empty(state.Ailments);
        Assert.False(state.IsGuarding);
        Assert.Empty(state.StatStages);
        Assert.Empty(state.Charges);
        Assert.Empty(state.Shields);
        Assert.Empty(state.AffinityOverrides);
        Assert.Empty(state.OtherStatuses);
        Assert.Equal([CleanTrainingAnnexPlayCommand.RecoveryTreat], commands);
        Assert.Contains(
            "Recovery complete: Echo Adept; HP 70->80/80; SP 28->28/28; wallet 20->10.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RecoveryFacilityInsufficientFundsAreDisabledWithoutMutation()
    {
        var io = new ScriptedGameIO().QueueMenu(3, 12, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Empty(summary.HospitalRestorations);
        Assert.Equal(0, summary.Wallet.Balance);
        Assert.Equal(70, Resource(summary, "hp").Current);

        GameIoMenuCall recoveryMenu = Assert.Single(io.Menus, menu => menu.Header == "Recovery Facility");
        Assert.Equal(
            [
                "Treat Echo Adept - 10 M [Not enough Macca]",
                "Back"
            ],
            recoveryMenu.Options);
        Assert.Equal([true, false], recoveryMenu.DisabledOptions);
        Assert.Contains(
            "Recovery canceled; wallet and actor state are unchanged.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RecoveryFacilityNoRestorationNeededIsDisabled()
    {
        var io = new ScriptedGameIO().QueueMenu(12, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(20));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Empty(summary.HospitalRestorations);
        Assert.Equal(20, summary.Wallet.Balance);
        Assert.Equal(80, Resource(summary, "hp").Current);

        GameIoMenuCall recoveryMenu = Assert.Single(io.Menus, menu => menu.Header == "Recovery Facility");
        Assert.Equal(
            [
                "Treat Echo Adept - 0 M [No restoration needed]",
                "Back"
            ],
            recoveryMenu.Options);
        Assert.Equal([true, false], recoveryMenu.DisabledOptions);
        io.AssertConsumed();
    }

    [Fact]
    public void CleanTrainingAnnexShell_DoesNotReferenceLegacyEffectInputs()
    {
        string root = Path.Combine(
            FindRepositoryRoot(),
            "samples",
            "Convergence.DemoHost",
            "Hosts",
            "TrainingAnnex");
        string[] banned =
        [
            "SkillData",
            "ItemData",
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
                CleanTrainingAnnexPlayCommand.SelectFieldItem,
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
    public async Task CleanTrainingAnnexPlay_FieldInventoryExecutesSelectedCatalogItemFromSnapshot()
    {
        ContentId annexTonic = Qualified("annex_tonic");
        ContentId focusTea = Qualified("focus_tea");
        var io = new ScriptedGameIO().QueueMenu(4, 3, 8, 0, 0, 7, 1, 0, 9);
        using var output = new StringWriter();
        var initialInventory = new RuntimeInventorySnapshot(
        [
            KeyValuePair.Create(annexTonic, 1),
            KeyValuePair.Create(focusTea, 1)
        ]);
        var host = CreateHost(io, output, initialInventory: initialInventory);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(1, summary.Inventory.GetQuantity(annexTonic));
        Assert.Equal(0, summary.Inventory.GetQuantity(focusTea));
        Assert.Equal([Qualified("mend"), focusTea], summary.ExecutedFieldActionIds);
        Assert.Equal(80, Assert.Single(summary.PlayerResources, resource => resource.ResourceId == ContentId.Parse("hp")).Current);
        Assert.Equal(28, Assert.Single(summary.PlayerResources, resource => resource.ResourceId == ContentId.Parse("sp")).Current);
        GameIoMenuCall itemMenu = Assert.Single(io.Menus, menu => menu.Header == "Clean Inventory");
        Assert.Equal(["Annex Tonic x1", "Focus Tea x1", "Back"], itemMenu.Options);
        string text = output.ToString();
        Assert.Contains("Inventory: Annex Tonic x1, Focus Tea x1.", text, StringComparison.Ordinal);
        Assert.Contains(
            "Field action executed: Focus Tea; HP 80->80/80; SP 26->28/28; inventory convergence.training_annex_slice:focus_tea x0.",
            text,
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_FieldInventoryOmitsNonUsableAndEmptyItems()
    {
        var io = new ScriptedGameIO().QueueMenu(7, 1, 9);
        using var output = new StringWriter();
        var initialInventory = new RuntimeInventorySnapshot(
        [
            KeyValuePair.Create(Qualified("annex_tonic"), 1),
            KeyValuePair.Create(Qualified("focus_tea"), 0),
            KeyValuePair.Create(Qualified("training_badge"), 1)
        ]);
        var host = CreateHost(io, output, initialInventory: initialInventory);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("training_badge")));
        Assert.Empty(summary.ExecutedFieldActionIds);
        GameIoMenuCall itemMenu = Assert.Single(io.Menus, menu => menu.Header == "Clean Inventory");
        Assert.Equal(["Annex Tonic x1", "Back"], itemMenu.Options);
        Assert.Contains("Inventory: Annex Tonic x1, Training Badge x1.", output.ToString(), StringComparison.Ordinal);
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
            "Field action executed: Mend; HP 70->80/80; SP 28->26/28; inventory unchanged.",
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
        Assert.Equal(0, summary.Wallet.Balance);
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
    public async Task CleanTrainingAnnexPlay_ManualLoadRejectsActorEntityMismatchBeforeMutation()
    {
        RuntimeSaveRecord record = await CreateTrainingAnnexSaveRecordAsync(snapshot =>
        {
            RuntimeInstanceId playerId = RuntimeInstanceId.Parse("echo_adept");
            RuntimeActorSnapshot[] actors = snapshot.Actors
                .Select(actor => actor.Identity.InstanceId == playerId
                    ? CopyActor(actor, entityId: Qualified("ashling"))
                    : actor)
                .ToArray();
            RuntimeActorReferenceSnapshot playerReference = Reference(
                Assert.Single(actors, actor => actor.Identity.InstanceId == playerId));
            RuntimePartyStockSnapshot party = new(
                playerReference,
                snapshot.PartyStock.OwnerLevel,
                activeParty: snapshot.PartyStock.ActiveParty.Select(reference =>
                    reference.InstanceId == playerId ? playerReference : reference),
                reserveMembers: snapshot.PartyStock.ReserveMembers,
                activeForm: snapshot.PartyStock.ActiveForm,
                personaStock: snapshot.PartyStock.PersonaStock,
                demonStock: snapshot.PartyStock.DemonStock,
                maxActivePartySize: snapshot.PartyStock.MaxActivePartySize);
            return CopySave(snapshot, actors: actors, partyStock: party);
        });
        var slots = new TrainingAnnexSaveSlotStore();
        slots.Save(record);
        var io = new ScriptedGameIO().QueueMenu(10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, saveSlots: slots);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.Equal(1, summary.SaveDiagnosticCount);
        Assert.Equal(Qualified("echo_adept"), summary.PlayerEntityId);
        Assert.Equal(1, summary.Inventory.GetQuantity(Qualified("annex_tonic")));
        Assert.Contains(
            "Manual load rejected: Saved actor 'echo_adept' has entity 'convergence.training_annex_slice:ashling', expected 'convergence.training_annex_slice:echo_adept' for Player.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ManualLoadRejectsEnemyInSavedPartyStockBeforeMutation()
    {
        RuntimeSaveRecord record = await CreateTrainingAnnexSaveRecordAsync(snapshot =>
        {
            RuntimeActorReferenceSnapshot enemyReference = Reference(
                Assert.Single(snapshot.Actors, actor =>
                    actor.Identity.InstanceId == RuntimeInstanceId.Parse("enemy_ashling")));
            RuntimePartyStockSnapshot corruptedParty = new(
                snapshot.PartyStock.Owner,
                snapshot.PartyStock.OwnerLevel,
                activeParty: snapshot.PartyStock.ActiveParty,
                reserveMembers: [enemyReference],
                maxActivePartySize: snapshot.PartyStock.MaxActivePartySize);

            return CopySave(snapshot, partyStock: corruptedParty);
        });
        var slots = new TrainingAnnexSaveSlotStore();
        slots.Save(record);
        var io = new ScriptedGameIO().QueueMenu(10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, saveSlots: slots);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.Equal(1, summary.SaveDiagnosticCount);
        Assert.Equal(
            [RuntimeInstanceId.Parse("support_annex_mentor")],
            summary.PartyStock.ReserveMembers.Select(actor => actor.InstanceId));
        Assert.Contains(
            "Manual load rejected: Saved reserve party actor 'enemy_ashling' belongs to team 'enemy_team', expected 'player_team'.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ManualLoadRejectsEnemyInSavedDemonStockBeforeMutation()
    {
        RuntimeSaveRecord record = await CreateTrainingAnnexSaveRecordAsync(snapshot =>
        {
            RuntimeActorReferenceSnapshot enemyReference = Reference(
                Assert.Single(snapshot.Actors, actor =>
                    actor.Identity.InstanceId == RuntimeInstanceId.Parse("enemy_ashling")));
            RuntimePartyStockSnapshot corruptedParty = snapshot.PartyStock.With(
                demonStock: [enemyReference]);

            return CopySave(snapshot, partyStock: corruptedParty);
        });
        var slots = new TrainingAnnexSaveSlotStore();
        slots.Save(record);
        var io = new ScriptedGameIO().QueueMenu(10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, saveSlots: slots);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.Equal(1, summary.SaveDiagnosticCount);
        Assert.Equal(
            [RuntimeInstanceId.Parse("demon_ashling"), RuntimeInstanceId.Parse("demon_ward_shell")],
            summary.PartyStock.DemonStock.Select(actor => actor.InstanceId));
        Assert.Contains(
            "Manual load rejected: Saved Demon stock actor 'enemy_ashling' belongs to team 'enemy_team', expected 'player_team'.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ManualLoadRejectsSavedContextThatWasNotSaveEligible()
    {
        RuntimeSaveRecord record = await CreateTrainingAnnexSaveRecordAsync(
            snapshot => snapshot,
            new RuntimeSaveContextSnapshot(TrainingAnnexHostSupport.BattleSaveContext));
        var slots = new TrainingAnnexSaveSlotStore();
        slots.Save(record);
        var io = new ScriptedGameIO().QueueMenu(10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, saveSlots: slots);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.Equal(1, summary.SaveDiagnosticCount);
        Assert.Contains(
            "Manual load rejected [SavedContextNotAllowed]: Save record context 'battle' is not allowed for kind 'Manual'.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ManualLoadRejectsInvalidTrainingAnnexDungeonStateBeforeMutation()
    {
        ContentId missingNode = Qualified("missing_node");
        RuntimeSaveRecord record = await CreateTrainingAnnexSaveRecordAsync(snapshot =>
            CopySave(
                snapshot,
                field: new RuntimeFieldSnapshot(
                    new RuntimeNavigationSnapshot(TrainingAnnexHostSupport.TrainingAnnexEntrance),
                    new RuntimeDungeonTraversalSnapshot(
                        TrainingAnnexHostSupport.TrainingAnnexDungeon,
                        missingNode))));
        var slots = new TrainingAnnexSaveSlotStore();
        slots.Save(record);
        var io = new ScriptedGameIO().QueueMenu(10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, saveSlots: slots);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.True(summary.SaveDiagnosticCount >= 1);
        Assert.Equal(Qualified("staging_area"), summary.FinalLocationId);
        Assert.Contains(
            "Manual load rejected: Saved dungeon node 'convergence.training_annex_slice:missing_node' is not recognized by the Training Annex host.",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_ManualLoadRejectsContentPackVersionMismatch()
    {
        RuntimeSaveRecord record = await CreateTrainingAnnexSaveRecordAsync(snapshot =>
            CopySave(
                snapshot,
                contentPacks:
                [
                    new ContentPackIdentity(
                        TrainingAnnexHostSupport.PackId,
                        SemanticVersion.Parse("9.9.9"))
                ]));
        var slots = new TrainingAnnexSaveSlotStore();
        slots.Save(record);
        var io = new ScriptedGameIO().QueueMenu(10, 1, 9);
        using var output = new StringWriter();
        var host = CreateHost(io, output, saveSlots: slots);

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        Assert.Equal(0, summary.ManualLoadCount);
        Assert.True(summary.HasManualSave);
        Assert.Equal(1, summary.SaveDiagnosticCount);
        Assert.Contains(
            "Manual load rejected [ContentPackVersionMismatch] $.contentPacks[0].version",
            output.ToString(),
            StringComparison.Ordinal);
        io.AssertConsumed();
    }

    [Fact]
    public async Task TrainingAnnexPersistenceController_BuildsSaveSnapshotWithHostProgressFlags()
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();
        TrainingAnnexActorRoster roster = TrainingAnnexHostSupport.CreateActorRoster(catalog).RequireRoster();
        RuntimePartyStockSnapshot partyStock = new TrainingAnnexPartyController()
            .CreateInitialParty(roster)
            .Snapshot;
        RuntimeSaveGameSnapshot snapshot = TrainingAnnexPersistenceController.BuildCurrentSaveSnapshot(
            roster,
            partyStock,
            new RuntimeFieldSnapshot(new RuntimeNavigationSnapshot(TrainingAnnexHostSupport.TrainingAnnexEntrance)),
            new RuntimeKnowledgeSnapshot(),
            new RuntimeInventorySnapshot([new KeyValuePair<ContentId, int>(Qualified("annex_tonic"), 1)]),
            new RuntimeWalletSnapshot(50),
            new RuntimeSessionProgressSnapshot(),
            encounterTriggerConsumed: true,
            preparedBattleStarted: true,
            preparedBattleOutcome: BattleEncounterOutcome.Victory,
            preparedBattleWinningTeamId: TrainingAnnexHostSupport.PlayerTeam);

        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(snapshot, catalog);

        Assert.True(validation.IsValid);
        Assert.Equal(RuntimeSaveGameSnapshot.CurrentContractVersion, snapshot.ContractVersion);
        Assert.Equal(
            [RuntimeInstanceId.Parse("echo_adept")],
            snapshot.PartyStock.ActiveParty.Select(actor => actor.InstanceId));
        Assert.Equal(
            [RuntimeInstanceId.Parse("support_annex_mentor")],
            snapshot.PartyStock.ReserveMembers.Select(actor => actor.InstanceId));
        Assert.NotNull(snapshot.PartyStock.ActiveForm);
        RuntimeActorReferenceSnapshot activeForm = snapshot.PartyStock.ActiveForm!;
        Assert.Equal(RuntimeInstanceId.Parse("form_annex_mentor"), activeForm.InstanceId);
        Assert.Equal(
            [RuntimeInstanceId.Parse("persona_bramble_runner")],
            snapshot.PartyStock.PersonaStock.Select(actor => actor.InstanceId));
        Assert.Equal(
            [RuntimeInstanceId.Parse("demon_ashling"), RuntimeInstanceId.Parse("demon_ward_shell")],
            snapshot.PartyStock.DemonStock.Select(actor => actor.InstanceId));
        Assert.Equal("True", snapshot.HostContext[ContentId.Parse("ashling_trigger_consumed")]);
        Assert.Equal("True", snapshot.HostContext[ContentId.Parse("prepared_battle_started")]);
        Assert.Equal("Victory", snapshot.HostContext[ContentId.Parse("prepared_battle_outcome")]);
        Assert.Equal(
            TrainingAnnexHostSupport.PlayerTeam.ToString(),
            snapshot.HostContext[ContentId.Parse("prepared_battle_winning_team")]);
    }

    [Fact]
    public async Task TrainingAnnexFieldPresenter_PreservesNavigationAndDungeonMessages()
    {
        using var output = new StringWriter();
        var presenter = new TrainingAnnexFieldPresenter(new TextWriterEventSink(output));
        var navigation = new RuntimeNavigationService(new TrainingAnnexNavigationPolicy());
        var dungeonTraversal = new RuntimeDungeonTraversalService(new TrainingAnnexDungeonPolicy());
        RuntimeFieldSnapshot field = new(new RuntimeNavigationSnapshot(TrainingAnnexHostSupport.StagingArea));

        field = await presenter.ApplyNavigationAsync(
            field,
            navigation.Navigate(field.Navigation, TrainingAnnexHostSupport.EnterTrainingAnnexTransition),
            "entered Training Annex",
            CancellationToken.None);
        field = new RuntimeFieldSnapshot(
            field.Navigation,
            new RuntimeDungeonTraversalSnapshot(
                TrainingAnnexHostSupport.TrainingAnnexDungeon,
                TrainingAnnexHostSupport.TrainingAnnexEntrance));
        field = await presenter.ApplyDungeonTraversalAsync(
            field,
            dungeonTraversal.Traverse(
                TrainingAnnexFieldPresenter.RequireDungeonTraversal(field),
                TrainingAnnexHostSupport.EnterReviewHallTransition),
            CancellationToken.None);

        Assert.Equal(TrainingAnnexHostSupport.TrainingAnnexEntrance, field.Navigation.CurrentLocationId);
        Assert.Equal(TrainingAnnexHostSupport.ReviewHall, field.DungeonTraversal?.CurrentNodeId);
        string text = output.ToString();
        Assert.Contains(
            $"Field navigation: entered Training Annex; location Training Annex Entrance ({TrainingAnnexHostSupport.TrainingAnnexEntrance}).",
            text,
            StringComparison.Ordinal);
        Assert.Contains("Dungeon traversal: Training Annex Entrance -> Review Hall.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrainingAnnexBattleRewardApplicator_RejectedWalletDoesNotMutateProgression()
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();
        TrainingAnnexActorRoster roster = TrainingAnnexHostSupport.CreateActorRoster(catalog).RequireRoster();
        GrowthRulesetServices growthServices = new RuntimeRulesetBindingResolver()
            .BindGrowthServices(catalog, TrainingAnnexHostSupport.Qualified("standard_growth"))
            .RequireService();
        RuntimeActorSnapshot before = roster.Player.Actor.State.ToSnapshot();
        var reward = new BattleRewardResult(100, 50);
        using var output = new StringWriter();
        var applicator = new TrainingAnnexBattleRewardApplicator(
            new TextWriterEventSink(output),
            new TrainingAnnexMinimumRandomSource());

        TrainingAnnexBattleRewardApplication result = await applicator.ApplyAsync(
            roster.Player,
            reward,
            growthServices,
            new RejectingEconomyTransactionService(),
            new RuntimeWalletSnapshot(0),
            CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(before.Progression, roster.Player.Actor.State.ToSnapshot().Progression);
        Assert.Equal(0, result.Wallet.Balance);
        Assert.False(result.WalletTransaction.Applied);
        Assert.Equal(ResourceTransactionCode.InsufficientCurrency, result.WalletTransaction.Code);
        Assert.Same(result.WalletTransaction.Before, result.WalletTransaction.After);
        Assert.Contains("[InsufficientCurrency]: blocked for test", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CleanTrainingAnnexPlay_RewardAddsToInjectedWalletThroughBoundEconomy()
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
        var host = CreateHost(
            io,
            output,
            initialWallet: new RuntimeWalletSnapshot(100));

        int exitCode = await host.RunAsync();

        Assert.Equal(0, exitCode);
        CleanTrainingAnnexPlaySummary summary = Assert.IsType<CleanTrainingAnnexPlaySummary>(host.LastSummary);
        WalletTransactionResult transaction = Assert.IsType<WalletTransactionResult>(
            summary.AppliedWalletTransaction);
        Assert.True(transaction.Applied);
        Assert.Equal(100, transaction.Before.Balance);
        Assert.Equal(114, transaction.After.Balance);
        Assert.Equal(114, summary.Wallet.Balance);
        Assert.Contains("wallet 100->114", output.ToString(), StringComparison.Ordinal);
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
        Assert.Equal(14, summary.Wallet.Balance);
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

    private static ContentId Id(string value) => ContentId.Parse(value);

    private static StatResolutionResult Resolved(CleanTrainingAnnexPlaySummary summary, string statId) =>
        Assert.Single(summary.PlayerResolvedStats, result => result.StatId == ContentId.Parse(statId));

    private static RuntimeResourceSnapshot Resource(CleanTrainingAnnexPlaySummary summary, string resourceId) =>
        Assert.Single(summary.PlayerResources, resource => resource.ResourceId == ContentId.Parse(resourceId));

    private static TurnDurationDefinition Turns(int value) =>
        new(value, ContentId.Parse("owner_turn_end"), false);

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
        RuntimeInstanceId actorId,
        BattleStatusLifecycleEventKind kind,
        ContentId relatedId,
        ContentId sourceActionId) =>
        evidence.ActorId == actorId &&
        evidence.EventKind == kind &&
        evidence.RelatedContentId == relatedId &&
        evidence.SourceActionId == sourceActionId;

    private static async Task<GameDataCatalog> LoadTrainingAnnexCatalogAsync()
    {
        string root = ContentRoot();
        var source = new RecordingContentPackTextSource(root);
        ContentPackTextBundle bundle = await source.ReadAsync(TrainingAnnexHostSupport.CreateContentRequest());
        CatalogLoadResult load = new SkillSystemCatalogLoader().Load(
            new SkillSystemCatalogLoadRequest(TrainingAnnexHostSupport.BuildRegistrations(), [bundle]));
        return load.RequireCatalog();
    }

    private static async Task<RuntimeSaveRecord> CreateTrainingAnnexSaveRecordAsync(
        Func<RuntimeSaveGameSnapshot, RuntimeSaveGameSnapshot> mutate,
        RuntimeSaveContextSnapshot? context = null)
    {
        GameDataCatalog catalog = await LoadTrainingAnnexCatalogAsync();
        TrainingAnnexActorRoster roster = TrainingAnnexHostSupport.CreateActorRoster(catalog).RequireRoster();
        RuntimePartyStockSnapshot partyStock = new TrainingAnnexPartyController()
            .CreateInitialParty(roster)
            .Snapshot;
        RuntimeSaveGameSnapshot snapshot = TrainingAnnexHostSupport.BuildStartupSaveSnapshot(
            roster,
            partyStock,
            new RuntimeFieldSnapshot(new RuntimeNavigationSnapshot(TrainingAnnexHostSupport.StagingArea)),
            new RuntimeKnowledgeSnapshot(),
            new RuntimeInventorySnapshot(
                [new KeyValuePair<ContentId, int>(Qualified("annex_tonic"), 1)]),
            new RuntimeWalletSnapshot(0),
            new RuntimeSessionProgressSnapshot());
        return new RuntimeSaveRecord(
            RuntimeSaveKind.Manual,
            mutate(snapshot),
            context ?? new RuntimeSaveContextSnapshot(TrainingAnnexHostSupport.FieldMenuSaveContext));
    }

    private static RuntimeSaveGameSnapshot CopySave(
        RuntimeSaveGameSnapshot snapshot,
        IEnumerable<RuntimeActorSnapshot>? actors = null,
        RuntimeFieldSnapshot? field = null,
        IEnumerable<ContentPackIdentity>? contentPacks = null,
        RuntimePartyStockSnapshot? partyStock = null) =>
        new(
            snapshot.FrameworkVersion,
            contentPacks ?? snapshot.ContentPacks,
            actors ?? snapshot.Actors,
            partyStock ?? snapshot.PartyStock,
            snapshot.Inventory,
            snapshot.Equipment,
            snapshot.Wallet,
            field ?? snapshot.Field,
            snapshot.Compendium,
            snapshot.Knowledge,
            snapshot.Session,
            snapshot.Checkpoints,
            snapshot.HostContext,
            snapshot.ContractVersion);

    private static RuntimeActorSnapshot CopyActor(
        RuntimeActorSnapshot actor,
        ContentId? entityId = null,
        ContentId? actorKindId = null,
        RuntimeActorOwnershipSnapshot? ownership = null) =>
        new(
            new RuntimeActorIdentitySnapshot(
                actor.Identity.InstanceId,
                entityId ?? actor.Identity.EntityDefinitionId,
                actorKindId ?? actor.Identity.ActorKindId,
                actor.Identity.DisplayName,
                actor.Identity.DisplaySubtitle),
            ownership ?? actor.Ownership,
            actor.Deployment,
            actor.Progression,
            actor.Resources,
            actor.Stats,
            actor.Skills,
            actor.Forms,
            actor.Equipment,
            actor.BattleStatus,
            actor.BattleActivations,
            actor.BaseResourceValues,
            actor.VitalResourceId,
            actor.CapabilityIds);

    private static RuntimeActorReferenceSnapshot Reference(RuntimeActorSnapshot actor) =>
        new(actor.Identity.InstanceId, actor.Identity.EntityDefinitionId, actor.Identity.DisplayName);

    private static CleanTrainingAnnexPlayHost CreateHost(
        ScriptedGameIO io,
        StringWriter output,
        IContentPackTextSource? source = null,
        IRandomSource? randomSource = null,
        TrainingAnnexSaveSlotStore? saveSlots = null,
        RuntimeInventorySnapshot? initialInventory = null,
        RuntimeEquipmentSnapshot? initialEquipment = null,
        RuntimeWalletSnapshot? initialWallet = null) =>
        new(
            source ?? new RecordingContentPackTextSource(ContentRoot()),
            new TextWriterEventSink(output),
            new ConsoleHostCommandSource<CleanTrainingAnnexPlayCommand>(io),
            randomSource,
            saveSlots,
            initialInventory,
            initialEquipment,
            initialWallet);

    private static string ContentRoot() => Path.Combine(AppContext.BaseDirectory, "Content");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Convergence.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not find Convergence.sln.");
    }

    private sealed class RejectingEconomyTransactionService : IEconomyTransactionService
    {
        public WalletTransactionResult Credit(RuntimeWalletSnapshot snapshot, int amount) =>
            new(
                ResourceTransactionCode.InsufficientCurrency,
                snapshot,
                snapshot,
                [new ResourceTransactionDiagnostic(ResourceTransactionCode.InsufficientCurrency, "blocked for test")]);

        public WalletTransactionResult Debit(RuntimeWalletSnapshot snapshot, int amount) =>
            new(
                ResourceTransactionCode.InsufficientCurrency,
                snapshot,
                snapshot,
                [new ResourceTransactionDiagnostic(ResourceTransactionCode.InsufficientCurrency, "blocked for test")]);
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

    private sealed class EquipmentAddingContentPackTextSource(string root) : IContentPackTextSource
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
                documents.Add(new ContentDocumentText(
                    path,
                    path,
                    path == "training_annex_slice.equipment.json"
                        ? AddWeightedClub(text)
                        : text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }

        private static string AddWeightedClub(string json)
        {
            JsonNode node = JsonNode.Parse(json) ??
                throw new InvalidOperationException("Training Annex equipment JSON could not be parsed.");
            JsonArray equipment = node["equipment"]?.AsArray() ??
                throw new InvalidOperationException("Training Annex equipment JSON must contain equipment.");
            equipment.Add(new JsonObject
            {
                ["id"] = "weighted_club",
                ["displayName"] = "Weighted Club",
                ["description"] = "Test-only alternate weapon.",
                ["slot"] = "weapon",
                ["baseValue"] = 1,
                ["weapon"] = new JsonObject
                {
                    ["basicAttack"] = new JsonObject
                    {
                        ["element"] = "physical",
                        ["power"] = 4,
                        ["accuracy"] = 88,
                        ["isLongRange"] = false
                    }
                }
            });
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private sealed class RuntimeUnsupportedShopOfferContentPackTextSource(string root) : IContentPackTextSource
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
                documents.Add(new ContentDocumentText(
                    path,
                    path,
                    path == "training_annex_slice.shops.json"
                        ? AddUnsupportedRuntimeOffer(text)
                        : text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }

        private static string AddUnsupportedRuntimeOffer(string json)
        {
            JsonObject node = JsonNode.Parse(json)?.AsObject() ??
                throw new InvalidOperationException("Training Annex shops JSON could not be parsed.");
            JsonArray offers = node["shops"]?[0]?["offers"]?.AsArray() ??
                throw new InvalidOperationException("Training Annex shop offers were not found.");
            offers.Add(new JsonObject
            {
                ["contentKind"] = "item",
                ["contentId"] = "annex_tonic",
                ["price"] = new JsonObject
                {
                    ["kind"] = "policy",
                    ["pricingPolicyId"] = "standard_economy"
                },
                ["stock"] = new JsonObject
                {
                    ["kind"] = "unlimited"
                }
            });
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
    }

    private sealed class NegotiationDemandAmountContentPackTextSource(string root, int amount) : IContentPackTextSource
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
                documents.Add(new ContentDocumentText(
                    path,
                    path,
                    path == "training_annex_slice.negotiations.json"
                        ? ReplaceDemandAmount(text)
                        : text));
            }

            return new ContentPackTextBundle(request.ManifestPath, manifest, documents);
        }

        private string ReplaceDemandAmount(string json)
        {
            JsonObject node = JsonNode.Parse(json)?.AsObject() ??
                throw new InvalidOperationException("Training Annex negotiation JSON could not be parsed.");
            JsonArray negotiations = node["negotiations"]?.AsArray() ??
                throw new InvalidOperationException("Training Annex negotiations document has no negotiations array.");
            JsonObject negotiation = negotiations
                .Select(child => child?.AsObject())
                .Single(child => child?["id"]?.GetValue<string>() == "steady_sample") ??
                throw new InvalidOperationException("Training Annex steady_sample negotiation was not found.");
            JsonObject demand = negotiation["demands"]?.AsArray()[0]?.AsObject() ??
                throw new InvalidOperationException("Training Annex steady_sample negotiation has no demand.");
            JsonObject parameters = demand["parameters"]?.AsObject() ??
                throw new InvalidOperationException("Training Annex steady_sample demand has no parameters.");
            parameters["amount"] = amount;
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
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

    private sealed class MultiEnemyAshlingDrillContentSource(string root) : IContentPackTextSource
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
                if (path.EndsWith(".encounters.json", StringComparison.Ordinal))
                {
                    JsonObject document = JsonNode.Parse(text)?.AsObject() ??
                        throw new InvalidOperationException("Training Annex encounters JSON could not be parsed.");
                    JsonObject encounter = document["encounters"]?.AsArray()
                        .Select(node => node?.AsObject())
                        .Single(node => node?["id"]?.GetValue<string>() == "ashling_drill") ??
                        throw new InvalidOperationException("Ashling Drill was not found.");
                    JsonArray members = encounter["formations"]?[0]?["members"]?.AsArray() ??
                        throw new InvalidOperationException("Ashling Drill members were not found.");
                    members.Add(new JsonObject
                    {
                        ["entityId"] = "bramble_runner",
                        ["level"] = 3,
                        ["count"] = 1
                    });
                    text = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
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
