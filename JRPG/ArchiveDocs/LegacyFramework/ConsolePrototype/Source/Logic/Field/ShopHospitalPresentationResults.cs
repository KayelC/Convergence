using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Field
{
    public enum ShopSessionCommandKind
    {
        Buy,
        Sell,
        Exit,
        Back,
        Unavailable
    }

    public enum ShopSelectionResultKind
    {
        Selected,
        Back,
        Unavailable
    }

    public enum ShopTransactionConfirmationKind
    {
        Confirmed,
        Declined,
        Back
    }

    public enum ShopTransactionOperation
    {
        Buy,
        Sell
    }

    public enum HospitalSelectionResultKind
    {
        Selected,
        Back,
        Unavailable
    }

    public sealed record ShopHospitalPresentationEvent(
        string? Message,
        ConsoleColor Color,
        int Delay,
        bool WaitForInput = false,
        bool ClearScreen = false);

    public sealed record ShopSessionCommandResult(ShopSessionCommandKind Kind, int SelectedIndex = -1)
    {
        public static ShopSessionCommandResult Buy(int selectedIndex) =>
            new(ShopSessionCommandKind.Buy, selectedIndex);

        public static ShopSessionCommandResult Sell(int selectedIndex) =>
            new(ShopSessionCommandKind.Sell, selectedIndex);

        public static ShopSessionCommandResult Exit(int selectedIndex) =>
            new(ShopSessionCommandKind.Exit, selectedIndex);

        public static ShopSessionCommandResult Back { get; } =
            new(ShopSessionCommandKind.Back);

        public static ShopSessionCommandResult Unavailable { get; } =
            new(ShopSessionCommandKind.Unavailable);
    }

    public sealed record ShopOfferPresentation(
        ShopEntry Entry,
        string ContentId,
        string Name,
        ShopCategory Category,
        int Index,
        int DisplayedPrice,
        string Label,
        bool IsEquipped = false);

    public sealed record ShopOfferSelectionResult(ShopSelectionResultKind Kind, ShopOfferPresentation? Offer = null)
    {
        public static ShopOfferSelectionResult Back { get; } =
            new(ShopSelectionResultKind.Back);

        public static ShopOfferSelectionResult Unavailable { get; } =
            new(ShopSelectionResultKind.Unavailable);

        public static ShopOfferSelectionResult Selected(ShopOfferPresentation offer) =>
            new(ShopSelectionResultKind.Selected, offer);
    }

    public sealed record ShopTransactionConfirmationResult(ShopTransactionConfirmationKind Kind)
    {
        public static ShopTransactionConfirmationResult Confirmed { get; } =
            new(ShopTransactionConfirmationKind.Confirmed);

        public static ShopTransactionConfirmationResult Declined { get; } =
            new(ShopTransactionConfirmationKind.Declined);

        public static ShopTransactionConfirmationResult Back { get; } =
            new(ShopTransactionConfirmationKind.Back);
    }

    public sealed record ShopInspectionPresentationResult
    {
        public ShopInspectionPresentationResult(
            string description,
            string stats,
            int? price,
            IEnumerable<ShopHospitalPresentationEvent>? events = null)
        {
            Description = description;
            Stats = stats;
            Price = price;
            Events = new ReadOnlyCollection<ShopHospitalPresentationEvent>(
                new List<ShopHospitalPresentationEvent>(events ?? Array.Empty<ShopHospitalPresentationEvent>()));
        }

        public string Description { get; }
        public string Stats { get; }
        public int? Price { get; }
        public IReadOnlyList<ShopHospitalPresentationEvent> Events { get; }
    }

    public sealed record ShopTransactionPresentationResult
    {
        public ShopTransactionPresentationResult(
            ShopTransactionOperation operation,
            ShopCategory category,
            string contentId,
            string displayName,
            int displayedPrice,
            ShopTransactionResult transaction,
            string? message,
            ConsoleColor color,
            int delay)
        {
            Operation = operation;
            Category = category;
            ContentId = contentId;
            DisplayName = displayName;
            DisplayedPrice = displayedPrice;
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            Message = message;
            Color = color;
            Delay = delay;
        }

        public ShopTransactionOperation Operation { get; }
        public ShopCategory Category { get; }
        public string ContentId { get; }
        public string DisplayName { get; }
        public int DisplayedPrice { get; }
        public ShopTransactionResult Transaction { get; }
        public bool LegacySuccess => Transaction.Applied;
        public string? Message { get; }
        public ConsoleColor Color { get; }
        public int Delay { get; }
    }

    public sealed record HospitalPatientPresentation(
        Combatant Patient,
        int Index,
        int MissingHp,
        int MissingSp,
        int Cost,
        bool IsHealthy,
        string Label);

    public sealed record HospitalPatientSelectionResult(
        HospitalSelectionResultKind Kind,
        HospitalPatientPresentation? Presentation = null)
    {
        public Combatant? Patient => Presentation?.Patient;

        public static HospitalPatientSelectionResult Back { get; } =
            new(HospitalSelectionResultKind.Back);

        public static HospitalPatientSelectionResult Unavailable { get; } =
            new(HospitalSelectionResultKind.Unavailable);

        public static HospitalPatientSelectionResult Selected(HospitalPatientPresentation presentation) =>
            new(HospitalSelectionResultKind.Selected, presentation);
    }

    public sealed record HospitalTreatmentPresentationResult
    {
        public HospitalTreatmentPresentationResult(
            Combatant patient,
            HospitalRestorationResult transaction,
            string? message,
            ConsoleColor color,
            int delay)
        {
            Patient = patient ?? throw new ArgumentNullException(nameof(patient));
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            Message = message;
            Color = color;
            Delay = delay;
        }

        public Combatant Patient { get; }
        public HospitalRestorationResult Transaction { get; }
        public bool LegacySuccess => Transaction.Applied;
        public int Cost => Transaction.Cost;
        public string? Message { get; }
        public ConsoleColor Color { get; }
        public int Delay { get; }
    }
}
