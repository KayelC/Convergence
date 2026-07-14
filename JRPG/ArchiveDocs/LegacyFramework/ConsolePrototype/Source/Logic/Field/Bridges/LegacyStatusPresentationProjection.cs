using System.Collections.ObjectModel;
using System.Text;
using JRPGPrototype.Core;
using JRPGPrototype.Data;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Field.Bridges;

internal sealed record LegacyStatusPresentationProjection(
    RuntimeActorSnapshot Snapshot,
    ClassType Class,
    long ExpRequired,
    LegacyPersonaStatusProjection? ActivePersona,
    IReadOnlyList<string> DisplaySkills)
{
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");
    private static readonly ContentId Strength = ContentId.Parse("strength");
    private static readonly ContentId Magic = ContentId.Parse("magic");
    private static readonly ContentId Vitality = ContentId.Parse("vitality");
    private static readonly ContentId Agility = ContentId.Parse("agility");
    private static readonly ContentId Luck = ContentId.Parse("luck");

    private static readonly IReadOnlyList<(StatType Legacy, ContentId Clean)> StatOrder =
    [
        (StatType.St, Strength),
        (StatType.Ma, Magic),
        (StatType.Vi, Vitality),
        (StatType.Ag, Agility),
        (StatType.Lu, Luck)
    ];

    public static LegacyStatusPresentationProjection FromCombatant(Combatant entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        RuntimeActorReferenceSnapshot reference = LegacyRuntimeIdentityRegistry.Shared.ActorReference(entity);
        var baseStats = new List<KeyValuePair<ContentId, decimal>>();
        var effectiveStats = new List<KeyValuePair<ContentId, decimal>>();
        foreach ((StatType legacy, ContentId clean) in StatOrder)
        {
            baseStats.Add(new KeyValuePair<ContentId, decimal>(
                clean,
                entity.CharacterStats.GetValueOrDefault(legacy, 0)));
            effectiveStats.Add(new KeyValuePair<ContentId, decimal>(
                clean,
                entity.GetStat(legacy)));
        }

        var equipment = new List<KeyValuePair<EquipmentSlot, ContentId>>();
        AddEquipment(equipment, EquipmentSlot.Weapon, entity.EquippedWeapon?.Id);
        AddEquipment(equipment, EquipmentSlot.Armor, entity.EquippedArmor?.Id);
        AddEquipment(equipment, EquipmentSlot.Boots, entity.EquippedBoots?.Id);
        AddEquipment(equipment, EquipmentSlot.Accessory, entity.EquippedAccessory?.Id);

        var snapshot = new RuntimeActorSnapshot(
            new RuntimeActorIdentitySnapshot(
                reference.InstanceId,
                reference.EntityDefinitionId,
                ActorKind(entity.Class),
                entity.Name),
            new RuntimeActorOwnershipSnapshot(ContentId.Parse("player"), ContentId.Parse("player_team")),
            new RuntimeActorDeploymentSnapshot(RuntimeActorDeployment.Active, IsActive: true),
            new RuntimeProgressionSnapshot(entity.Level, entity.Exp, entity.LifetimeEarnedExp, entity.StatPoints),
            [
                CreateResource(Hp, entity.CurrentHP, entity.MaxHP),
                CreateResource(Sp, entity.CurrentSP, entity.MaxSP)
            ],
            new RuntimeStatBlockSnapshot(baseStats, effectiveStats),
            new RuntimeSkillStateSnapshot(entity.GetConsolidatedSkills().Select(ToContentId)),
            new RuntimeFormStockSnapshot(
                entity.ActivePersona is null ? null : LegacyRuntimeIdentityRegistry.Shared.PersonaReference(entity.ActivePersona),
                entity.PersonaStock.Select(LegacyRuntimeIdentityRegistry.Shared.PersonaReference),
                entity.DemonStock.Select(LegacyRuntimeIdentityRegistry.Shared.ActorReference)),
            new RuntimeEquipmentSnapshot(equipment),
            new RuntimeBattleStatusSnapshot(),
            new RuntimeBattleActivationSnapshot(),
            [
                new KeyValuePair<ContentId, decimal>(Hp, entity.BaseHP),
                new KeyValuePair<ContentId, decimal>(Sp, entity.BaseSP)
            ],
            Hp);

        return new LegacyStatusPresentationProjection(
            snapshot,
            entity.Class,
            entity.ExpRequired,
            entity.ActivePersona is null ? null : LegacyPersonaStatusProjection.FromPersona(entity.ActivePersona),
            Array.AsReadOnly(entity.GetConsolidatedSkills().ToArray()));
    }

    public string RenderHumanStatus()
    {
        var builder = new StringBuilder();
        RuntimeResourceSnapshot hp = Snapshot.Resources.Single(resource => resource.ResourceId == Hp);
        RuntimeResourceSnapshot sp = Snapshot.Resources.Single(resource => resource.ResourceId == Sp);

        builder.AppendLine("=== STATUS & PARAMETERS ===");
        builder.AppendLine($"Name: {Snapshot.Identity.DisplayName} (Lv.{Snapshot.Progression.Level}) | Class: {Class}");
        builder.AppendLine($"HP: {(int)hp.Current,3}/{(int)hp.Maximum,3} SP: {(int)sp.Current,3}/{(int)sp.Maximum,3}");
        builder.AppendLine($"EXP: {Snapshot.Progression.Experience,6}/{ExpRequired,6} Next: {ExpRequired - Snapshot.Progression.Experience,6}");
        builder.AppendLine("-----------------------------");

        foreach ((StatType legacy, ContentId clean) in StatOrder)
        {
            int total = (int)Snapshot.Stats.EffectiveStats.GetValueOrDefault(clean);
            int baseValue = (int)Snapshot.Stats.BaseStats.GetValueOrDefault(clean);
            int modifier = total - baseValue;
            if (modifier > 0)
            {
                builder.AppendLine($"{legacy,-4}: {total,3} (+{modifier})");
            }
            else
            {
                builder.AppendLine($"{legacy,-4}: {total,3}");
            }
        }

        builder.Append("-----------------------------");
        return builder.ToString();
    }

    public string RenderDemonDetails()
    {
        var builder = new StringBuilder();
        RuntimeResourceSnapshot hp = Snapshot.Resources.Single(resource => resource.ResourceId == Hp);
        RuntimeResourceSnapshot sp = Snapshot.Resources.Single(resource => resource.ResourceId == Sp);

        builder.AppendLine("=== DEMON DETAILS ===");
        builder.AppendLine($"Name: {Snapshot.Identity.DisplayName} (Lv.{Snapshot.Progression.Level})");
        builder.AppendLine($"HP: {(int)hp.Current,3}/{(int)hp.Maximum,3} SP: {(int)sp.Current,3}/{(int)sp.Maximum,3}");
        builder.AppendLine($"EXP: {Snapshot.Progression.Experience,6}/{ExpRequired,6} Next: {ExpRequired - Snapshot.Progression.Experience,6}");
        builder.AppendLine("-----------------------------");

        foreach ((StatType legacy, ContentId clean) in StatOrder)
        {
            int total = (int)Snapshot.Stats.EffectiveStats.GetValueOrDefault(clean);
            builder.AppendLine($"{legacy,-4}: {total,3}");
        }

        builder.AppendLine();
        builder.AppendLine("RESISTANCES:");
        if (ActivePersona is not null)
        {
            AppendAffinities(builder, ActivePersona.Affinities);
        }

        builder.AppendLine("-----------------------------");
        builder.AppendLine("Skills:");
        foreach (string skill in DisplaySkills)
        {
            builder.AppendLine($" - {skill}");
        }

        if (ActivePersona is not null)
        {
            AppendNextSkills(builder, ActivePersona.NextSkills, ActivePersona.HasLearnableSkills);
        }

        return builder.ToString();
    }

    public string OrganizationSlotLabel(int index)
    {
        string label = index == 0 ? "Leader: " : $"Slot {index + 1}: ";
        return $"{label}{Snapshot.Identity.DisplayName,-15} (Lv.{Snapshot.Progression.Level})";
    }

    public string DemonStockLabel(bool isInParty)
    {
        string status = isInParty ? "[PARTY]" : "[STOCK]";
        return $"{Snapshot.Identity.DisplayName,-15} (Lv.{Snapshot.Progression.Level}) {status}";
    }

    public string SummonTargetLabel(bool isInParty)
    {
        string status = isInParty ? "[IN PARTY]" : Snapshot.Resources.Any(resource => resource.ResourceId == Hp && resource.Current <= 0) ? "[DEAD]" : "";
        return $"{Snapshot.Identity.DisplayName,-15} (Lv.{Snapshot.Progression.Level}) {status}";
    }

    public static string EmptyOrganizationSlotLabel(int index) => $"Slot {index + 1}: [EMPTY]";

    public static string ReturnToCompLabel(Combatant occupant) =>
        $"[ RETURN {FromCombatant(occupant).Snapshot.Identity.DisplayName.ToUpperInvariant()} TO COMP ]";

    public static string EquipmentSlotLabel(EquipmentSlotMenuCommand command, string resolvedName) => command switch
    {
        EquipmentSlotMenuCommand.Weapon => $"Weapon:    {resolvedName}",
        EquipmentSlotMenuCommand.Armor => $"Armor:     {resolvedName}",
        EquipmentSlotMenuCommand.Boots => $"Boots:     {resolvedName}",
        EquipmentSlotMenuCommand.Accessory => $"Accessory: {resolvedName}",
        _ => "Back"
    };

    private static void AppendAffinities(StringBuilder builder, IReadOnlyDictionary<Element, Affinity> affinities)
    {
        foreach (Element elem in Enum.GetValues<Element>())
        {
            if (elem == Element.None) continue;
            Affinity aff = affinities.GetValueOrDefault(elem, Affinity.Normal);
            if (aff != Affinity.Normal)
            {
                builder.AppendLine($" {elem,-10}: {aff}");
            }
        }
    }

    private static void AppendNextSkills(
        StringBuilder builder,
        IReadOnlyList<KeyValuePair<int, string>> nextSkills,
        bool hasLearnableSkills)
    {
        if (nextSkills.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Next to Learn:");
            foreach (KeyValuePair<int, string> ns in nextSkills)
            {
                builder.AppendLine($" [Lv.{ns.Key,2}] {ns.Value}");
            }
        }
        else if (hasLearnableSkills)
        {
            builder.AppendLine();
            builder.AppendLine("(Mastered)");
        }
    }

    private static void AddEquipment(List<KeyValuePair<EquipmentSlot, ContentId>> equipment, EquipmentSlot slot, string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            equipment.Add(new KeyValuePair<EquipmentSlot, ContentId>(slot, ToContentId(id)));
        }
    }

    private static RuntimeResourceSnapshot CreateResource(ContentId resourceId, int current, int maximum) =>
        new(resourceId, Math.Max(0, current), Math.Max(Math.Max(0, maximum), Math.Max(0, current)));

    private static ContentId ActorKind(ClassType classType) => classType switch
    {
        ClassType.PersonaUser => ContentId.Parse("persona_user"),
        ClassType.WildCard => ContentId.Parse("wild_card"),
        ClassType.Operator => ContentId.Parse("operator"),
        ClassType.Demon => ContentId.Parse("demon"),
        _ => ContentId.Parse("human")
    };

    internal static ContentId ToContentId(string value)
    {
        string normalized = new string(value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('_');
        return ContentId.Parse(string.IsNullOrWhiteSpace(normalized) ? "legacy_unknown" : normalized);
    }
}

internal sealed record LegacyPersonaStatusProjection(
    RuntimeActorReferenceSnapshot Reference,
    RuntimeProgressionSnapshot Progression,
    RuntimeStatBlockSnapshot Stats,
    RuntimeSkillStateSnapshot Skills,
    int DisplayLevel,
    string Race,
    long ExpRequired,
    IReadOnlyDictionary<Element, Affinity> Affinities,
    IReadOnlyList<string> DisplaySkills,
    IReadOnlyList<KeyValuePair<int, string>> NextSkills,
    bool HasLearnableSkills)
{
    public static LegacyPersonaStatusProjection FromPersona(Persona persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        var stats = new List<KeyValuePair<ContentId, decimal>>();
        foreach (StatType stat in Enum.GetValues<StatType>())
        {
            stats.Add(new KeyValuePair<ContentId, decimal>(
                LegacyStatusPresentationProjection.ToContentId(stat.ToString()),
                persona.StatModifiers.GetValueOrDefault(stat, 0)));
        }

        return new LegacyPersonaStatusProjection(
            LegacyRuntimeIdentityRegistry.Shared.PersonaReference(persona),
            new RuntimeProgressionSnapshot(Math.Max(1, persona.Level), persona.Exp, persona.LifetimeEarnedExp, 0),
            new RuntimeStatBlockSnapshot(stats, stats),
            new RuntimeSkillStateSnapshot(persona.SkillSet.Select(LegacyStatusPresentationProjection.ToContentId)),
            persona.Level,
            persona.Race,
            persona.ExpRequired,
            new ReadOnlyDictionary<Element, Affinity>(new Dictionary<Element, Affinity>(persona.AffinityMap)),
            Array.AsReadOnly(persona.SkillSet.ToArray()),
            Array.AsReadOnly(persona.SkillsToLearn
                .Where(skill => skill.Key > persona.Level)
                .OrderBy(skill => skill.Key)
                .Take(3)
                .ToArray()),
            persona.SkillsToLearn.Count > 0);
    }

    public string StockLabel(bool isEquipped) =>
        $"{Reference.DisplayName,-15} (Lv.{DisplayLevel}) {Race,-10} {(isEquipped ? "[E]" : "")}";

    public string RenderDetails(bool isEquipped)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"=== PERSONA DETAILS {(isEquipped ? "[EQUIPPED]" : "")} ===");
        builder.AppendLine($"Name: {Reference.DisplayName} (Lv.{DisplayLevel}) | Race: {Race}");
        builder.AppendLine($"EXP: {Progression.Experience,6}/{ExpRequired,6} Next: {ExpRequired - Progression.Experience,6}");
        builder.AppendLine("-----------------------------");
        builder.AppendLine("Raw Stats:");

        foreach (StatType stat in new[] { StatType.St, StatType.Ma, StatType.Vi, StatType.Ag, StatType.Lu })
        {
            int val = (int)Stats.BaseStats.GetValueOrDefault(LegacyStatusPresentationProjection.ToContentId(stat.ToString()));
            builder.AppendLine($" {stat,-4}: {val,3}");
        }

        builder.AppendLine();
        builder.AppendLine("RESISTANCES:");
        foreach (Element elem in Enum.GetValues<Element>())
        {
            if (elem == Element.None) continue;
            Affinity aff = Affinities.GetValueOrDefault(elem, Affinity.Normal);
            if (aff != Affinity.Normal)
            {
                builder.AppendLine($" {elem,-10}: {aff}");
            }
        }

        builder.AppendLine("-----------------------------");
        builder.AppendLine("Skills:");
        foreach (string skill in DisplaySkills)
        {
            builder.AppendLine($" - {skill}");
        }

        if (NextSkills.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Next to Learn:");
            foreach (KeyValuePair<int, string> ns in NextSkills)
            {
                builder.AppendLine($" [Lv.{ns.Key,2}] {ns.Value}");
            }
        }
        else if (HasLearnableSkills)
        {
            builder.AppendLine();
            builder.AppendLine("(Mastered)");
        }

        return builder.ToString();
    }
}
