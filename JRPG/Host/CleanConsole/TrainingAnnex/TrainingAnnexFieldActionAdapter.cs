using JRPGPrototype.Data.Definitions;
using JRPGPrototype.Logic.Battle.Execution;
using JRPGPrototype.Logic.Runtime;

namespace JRPGPrototype.Host.CleanConsole.TrainingAnnex;

internal sealed record TrainingAnnexFieldActionResult(
    ContentId ActionId,
    BattleActionAssessment Assessment,
    BattleActionExecutionResult? Execution,
    RuntimeResourceSnapshot HpBefore,
    RuntimeResourceSnapshot HpAfter,
    RuntimeResourceSnapshot SpBefore,
    RuntimeResourceSnapshot SpAfter)
{
    public bool Applied => Execution?.Status == BattleActionExecutionStatus.Executed;
}

internal sealed class TrainingAnnexFieldActionAdapter
{
    private static readonly ContentId Field = ContentId.Parse("field");
    private static readonly ContentId Hp = ContentId.Parse("hp");
    private static readonly ContentId Sp = ContentId.Parse("sp");

    private readonly IBattleActionExecutor _actions;

    public TrainingAnnexFieldActionAdapter(BattleExecutionServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _actions = new BattleActionExecutor(
            new SkillExecutor(services),
            new ItemExecutor(services),
            services);
    }

    public ValueTask<TrainingAnnexFieldActionResult> UseItemAsync(
        TrainingAnnexRuntimeActor actor,
        ItemDefinition item,
        TrainingAnnexItemActionInventory inventory,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            actor,
            new ItemBattleActionCommand(item, [actor.Actor.State.InstanceId]),
            inventory,
            cancellationToken);

    public ValueTask<TrainingAnnexFieldActionResult> UseSkillAsync(
        TrainingAnnexRuntimeActor actor,
        SkillDefinition skill,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            actor,
            new SkillBattleActionCommand(skill, [actor.Actor.State.InstanceId]),
            null,
            cancellationToken);

    private async ValueTask<TrainingAnnexFieldActionResult> ExecuteAsync(
        TrainingAnnexRuntimeActor actor,
        BattleActionCommand command,
        IItemActionInventory? inventory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RuntimeActorSnapshot before = actor.Actor.State.ToSnapshot();
        var request = new BattleActionExecutionRequest(
            command,
            actor.Actor.State,
            [actor.Actor.State],
            new EffectExecutionEnvironment(Field),
            inventory);
        BattleActionAssessment assessment = _actions.Assess(request);
        BattleActionExecutionResult? execution = null;
        if (assessment.CanExecute)
        {
            execution = await _actions.ExecuteAsync(request, assessment, cancellationToken).ConfigureAwait(false);
        }

        RuntimeActorSnapshot after = actor.Actor.State.ToSnapshot();
        return new TrainingAnnexFieldActionResult(
            command switch
            {
                ItemBattleActionCommand item => item.Item.Id,
                SkillBattleActionCommand skill => skill.Skill.Id,
                _ => throw new InvalidOperationException("Training Annex field actions support items and skills only.")
            },
            assessment,
            execution,
            Resource(before, Hp),
            Resource(after, Hp),
            Resource(before, Sp),
            Resource(after, Sp));
    }

    private static RuntimeResourceSnapshot Resource(RuntimeActorSnapshot snapshot, ContentId resourceId) =>
        snapshot.Resources.First(resource => resource.ResourceId == resourceId);
}

internal sealed class TrainingAnnexItemActionInventory : IItemActionInventory
{
    private readonly IInventoryTransitionService _transitions;

    public TrainingAnnexItemActionInventory(
        RuntimeInventorySnapshot snapshot,
        IInventoryTransitionService? transitions = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _transitions = transitions ?? new InventoryTransitionService();
    }

    public RuntimeInventorySnapshot Snapshot { get; private set; }

    public void Replace(RuntimeInventorySnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public bool HasAvailable(ContentId itemId, int quantity) =>
        quantity > 0 && Snapshot.GetQuantity(itemId) >= quantity;

    public IItemActionReservation Reserve(ContentId itemId, int quantity)
    {
        InventoryReservationResult result = _transitions.ReserveItem(Snapshot, itemId, quantity);
        if (!result.Reserved || result.Reservation is null)
        {
            throw new InvalidOperationException(
                result.Diagnostics.FirstOrDefault()?.Message ?? $"Item '{itemId}' could not be reserved.");
        }

        return new Reservation(this, result.Reservation);
    }

    private sealed class Reservation(
        TrainingAnnexItemActionInventory owner,
        RuntimeItemReservation reservation) : IItemActionReservation
    {
        public ContentId ItemId => reservation.ItemId;
        public int Quantity => reservation.Quantity;
        public bool IsCommitted => reservation.IsCommitted;
        public bool IsRolledBack => reservation.IsRolledBack;

        public ItemActionReservationTransitionResult Commit()
        {
            try
            {
                InventoryTransitionResult result = reservation.Commit();
                if (!result.Applied)
                {
                    return ItemActionReservationTransitionResult.Rejected(
                        result.Diagnostics.FirstOrDefault()?.Message ?? "Item reservation commit failed.");
                }

                owner.Snapshot = result.After;
                return ItemActionReservationTransitionResult.Success;
            }
            catch (Exception exception)
            {
                return ItemActionReservationTransitionResult.Rejected(exception.Message);
            }
        }

        public ItemActionReservationTransitionResult Rollback()
        {
            try
            {
                InventoryTransitionResult result = reservation.Rollback();
                return result.Applied
                    ? ItemActionReservationTransitionResult.Success
                    : ItemActionReservationTransitionResult.Rejected(
                        result.Diagnostics.FirstOrDefault()?.Message ?? "Item reservation rollback failed.");
            }
            catch (Exception exception)
            {
                return ItemActionReservationTransitionResult.Rejected(exception.Message);
            }
        }
    }
}
