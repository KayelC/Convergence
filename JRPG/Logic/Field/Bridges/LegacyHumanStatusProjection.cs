using System.Text;
using JRPGPrototype.Core;
using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Entities;
using JRPGPrototype.Logic.Core;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Logic.Field.Bridges;

internal sealed record LegacyHumanStatusProjection(RuntimeActorSnapshot Snapshot, ClassType Class, long ExpRequired)
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

    public static LegacyHumanStatusProjection FromCombatant(Combatant entity)
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
            ]);

        return new LegacyHumanStatusProjection(snapshot, entity.Class, entity.ExpRequired);
    }

    public string Render()
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

    private static ContentId ToContentId(string value)
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
