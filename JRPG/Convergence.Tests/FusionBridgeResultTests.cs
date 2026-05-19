using JRPGPrototype.Logic.Fusion.Bridges;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using Xunit;

namespace Convergence.Tests
{
    public class FusionBridgeResultTests
    {
        [Fact]
        public void MainMenuBack_HasBackKindAndNoAction()
        {
            var result = FusionMainMenuResult.Back;

            Assert.Equal(FusionMenuResultKind.Back, result.Kind);
            Assert.Null(result.Action);
        }

        [Theory]
        [InlineData(FusionMainMenuAction.BinaryFusion)]
        [InlineData(FusionMainMenuAction.SacrificialFusion)]
        [InlineData(FusionMainMenuAction.BrowseCompendium)]
        [InlineData(FusionMainMenuAction.RegisterDemon)]
        public void MainMenuSelected_CarriesTypedAction(FusionMainMenuAction action)
        {
            var result = FusionMainMenuResult.Selected(action);

            Assert.Equal(FusionMenuResultKind.Selected, result.Kind);
            Assert.Equal(action, result.Action);
        }

        [Theory]
        [InlineData(RitualConfirmationKind.Commence)]
        [InlineData(RitualConfirmationKind.Wait)]
        [InlineData(RitualConfirmationKind.Cancel)]
        [InlineData(RitualConfirmationKind.Forbidden)]
        public void RitualConfirmation_StaticResultsExposeNamedKinds(RitualConfirmationKind kind)
        {
            RitualConfirmationResult result = kind switch
            {
                RitualConfirmationKind.Commence => RitualConfirmationResult.Commence,
                RitualConfirmationKind.Wait => RitualConfirmationResult.Wait,
                RitualConfirmationKind.Cancel => RitualConfirmationResult.Cancel,
                RitualConfirmationKind.Forbidden => RitualConfirmationResult.Forbidden,
                _ => throw new Xunit.Sdk.XunitException("Unhandled ritual confirmation kind.")
            };

            Assert.Equal(kind, result.Kind);
        }

        [Fact]
        public void ParticipantSelectionSelected_CarriesTypedParticipant()
        {
            object participant = new object();

            var result = RitualParticipantSelectionResult<object>.Selected(participant);

            Assert.Equal(RitualParticipantSelectionKind.Selected, result.Kind);
            Assert.Same(participant, result.Participant);
        }

        [Theory]
        [InlineData(RitualParticipantSelectionKind.Canceled)]
        [InlineData(RitualParticipantSelectionKind.Unavailable)]
        public void ParticipantSelection_NonSelectedResultsHaveNoParticipant(RitualParticipantSelectionKind kind)
        {
            RitualParticipantSelectionResult<object> result = kind switch
            {
                RitualParticipantSelectionKind.Canceled => RitualParticipantSelectionResult<object>.Canceled,
                RitualParticipantSelectionKind.Unavailable => RitualParticipantSelectionResult<object>.Unavailable,
                _ => throw new Xunit.Sdk.XunitException("Unhandled non-selected participant kind.")
            };

            Assert.Equal(kind, result.Kind);
            Assert.Null(result.Participant);
        }

        [Fact]
        public void CompendiumRecallSelected_CarriesSelectedEntry()
        {
            var entry = new Combatant("Pixie", ClassType.Demon);

            var result = CompendiumRecallResult.Selected(entry);

            Assert.Equal(CompendiumRecallResultKind.Selected, result.Kind);
            Assert.Same(entry, result.Entry);
        }

        [Theory]
        [InlineData(CompendiumRecallResultKind.Back)]
        [InlineData(CompendiumRecallResultKind.Unavailable)]
        public void CompendiumRecall_NonSelectedResultsHaveNoEntry(CompendiumRecallResultKind kind)
        {
            CompendiumRecallResult result = kind switch
            {
                CompendiumRecallResultKind.Back => CompendiumRecallResult.Back,
                CompendiumRecallResultKind.Unavailable => CompendiumRecallResult.Unavailable,
                _ => throw new Xunit.Sdk.XunitException("Unhandled non-selected Compendium recall kind.")
            };

            Assert.Equal(kind, result.Kind);
            Assert.Null(result.Entry);
        }
    }
}
