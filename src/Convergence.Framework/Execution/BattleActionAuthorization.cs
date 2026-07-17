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
    BasicAttackTargetingMismatch
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
    private readonly IEquipmentDefinitionRepository _equipment;
    private readonly IRuntimeEquipmentProfileResolver _profiles;
    private readonly TargetingDefinition _targeting;

    public EquipmentBattleBasicAttackProfileSource(
        IEquipmentDefinitionRepository equipment,
        TargetingDefinition targeting,
        IRuntimeEquipmentProfileResolver? profiles = null)
    {
        _equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        _profiles = profiles ?? new RuntimeEquipmentProfileResolver();
    }

    public BattleBasicAttackProfile? Resolve(RuntimeActorState actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        RuntimeEquipmentProfile equipmentProfile = _profiles.Resolve(actor.Equipment, _equipment);
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
    private readonly IBattleBasicAttackProfileSource _basicAttacks;

    public CatalogBattleActionAuthorizationPolicy(
        ISkillDefinitionRepository skills,
        IBattleBasicAttackProfileSource basicAttacks)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _basicAttacks = basicAttacks ?? throw new ArgumentNullException(nameof(basicAttacks));
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
            BasicAttackBattleActionCommand basicAttack => AuthorizeBasicAttack(actor, basicAttack),
            _ => BattleActionAuthorizationResult.Authorized
        };
    }

    private BattleActionAuthorizationResult AuthorizeSkill(
        RuntimeActorState actor,
        SkillBattleActionCommand command)
    {
        if (!actor.HasSkill(command.Skill.Id))
        {
            return BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.SkillNotEquipped,
                $"Actor '{actor.InstanceId}' does not have skill '{command.Skill.Id}' equipped.");
        }

        if (!_skills.TryGetSkill(command.Skill.Id, out SkillDefinition? canonical) || canonical is null)
        {
            return BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.SkillDefinitionMissing,
                $"Equipped skill '{command.Skill.Id}' is not available from the authorization catalog.");
        }

        return ReferenceEquals(canonical, command.Skill)
            ? BattleActionAuthorizationResult.Authorized
            : BattleActionAuthorizationResult.Rejected(
                BattleActionAuthorizationDiagnosticCode.SkillDefinitionSubstituted,
                $"Skill '{command.Skill.Id}' is not the actor's canonical catalog definition.");
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
