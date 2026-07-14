using System;
using System.Collections.Generic;
using System.Linq;
using Convergence.Tests.TestSupport;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Entities.Components;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Field;
using JRPGPrototype.Logic.Field.State;
using JRPGPrototype.Logic.Fusion;
using JRPGPrototype.Logic.Fusion.Bridges;
using JRPGPrototype.Logic.Fusion.Messaging;
using Xunit;

namespace Convergence.Tests.Host;

[Collection(LegacyBaselineSupport.CollectionName)]
public sealed class FusionCompendiumPresentationTests
{
    [Fact]
    public void CathedralMainMenuDetailed_PreservesOrderingFullMoonGateAndLegacyWrapper()
    {
        var io = new ScriptedGameIO().QueueMenu(-1, 1);
        var bridge = new CathedralUIBridge(io, new FieldUIState(), new CompendiumRegistry(io));

        CathedralMainMenuPresentationResult fullMoon = bridge.ShowCathedralMainMenuDetailed(8);
        CathedralMainMenuPresentationResult normalMoon = bridge.ShowCathedralMainMenuDetailed(0);

        Assert.Equal(FusionPresentationResultKind.Back, fullMoon.Kind);
        Assert.Equal(FusionMenuResultKind.Back, fullMoon.LegacyResult.Kind);
        Assert.Equal(
            ["Binary Fusion", "Sacrificial Fusion", "Browse Compendium", "Register Demon", "Back"],
            fullMoon.Options);
        Assert.Contains(FusionMainMenuAction.SacrificialFusion, fullMoon.Actions);

        Assert.Equal(FusionPresentationResultKind.Selected, normalMoon.Kind);
        Assert.Equal(FusionMainMenuAction.BrowseCompendium, normalMoon.LegacyResult.Action);
        Assert.Equal(["Binary Fusion", "Browse Compendium", "Register Demon", "Back"], normalMoon.Options);
        Assert.DoesNotContain(FusionMainMenuAction.SacrificialFusion, normalMoon.Actions);
    }

    [Fact]
    public void ParticipantSelectionDetailed_PreservesLabelsDisabledReasonsAndCancellation()
    {
        var io = new ScriptedGameIO().QueueMenu(-1);
        var bridge = new CathedralUIBridge(io, new FieldUIState(), new CompendiumRegistry(io));
        Combatant first = Demon("Pixie", "pixie", "Fairy");
        Combatant second = Demon("Slime", "slime", "Foul");

        RitualParticipantPresentationResult<Combatant> result = bridge.SelectRitualParticipantDetailed(
            new List<Combatant> { first, second },
            "CHOOSE THE SECOND PARTICIPANT:",
            [],
            new Dictionary<Combatant, string> { [second] = "Owned Result: Jack Frost" });

        Assert.Equal(FusionPresentationResultKind.Canceled, result.Kind);
        Assert.Equal(RitualParticipantSelectionKind.Canceled, result.LegacyResult.Kind);
        Assert.Equal("CHOOSE THE SECOND PARTICIPANT:", result.Prompt);
        Assert.Equal(
            ["Pixie           (Lv.10) Fairy (Rk.1)", "Slime           (Lv.10) Foul (Rk.1) (Owned Result: Jack Frost)", "Cancel"],
            result.Labels);
        Assert.Equal([false, true, false], result.DisabledOptions);
    }

    [Fact]
    public void SkillInheritanceDetailed_CarriesRowsFrameworkReasonsAndSelectedOrder()
    {
        var io = new ScriptedGameIO().QueueMenu(1, 3);
        var bridge = new CathedralUIBridge(io, new FieldUIState(), new CompendiumRegistry(io));
        var frameworkRows = new[]
        {
            new FusionInheritanceEntry(ContentId.Parse("frost_lance"), "Frost Lance", false, "group_denied"),
            new FusionInheritanceEntry(ContentId.Parse("ice_boost"), "Ice Boost", true, "allowed")
        };

        SkillInheritancePresentationResult result = bridge.SelectInheritedSkillsDetailed(
            ["Frost Lance", "Ice Boost", "Dia"],
            maxSlots: 2,
            inherentSkills: ["Dia"],
            exclusivePool: ["Frost Lance"],
            frameworkRows);

        Assert.Equal(FusionPresentationResultKind.Confirmed, result.Kind);
        Assert.Equal(SkillInheritanceSelectionKind.Confirmed, result.LegacyResult.Kind);
        Assert.Equal(["Ice Boost"], result.LegacyResult.Skills);
        Assert.Equal(2, result.MaximumSlots);

        SkillInheritanceRowPresentation frost = Assert.Single(result.Rows, row => row.SkillName == "Frost Lance");
        SkillInheritanceRowPresentation boost = Assert.Single(result.Rows, row => row.SkillName == "Ice Boost");
        SkillInheritanceRowPresentation dia = Assert.Single(result.Rows, row => row.SkillName == "Dia");

        Assert.Equal("[-] Frost Lance (Exclusive)", frost.Label);
        Assert.False(frost.IsSelectable);
        Assert.Equal("group_denied", frost.ReasonCode);
        Assert.True(boost.IsSelected);
        Assert.Equal("[X] Ice Boost", boost.Label);
        Assert.Equal("allowed", boost.ReasonCode);
        Assert.True(dia.IsAlreadyKnown);
        Assert.Equal("already_known", dia.ReasonCode);
    }

    [Fact]
    public void RitualConfirmationDetailed_PreservesPreviewDecisionAndForbiddenGate()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        PersonaData allowedTemplate = Database.Personas.Values.First(persona => persona.Level <= 20);
        PersonaData forbiddenTemplate = Database.Personas.Values.First(persona => persona.Level > 1);

        var confirmIo = new ScriptedGameIO().QueueMenu(0);
        var confirmBridge = new CathedralUIBridge(confirmIo, new FieldUIState(), new CompendiumRegistry(confirmIo));
        Combatant allowed = CombatantFactory.CreatePlayerDemon(allowedTemplate.Id, allowedTemplate.Level);

        RitualConfirmationPresentationResult confirmed = confirmBridge.ConfirmRitualDetailed(
            allowed,
            originalParent: null,
            inheritedSkills: ["Dia"],
            playerLevel: 99,
            FusionOperationType.CreateNewDemon);

        Assert.Equal(FusionPresentationResultKind.Confirmed, confirmed.Kind);
        Assert.Equal(RitualConfirmationKind.Commence, confirmed.LegacyResult.Kind);
        Assert.Equal(["Commence Ritual", "Wait", "Cancel Fusion"], Assert.Single(confirmIo.Menus).Options);
        Assert.Equal(allowedTemplate.Level, confirmed.BaseTemplateLevel);
        Assert.Equal(["Dia"], confirmed.InheritedSkills);

        var forbiddenIo = new ScriptedGameIO();
        var forbiddenBridge = new CathedralUIBridge(forbiddenIo, new FieldUIState(), new CompendiumRegistry(forbiddenIo));
        Combatant forbidden = CombatantFactory.CreatePlayerDemon(forbiddenTemplate.Id, forbiddenTemplate.Level);

        RitualConfirmationPresentationResult rejected = forbiddenBridge.ConfirmRitualDetailed(
            forbidden,
            originalParent: null,
            inheritedSkills: [],
            playerLevel: forbiddenTemplate.Level - 1,
            FusionOperationType.CreateNewDemon);

        Assert.Equal(FusionPresentationResultKind.Rejected, rejected.Kind);
        Assert.Equal(RitualConfirmationKind.Forbidden, rejected.LegacyResult.Kind);
        Assert.Empty(forbiddenIo.Menus);
        Assert.Contains("=== RITUAL FORBIDDEN ===", forbiddenIo.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RitualSequenceDetailed_RecordsAccidentMessagesWithoutChangingVisibleOutput()
    {
        var io = new ScriptedGameIO();
        var bridge = new CathedralUIBridge(io, new FieldUIState(), new CompendiumRegistry(io));

        RitualSequencePresentationResult result = bridge.DisplayRitualSequenceDetailed(isAccident: true);

        Assert.True(result.IsAccident);
        Assert.Equal(5, result.Events.Count);
        Assert.Equal("!!! WARNING: LUNAR INTERFERENCE DETECTED !!!", result.Events[3].Message);
        Assert.Equal(ConsoleColor.Red, result.Events[3].Color);
        Assert.Equal(
            result.Events.Select(evt => evt.Message),
            io.Writes.Select(write => write.Text));
        Assert.Equal([1200, 1200, 1200, 2000], io.Waits);
    }

    [Fact]
    public void CompendiumDetailedResults_PreserveRegistrationRecallAssessmentAndSnapshotIsolation()
    {
        LegacyBaselineSupport.ResetAndLoadLegacyDatabase();
        var io = new ScriptedGameIO();
        var compendium = new CompendiumRegistry(io);
        Combatant pixie = CombatantFactory.CreatePlayerDemon("pixie", 10);
        pixie.ExtraSkills.Add("Dia");

        CompendiumRegistrationPresentationResult added = compendium.RegisterDemonDetailed(pixie);
        pixie.ExtraSkills.Add("Agi");
        CompendiumRegistrationPresentationResult updated = compendium.RegisterDemonDetailed(pixie);

        Assert.Equal(FusionPresentationResultKind.Applied, added.Kind);
        Assert.Equal(CompendiumRegistrationCode.Added, added.Result!.Code);
        Assert.Equal(CompendiumRegistrationCode.Updated, updated.Result!.Code);
        Assert.Equal(ConsoleColor.Green, added.Event!.Color);
        Assert.Equal(ConsoleColor.Cyan, updated.Event!.Color);

        Combatant recallEntry = compendium.GetRecallEntry("pixie");
        Assert.Equal(["Dia", "Agi"], recallEntry.ExtraSkills);
        recallEntry.ExtraSkills.Add("Mutated");
        Assert.DoesNotContain("Mutated", compendium.GetRecallEntry("pixie").ExtraSkills);

        io.QueueMenu(0);
        var bridge = new CathedralUIBridge(io, new FieldUIState(), compendium);
        CompendiumRecallPresentationResult recall = bridge.ShowCompendiumRecallMenuDetailed();

        Assert.Equal(FusionPresentationResultKind.Selected, recall.Kind);
        Assert.Equal(CompendiumRecallResultKind.Selected, recall.LegacyResult.Kind);
        Assert.Equal(2, recall.Labels.Count);
        Assert.Contains("Pixie", recall.Labels[0], StringComparison.Ordinal);
        Assert.Contains(" M", recall.Labels[0], StringComparison.Ordinal);
        Assert.Equal("Back", recall.Labels[1]);

        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        CompendiumRecallAssessment insufficient = compendium.AssessRecall(
            owner,
            "pixie",
            currentMacca: 0,
            alreadyOwned: false,
            hasOpenStockSlot: true);
        Assert.Equal(CompendiumRecallCode.InsufficientCurrency, insufficient.Code);
    }

    [Fact]
    public void MutatorDetailedResults_ReportDuplicateFusionAndRecallRejectionsWithoutMutation()
    {
        var owner = new Combatant("Hero", ClassType.Operator) { Level = 50 };
        var existing = Demon("Pixie", "pixie", "Fairy");
        var parentA = Demon("Parent A", "parent_a", "Fairy");
        var parentB = Demon("Parent B", "parent_b", "Foul");
        owner.DemonStock.Add(existing);
        owner.DemonStock.Add(parentA);
        owner.DemonStock.Add(parentB);
        var party = new PartyManager(owner);
        var mutator = new FusionMutator(party, new EconomyManager(), new FusionMessenger());
        var context = new FusionContext(
            owner,
            new List<object> { parentA, parentB },
            sacrifice: null,
            chosenSkills: [],
            resultId: "pixie",
            messenger: new FusionMessenger(),
            party: party);

        FusionTransactionPresentationResult duplicate = mutator.ExecuteFusionTransactionDetailed(
            context,
            FusionOperationType.CreateNewDemon);

        Assert.Equal(FusionPresentationResultKind.Rejected, duplicate.Kind);
        Assert.Equal(FusionRuntimeDiagnosticCode.DuplicateResult, Assert.Single(duplicate.Diagnostics).Code);
        Assert.Equal(3, owner.DemonStock.Count);

        var economy = new EconomyManager();
        economy.AddMacca(5000);
        var recallMutator = new FusionMutator(party, economy, new FusionMessenger());
        CompendiumRecallTransactionPresentationResult recall = recallMutator.FinalizeRecallDetailed(
            owner,
            new Combatant("Pixie", ClassType.Demon) { SourceId = "PIXIE" },
            cost: 1000);

        Assert.Equal(FusionPresentationResultKind.Rejected, recall.Kind);
        Assert.Equal("Pixie is already in your party or COMP.", recall.Event!.Message);
        Assert.Equal(5000, economy.Macca);
        Assert.Equal(3, owner.DemonStock.Count);
    }

    private static Combatant Demon(string name, string id, string race) =>
        new(name, ClassType.Demon)
        {
            SourceId = id,
            Level = 10,
            ActivePersona = new Persona
            {
                Name = name,
                Race = race,
                Rank = 1,
                Level = 10
            }
        };
}
