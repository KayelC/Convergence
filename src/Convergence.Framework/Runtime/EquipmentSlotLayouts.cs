using Convergence.Content;

namespace Convergence.Runtime;

/// <summary>Provides the stable equipment-slot IDs used by the supplied standard layout.</summary>
public static class StandardEquipmentSlotIds
{
    public static ContentId Weapon { get; } = ContentId.Parse("weapon");
    public static ContentId Armor { get; } = ContentId.Parse("armor");
    public static ContentId Boots { get; } = ContentId.Parse("boots");
    public static ContentId Accessory { get; } = ContentId.Parse("accessory");

    public static IReadOnlyList<ContentId> All { get; } = Array.AsReadOnly(
    [
        Weapon,
        Armor,
        Boots,
        Accessory
    ]);
}

public enum EquipmentSlotLayoutCode
{
    Compatible,
    UnsupportedSlot,
    ProfileMismatch,
    AssignmentMismatch
}

public sealed record EquipmentSlotLayoutResult(
    EquipmentSlotLayoutCode Code,
    string? Message = null)
{
    public bool IsCompatible => Code == EquipmentSlotLayoutCode.Compatible;

    public static EquipmentSlotLayoutResult Compatible { get; } =
        new(EquipmentSlotLayoutCode.Compatible);
}

/// <summary>Owns the authored slot vocabulary and its definition/assignment compatibility rules.</summary>
public interface IEquipmentSlotLayoutPolicy
{
    IReadOnlyList<ContentId> SlotIds { get; }

    EquipmentSlotLayoutResult ValidateDefinition(EquipmentDefinition definition);

    EquipmentSlotLayoutResult ValidateAssignment(
        ContentId authoredSlotId,
        ContentId targetSlotId);
}

/// <summary>Supplies the conventional Weapon, Armor, Boots, and Accessory layout.</summary>
public sealed class StandardEquipmentSlotLayoutPolicy : IEquipmentSlotLayoutPolicy
{
    private StandardEquipmentSlotLayoutPolicy()
    {
    }

    public static StandardEquipmentSlotLayoutPolicy Instance { get; } = new();

    public IReadOnlyList<ContentId> SlotIds => StandardEquipmentSlotIds.All;

    public EquipmentSlotLayoutResult ValidateDefinition(EquipmentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!SlotIds.Contains(definition.SlotId))
        {
            return new EquipmentSlotLayoutResult(
                EquipmentSlotLayoutCode.UnsupportedSlot,
                $"Equipment slot '{definition.SlotId}' is not part of the standard layout.");
        }

        int profileCount =
            (definition.Weapon is null ? 0 : 1) +
            (definition.Armor is null ? 0 : 1) +
            (definition.Boots is null ? 0 : 1) +
            (definition.Accessory is null ? 0 : 1);
        bool matchingProfile =
            (definition.SlotId == StandardEquipmentSlotIds.Weapon && definition.Weapon is not null) ||
            (definition.SlotId == StandardEquipmentSlotIds.Armor && definition.Armor is not null) ||
            (definition.SlotId == StandardEquipmentSlotIds.Boots && definition.Boots is not null) ||
            (definition.SlotId == StandardEquipmentSlotIds.Accessory && definition.Accessory is not null);
        if (profileCount != 1 || !matchingProfile)
        {
            return new EquipmentSlotLayoutResult(
                EquipmentSlotLayoutCode.ProfileMismatch,
                $"Equipment slot '{definition.SlotId}' requires exactly its matching standard profile.");
        }

        return EquipmentSlotLayoutResult.Compatible;
    }

    public EquipmentSlotLayoutResult ValidateAssignment(
        ContentId authoredSlotId,
        ContentId targetSlotId)
    {
        if (!authoredSlotId.IsValid || !SlotIds.Contains(authoredSlotId))
        {
            return new EquipmentSlotLayoutResult(
                EquipmentSlotLayoutCode.UnsupportedSlot,
                $"Authored equipment slot '{authoredSlotId}' is not part of the standard layout.");
        }

        if (!targetSlotId.IsValid || !SlotIds.Contains(targetSlotId))
        {
            return new EquipmentSlotLayoutResult(
                EquipmentSlotLayoutCode.UnsupportedSlot,
                $"Target equipment slot '{targetSlotId}' is not part of the standard layout.");
        }

        return authoredSlotId == targetSlotId
            ? EquipmentSlotLayoutResult.Compatible
            : new EquipmentSlotLayoutResult(
                EquipmentSlotLayoutCode.AssignmentMismatch,
                $"Equipment authored for slot '{authoredSlotId}' cannot be assigned to '{targetSlotId}'.");
    }
}
