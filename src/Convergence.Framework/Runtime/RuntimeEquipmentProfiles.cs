using Convergence.Content;
using Convergence.Catalog;

namespace Convergence.Runtime;

public enum RuntimeEquipmentProfileDiagnosticCode
{
    MissingEquipmentDefinition,
    SlotProfileMismatch,
    InvalidIdentifier,
    MissingEquipmentInstance
}

public sealed record RuntimeEquipmentProfileDiagnostic(
    RuntimeEquipmentProfileDiagnosticCode Code,
    ContentId SlotId,
    RuntimeInstanceId EquipmentInstanceId,
    ContentId EquipmentId,
    string Message);

public sealed record RuntimeBasicAttackProfile(
    RuntimeInstanceId EquipmentInstanceId,
    ContentId EquipmentId,
    EquipmentBasicAttackDefinition BasicAttack);

public sealed record RuntimeEquipmentProfile
{
    public RuntimeEquipmentProfile(
        IEnumerable<KeyValuePair<ContentId, EquipmentDefinition>>? equippedDefinitions = null,
        IEnumerable<KeyValuePair<ContentId, decimal>>? statModifiers = null,
        RuntimeBasicAttackProfile? basicAttack = null,
        IEnumerable<RuntimeEquipmentProfileDiagnostic>? diagnostics = null)
    {
        EquippedDefinitions = RuntimeSnapshotCollections.Dictionary(equippedDefinitions);
        StatModifiers = RuntimeSnapshotCollections.Dictionary(statModifiers);
        BasicAttack = basicAttack;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public IReadOnlyDictionary<ContentId, EquipmentDefinition> EquippedDefinitions { get; }
    public IReadOnlyDictionary<ContentId, decimal> StatModifiers { get; }
    public RuntimeBasicAttackProfile? BasicAttack { get; }
    public IReadOnlyList<RuntimeEquipmentProfileDiagnostic> Diagnostics { get; }
}

public interface IRuntimeEquipmentProfileResolver
{
    RuntimeEquipmentProfile Resolve(
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot equipment,
        IEquipmentDefinitionRepository equipmentRepository);
}

public sealed class RuntimeEquipmentProfileResolver : IRuntimeEquipmentProfileResolver
{
    private readonly IEquipmentSlotLayoutPolicy _slotLayout;

    public RuntimeEquipmentProfileResolver(IEquipmentSlotLayoutPolicy? slotLayout = null)
    {
        _slotLayout = slotLayout ?? StandardEquipmentSlotLayoutPolicy.Instance;
    }

    public RuntimeEquipmentProfile Resolve(
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot equipment,
        IEquipmentDefinitionRepository equipmentRepository)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(equipmentRepository);

        var definitions = new List<KeyValuePair<ContentId, EquipmentDefinition>>();
        var statModifiers = new Dictionary<ContentId, decimal>();
        var diagnostics = new List<RuntimeEquipmentProfileDiagnostic>();
        RuntimeBasicAttackProfile? basicAttack = null;

        foreach ((ContentId slotId, RuntimeInstanceId equipmentInstanceId) in
                 equipment.EquippedInstanceIds.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
        {
            if (!equipmentInstanceId.IsValid)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    RuntimeEquipmentProfileDiagnosticCode.InvalidIdentifier,
                    slotId,
                    equipmentInstanceId,
                    default,
                    "Equipped equipment instance ID cannot be empty."));
                continue;
            }

            if (!inventory.TryGetEquipmentInstance(
                    equipmentInstanceId,
                    out RuntimeEquipmentInstanceSnapshot? instance,
                    out ContentId ownedSlotId) ||
                instance is null)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    RuntimeEquipmentProfileDiagnosticCode.MissingEquipmentInstance,
                    slotId,
                    equipmentInstanceId,
                    default,
                    $"Equipped equipment instance '{equipmentInstanceId}' is not owned."));
                continue;
            }

            EquipmentSlotLayoutResult inventoryAssignment =
                _slotLayout.ValidateAssignment(ownedSlotId, slotId);
            if (!inventoryAssignment.IsCompatible)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    RuntimeEquipmentProfileDiagnosticCode.SlotProfileMismatch,
                    slotId,
                    equipmentInstanceId,
                    instance.DefinitionId,
                    inventoryAssignment.Message ??
                    $"Equipment instance '{equipmentInstanceId}' is owned for slot '{ownedSlotId}', not '{slotId}'."));
                continue;
            }

            ContentId equipmentId = instance.DefinitionId;
            if (!equipmentRepository.TryGetEquipment(equipmentId, out EquipmentDefinition? definition) ||
                definition is null)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    RuntimeEquipmentProfileDiagnosticCode.MissingEquipmentDefinition,
                    slotId,
                    equipmentInstanceId,
                    equipmentId,
                    $"Equipped item '{equipmentId}' was not found."));
                continue;
            }

            EquipmentSlotLayoutResult definitionLayout =
                _slotLayout.ValidateDefinition(definition);
            EquipmentSlotLayoutResult definitionAssignment =
                _slotLayout.ValidateAssignment(definition.SlotId, slotId);
            if (!definitionLayout.IsCompatible || !definitionAssignment.IsCompatible)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    RuntimeEquipmentProfileDiagnosticCode.SlotProfileMismatch,
                    slotId,
                    equipmentInstanceId,
                    equipmentId,
                    definitionLayout.Message ??
                    definitionAssignment.Message ??
                    $"Equipped item '{equipmentId}' is not compatible with slot '{slotId}'."));
                continue;
            }

            definitions.Add(new KeyValuePair<ContentId, EquipmentDefinition>(slotId, definition));
            if (definition.Weapon is EquipmentWeaponProfileDefinition weapon)
            {
                basicAttack = new RuntimeBasicAttackProfile(
                    equipmentInstanceId,
                    definition.Id,
                    weapon.BasicAttack);
            }

            if (definition.Accessory is EquipmentAccessoryProfileDefinition accessory)
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
