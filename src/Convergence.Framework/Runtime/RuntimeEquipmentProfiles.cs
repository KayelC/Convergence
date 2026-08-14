using Convergence.Content;
using Convergence.Catalog;
using Convergence.Execution;

namespace Convergence.Runtime;

public enum RuntimeEquipmentProfileDiagnosticCode
{
    MissingEquipmentDefinition,
    SlotProfileMismatch,
    InvalidIdentifier,
    MissingEquipmentInstance,
    PolicyRejected
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
        IEnumerable<ContentId>? grantedSkillIds = null,
        RuntimeBasicAttackProfile? basicAttack = null,
        IEnumerable<RuntimeEquipmentProfileDiagnostic>? diagnostics = null)
    {
        EquippedDefinitions = RuntimeSnapshotCollections.Dictionary(equippedDefinitions);
        StatModifiers = RuntimeSnapshotCollections.Dictionary(statModifiers);
        GrantedSkillIds = RuntimeSnapshotCollections.List(grantedSkillIds);
        BasicAttack = basicAttack;
        Diagnostics = RuntimeSnapshotCollections.List(diagnostics);
    }

    public IReadOnlyDictionary<ContentId, EquipmentDefinition> EquippedDefinitions { get; }
    public IReadOnlyDictionary<ContentId, decimal> StatModifiers { get; }
    public IReadOnlyList<ContentId> GrantedSkillIds { get; }
    public RuntimeBasicAttackProfile? BasicAttack { get; }
    public IReadOnlyList<RuntimeEquipmentProfileDiagnostic> Diagnostics { get; }

    public static RuntimeEquipmentProfile Empty { get; } = new();
}

public interface IRuntimeEquipmentProfileResolver
{
    RuntimeEquipmentProfile Resolve(
        RuntimeInventorySnapshot inventory,
        RuntimeEquipmentSnapshot equipment,
        IEquipmentDefinitionRepository equipmentRepository);
}

/// <summary>Resolves the current equipment profile for a live runtime actor.</summary>
public interface IRuntimeActorEquipmentProfileSource
{
    RuntimeEquipmentProfile Resolve(RuntimeActorState actor);
}

/// <summary>Supplies an empty equipment profile to games or actors that do not use equipment.</summary>
public sealed class NoRuntimeActorEquipmentProfileSource : IRuntimeActorEquipmentProfileSource
{
    private NoRuntimeActorEquipmentProfileSource()
    {
    }

    public static NoRuntimeActorEquipmentProfileSource Instance { get; } = new();

    public RuntimeEquipmentProfile Resolve(RuntimeActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return RuntimeEquipmentProfile.Empty;
    }
}

/// <summary>
/// Resolves actor equipment against the inventory ownership snapshot and catalog supplied by a host session.
/// </summary>
public sealed class RuntimeActorEquipmentProfileSource : IRuntimeActorEquipmentProfileSource
{
    private readonly RuntimeInventorySnapshot _inventory;
    private readonly IEquipmentDefinitionRepository _equipment;
    private readonly IRuntimeEquipmentProfileResolver _profiles;

    public RuntimeActorEquipmentProfileSource(
        RuntimeInventorySnapshot inventory,
        IEquipmentDefinitionRepository equipment,
        IRuntimeEquipmentProfileResolver? profiles = null)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        _profiles = profiles ?? new RuntimeEquipmentProfileResolver();
    }

    public RuntimeEquipmentProfile Resolve(RuntimeActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return _profiles.Resolve(_inventory, actor.Equipment, _equipment);
    }
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
        var grantedSkillIds = new List<ContentId>();
        var grantedSkillSet = new HashSet<ContentId>();
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
                EquipmentSlotLayoutPolicyEvaluator.ValidateAssignment(
                    _slotLayout,
                    ownedSlotId,
                    slotId);
            if (!inventoryAssignment.IsCompatible)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    inventoryAssignment.Code == EquipmentSlotLayoutCode.PolicyRejected
                        ? RuntimeEquipmentProfileDiagnosticCode.PolicyRejected
                        : RuntimeEquipmentProfileDiagnosticCode.SlotProfileMismatch,
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
                EquipmentSlotLayoutPolicyEvaluator.ValidateDefinition(_slotLayout, definition);
            EquipmentSlotLayoutResult definitionAssignment =
                EquipmentSlotLayoutPolicyEvaluator.ValidateAssignment(
                    _slotLayout,
                    definition.SlotId,
                    slotId);
            if (!definitionLayout.IsCompatible || !definitionAssignment.IsCompatible)
            {
                diagnostics.Add(new RuntimeEquipmentProfileDiagnostic(
                    definitionLayout.Code == EquipmentSlotLayoutCode.PolicyRejected ||
                    definitionAssignment.Code == EquipmentSlotLayoutCode.PolicyRejected
                        ? RuntimeEquipmentProfileDiagnosticCode.PolicyRejected
                        : RuntimeEquipmentProfileDiagnosticCode.SlotProfileMismatch,
                    slotId,
                    equipmentInstanceId,
                    equipmentId,
                    definitionLayout.Message ??
                    definitionAssignment.Message ??
                    $"Equipped item '{equipmentId}' is not compatible with slot '{slotId}'."));
                continue;
            }

            definitions.Add(new KeyValuePair<ContentId, EquipmentDefinition>(slotId, definition));
            foreach (ContentId grantedSkillId in definition.GrantedSkillIds)
            {
                if (grantedSkillSet.Add(grantedSkillId))
                {
                    grantedSkillIds.Add(grantedSkillId);
                }
            }

            if (definition.Weapon is EquipmentWeaponProfileDefinition weapon)
            {
                basicAttack = new RuntimeBasicAttackProfile(
                    equipmentInstanceId,
                    definition.Id,
                    weapon.BasicAttack);
            }

            if (definition.Armor is EquipmentArmorProfileDefinition armor)
            {
                AddModifier(statModifiers, StandardProgressionIds.Defense, armor.Defense);
                AddModifier(statModifiers, StandardProgressionIds.Evasion, armor.Evasion);
            }

            if (definition.Boots is EquipmentBootsProfileDefinition boots)
            {
                AddModifier(statModifiers, StandardProgressionIds.Evasion, boots.Evasion);
            }

            if (definition.Accessory is EquipmentAccessoryProfileDefinition accessory)
            {
                foreach (StatModifierDefinition modifier in accessory.StatModifiers)
                {
                    AddModifier(statModifiers, modifier.StatId, modifier.Value);
                }
            }
        }

        return new RuntimeEquipmentProfile(
            definitions,
            statModifiers,
            grantedSkillIds,
            basicAttack,
            diagnostics);
    }

    private static void AddModifier(
        IDictionary<ContentId, decimal> modifiers,
        ContentId statId,
        decimal value)
    {
        try
        {
            decimal current = modifiers.TryGetValue(statId, out decimal existing) ? existing : 0m;
            modifiers[statId] = checked(current + value);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                $"Equipment contributions for stat '{statId}' exceed the supported numeric range.",
                exception);
        }
    }
}
