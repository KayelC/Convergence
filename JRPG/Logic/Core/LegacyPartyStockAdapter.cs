using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Core
{
    internal sealed class LegacyPartyStockAdapter
    {
        public static LegacyPartyStockAdapter Shared { get; } = new(
            LegacyRuntimeIdentityRegistry.Shared,
            new PartyStockTransitionService(),
            new LegacyStockCapacityPolicy());

        private readonly LegacyRuntimeIdentityRegistry _ids;
        private readonly IPartyStockTransitionService _transitions;
        private readonly IStockCapacityPolicy _stockCapacity;

        public LegacyPartyStockAdapter(
            LegacyRuntimeIdentityRegistry ids,
            IPartyStockTransitionService transitions,
            IStockCapacityPolicy stockCapacity)
        {
            _ids = ids;
            _transitions = transitions;
            _stockCapacity = stockCapacity;
        }

        public int GetStockCapacity(int ownerLevel) => _stockCapacity.GetCapacity(ownerLevel);

        public bool HasOpenDemonStockSlot(Combatant owner) =>
            owner.DemonStock.Count < GetStockCapacity(owner.Level);

        public bool HasOpenPersonaStockSlot(Combatant owner) =>
            owner.PersonaStock.Count < GetStockCapacity(owner.Level);

        public bool AddMember(PartyManager party, Combatant member)
        {
            Combatant owner = GetPartyOwner(party, member);
            RuntimePartyStockSnapshot before = Snapshot(party, owner);
            PartyStockTransitionResult result = _transitions.AddPartyMember(new AddPartyMemberRequest(
                before,
                _ids.ActorReference(member)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyActors(party, owner, result.After, [member]);
            return result.After.ActiveParty.Count > before.ActiveParty.Count;
        }

        public void SwapMember(PartyManager party, int activeIndex, int reserveIndex)
        {
            Combatant owner = GetPartyOwner(party, null);
            PartyStockTransitionResult result = _transitions.SwapPartyMember(new SwapPartyMemberRequest(
                Snapshot(party, owner),
                activeIndex,
                reserveIndex));

            if (result.Applied)
            {
                ApplyActors(party, owner, result.After);
            }
        }

        public bool SummonDemon(PartyManager party, Combatant owner, Combatant demon)
        {
            PartyStockTransitionResult result = _transitions.SummonDemon(new SummonDemonRequest(
                Snapshot(party, owner),
                _ids.GetActorId(demon)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyActors(party, owner, result.After, directControlActors: [demon]);
            return true;
        }

        public bool SwapActiveDemon(PartyManager party, Combatant owner, Combatant activeToRemove, Combatant standbyToAdd)
        {
            PartyStockTransitionResult result = _transitions.SwapActiveDemon(new SwapActiveDemonRequest(
                Snapshot(party, owner),
                _ids.GetActorId(activeToRemove),
                _ids.GetActorId(standbyToAdd)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyActors(party, owner, result.After, directControlActors: [standbyToAdd]);
            return true;
        }

        public bool ReturnDemon(PartyManager party, Combatant owner, Combatant demon)
        {
            PartyStockTransitionResult result = _transitions.ReturnDemon(new ReturnDemonRequest(
                Snapshot(party, owner),
                _ids.GetActorId(demon)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyActors(party, owner, result.After);
            return true;
        }

        public bool DismissDemon(PartyManager party, Combatant owner, Combatant demon)
        {
            PartyStockTransitionResult result = _transitions.DismissDemon(new DismissDemonRequest(
                Snapshot(party, owner),
                _ids.GetActorId(demon)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyActors(party, owner, result.After);
            return true;
        }

        public void ReplaceDemon(PartyManager party, Combatant owner, Combatant oldDemon, Combatant newDemon)
        {
            PartyStockTransitionResult result = _transitions.ReplaceDemon(new ReplaceDemonRequest(
                Snapshot(party, owner),
                _ids.GetActorId(oldDemon),
                _ids.ActorReference(newDemon)));

            if (result.Applied)
            {
                ApplyActors(party, owner, result.After, [newDemon]);
            }
        }

        public bool ConsumeDemon(PartyManager party, Combatant owner, Combatant demon)
        {
            PartyStockTransitionResult result = _transitions.ConsumeDemon(new ConsumeDemonRequest(
                Snapshot(party, owner),
                _ids.GetActorId(demon)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyActors(party, owner, result.After);
            return true;
        }

        public bool SwapActivePersona(Combatant owner, Persona newPersona)
        {
            PartyStockTransitionResult result = _transitions.SwapActivePersona(new SwapActivePersonaRequest(
                Snapshot(owner),
                _ids.GetPersonaId(newPersona)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyForms(owner, result.After);
            return true;
        }

        public bool ConsumePersona(Combatant owner, Persona persona)
        {
            PartyStockTransitionResult result = _transitions.ConsumePersona(new ConsumePersonaRequest(
                Snapshot(owner),
                _ids.GetPersonaId(persona)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyForms(owner, result.After);
            return true;
        }

        public bool ReplacePersona(Combatant owner, Persona oldPersona, Persona newPersona)
        {
            PartyStockTransitionResult result = _transitions.ReplacePersona(new ReplacePersonaRequest(
                Snapshot(owner),
                _ids.GetPersonaId(oldPersona),
                _ids.PersonaReference(newPersona)));

            if (!result.Applied)
            {
                return false;
            }

            ApplyForms(owner, result.After, [newPersona]);
            return true;
        }

        public RuntimePartyStockSnapshot Snapshot(PartyManager party, Combatant owner) =>
            new(
                _ids.ActorReference(owner),
                owner.Level,
                party.ActiveParty.Select(_ids.ActorReference),
                party.ReserveMembers.Select(_ids.ActorReference),
                owner.ActivePersona is null ? null : _ids.PersonaReference(owner.ActivePersona),
                owner.PersonaStock.Select(_ids.PersonaReference),
                owner.DemonStock.Select(_ids.ActorReference));

        public RuntimePartyStockSnapshot Snapshot(Combatant owner) =>
            new(
                _ids.ActorReference(owner),
                owner.Level,
                activeForm: owner.ActivePersona is null ? null : _ids.PersonaReference(owner.ActivePersona),
                personaStock: owner.PersonaStock.Select(_ids.PersonaReference),
                demonStock: owner.DemonStock.Select(_ids.ActorReference));

        private void ApplyActors(
            PartyManager party,
            Combatant owner,
            RuntimePartyStockSnapshot after,
            IEnumerable<Combatant>? additionalActors = null,
            IEnumerable<Combatant>? directControlActors = null)
        {
            List<Combatant> previousActive = party.ActiveParty.ToList();
            Dictionary<RuntimeInstanceId, Combatant> actors = ActorLookup(party, owner, additionalActors);

            party.ActiveParty.Clear();
            int slot = 0;
            foreach (RuntimeActorReferenceSnapshot reference in after.ActiveParty)
            {
                Combatant actor = actors[reference.InstanceId];
                actor.PartySlot = slot++;
                party.ActiveParty.Add(actor);
            }

            foreach (Combatant actor in previousActive.Where(actor => !party.ActiveParty.Contains(actor)))
            {
                actor.PartySlot = -1;
            }

            party.ReserveMembers.Clear();
            foreach (RuntimeActorReferenceSnapshot reference in after.ReserveMembers)
            {
                Combatant actor = actors[reference.InstanceId];
                actor.PartySlot = -1;
                party.ReserveMembers.Add(actor);
            }

            owner.DemonStock.Clear();
            foreach (RuntimeActorReferenceSnapshot reference in after.DemonStock)
            {
                owner.DemonStock.Add(actors[reference.InstanceId]);
            }

            foreach (Combatant actor in directControlActors ?? [])
            {
                actor.BattleControl = ControlState.DirectControl;
            }
        }

        private void ApplyForms(
            Combatant owner,
            RuntimePartyStockSnapshot after,
            IEnumerable<Persona>? additionalForms = null)
        {
            Dictionary<RuntimeInstanceId, Persona> personas = PersonaLookup(owner, additionalForms);
            owner.ActivePersona = after.ActiveForm is null ? null : personas[after.ActiveForm.InstanceId];

            owner.PersonaStock.Clear();
            foreach (RuntimeActorReferenceSnapshot reference in after.PersonaStock)
            {
                owner.PersonaStock.Add(personas[reference.InstanceId]);
            }
        }

        private Dictionary<RuntimeInstanceId, Combatant> ActorLookup(
            PartyManager party,
            Combatant owner,
            IEnumerable<Combatant>? additionalActors)
        {
            var actors = new Dictionary<RuntimeInstanceId, Combatant>();
            Add(owner);
            foreach (Combatant actor in party.ActiveParty) Add(actor);
            foreach (Combatant actor in party.ReserveMembers) Add(actor);
            foreach (Combatant actor in owner.DemonStock) Add(actor);
            foreach (Combatant actor in additionalActors ?? []) Add(actor);
            return actors;

            void Add(Combatant actor)
            {
                RuntimeInstanceId id = _ids.GetActorId(actor);
                actors[id] = actor;
            }
        }

        private Dictionary<RuntimeInstanceId, Persona> PersonaLookup(
            Combatant owner,
            IEnumerable<Persona>? additionalForms)
        {
            var personas = new Dictionary<RuntimeInstanceId, Persona>();
            if (owner.ActivePersona is not null) Add(owner.ActivePersona);
            foreach (Persona persona in owner.PersonaStock) Add(persona);
            foreach (Persona persona in additionalForms ?? []) Add(persona);
            return personas;

            void Add(Persona persona)
            {
                RuntimeInstanceId id = _ids.GetPersonaId(persona);
                personas[id] = persona;
            }
        }

        private static Combatant GetPartyOwner(PartyManager party, Combatant? fallback) =>
            party.ActiveParty.FirstOrDefault() ?? fallback ?? throw new InvalidOperationException("Party has no owner.");
    }
}
