using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Hosting;
using JRPGPrototype.Logic.Runtime;
using JRPGPrototype.Services;

namespace JRPGPrototype.Entities.Components
{
    internal static class LegacyProgressionAdapter
    {
        private static readonly IStatResolutionPolicy StatPolicy = new StandardStatResolutionPolicy();
        private static readonly IResourceGrowthPolicy ResourcePolicy = new StandardResourceGrowthPolicy();
        private static readonly IExperienceCurve ExperienceCurve = new CubicExperienceCurve();
        private static readonly ILevelGrowthPolicy LevelGrowthPolicy = new StandardLevelGrowthPolicy(ExperienceCurve, ResourcePolicy);
        private static readonly IStatAllocationService StatAllocationService = new StatAllocationService(ResourcePolicy);
        private static readonly Random SharedRandom = new();

        public static int GetExpRequired(int level) => (int)ExperienceCurve.GetRequiredExperience(level);

        public static int GetStat(Combatant combatant, StatType stat)
        {
            StatResolutionResult result = StatPolicy.Resolve(new StatResolutionRequest(
                ToStatResolutionActorKindId(combatant),
                ToStatId(stat),
                BaseStats(combatant.CharacterStats),
                ActiveFormStats(combatant.ActivePersona),
                AccessoryModifiers(combatant),
                StatStages(combatant.Buffs)));
            return result.FinalValue;
        }

        public static void RecalculateResources(Combatant combatant)
        {
            ApplyResources(
                combatant,
                ResourcePolicy.Recalculate(new ResourceRecalculationRequest(
                    ResourceSnapshots(combatant),
                    BaseResourceValues(combatant),
                    EffectiveStats(combatant),
                    ResourceCurrentAdjustmentMode.PreserveCurrent)));
        }

        public static void GainExp(Combatant combatant, int amount, IGameIO? io = null, IRandomSource? randomSource = null)
        {
            LevelGrowthResult result = LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
                Progression(combatant),
                StatBlock(combatant),
                ToActorKindId(combatant.Class),
                amount,
                randomSource ?? new RandomSource(SharedRandom),
                ProgressionSubjectKind.Actor,
                ResourceSnapshots(combatant),
                BaseResourceValues(combatant)));

            if (!result.Applied)
            {
                return;
            }

            ApplyProgression(combatant, result.Progression);
            ApplyBaseResources(combatant, result.BaseResourceValues);
            ApplyResources(combatant, new ResourceRecalculationResult(result.Resources));

            foreach (LevelUpEvent levelUp in result.LevelUps)
            {
                if (io is null)
                {
                    continue;
                }

                io.WriteLine($"{combatant.Name} leveled up to {levelUp.Level}!", ConsoleColor.Cyan);
                RuntimeResourceSnapshot? oldHp = levelUp.ResourcesBefore.FirstOrDefault(resource => resource.ResourceId == StandardProgressionIds.Hp);
                RuntimeResourceSnapshot? newHp = levelUp.ResourcesAfter.FirstOrDefault(resource => resource.ResourceId == StandardProgressionIds.Hp);
                RuntimeResourceSnapshot? oldSp = levelUp.ResourcesBefore.FirstOrDefault(resource => resource.ResourceId == StandardProgressionIds.Sp);
                RuntimeResourceSnapshot? newSp = levelUp.ResourcesAfter.FirstOrDefault(resource => resource.ResourceId == StandardProgressionIds.Sp);
                int hpGain = oldHp is null || newHp is null ? 0 : (int)(newHp.Maximum - oldHp.Maximum);
                int spGain = oldSp is null || newSp is null ? 0 : (int)(newSp.Maximum - oldSp.Maximum);
                if (hpGain > 0 || spGain > 0)
                {
                    io.WriteLine($"+{hpGain} Max HP / +{spGain} Max SP", ConsoleColor.Green);
                }
            }
        }

        public static bool AllocateStat(Combatant combatant, StatType stat)
        {
            StatAllocationResult result = StatAllocationService.Allocate(new StatAllocationRequest(
                Progression(combatant),
                StatBlock(combatant),
                ToStatId(stat),
                ResourceSnapshots(combatant),
                BaseResourceValues(combatant)));
            if (!result.Applied)
            {
                return false;
            }

            combatant.CharacterStats[stat] = combatant.CharacterStats.GetValueOrDefault(stat, 0) + 1;
            combatant.StatPoints = result.Progression.UnspentStatPoints;
            RecalculateResources(combatant);
            return true;
        }

        public static void RollbackStats(
            Combatant combatant,
            Dictionary<StatType, int> statBackup,
            int pointBackup)
        {
            StatAllocationResult result = StatAllocationService.Rollback(new StatRollbackRequest(
                Progression(combatant),
                new RuntimeProgressionSnapshot(combatant.Level, combatant.Exp, combatant.LifetimeEarnedExp, pointBackup),
                new RuntimeStatBlockSnapshot(BaseStats(statBackup), EffectiveStatsFromBase(statBackup)),
                ResourceSnapshots(combatant),
                BaseResourceValues(combatant)));

            foreach ((StatType stat, int value) in statBackup)
            {
                combatant.CharacterStats[stat] = value;
            }

            combatant.StatPoints = result.Progression.UnspentStatPoints;
            ApplyResources(combatant, new ResourceRecalculationResult(result.Resources));
        }

        public static void GainPersonaExp(Persona persona, int amount, IGameIO? io = null, IRandomSource? randomSource = null)
        {
            LevelGrowthResult result = LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
                new RuntimeProgressionSnapshot(persona.Level, persona.Exp, persona.LifetimeEarnedExp, 0),
                PersonaStatBlock(persona),
                StandardProgressionIds.WildCard,
                amount,
                randomSource ?? new RandomSource(SharedRandom),
                ProgressionSubjectKind.Form));

            if (!result.Applied)
            {
                return;
            }

            persona.Level = result.Progression.Level;
            persona.Exp = (int)result.Progression.Experience;
            persona.LifetimeEarnedExp = (int)result.Progression.LifetimeExperience;
            ApplyPersonaStats(persona, result.Stats.BaseStats);
            AnnouncePersonaLevelUps(persona, result.LevelUps, io);
        }

        public static void ScalePersonaToLevel(Persona persona, int targetLevel)
        {
            if (targetLevel <= persona.Level)
            {
                persona.RecalculateSkills();
                return;
            }

            while (persona.Level < targetLevel)
            {
                long required = ExperienceCurve.GetRequiredExperience(persona.Level);
                LevelGrowthResult result = LevelGrowthPolicy.ApplyExperience(new LevelGrowthRequest(
                    new RuntimeProgressionSnapshot(persona.Level, 0, persona.LifetimeEarnedExp, 0),
                    PersonaStatBlock(persona),
                    StandardProgressionIds.WildCard,
                    required,
                    new RandomSource(SharedRandom),
                    ProgressionSubjectKind.Form));

                persona.Level = result.Progression.Level;
                ApplyPersonaStats(persona, result.Stats.BaseStats);
                LearnPersonaSkillAtCurrentLevel(persona, null);
            }

            persona.RecalculateSkills();
        }

        private static void AnnouncePersonaLevelUps(
            Persona persona,
            IReadOnlyList<LevelUpEvent> levelUps,
            IGameIO? io)
        {
            foreach (LevelUpEvent levelUp in levelUps)
            {
                if (io is not null)
                {
                    io.WriteLine($"\n[PERSONA] {persona.Name} grew to Lv.{levelUp.Level}!", ConsoleColor.Green);
                }

                foreach ((ContentId statId, decimal value) in levelUp.StatIncreases)
                {
                    if (value <= 0 || io is null)
                    {
                        continue;
                    }

                    io.WriteLine($"-> {ToStatType(statId)} increased!");
                }

                LearnPersonaSkillAtCurrentLevel(persona, io);
            }
        }

        private static void LearnPersonaSkillAtCurrentLevel(Persona persona, IGameIO? io)
        {
            if (!persona.SkillsToLearn.TryGetValue(persona.Level, out string? newSkill))
            {
                return;
            }

            if (persona.SkillSet.Contains(newSkill))
            {
                return;
            }

            persona.SkillSet.Add(newSkill);
            io?.WriteLine($"-> {persona.Name} learned a new skill: {newSkill}!", ConsoleColor.Cyan);
        }

        private static RuntimeProgressionSnapshot Progression(Combatant combatant) =>
            new(combatant.Level, combatant.Exp, combatant.LifetimeEarnedExp, combatant.StatPoints);

        private static void ApplyProgression(Combatant combatant, RuntimeProgressionSnapshot progression)
        {
            combatant.Level = progression.Level;
            combatant.Exp = (int)progression.Experience;
            combatant.LifetimeEarnedExp = (int)progression.LifetimeExperience;
            combatant.StatPoints = progression.UnspentStatPoints;
        }

        private static RuntimeStatBlockSnapshot StatBlock(Combatant combatant) =>
            new(BaseStats(combatant.CharacterStats), EffectiveStats(combatant));

        private static RuntimeStatBlockSnapshot PersonaStatBlock(Persona persona) =>
            new(BaseStats(persona.StatModifiers), BaseStats(persona.StatModifiers));

        private static IEnumerable<KeyValuePair<ContentId, decimal>> BaseStats(Dictionary<StatType, int> stats)
        {
            foreach ((StatType stat, int value) in stats)
            {
                yield return new KeyValuePair<ContentId, decimal>(ToStatId(stat), value);
            }
        }

        private static IEnumerable<KeyValuePair<ContentId, decimal>> EffectiveStatsFromBase(Dictionary<StatType, int> stats) =>
            BaseStats(stats);

        private static IEnumerable<KeyValuePair<ContentId, decimal>> ActiveFormStats(Persona? persona) =>
            persona is null ? [] : BaseStats(persona.StatModifiers);

        private static IEnumerable<KeyValuePair<ContentId, decimal>> EffectiveStats(Combatant combatant)
        {
            foreach (StatType stat in Enum.GetValues<StatType>())
            {
                yield return new KeyValuePair<ContentId, decimal>(ToStatId(stat), GetStat(combatant, stat));
            }
        }

        private static IEnumerable<KeyValuePair<ContentId, decimal>> AccessoryModifiers(Combatant combatant)
        {
            if (combatant.EquippedAccessory is null)
            {
                yield break;
            }

            if (!Enum.TryParse(combatant.EquippedAccessory.ModifierStat, true, out StatType accessoryStat))
            {
                yield break;
            }

            yield return new KeyValuePair<ContentId, decimal>(
                ToStatId(accessoryStat),
                combatant.EquippedAccessory.ModifierValue);
        }

        private static IEnumerable<RuntimeStatStageSnapshot> StatStages(Dictionary<string, int> buffs)
        {
            if (buffs.GetValueOrDefault("PhysAtk") > 0)
            {
                yield return new RuntimeStatStageSnapshot(StandardProgressionIds.PhysicalAttack, 1);
            }
            if (buffs.GetValueOrDefault("PhysAtkDown") > 0)
            {
                yield return new RuntimeStatStageSnapshot(StandardProgressionIds.PhysicalAttack, -1);
            }
            if (buffs.GetValueOrDefault("MagAtk") > 0)
            {
                yield return new RuntimeStatStageSnapshot(StandardProgressionIds.MagicalAttack, 1);
            }
            if (buffs.GetValueOrDefault("MagAtkDown") > 0)
            {
                yield return new RuntimeStatStageSnapshot(StandardProgressionIds.MagicalAttack, -1);
            }
            if (buffs.GetValueOrDefault("Defense") > 0)
            {
                yield return new RuntimeStatStageSnapshot(StandardProgressionIds.Defense, 1);
            }
            if (buffs.GetValueOrDefault("DefenseDown") > 0)
            {
                yield return new RuntimeStatStageSnapshot(StandardProgressionIds.Defense, -1);
            }
            if (buffs.GetValueOrDefault("Agility") > 0)
            {
                yield return new RuntimeStatStageSnapshot(StandardProgressionIds.AgilityTrack, 1);
            }
            if (buffs.GetValueOrDefault("AgilityDown") > 0)
            {
                yield return new RuntimeStatStageSnapshot(StandardProgressionIds.AgilityTrack, -1);
            }
        }

        private static IEnumerable<RuntimeResourceSnapshot> ResourceSnapshots(Combatant combatant)
        {
            yield return new RuntimeResourceSnapshot(
                StandardProgressionIds.Hp,
                Math.Max(0, combatant.CurrentHP),
                Math.Max(Math.Max(0, combatant.MaxHP), Math.Max(0, combatant.CurrentHP)));
            yield return new RuntimeResourceSnapshot(
                StandardProgressionIds.Sp,
                Math.Max(0, combatant.CurrentSP),
                Math.Max(Math.Max(0, combatant.MaxSP), Math.Max(0, combatant.CurrentSP)));
        }

        private static IEnumerable<KeyValuePair<ContentId, decimal>> BaseResourceValues(Combatant combatant)
        {
            yield return new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Hp, combatant.BaseHP);
            yield return new KeyValuePair<ContentId, decimal>(StandardProgressionIds.Sp, combatant.BaseSP);
        }

        private static void ApplyBaseResources(
            Combatant combatant,
            IReadOnlyDictionary<ContentId, decimal> baseResourceValues)
        {
            combatant.BaseHP = (int)baseResourceValues.GetValueOrDefault(StandardProgressionIds.Hp);
            combatant.BaseSP = (int)baseResourceValues.GetValueOrDefault(StandardProgressionIds.Sp);
        }

        private static void ApplyResources(Combatant combatant, ResourceRecalculationResult result)
        {
            RuntimeResourceSnapshot hp = result.GetRequired(StandardProgressionIds.Hp);
            RuntimeResourceSnapshot sp = result.GetRequired(StandardProgressionIds.Sp);
            combatant.MaxHP = (int)hp.Maximum;
            combatant.CurrentHP = (int)hp.Current;
            combatant.MaxSP = (int)sp.Maximum;
            combatant.CurrentSP = (int)sp.Current;
        }

        private static void ApplyPersonaStats(
            Persona persona,
            IReadOnlyDictionary<ContentId, decimal> stats)
        {
            foreach (ContentId statId in StandardProgressionIds.CoreStats)
            {
                persona.StatModifiers[ToStatType(statId)] = (int)stats.GetValueOrDefault(statId);
            }
        }

        private static ContentId ToActorKindId(ClassType classType) => classType switch
        {
            ClassType.Human => StandardProgressionIds.Human,
            ClassType.PersonaUser => StandardProgressionIds.PersonaUser,
            ClassType.WildCard => StandardProgressionIds.WildCard,
            ClassType.Operator => StandardProgressionIds.Operator,
            ClassType.Demon => StandardProgressionIds.Demon,
            _ => StandardProgressionIds.WildCard
        };

        private static ContentId ToStatResolutionActorKindId(Combatant combatant)
        {
            if (combatant.Class == ClassType.Human && combatant.ActivePersona is not null)
            {
                return StandardProgressionIds.WildCard;
            }

            return ToActorKindId(combatant.Class);
        }

        private static ContentId ToStatId(StatType stat) => stat switch
        {
            StatType.St => StandardProgressionIds.Strength,
            StatType.Ma => StandardProgressionIds.Magic,
            StatType.Vi => StandardProgressionIds.Vitality,
            StatType.Ag => StandardProgressionIds.Agility,
            StatType.Lu => StandardProgressionIds.Luck,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unknown stat.")
        };

        private static StatType ToStatType(ContentId id)
        {
            if (id == StandardProgressionIds.Strength) return StatType.St;
            if (id == StandardProgressionIds.Magic) return StatType.Ma;
            if (id == StandardProgressionIds.Vitality) return StatType.Vi;
            if (id == StandardProgressionIds.Agility) return StatType.Ag;
            if (id == StandardProgressionIds.Luck) return StatType.Lu;
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown standard stat ID.");
        }

        private sealed class RandomSource(Random random) : IRandomSource
        {
            public int NextInt32(int minimumInclusive, int maximumExclusive) =>
                random.Next(minimumInclusive, maximumExclusive);

            public decimal NextUnitDecimal() => (decimal)random.NextDouble();
        }
    }
}
