using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JRPGPrototype.Core;
using JRPGPrototype.Entities;
using JRPGPrototype.Data;

namespace JRPGPrototype.Logic.Battle.Effects
{
    /// <summary>
    /// The primary strategy for all offensive actions (Physical and Magical).
    /// Handles Affinities, Critical Hits, Reflection, Instant Kills, and Knowledge discovery.
    /// </summary>
    public class DamageEffect : IBattleEffect
    {
        private readonly Element _element;

        // Constructor allows the Registry to create one instance per Element type.
        public DamageEffect(Element element)
        {
            _element = element;
        }

        public List<CombatResult> Apply(
            Combatant user,
            List<Combatant> targets,
            int power,
            string metadata,
            IBattleMessenger messenger,
            StatusRegistry status,
            BattleKnowledge knowledge)
        {
            var results = new List<CombatResult>();

            // Determine if this is an Instant Kill skill (e.g., Hama, Mudo)
            bool isInstantKill = metadata.ToLower().Contains("instant kill");

            foreach (var target in targets)
            {
                if (target.IsDead) continue;

                // 1. Check for Repel (Shields or Innate)
                // Repel takes absolute priority in the SMT calculation stack.
                Affinity aff = CombatMath.GetEffectiveAffinity(target, _element);

                if (aff == Affinity.Repel)
                {
                    messenger.Publish($"{target.Name} repelled the attack!", ConsoleColor.Red);

                    // Calculate reflected damage against the USER using their own stats
                    int refDmg = CombatMath.CalculateReflectedDamage(user, power, _element);
                    var repResult = user.ReceiveDamage(refDmg, _element, false);

                    messenger.Publish($"{user.Name} is hit by the reflection!", ConsoleColor.Red);
                    ReportDamageResult(repResult, user.Name, messenger);

                    // Rule: A repelled attack immediately ends the phase (Repel is worst-case HitType)
                    repResult.Type = HitType.Repel;
                    results.Add(repResult);

                    // Stop processing other targets for this skill if one repels (SMT Standard)
                    break;
                }

                // 2. Handle Instant Kill (Hama/Mudo) logic
                if (isInstantKill)
                {
                    // IK Rule: Null/Absorb function as normal blocks against Instant Kill
                    if (aff == Affinity.Null || aff == Affinity.Absorb)
                    {
                        knowledge.Learn(target.SourceId, _element, aff);
                        messenger.Publish("Blocked!", ConsoleColor.Gray);
                        results.Add(new CombatResult { Type = HitType.Null });
                        continue;
                    }

                    // Extract the percentage accuracy from the metadata (e.g., "Hama (40%)")
                    string ikAccuracy = "25%"; // Default
                    Match match = Regex.Match(metadata, @"(\d+%)");
                    if (match.Success) ikAccuracy = match.Value;

                    if (CombatMath.CalculateInstantKill(user, target, ikAccuracy))
                    {
                        int hpBefore = target.CurrentHP;
                        target.CurrentHP = 0; // Immediate Death
                        knowledge.Learn(target.SourceId, _element, aff);
                        messenger.Publish("INSTANT KILL!", ConsoleColor.Red);

                        // IK hits count as Weakness for Press Turn purposes
                        results.Add(new CombatResult { Type = HitType.Weakness, DamageDealt = hpBefore });
                        continue;
                    }
                }

                // 3. Logic: Execute Standard Damage Math
                // Returns the final damage value and whether it was a critical hit.
                int damage = CombatMath.CalculateDamage(user, target, power, _element, out bool isCritical);
                CombatResult result = target.ReceiveDamage(damage, _element, isCritical);

                // 4. Knowledge: Record the discovery for the Player's UI/AI memory
                knowledge.Learn(target.SourceId, _element, aff);

                // 5. UI: Report the result (Damage, Weakness, Block, etc.)
                ReportDamageResult(result, target.Name, messenger);

                // 6. Logic: Secondary Ailment Infliction
                // Only try to infect if the attack actually landed (not Nulled or Absorbed)
                if (result.Type != HitType.Null && result.Type != HitType.Absorb)
                {
                    // metadata usually contains the skill effect string (e.g., "Poison 40%")
                    if (status.TryInflict(user, target, metadata))
                    {
                        // Note: TryInflict handles its own messenger publishing for success
                    }
                }

                results.Add(result);
            }

            return results;
        }

        // Standardized reporting for damage results.
        private void ReportDamageResult(CombatResult result, string targetName, IBattleMessenger messenger)
        {
            if (result.DamageDealt > 0)
            {
                string msg = $"{targetName} took {result.DamageDealt} damage";
                if (result.IsCritical) msg += " (CRITICAL)";
                if (result.Type == HitType.Weakness) msg += " (WEAKNESS)";

                messenger.Publish(msg);
            }
            else if (result.Type == HitType.Absorb)
            {
                messenger.Publish($"{targetName} absorbed the attack!", ConsoleColor.Green);
            }
            else if (result.Type == HitType.Null)
            {
                messenger.Publish($"{targetName} blocked the attack!");
            }
            else if (result.Type == HitType.Miss)
            {
                messenger.Publish("MISS!", ConsoleColor.Gray, 400);
            }
        }
    }
}