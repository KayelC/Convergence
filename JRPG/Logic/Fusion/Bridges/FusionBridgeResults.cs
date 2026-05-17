namespace JRPGPrototype.Logic.Fusion.Bridges
{
    public enum FusionMainMenuAction
    {
        BinaryFusion,
        SacrificialFusion,
        BrowseCompendium,
        RegisterDemon
    }

    public enum FusionMenuResultKind
    {
        Selected,
        Back
    }

    public sealed record FusionMainMenuResult(FusionMenuResultKind Kind, FusionMainMenuAction? Action = null)
    {
        public static FusionMainMenuResult Back { get; } = new FusionMainMenuResult(FusionMenuResultKind.Back);

        public static FusionMainMenuResult Selected(FusionMainMenuAction action)
            => new FusionMainMenuResult(FusionMenuResultKind.Selected, action);
    }

    public enum RitualConfirmationKind
    {
        Commence,
        Wait,
        Cancel,
        Forbidden
    }

    public sealed record RitualConfirmationResult(RitualConfirmationKind Kind)
    {
        public static RitualConfirmationResult Commence { get; } = new RitualConfirmationResult(RitualConfirmationKind.Commence);
        public static RitualConfirmationResult Wait { get; } = new RitualConfirmationResult(RitualConfirmationKind.Wait);
        public static RitualConfirmationResult Cancel { get; } = new RitualConfirmationResult(RitualConfirmationKind.Cancel);
        public static RitualConfirmationResult Forbidden { get; } = new RitualConfirmationResult(RitualConfirmationKind.Forbidden);
    }
}
