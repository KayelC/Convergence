using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Field
{
    public enum PartyStockSelectionResultKind
    {
        Back,
        Unavailable,
        Selected
    }

    public enum PersonaStockActionKind
    {
        Back,
        Equip
    }

    public enum SummonTargetSelectionKind
    {
        Back,
        Unavailable,
        ReturnToComp,
        SelectedDemon
    }

    public enum PartyStockPresentationOperation
    {
        SummonDemon,
        ReturnDemon,
        SwapActiveDemon,
        DismissDemon,
        ReplaceDemon,
        SwapActivePersona
    }

    public sealed record OrganizationSlotSelectionResult(PartyStockSelectionResultKind Kind, int SlotIndex = -1)
    {
        public static OrganizationSlotSelectionResult Back { get; } =
            new(PartyStockSelectionResultKind.Back);

        public static OrganizationSlotSelectionResult Unavailable { get; } =
            new(PartyStockSelectionResultKind.Unavailable);

        public static OrganizationSlotSelectionResult Selected(int slotIndex) =>
            new(PartyStockSelectionResultKind.Selected, slotIndex);
    }

    public sealed record PersonaStockSelectionResult(PartyStockSelectionResultKind Kind, Persona? Persona = null)
    {
        public static PersonaStockSelectionResult Back { get; } =
            new(PartyStockSelectionResultKind.Back);

        public static PersonaStockSelectionResult Unavailable { get; } =
            new(PartyStockSelectionResultKind.Unavailable);

        public static PersonaStockSelectionResult Selected(Persona persona) =>
            new(PartyStockSelectionResultKind.Selected, persona);
    }

    public sealed record PersonaStockActionResult(PersonaStockActionKind Kind)
    {
        public static PersonaStockActionResult Back { get; } = new(PersonaStockActionKind.Back);
        public static PersonaStockActionResult Equip { get; } = new(PersonaStockActionKind.Equip);
    }

    public sealed record DemonStockSelectionResult(PartyStockSelectionResultKind Kind, Combatant? Demon = null)
    {
        public static DemonStockSelectionResult Back { get; } =
            new(PartyStockSelectionResultKind.Back);

        public static DemonStockSelectionResult Unavailable { get; } =
            new(PartyStockSelectionResultKind.Unavailable);

        public static DemonStockSelectionResult Selected(Combatant demon) =>
            new(PartyStockSelectionResultKind.Selected, demon);
    }

    public sealed record SummonTargetSelectionResult(SummonTargetSelectionKind Kind, Combatant? Demon = null)
    {
        public static SummonTargetSelectionResult Back { get; } =
            new(SummonTargetSelectionKind.Back);

        public static SummonTargetSelectionResult Unavailable { get; } =
            new(SummonTargetSelectionKind.Unavailable);

        public static SummonTargetSelectionResult ReturnToComp { get; } =
            new(SummonTargetSelectionKind.ReturnToComp);

        public static SummonTargetSelectionResult SelectedDemon(Combatant demon) =>
            new(SummonTargetSelectionKind.SelectedDemon, demon);
    }

    public sealed record PartyStockPresentationEvent(
        string? Message,
        ConsoleColor Color,
        int Delay,
        bool WaitForInput,
        bool ClearScreen);

    public sealed record PartyStockPresentationResult
    {
        public PartyStockPresentationResult(
            PartyStockPresentationOperation operation,
            bool applied,
            PartyStockTransitionCode code,
            IEnumerable<RuntimeInstanceId>? affectedInstanceIds = null,
            IEnumerable<PartyStockPresentationEvent>? presentationEvents = null)
        {
            Operation = operation;
            Applied = applied;
            Code = code;
            AffectedInstanceIds = new ReadOnlyCollection<RuntimeInstanceId>(
                new List<RuntimeInstanceId>(affectedInstanceIds ?? Array.Empty<RuntimeInstanceId>()));
            PresentationEvents = new ReadOnlyCollection<PartyStockPresentationEvent>(
                new List<PartyStockPresentationEvent>(presentationEvents ?? Array.Empty<PartyStockPresentationEvent>()));
        }

        public PartyStockPresentationOperation Operation { get; }
        public bool Applied { get; }
        public PartyStockTransitionCode Code { get; }
        public IReadOnlyList<RuntimeInstanceId> AffectedInstanceIds { get; }
        public IReadOnlyList<PartyStockPresentationEvent> PresentationEvents { get; }
    }
}
