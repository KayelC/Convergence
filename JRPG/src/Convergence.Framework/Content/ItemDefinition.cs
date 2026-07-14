namespace Convergence.Content;

public enum ItemKind
{
    Consumable,
    Key,
    Material,
    Valuable
}

public enum ItemConsumptionMode
{
    SuccessfulExecution
}

public sealed record ItemUsageDefinition
{
    public ItemUsageDefinition(
        IEnumerable<ContentId> contextIds,
        TargetingDefinition targeting,
        IEnumerable<EffectDefinition> effects,
        ItemConsumptionMode consumptionMode = ItemConsumptionMode.SuccessfulExecution)
    {
        ArgumentNullException.ThrowIfNull(contextIds);
        Targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        ArgumentNullException.ThrowIfNull(effects);

        ContextIds = Array.AsReadOnly(contextIds.ToArray());
        Effects = Array.AsReadOnly(effects.ToArray());
        ConsumptionMode = consumptionMode;
    }

    public IReadOnlyList<ContentId> ContextIds { get; }
    public TargetingDefinition Targeting { get; }
    public IReadOnlyList<EffectDefinition> Effects { get; }
    public ItemConsumptionMode ConsumptionMode { get; }
}

public sealed record ItemDefinition
{
    public ItemDefinition(
        ContentId id,
        string displayName,
        string description,
        ItemKind itemKind,
        int stackLimit,
        decimal baseValue,
        ItemUsageDefinition? usage = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        ItemKind = itemKind;
        StackLimit = stackLimit;
        BaseValue = baseValue;
        Usage = usage;
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ItemKind ItemKind { get; }
    public int StackLimit { get; }
    public decimal BaseValue { get; }
    public ItemUsageDefinition? Usage { get; }
}
