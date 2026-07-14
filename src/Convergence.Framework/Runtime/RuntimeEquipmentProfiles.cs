using Convergence.Content;
using Convergence.Catalog;

namespace Convergence.Runtime;

public enum RuntimeEquipmentProfileDiagnosticCode
{
    MissingEquipmentDefinition,
    SlotProfileMismatch,
    InvalidIdentifier
}

public sealed record RuntimeEquipmentProfileDiagnostic(
    RuntimeEquipmentProfileDiagnosticCode Code,
    EquipmentSlot Slot,
    ContentId EquipmentId,
    string Message);

public sealed record RuntimeBasicAttackProfile(
    ContentId EquipmentId,
    EquipmentBasicAttackDefinition BasicAttack);

public sealed record RuntimeEquipmentProfile
{
    public RuntimeEquipmentProfile(
        IEnumerable<KeyValuePair<EquipmentSlot, EquipmentDefinition>>? equippedDefinitions = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? statModifiers = null,
        RuntimeBasicAttackProfile? basicAttack = null,
        IEnumerable<RuntimeEquipmentProfileDiagnostic>? diagnostics = null)
    {
        EquippedDefinitions = RuntimeSnapshotCollections.Dictionary(equippedDefinitions);
        StatModifiers = RuntimeSnapshotCollections.Dictionary(statModifiers);
        BasicAttack = basicAttack;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public IReadOnlyDictionary<EquipmentSlot, EquipmentDefinition> EquippedDefinitions { get; }
    public IReadOnlyDictionary<ContentId, decimal> StatModifiers { get; }
    public RuntimeBasicAttackProfile? BasicAttack { get; }
    public IReadOnlyList<RuntimeEquipmentProfileDiagnostic> Diagnostics { get; }
}

public interface IRuntimeEquipmentProfileResolver
{
    RuntimeEquipmentProfile Resolve(
        RuntimeEquipmentSnapshot equipment,
        IEquipmentDefinitionRepository equipmentRepository);
}

public sealed class RuntimeEquipmentProfileResolver : IRuntimeEquipmentProfileResolver
{
    public RuntimeEquipmentProfile Resolve(
        RuntimeEquipmentSnapshot equipment,
        IEquipmentDefinitionRepository equipmentRepository)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(equipmentRepository);

        var definitions = new List<KeyValuePair<EquipmentSlot, EquipmentDefinition>>();
        var statModifiers = new Dictionary<ContentId, decimal>();
        var diagnostics = new List<RuntimeEquipmentProfileDiagnostic>();
        RuntimeBasicAttackProfile? basicAttack = null;

        foreach ((EquipmentSlot slot, ContentId equipmentId) in equipment.EquippedItemIds.OrderBy(pair => pair.Key))
        {
            if (!equipmentId.IsValid)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    RuntimeEquipmentProfileDiagnosticCode.InvalidIdentifier,
                    slot,
                    equipmentId,
                    "Equipped item ID cannot be empty."));
                continue;
            }

            if (!equipmentRepository.TryGetEquipment(equipmentId, out EquipmentDefinition? definition) ||
                definition is null)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    RuntimeEquipmentProfileDiagnosticCode.MissingEquipmentDefinition,
                    slot,
                    equipmentId,
                    $"Equipped item '{equipmentId}' was not found."));
                continue;
            }

            if (definition.Slot != slot)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    RuntimeEquipmentProfileDiagnosticCode.SlotProfileMismatch,
                    slot,
                    equipmentId,
                    $"Equipped item '{equipmentId}' is authored for slot '{definition.Slot}', not '{slot}'."));
                continue;
            }

            definitions.Add(new KeyValuePair<EquipmentSlot, EquipmentDefinition>(slot, definition));
            if (slot == EquipmentSlot.Weapon && definition.Weapon is EquipmentWeaponProfileDefinition weapon)
            {
                basicAttack = new RuntimeBasicAttackProfile(definition.Id, weapon.BasicAttack);
            }

            if (slot == EquipmentSlot.Accessory && definition.Accessory is EquipmentAccessoryProfileDefinition accessory)
            {
                foreach (StatModifierDefinition modifier in accessory.StatModifiers)
                {
                    statModifiers[modifier.StatId] =
                        statModifiers.GetValueOrDefault(modifier.StatId) + modifier.Value;
                }
            }
        }

        return new RuntimeEquipmentProfile(
            definitions,
            statModifiers,
            basicAttack,
            diagnostics);
    }
}
