using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Field;
using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Fusion.Strategies;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Logic.Fusion.Bridges;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The state-mutation authority for the Fusion Sub-System.
    /// Strategy Runner that executes fusion transactions via a Registry.
    /// Handles the atomic transactions for participant consumption, child instantiation, 
    /// and class-specific stock management (DemonStock vs PersonaStock).
    /// </summary>
    public class FusionMutator
    {
        private readonly PartyManager _partyManager;
        private readonly EconomyManager _economy;
        private readonly IFusionMessenger _messenger;
        private readonly FusionStrategyRegistry _registry;
        private readonly FusionOwnershipRules _ownershipRules;

        public FusionMutator(PartyManager partyManager, EconomyManager economy, IFusionMessenger messenger)
        {
            _partyManager = partyManager;
            _economy = economy;
            _messenger = messenger;
            _registry = new FusionStrategyRegistry();
            _ownershipRules = new FusionOwnershipRules(partyManager);
        }

        #region Stock Access Management (Preserved for Conductor usage)

        /// <summary>
        /// Retrieves the list of fusible entities for an Operator.
        /// Updated for the Unified 12-Slot Model: Returns the master DemonStock.
        /// </summary>
        public List<Combatant> GetFusibleDemonPool(Combatant owner)
        {
            // Under the Unified model, all owned demons (Field + Reserve) are stored in the Master Stock list.
            return owner.DemonStock.ToList();
        }

        /// <summary>
        /// Retrieves the list of fusible entities for a WildCard.
        /// Sources: The currently manifested ActivePersona and the internal PersonaStock.
        /// </summary>
        public List<Persona> GetFusiblePersonaPool(Combatant owner)
        {
            List<Persona> pool = new List<Persona>();

            // 1. Add the currently equipped persona
            if (owner.ActivePersona != null)
            {
                pool.Add(owner.ActivePersona);
            }

            // 2. Add personas stored in the owner's internal stock
            if (owner.PersonaStock != null)
            {
                pool.AddRange(owner.PersonaStock);
            }

            return pool.Distinct().ToList();
        }

        #endregion

        #region Fusion Execution

        /// <summary>
        /// Commits the fusion ritual to the game state.
        /// Dispatches the transaction to specific logic paths based on the owner's ClassType.
        /// Executes a fusion strategy based on the operation type.
        /// </summary>
        public void ExecuteFusionTransaction(FusionContext context, FusionOperationType type)
            => ExecuteFusionTransactionDetailed(context, type);

        internal FusionTransactionPresentationResult ExecuteFusionTransactionDetailed(FusionContext context, FusionOperationType type)
        {
            if (type == FusionOperationType.CreateNewDemon && IsDuplicateFusionResult(context))
            {
                return new FusionTransactionPresentationResult(
                    FusionPresentationResultKind.Rejected,
                    type,
                    context.ResultId,
                    [new FusionRuntimeDiagnostic(
                        FusionRuntimeDiagnosticCode.DuplicateResult,
                        "The fusion result is already owned.",
                        LegacyFusionContentAdapter.ToContentId(context.ResultId))]);
            }

            var strategy = _registry.GetStrategy(type);
            if (strategy != null)
            {
                strategy.Execute(context);
                return new FusionTransactionPresentationResult(
                    FusionPresentationResultKind.Applied,
                    type,
                    context.ResultId,
                    consumedParticipants: context.Materials);
            }
            else
            {
                string message = $"[System Error] No strategy found for {type}";
                _messenger.Publish(message, ConsoleColor.Red);
                return new FusionTransactionPresentationResult(
                    FusionPresentationResultKind.Rejected,
                    type,
                    context.ResultId,
                    [new FusionRuntimeDiagnostic(FusionRuntimeDiagnosticCode.NoFusionPossible, message)]);
            }
        }

        private bool IsDuplicateFusionResult(FusionContext context)
        {
            if (_ownershipRules.TryGetOwnedCreateResult(context.Owner, context.ResultId, out FusionOwnedResult ownedResult))
            {
                context.Messenger.Publish(ownedResult.TransactionAbortMessage, ConsoleColor.Red, 1000);
                return true;
            }

            return false;
        }

        #endregion

        #region Compendium Recall Logic

        /// <summary>
        /// Finalizes the recall transaction from the Compendium.
        /// Uses Messenger for all feedback.
        /// Updated for Unified 12-Slot Model: Recalls enter the master stock first.
        /// </summary>
        public bool FinalizeRecall(Combatant owner, Combatant snapshot, int cost)
            => FinalizeRecallDetailed(owner, snapshot, cost).Applied;

        internal CompendiumRecallTransactionPresentationResult FinalizeRecallDetailed(
            Combatant owner,
            Combatant snapshot,
            int cost,
            CompendiumRecallAssessment? assessment = null)
        {
            if (_economy.Macca < cost)
            {
                string message = "Recall Aborted: Insufficient Macca.";
                _messenger.Publish(message, ConsoleColor.Red);
                return new CompendiumRecallTransactionPresentationResult(
                    FusionPresentationResultKind.Rejected,
                    snapshot,
                    cost,
                    assessment,
                    new FusionPresentationEvent(FusionPresentationResultKind.Shown, message, ConsoleColor.Red));
            }

            if (owner.Class == ClassType.Operator && _partyManager.IsDemonOwned(owner, snapshot.SourceId))
            {
                string message = $"{snapshot.Name} is already in your party or COMP.";
                _messenger.Publish(message, ConsoleColor.Red, 1000);
                return new CompendiumRecallTransactionPresentationResult(
                    FusionPresentationResultKind.Rejected,
                    snapshot,
                    cost,
                    assessment,
                    new FusionPresentationEvent(FusionPresentationResultKind.Shown, message, ConsoleColor.Red, 1000));
            }

            if (owner.Class == ClassType.WildCard && snapshot.ActivePersona != null &&
                _partyManager.IsPersonaOwned(owner, snapshot.ActivePersona.Name))
            {
                string message = $"{snapshot.ActivePersona.Name} is already in your Persona stock.";
                _messenger.Publish(message, ConsoleColor.Red, 1000);
                return new CompendiumRecallTransactionPresentationResult(
                    FusionPresentationResultKind.Rejected,
                    snapshot,
                    cost,
                    assessment,
                    new FusionPresentationEvent(FusionPresentationResultKind.Shown, message, ConsoleColor.Red, 1000));
            }

            if (_economy.SpendMacca(cost))
            {
                if (owner.Class == ClassType.Operator)
                {
                    // 1. Recalled entity enters the Master Stock
                    owner.DemonStock.Add(snapshot);

                    // 2. Attempt to automatically deploy to the Active Party if room exists
                    if (!_partyManager.SummonDemon(owner, snapshot))
                    {
                        string sentMessage = $"{snapshot.Name} was sent to the COMP.";
                        _messenger.Publish(sentMessage, ConsoleColor.Gray, 600);
                        return new CompendiumRecallTransactionPresentationResult(
                            FusionPresentationResultKind.Applied,
                            snapshot,
                            cost,
                            assessment,
                            new FusionPresentationEvent(FusionPresentationResultKind.Shown, sentMessage, ConsoleColor.Gray, 600));
                    }
                }
                else
                {
                    // WildCards receive the Persona
                    Persona essence = snapshot.ActivePersona;

                    var combinedSkills = snapshot.GetConsolidatedSkills();
                    essence.SkillSet.Clear();
                    foreach (var s in combinedSkills)
                    {
                        essence.SkillSet.Add(s);
                    }

                    owner.PersonaStock.Add(essence);
                }

                return new CompendiumRecallTransactionPresentationResult(
                    FusionPresentationResultKind.Applied,
                    snapshot,
                    cost,
                    assessment,
                    null);
            }

            return new CompendiumRecallTransactionPresentationResult(
                FusionPresentationResultKind.Rejected,
                snapshot,
                cost,
                assessment,
                null);
        }

        #endregion
    }
}
