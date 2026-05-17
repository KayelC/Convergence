namespace JRPGPrototype.Logic.Fusion.Bridges
{
    /// <summary>
    /// Cathedral actions that the fusion conductor can dispatch.
    /// These are deliberately named after gameplay services, not menu text, so a future host can
    /// localize, rearrange, or redraw the menu without changing conductor branch logic.
    /// </summary>
    public enum FusionMainMenuAction
    {
        BinaryFusion,
        SacrificialFusion,
        BrowseCompendium,
        RegisterDemon
    }

    /// <summary>
    /// Outcome of a one-step Cathedral menu.
    /// Back is a normal navigation result, not an error and not an empty selection.
    /// </summary>
    public enum FusionMenuResultKind
    {
        Selected,
        Back
    }

    /// <summary>
    /// Main-menu result returned by the Cathedral bridge.
    /// Only <see cref="FusionMenuResultKind.Selected"/> carries an action; callers should treat
    /// <see cref="FusionMenuResultKind.Back"/> as "leave this Cathedral screen" rather than as failure.
    /// </summary>
    public sealed record FusionMainMenuResult(FusionMenuResultKind Kind, FusionMainMenuAction? Action = null)
    {
        public static FusionMainMenuResult Back { get; } = new FusionMainMenuResult(FusionMenuResultKind.Back);

        public static FusionMainMenuResult Selected(FusionMainMenuAction action)
            => new FusionMainMenuResult(FusionMenuResultKind.Selected, action);
    }

    /// <summary>
    /// Decision from the final ritual preview, after participants, result, and inherited skills are staged.
    /// Each state maps to a different conductor loop transition, which is why this cannot remain a raw menu index.
    /// </summary>
    public enum RitualConfirmationKind
    {
        /// <summary>Commit the staged transaction and consume the selected participants.</summary>
        Commence,

        /// <summary>Return to skill inheritance while keeping the selected participants and staged result context.</summary>
        Wait,

        /// <summary>Discard the staged result and return to participant selection.</summary>
        Cancel,

        /// <summary>The preview failed the level-authority gate before the player could confirm it.</summary>
        Forbidden
    }

    /// <summary>
    /// Confirmation result for the ritual preview flow.
    /// There is no payload because the conductor already owns the staged demon, selected parents,
    /// sacrifice, and inherited-skill list when this result is returned.
    /// </summary>
    public sealed record RitualConfirmationResult(RitualConfirmationKind Kind)
    {
        public static RitualConfirmationResult Commence { get; } = new RitualConfirmationResult(RitualConfirmationKind.Commence);
        public static RitualConfirmationResult Wait { get; } = new RitualConfirmationResult(RitualConfirmationKind.Wait);
        public static RitualConfirmationResult Cancel { get; } = new RitualConfirmationResult(RitualConfirmationKind.Cancel);
        public static RitualConfirmationResult Forbidden { get; } = new RitualConfirmationResult(RitualConfirmationKind.Forbidden);
    }

    /// <summary>
    /// Outcome of selecting one ritual participant.
    /// Selection screens can end because the player backed out or because filtering left no legal
    /// candidates; those cases drive different conductor behavior and must not collapse into null.
    /// </summary>
    public enum RitualParticipantSelectionKind
    {
        /// <summary>A candidate was selected and is available in the result payload.</summary>
        Selected,

        /// <summary>The player intentionally backed out through Cancel, Back, or an equivalent host action.</summary>
        Canceled,

        /// <summary>The supplied pool had no legal candidates after already-selected participants were excluded.</summary>
        Unavailable
    }

    /// <summary>
    /// Participant selection result used by fusion and Wild Card registration flows.
    /// The payload is present only for <see cref="RitualParticipantSelectionKind.Selected"/>; every other
    /// state forces the caller to choose the correct navigation path explicitly.
    /// </summary>
    public sealed record RitualParticipantSelectionResult<T>(RitualParticipantSelectionKind Kind, T? Participant = null)
        where T : class
    {
        public static RitualParticipantSelectionResult<T> Canceled { get; } =
            new RitualParticipantSelectionResult<T>(RitualParticipantSelectionKind.Canceled);

        public static RitualParticipantSelectionResult<T> Unavailable { get; } =
            new RitualParticipantSelectionResult<T>(RitualParticipantSelectionKind.Unavailable);

        public static RitualParticipantSelectionResult<T> Selected(T participant)
            => new RitualParticipantSelectionResult<T>(RitualParticipantSelectionKind.Selected, participant);
    }
}
