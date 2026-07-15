using Convergence.Content;
using Convergence.Knowledge;
using Convergence.TurnEconomy;
using Convergence.Execution;
using Convergence.Runtime;

namespace Convergence.Encounters;

public enum AutomatedRestrictedActionSelectionStatus
{
    Selected,
    Unavailable
}

public sealed record AutomatedRestrictedActionSelection
{
    private AutomatedRestrictedActionSelection(
        AutomatedRestrictedActionSelectionStatus status,
        ContentId? actionId,
        BattleActionCommand? command,
        IItemActionInventory? itemInventory,
        string? message)
    {
        if (status == AutomatedRestrictedActionSelectionStatus.Selected && command is null)
        {
            throw new ArgumentException("A selected restricted action requires a command.", nameof(command));
        }

        if (status == AutomatedRestrictedActionSelectionStatus.Selected &&
            (actionId is null || actionId.Value == default))
        {
            throw new ArgumentException("A selected restricted action requires a valid action ID.", nameof(actionId));
        }

        if (status == AutomatedRestrictedActionSelectionStatus.Unavailable && command is not null)
        {
            throw new ArgumentException("An unavailable restricted action cannot contain a command.", nameof(command));
        }

        Status = status;
        ActionId = actionId;
        Command = command;
        ItemInventory = itemInventory;
        Message = message;
    }

    public AutomatedRestrictedActionSelectionStatus Status { get; }
    public ContentId? ActionId { get; }
    public BattleActionCommand? Command { get; }
    public IItemActionInventory? ItemInventory { get; }
    public string? Message { get; }

    public static AutomatedRestrictedActionSelection Selected(
        ContentId actionId,
        BattleActionCommand command,
        IItemActionInventory? itemInventory = null) =>
        new(
            AutomatedRestrictedActionSelectionStatus.Selected,
            actionId,
            command ?? throw new ArgumentNullException(nameof(command)),
            itemInventory,
            null);

    public static AutomatedRestrictedActionSelection Unavailable(string message) =>
        new(
            AutomatedRestrictedActionSelectionStatus.Unavailable,
            null,
            null,
            null,
            string.IsNullOrWhiteSpace(message)
                ? "No automated command is available for the turn restriction."
                : message);
}

public sealed record AutomatedBattleRestrictionActionRequest
{
    internal AutomatedBattleRestrictionActionRequest(
        BattleEncounterTurnRequest turn,
        CatalogBattleActor actor,
        IEnumerable<CatalogBattleActor> participants,
        ElementalAffinityKnowledge knowledge)
    {
        Turn = turn ?? throw new ArgumentNullException(nameof(turn));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        Knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));
    }

    public BattleEncounterTurnRequest Turn { get; }
    public CatalogBattleActor Actor { get; }
    public IReadOnlyList<CatalogBattleActor> Participants { get; }
    public ElementalAffinityKnowledge Knowledge { get; }
}

public interface IAutomatedBattleRestrictionActionSource
{
    ValueTask<AutomatedRestrictedActionSelection> SelectAsync(
        AutomatedBattleRestrictionActionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AutomatedBattleTurnRestrictionRequest
{
    internal AutomatedBattleTurnRestrictionRequest(
        BattleEncounterTurnRequest turn,
        CatalogBattleActor actor,
        IEnumerable<CatalogBattleActor> participants,
        ElementalAffinityKnowledge knowledge)
    {
        Turn = turn ?? throw new ArgumentNullException(nameof(turn));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        Participants = Array.AsReadOnly(
            participants?.ToArray() ?? throw new ArgumentNullException(nameof(participants)));
        Knowledge = knowledge ?? throw new ArgumentNullException(nameof(knowledge));

        if (actor.State.InstanceId != turn.Actor.InstanceId ||
            Participants.All(candidate => candidate.State.InstanceId != actor.State.InstanceId))
        {
            throw new ArgumentException(
                "The restricted actor must match the encounter turn and participant collection.",
                nameof(actor));
        }
    }

    public BattleEncounterTurnRequest Turn { get; }
    public CatalogBattleActor Actor { get; }
    public IReadOnlyList<CatalogBattleActor> Participants { get; }
    public ElementalAffinityKnowledge Knowledge { get; }
}

public interface IAutomatedBattleTurnRestrictionResolver
{
    ValueTask<BattleEncounterCommandResult> ResolveAsync(
        AutomatedBattleTurnRestrictionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves restriction-only turns without inventing host equipment, inventory, or confusion rules.
/// Command-bearing restrictions require an explicit action source and use the canonical action executor.
/// </summary>
public sealed class AutomatedBattleTurnRestrictionResolver : IAutomatedBattleTurnRestrictionResolver
{
    private readonly IBattleActionExecutor? _actionExecutor;
    private readonly IAutomatedBattleRestrictionActionSource? _actionSource;

    public AutomatedBattleTurnRestrictionResolver()
    {
    }

    public AutomatedBattleTurnRestrictionResolver(
        IBattleActionExecutor actionExecutor,
        IAutomatedBattleRestrictionActionSource actionSource)
    {
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _actionSource = actionSource ?? throw new ArgumentNullException(nameof(actionSource));
    }

    public async ValueTask<BattleEncounterCommandResult> ResolveAsync(
        AutomatedBattleTurnRestrictionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return request.Turn.TurnStartOutcome switch
            {
                BattleTurnStartOutcome.Skip => Skip(request),
                BattleTurnStartOutcome.FleeBattle => LeaveBattle(
                    request,
                    recallToRoster: false,
                    cancellationToken),
                BattleTurnStartOutcome.RecallToRoster => LeaveBattle(
                    request,
                    recallToRoster: true,
                    cancellationToken),
                BattleTurnStartOutcome.LimitedAction or
                    BattleTurnStartOutcome.ForcedPhysical or
                    BattleTurnStartOutcome.ForcedConfusion =>
                    await ExecuteRestrictedActionAsync(request, cancellationToken).ConfigureAwait(false),
                BattleTurnStartOutcome.CanAct => Fault(
                    request,
                    "The automated restriction resolver cannot resolve an unrestricted turn."),
                _ => Fault(
                    request,
                    $"Unsupported automated turn restriction '{request.Turn.TurnStartOutcome}'.")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Fault(
                request,
                $"Automated turn restriction '{request.Turn.TurnStartOutcome}' failed: {exception.Message}");
        }
    }

    private static BattleEncounterCommandResult Skip(AutomatedBattleTurnRestrictionRequest request) =>
        BattleEncounterCommandResult.Executed(
            ActionTurnConsumption.Normal,
            [new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.CommandPassed,
                $"{request.Actor.State.InstanceId} could not act.",
                request.Actor.State.InstanceId)]);

    private static BattleEncounterCommandResult LeaveBattle(
        AutomatedBattleTurnRestrictionRequest request,
        bool recallToRoster,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RuntimeActorState actor = request.Actor.State;
        RuntimeActorDeployment deployment = recallToRoster
            ? RuntimeActorDeployment.Reserve
            : actor.Deployment.Deployment;
        actor.SetDeployment(deployment, isActive: false);

        string message = recallToRoster
            ? $"{actor.InstanceId} was recalled to its roster."
            : $"{actor.InstanceId} fled the battle.";
        return BattleEncounterCommandResult.Executed(
            ActionTurnConsumption.Normal,
            [new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.DeploymentChanged,
                message,
                actor.InstanceId)]);
    }

    private async ValueTask<BattleEncounterCommandResult> ExecuteRestrictedActionAsync(
        AutomatedBattleTurnRestrictionRequest request,
        CancellationToken cancellationToken)
    {
        if (_actionExecutor is null || _actionSource is null)
        {
            return Fault(
                request,
                $"Automated turn restriction '{request.Turn.TurnStartOutcome}' requires an explicit action source.");
        }

        var sourceRequest = new AutomatedBattleRestrictionActionRequest(
            request.Turn,
            request.Actor,
            request.Participants,
            request.Knowledge);
        AutomatedRestrictedActionSelection selection = await _actionSource
            .SelectAsync(sourceRequest, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (selection.Status == AutomatedRestrictedActionSelectionStatus.Unavailable ||
            selection.Command is null ||
            selection.ActionId is not ContentId actionId)
        {
            return Fault(
                request,
                selection.Message ??
                $"No automated command is available for '{request.Turn.TurnStartOutcome}'.");
        }

        BattleActionCommand command = selection.Command;
        string? validationFailure = ValidateCommand(
            request,
            actionId,
            command,
            selection.ItemInventory);
        if (validationFailure is not null)
        {
            return Fault(request, validationFailure);
        }

        var executionRequest = new BattleActionExecutionRequest(
            command,
            request.Actor.State,
            request.Participants.Select(participant => participant.State),
            new EffectExecutionEnvironment(
                request.Turn.Encounter.ContextId,
                request.Turn.Encounter.BattleKindId,
                request.Turn.Encounter.MoonPhaseId),
            selection.ItemInventory);
        cancellationToken.ThrowIfCancellationRequested();
        BattleActionAssessment assessment = _actionExecutor.Assess(executionRequest);
        if (!assessment.CanExecute)
        {
            return Fault(
                request,
                $"Restricted automated action '{actionId}' was rejected: " +
                string.Join("; ", assessment.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        BattleActionExecutionResult execution = await _actionExecutor
            .ExecuteAsync(executionRequest, assessment, cancellationToken)
            .ConfigureAwait(false);
        if (execution.Status == BattleActionExecutionStatus.Rejected)
        {
            return Fault(
                request,
                $"Restricted automated action '{actionId}' was rejected during execution: " +
                string.Join("; ", execution.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        IReadOnlyList<BattleEncounterEvent> events = MapExecutionEvents(
            request,
            actionId,
            command,
            execution);
        return BattleEncounterCommandResult.Executed(
            execution.TurnConsumption,
            events,
            execution.EscapeRequested ? BattleEncounterOutcome.Escape : null);
    }

    private static string? ValidateCommand(
        AutomatedBattleTurnRestrictionRequest request,
        ContentId actionId,
        BattleActionCommand command,
        IItemActionInventory? itemInventory)
    {
        if (command.Kind is not (
            BattleActionKind.BasicAttack or
            BattleActionKind.Skill or
            BattleActionKind.Item or
            BattleActionKind.Guard or
            BattleActionKind.Pass or
            BattleActionKind.Analyze or
            BattleActionKind.EscapeAttempt))
        {
            return $"Automated restricted action kind '{command.Kind}' requires a custom restriction resolver.";
        }

        if (command is SkillBattleActionCommand selectedSkill)
        {
            if (selectedSkill.Skill.Id != actionId)
            {
                return $"Restricted skill '{selectedSkill.Skill.Id}' does not match action ID '{actionId}'.";
            }

            SkillDefinition? loadedSkill = request.Actor.ActiveSkills.FirstOrDefault(
                skill => skill.Id == selectedSkill.Skill.Id);
            if (loadedSkill is null)
            {
                return $"Actor '{request.Actor.State.InstanceId}' does not know active skill " +
                       $"'{selectedSkill.Skill.Id}'.";
            }

            if (!ReferenceEquals(loadedSkill, selectedSkill.Skill))
            {
                return $"Restricted skill '{selectedSkill.Skill.Id}' is not the actor's catalog definition.";
            }
        }

        if (command is ItemBattleActionCommand selectedItem && selectedItem.Item.Id != actionId)
        {
            return $"Restricted item '{selectedItem.Item.Id}' does not match action ID '{actionId}'.";
        }

        if (command is BasicAttackBattleActionCommand basicAttack && basicAttack.ActionId != actionId)
        {
            return $"Restricted basic attack '{basicAttack.ActionId}' does not match action ID '{actionId}'.";
        }

        if (command is ItemBattleActionCommand && itemInventory is null)
        {
            return "An automated restricted item command requires an explicit item inventory.";
        }

        BattleTurnStartRestriction restriction = request.Turn.TurnStartRestriction;
        if (restriction.Outcome == BattleTurnStartOutcome.ForcedPhysical &&
            command.Kind != BattleActionKind.BasicAttack)
        {
            return "A forced-physical restriction requires a typed basic-attack command.";
        }

        if (restriction.Outcome == BattleTurnStartOutcome.LimitedAction &&
            !restriction.AllowedActionIds.Contains(actionId))
        {
            return $"Action '{actionId}' is not allowed by the limited-action restriction.";
        }

        return null;
    }

    private static IReadOnlyList<BattleEncounterEvent> MapExecutionEvents(
        AutomatedBattleTurnRestrictionRequest request,
        ContentId actionId,
        BattleActionCommand command,
        BattleActionExecutionResult execution)
    {
        var events = new List<BattleEncounterEvent>
        {
            new(
                0,
                BattleEncounterEventKind.CommandSelected,
                $"{request.Actor.State.InstanceId} selected {actionId} under " +
                $"{request.Turn.TurnStartOutcome}.",
                request.Actor.State.InstanceId,
                execution.Effects.FirstOrDefault()?.TargetId,
                actionId)
        };

        foreach (EffectExecutionResult effect in execution.Effects)
        {
            events.Add(new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.EffectResolved,
                $"Effect {effect.EffectIndex} resolved as {effect.Outcome} ({effect.TurnEconomyOutcome}).",
                request.Actor.State.InstanceId,
                effect.TargetId,
                actionId,
                effect.Value));
            if (effect.Value is decimal value)
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.ResourceChanged,
                    $"Resource changed by {value}.",
                    request.Actor.State.InstanceId,
                    effect.TargetId,
                    actionId,
                    value));
            }

            LearnAffinity(request, command, effect);
            foreach (PassiveTriggerExecutionResult passive in effect.PassiveActivations ?? [])
            {
                events.Add(new BattleEncounterEvent(
                    0,
                    BattleEncounterEventKind.PassiveActivated,
                    $"Passive {passive.SkillId} resolved as {passive.Outcome}.",
                    passive.TargetId,
                    passive.TargetId,
                    passive.SkillId));
            }
        }

        foreach (ContentId hostActionId in execution.HostActionRequestIds)
        {
            events.Add(new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.HostActionRequested,
                $"Host action '{hostActionId}' was requested.",
                request.Actor.State.InstanceId,
                SourceId: hostActionId));
        }

        return Array.AsReadOnly(events.ToArray());
    }

    private static void LearnAffinity(
        AutomatedBattleTurnRestrictionRequest request,
        BattleActionCommand command,
        EffectExecutionResult effect)
    {
        if (effect.TargetId is not RuntimeInstanceId targetId ||
            effect.ResolvedAffinity is not ElementalAffinity affinity ||
            DamageElementFor(command, effect.EffectIndex) is not DamageElement element)
        {
            return;
        }

        CatalogBattleActor? target = request.Participants.FirstOrDefault(
            candidate => candidate.State.InstanceId == targetId);
        if (target is not null)
        {
            request.Knowledge.Learn(target.Entity.Id, element, affinity);
        }
    }

    private static DamageElement? DamageElementFor(BattleActionCommand command, int effectIndex) =>
        command switch
        {
            BasicAttackBattleActionCommand attack when effectIndex == 0 => attack.BasicAttack.Element,
            SkillBattleActionCommand skill
                when skill.Skill.Effects.ElementAtOrDefault(effectIndex) is DamageEffectDefinition damage =>
                damage.Element,
            ItemBattleActionCommand item
                when item.Item.Usage?.Effects.ElementAtOrDefault(effectIndex) is DamageEffectDefinition damage =>
                damage.Element,
            _ => null
        };

    private static BattleEncounterCommandResult Fault(
        AutomatedBattleTurnRestrictionRequest request,
        string message) =>
        BattleEncounterCommandResult.Faulted(
            message,
            [new BattleEncounterEvent(
                0,
                BattleEncounterEventKind.ActionRejected,
                message,
                request.Actor.State.InstanceId)]);
}
