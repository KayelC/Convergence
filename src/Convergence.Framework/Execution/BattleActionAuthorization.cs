using Convergence.Catalog;
using Convergence.Content;
using Convergence.Runtime;

namespace Convergence.Execution;

public enum BattleActionAuthorizationDiagnosticCode
{
    SkillNotEquipped,
    SkillDefinitionMissing,
    SkillDefinitionSubstituted,
    BasicAttackUnavailable,
    BasicAttackSourceMismatch,
    BasicAttackDefinitionMismatch,
    BasicAttackTargetingMismatch,
    ItemDefinitionMissing,
    ItemDefinitionSubstituted
}

public sealed class BattleActionAuthorizationDiagnostic
{
    public BattleActionAuthorizationDiagnostic(
        BattleActionAuthorizationDiagnosticCode code,
        string message)
    {
        Code = code;
        Message = string.IsNullOrWhiteSpace(message)
            ? "Battle action is not authorized."
            : message;
    }

    public BattleActionAuthorizationDiagnosticCode Code { get; }
    public string Message { get; }
}

public sealed class BattleActionAuthorizationResult
{
    public BattleActionAuthorizationResult(
        IEnumerable<BattleActionAuthorizationDiagnostic>? diagnostics = null)
    {
        Diagnostics = Array.AsReadOnly(diagnostics?.ToArray() ?? []);
    }

    public bool IsAuthorized => Diagnostics.Count == 0;
    public IReadOnlyList<BattleActionAuthorizationDiagnostic> Diagnostics { get; }

    public static BattleActionAuthorizationResult Authorized { get; } = new();

    public static BattleActionAuthorizationResult Rejected(
        BattleActionAuthorizationDiagnosticCode code,
        string message) =>
        new([new BattleActionAuthorizationDiagnostic(code, message)]);
}

public sealed class BattleBasicAttackProfile
{
    public BattleBasicAttackProfile(
        ContentId actionId,
        EquipmentBasicAttackDefinition basicAttack,
        TargetingDefinition targeting)
    {
        if (!actionId.IsValid)
        {
            throw new ArgumentException("Basic-attack action ID must be valid.", nameof(actionId));
        }

        ActionId = actionId;
        BasicAttack = basicAttack ?? throw new ArgumentNullException(nameof(basicAttack));
        Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
    }

    public ContentId ActionId { get; }
    public EquipmentBasicAttackDefinition BasicAttack { get; }
    public TargetingDefinition Targeting { get; }
}

public interface IBattleBasicAttackProfileSource
{
    BattleBasicAttackProfile? Resolve(RuntimeActorState actor);
}

public sealed class NoBattleBasicAttackProfileSource : IBattleBasicAttackProfileSource
{
    private NoBattleBasicAttackProfileSource()
    {
    }

    public static NoBattleBasicAttackProfileSource Instance { get; } = new();

    public BattleBasicAttackProfile? Resolve(RuntimeActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return null;
    }
}

public sealed class EquipmentBattleBasicAttackProfileSource : IBattleBasicAttackProfileSource
{
    private readonly IRuntimeActorEquipmentProfileSource _equipmentProfiles;
    private readonly TargetingDefinition _targeting;

    public EquipmentBattleBasicAttackProfileSource(
        RuntimeInventorySnapshot inventory,
        IEquipmentDefinitionRepository equipment,
        TargetingDefinition targeting,
        IRuntimeEquipmentProfileResolver? profiles = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(equipment);
        _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        _equipmentProfiles = new RuntimeActorEquipmentProfileSource(inventory, equipment, profiles);
    }

    public EquipmentBattleBasicAttackProfileSource(
        IRuntimeActorEquipmentProfileSource equipmentProfiles,
        TargetingDefinition targeting)
    {
        _equipmentProfiles = equipmentProfiles ?? throw new ArgumentNullException(nameof(equipmentProfiles));
        _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
    }

    public BattleBasicAttackProfile? Resolve(RuntimeActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RuntimeEquipmentProfile equipmentProfile = _equipmentProfiles.Resolve(actor);
        return equipmentProfile.BasicAttack is RuntimeBasicAttackProfile basicAttack
            ? new BattleBasicAttackProfile(
                basicAttack.EquipmentId,
                basicAttack.BasicAttack,
                _targeting)
            : null;
    }
}

public interface IBattleActionAuthorizationPolicy
{
    BattleActionAuthorizationResult Authorize(
        RuntimeActorState actor,
        BattleActionCommand command);
}

public sealed class CatalogBattleActionAuthorizationPolicy : IBattleActionAuthorizationPolicy
{
    private readonly ISkillDefinitionRepository _skills;
    private readonly IItemDefinitionRepository _items;
    private readonly IBattleBasicAttackProfileSource _basicAttacks;
    private readonly IRuntimeActorEquipmentProfileSource _equipmentProfiles;

    public CatalogBattleActionAuthorizationPolicy(
        ISkillDefinitionRepository skills,
        IItemDefinitionRepository items,
        IBattleBasicAttackProfileSource basicAttacks,
        IRuntimeActorEquipmentProfileSource? equipmentProfiles = null)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _basicAttacks = basicAttacks ?? throw new ArgumentNullException(nameof(basicAttacks));
        _equipmentProfiles = equipmentProfiles ?? NoRuntimeActorEquipmentProfileSource.Instance;
    }

    public BattleActionAuthorizationResult Authorize(
        RuntimeActorState actor,
        BattleActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            SkillBattleActionCommand skill => AuthorizeSkill(actor, skill),
            ItemBattleActionCommand item => AuthorizeItem(item),
            BasicAttackBattleActionCommand basicAttack => AuthorizeBasicAttack(actor, basicAttack),
            _ => BattleActionAuthorizationResult.Authorized
        };
    }

    private BattleActionAuthorizationResult AuthorizeSkill(
        RuntimeActorState actor,
        SkillBattleActionCommand command) =>
        CatalogSkillActionAuthorization.Authorize(
            actor,
            command.Skill,
            _skills,
            _equipmentProfiles.Resolve(actor).GrantedSkillIds);

    private BattleActionAuthorizationResult AuthorizeItem(ItemBattleActionCommand command)
    {
        if (!_items.TryGetItem(command.Item.Id, out ItemDefinition? canonical) || canonical is null)
        {
            return BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.ItemDefinitionMissing,
                $"Item '{command.Item.Id}' is not available from the authorization catalog.");
        }

        return ReferenceEquals(canonical, command.Item)
            ? BattleActionAuthorizationResult.Authorized
            : BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.ItemDefinitionSubstituted,
                $"Item '{command.Item.Id}' is not its canonical catalog definition.");
    }

    private BattleActionAuthorizationResult AuthorizeBasicAttack(
        RuntimeActorState actor,
        BasicAttackBattleActionCommand command)
    {
        BattleBasicAttackProfile? profile = _basicAttacks.Resolve(actor);
        if (profile is null)
        {
            return BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.BasicAttackUnavailable,
                $"Actor '{actor.InstanceId}' has no resolved basic-attack profile.");
        }

        if (profile.ActionId != command.ActionId)
        {
            return BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.BasicAttackSourceMismatch,
                $"Basic attack '{command.ActionId}' does not match resolved source '{profile.ActionId}'.");
        }

        if (profile.BasicAttack != command.BasicAttack)
        {
            return BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.BasicAttackDefinitionMismatch,
                $"Basic attack '{command.ActionId}' does not match the resolved damage profile.");
        }

        return profile.Targeting == command.Targeting
            ? BattleActionAuthorizationResult.Authorized
            : BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.BasicAttackTargetingMismatch,
                $"Basic attack '{command.ActionId}' does not match the resolved targeting profile.");
    }
}

internal static class CatalogSkillActionAuthorization
{
    public static BattleActionAuthorizationResult Authorize(
        RuntimeActorState actor,
        SkillDefinition skill,
        ISkillDefinitionRepository skills,
        IEnumerable<ContentId>? grantedSkillIds = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(skills);

        if (!actor.HasSkill(skill.Id) && !(grantedSkillIds?.Contains(skill.Id) ?? false))
        {
            return BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.SkillNotEquipped,
                $"Actor '{actor.InstanceId}' does not have skill '{skill.Id}' equipped or granted by equipment.");
        }

        if (!skills.TryGetSkill(skill.Id, out SkillDefinition? canonical) || canonical is null)
        {
            return BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.SkillDefinitionMissing,
                $"Available skill '{skill.Id}' is not available from the authorization catalog.");
        }

        return ReferenceEquals(canonical, skill)
            ? BattleActionAuthorizationResult.Authorized
            : BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.SkillDefinitionSubstituted,
                $"Skill '{skill.Id}' is not the actor's canonical catalog definition.");
    }
}
