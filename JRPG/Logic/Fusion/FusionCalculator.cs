using System;
using System.Collections.Generic;
using System.Linq;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Services;
using JRPGPrototype.Logic.Fusion.Strategies;
using JRPGPrototype.Logic.Fusion.Messaging;
using JRPGPrototype.Logic.Fusion.Bridges;
using JRPGPrototype.Logic.Fusion.Inheritance;

namespace JRPGPrototype.Logic.Fusion
{
    /// <summary>
    /// The mathematical kernel for the Fusion Sub-System.
    /// Manages Race-based lookups and tier-matching logic based on recipe formulas.
    /// Handles deterministic skill inheritance calculations and accident probabilities.
    /// Fully decoupled diagnostic tracing via IFusionMessenger.
    /// </summary>
    public class FusionCalculator
    {
        private readonly IGameIO _io;
        private readonly IFusionMessenger _messenger;
        private readonly Random _rnd;
        private readonly LegacyFusionContentAdapter _adapter;
        private readonly FusionResultResolver _resultResolver;
        private readonly FusionPlanningService _planningService;

        // Lookup dictionary: Dictionary<RaceA, Dictionary<RaceB, ResultString>>
        private readonly Dictionary<string, Dictionary<string, string>> _raceTable;

        public FusionCalculator(IGameIO io, IFusionMessenger messenger)
            : this(io, messenger, new Random())
        {
        }

        internal FusionCalculator(IGameIO io, IFusionMessenger messenger, Random random)
        {
            _io = io;
            _messenger = messenger;
            _rnd = random ?? throw new ArgumentNullException(nameof(random));
            _adapter = LegacyFusionContentAdapter.Shared;
            var randomSource = new LegacyFusionRandomSource(_rnd);
            _resultResolver = new FusionResultResolver(_adapter, randomSource);
            _planningService = new FusionPlanningService(
                _adapter,
                _resultResolver,
                randomSource,
                new FusionInheritancePlanner());
            _raceTable = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            LoadFusionTable();
        }

        /// <summary>
        /// Hydrates the internal Race mapping from the centrally loaded Database.
        /// Ensures the sub-system remains data-driven and easily balanced.
        /// </summary>
        private void LoadFusionTable()
        {
            try
            {
                if (Database.FusionRecipes != null && Database.FusionRecipes.Count > 0)
                {
                    foreach (var recipe in Database.FusionRecipes)
                    {
                        RegisterMapping(recipe.ParentA, recipe.ParentB, recipe.Result);
                        // Ensure commutativity: A + B yields the same as B + A
                        RegisterMapping(recipe.ParentB, recipe.ParentA, recipe.Result);
                    }
                }
                else
                {
                    _messenger.Publish("[FusionCalculator] Warning: Fusion recipes not found in Database.", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                _messenger.Publish($"[FusionCalculator] Critical Error loading fusion data: {ex.Message}", ConsoleColor.Red);
            }
        }

        // Internal helper to populate the 2D lookup table.
        private void RegisterMapping(string a, string b, string res)
        {
            if (!_raceTable.ContainsKey(a))
            {
                _raceTable[a] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            _raceTable[a][b] = res;
        }

        /// <summary>
        /// Predicts the fusion result, handling normal, special, rank, and Mitama fusions.
        /// Accounts for Moon Phase influence on Fusion Accidents.
        /// </summary>
        /// <param name="a">The first parent participant.</param>
        /// <param name="b">The second parent participant.</param>
        /// <param name="moonPhase">The current phase from the MoonPhaseSystem.</param>
        /// <returns>A tuple containing the fusion operation type, a target ID, and an accident flag.</returns>
        public (FusionOperationType operation, string? targetEntityId, bool isAccident) CalculateResult(Combatant a, Combatant b, int moonPhase)
        {
            if (a.ActivePersona == null || b.ActivePersona == null)
                return (FusionOperationType.NoFusionPossible, null, false);

            FusionResolvedResult result = _resultResolver.Resolve(new FusionResultRequest(
                _adapter.ToParticipant(a),
                _adapter.ToParticipant(b),
                moonPhase));

            if (!result.IsSuccessful || result.ResultEntityId is null)
            {
                foreach (FusionRuntimeDiagnostic diagnostic in result.Diagnostics)
                {
                    _messenger.Publish($"[Fusion Trace] {diagnostic.Message}", ConsoleColor.DarkGray);
                }

                return (FusionOperationType.NoFusionPossible, null, false);
            }

            string targetEntityId = _adapter.EntityId(result.ResultEntityId.Value);
            FusionOperationType operation = result.Operation switch
            {
                FusionRuntimeOperation.CreateNewEntity => FusionOperationType.CreateNewDemon,
                FusionRuntimeOperation.RankUpParent => FusionOperationType.RankUpParent,
                FusionRuntimeOperation.RankDownParent => FusionOperationType.RankDownParent,
                FusionRuntimeOperation.StatBoost => FusionOperationType.StatBoostFusion,
                _ => FusionOperationType.NoFusionPossible
            };

            _messenger.Publish($"[Fusion Trace] Framework fusion resolved {operation} -> {targetEntityId}", ConsoleColor.DarkGray);
            return (operation, targetEntityId, result.IsAccident);
        }

        internal IReadOnlyList<FusionInheritanceEntry> CreateFrameworkInheritanceDisplayEntries(
            FusionOperationType operation,
            string targetId,
            Combatant previewBaseline,
            IEnumerable<Combatant> materials,
            IEnumerable<string> inherentSkills)
        {
            if (!_adapter.TryGetEntity(_adapter.ContentIdForEntity(targetId), out FusionEntitySnapshot? resultEntity) ||
                resultEntity is null)
            {
                return Array.Empty<FusionInheritanceEntry>();
            }

            var candidates = new List<SkillDefinition>();
            var seen = new HashSet<ContentId>();
            foreach (Combatant material in materials)
            {
                foreach (string skillName in material.GetConsolidatedSkills())
                {
                    ContentId skillId = _adapter.ContentIdForSkill(skillName);
                    if (!seen.Add(skillId))
                    {
                        continue;
                    }

                    if (_adapter.TryGetSkill(skillId, out SkillDefinition? skill) && skill is not null)
                    {
                        candidates.Add(skill);
                    }
                }
            }

            IReadOnlyList<ContentId> naturalSkillIds = operation == FusionOperationType.StatBoostFusion
                ? previewBaseline.GetConsolidatedSkills().Select(_adapter.ContentIdForSkill).ToArray()
                : inherentSkills.Select(_adapter.ContentIdForSkill).ToArray();

            FusionInheritancePlan inheritancePlan = new FusionInheritancePlanner().CreatePlan(new FusionInheritancePlanRequest(
                resultEntity.Definition,
                candidates,
                naturalSkillIds,
                maximumSelections: int.MaxValue));

            return inheritancePlan.Candidates
                .Select(candidate => new FusionInheritanceEntry(
                    candidate.Skill.Id,
                    candidate.Skill.DisplayName,
                    candidate.IsSelectable,
                    candidate.AvailabilityReasonCode))
                .ToArray();
        }

        // Aggregates all unique skills from parents to determine the total inheritable pool.
        public List<string> GetInheritableSkills(params Combatant[] parents)
        {
            var skills = new List<string>();
            foreach (Combatant parent in parents.Where(parent => parent != null))
            {
                foreach (string skillName in parent.GetConsolidatedSkills())
                {
                    if (_adapter.TryGetSkill(_adapter.ContentIdForSkill(skillName), out var skill) &&
                        skill is not null &&
                        skill.Inheritance.IsInheritable)
                    {
                        skills.Add(skill.DisplayName);
                    }
                }
            }

            return skills.Distinct().ToList();
        }

        /// <summary>
        /// Retrieves skills from parents that are specifically marked as exclusive.
        /// Used for UI display purposes (graying out).
        /// </summary>
        public List<string> GetExclusiveSkills(params Combatant[] parents)
        {
            var skills = new List<string>();
            foreach (Combatant parent in parents.Where(parent => parent != null))
            {
                foreach (string skillName in parent.GetConsolidatedSkills())
                {
                    if (_adapter.TryGetSkill(_adapter.ContentIdForSkill(skillName), out var skill) &&
                        skill is not null &&
                        !skill.Inheritance.IsInheritable)
                    {
                        skills.Add(skill.DisplayName);
                    }
                }
            }

            return skills.Distinct().ToList();
        }

        /// <summary>
        /// Attempts to mutate a skill into a higher or lower rank version within the same family.
        /// Used during Fusion Accidents.
        /// </summary>
        public string GetMutatedSkill(string originalSkillName)
        {
            return _adapter.SkillName(_planningService.MutateSkill(_adapter.ContentIdForSkill(originalSkillName)));
        }

        // Calculates the number of skill slots available for inheritance based on total unique parent skills.
        public int GetInheritanceSlotCount(params Combatant[] parents)
        {
            var legalSkills = GetInheritableSkills(parents)
                .Select(_adapter.ContentIdForSkill)
                .Select(id => _adapter.TryGetSkill(id, out var skill) ? skill : null)
                .Where(skill => skill is not null)
                .Cast<SkillDefinition>();
            return _planningService.GetInheritanceSlotCount(legalSkills);
        }
    }
}
