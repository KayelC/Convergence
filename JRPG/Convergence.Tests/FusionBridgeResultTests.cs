using JRPGPrototype.Logic.Fusion.Bridges;
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
    }
}
