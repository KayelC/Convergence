namespace Convergence.Content;

public enum EquipmentSlot
{
    Weapon,
    Armor,
    Boots,
    Accessory
}

public enum ShopContentKind
{
    Item,
    Equipment
}

public enum ShopPriceKind
{
    Fixed,
    Policy
}

public enum ShopStockKind
{
    Unlimited,
    Limited,
    Policy
}

public enum DungeonFixedFloorKind
{
    Empty,
    Battle,
    Boss,
    SafeRoom,
    BlockEnd,
    Terminal,
    Barrier,
    Transition
}

public enum FusionParentSelectorKind
{
    Entity,
    Race
}

public enum FusionParentRole
{
    Participant,
    Catalyst,
    RankShiftTarget
}

public enum FusionResultOperationKind
{
    CreateEntity,
    CatalystRankShift,
    StatBoost,
    Special
}

public enum RulesetCategory
{
    Damage,
    Growth,
    Stat,
    TurnEconomy,
    RosterCapacity,
    Reward,
    Economy,
    MoonPhase,
    StatModifier
}

public sealed record StatModifierDefinition(ContentId StatId, int Value);

public sealed record EquipmentBasicAttackDefinition(
    DamageElement Element,
    int Power,
    int Accuracy,
    bool IsLongRange);

public sealed record EquipmentWeaponProfileDefinition(EquipmentBasicAttackDefinition BasicAttack);

public sealed record EquipmentArmorProfileDefinition(int Defense, int Evasion);

public sealed record EquipmentBootsProfileDefinition(int Evasion);

public sealed record EquipmentAccessoryProfileDefinition
{
    public EquipmentAccessoryProfileDefinition(IEnumerable<StatModifierDefinition>? statModifiers = null)
    {
        StatModifiers = DefinitionCollections.Snapshot(statModifiers);
    }

    public IReadOnlyList<StatModifierDefinition> StatModifiers { get; }
}

public sealed record EquipmentDefinition
{
    public EquipmentDefinition(
        ContentId id,
        string displayName,
        string description,
        EquipmentSlot slot,
        decimal baseValue,
        IEnumerable<ContentId>? grantedSkillIds = null,
        EquipmentWeaponProfileDefinition? weapon = null,
        EquipmentArmorProfileDefinition? armor = null,
        EquipmentBootsProfileDefinition? boots = null,
        EquipmentAccessoryProfileDefinition? accessory = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Slot = slot;
        BaseValue = baseValue;
        GrantedSkillIds = DefinitionCollections.Snapshot(grantedSkillIds);
        Weapon = weapon;
        Armor = armor;
        Boots = boots;
        Accessory = accessory;
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public EquipmentSlot Slot { get; }
    public decimal BaseValue { get; }
    public IReadOnlyList<ContentId> GrantedSkillIds { get; }
    public EquipmentWeaponProfileDefinition? Weapon { get; }
    public EquipmentArmorProfileDefinition? Armor { get; }
    public EquipmentBootsProfileDefinition? Boots { get; }
    public EquipmentAccessoryProfileDefinition? Accessory { get; }
}

public abstract record ShopPriceDefinition(ShopPriceKind Kind);

public sealed record FixedShopPriceDefinition(decimal BasePrice)
    : ShopPriceDefinition(ShopPriceKind.Fixed);

public sealed record PolicyShopPriceDefinition : ShopPriceDefinition
{
    public PolicyShopPriceDefinition(
        ContentId PricingPolicyId,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : base(ShopPriceKind.Policy)
    {
        this.PricingPolicyId = PricingPolicyId;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId PricingPolicyId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

public abstract record ShopStockDefinition(ShopStockKind Kind);

public sealed record UnlimitedShopStockDefinition()
    : ShopStockDefinition(ShopStockKind.Unlimited);

public sealed record LimitedShopStockDefinition(int Quantity)
    : ShopStockDefinition(ShopStockKind.Limited);

public sealed record PolicyShopStockDefinition : ShopStockDefinition
{
    public PolicyShopStockDefinition(
        ContentId StockPolicyId,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : base(ShopStockKind.Policy)
    {
        this.StockPolicyId = StockPolicyId;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId StockPolicyId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

public sealed record ShopOfferDefinition(
    ShopContentKind ContentKind,
    ContentId ContentId,
    ShopPriceDefinition Price,
    ShopStockDefinition Stock);

public sealed record ShopCatalogDefinition
{
    public ShopCatalogDefinition(
        ContentId id,
        string displayName,
        string description,
        ContentId categoryId,
        IEnumerable<ContentId>? availabilityContextIds = null,
        IEnumerable<ShopOfferDefinition>? offers = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        CategoryId = categoryId;
        AvailabilityContextIds = DefinitionCollections.Snapshot(availabilityContextIds);
        Offers = DefinitionCollections.Snapshot(offers);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ContentId CategoryId { get; }
    public IReadOnlyList<ContentId> AvailabilityContextIds { get; }
    public IReadOnlyList<ShopOfferDefinition> Offers { get; }
}

public sealed record NegotiationAnswerDefinition(string Text, int Score);

public sealed record NegotiationQuestionDefinition
{
    public NegotiationQuestionDefinition(string text, IEnumerable<NegotiationAnswerDefinition>? answers = null)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Answers = DefinitionCollections.Snapshot(answers);
    }

    public string Text { get; }
    public IReadOnlyList<NegotiationAnswerDefinition> Answers { get; }
}

public sealed record NegotiationDemandDefinition
{
    public NegotiationDemandDefinition(
        ContentId demandId,
        int weight,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
    {
        DemandId = demandId;
        Weight = weight;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId DemandId { get; }
    public int Weight { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

public sealed record NegotiationDefinition
{
    public NegotiationDefinition(
        ContentId id,
        string displayName,
        string description,
        ContentId personalityId,
        IEnumerable<NegotiationQuestionDefinition>? questions = null,
        IEnumerable<string>? familiarDialogueLines = null,
        IEnumerable<NegotiationDemandDefinition>? demands = null,
        IEnumerable<ContentId>? defaultRaceIds = null,
        IEnumerable<ContentId>? defaultEntityIds = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        PersonalityId = personalityId;
        Questions = DefinitionCollections.Snapshot(questions);
        FamiliarDialogueLines = DefinitionCollections.Snapshot(familiarDialogueLines);
        Demands = DefinitionCollections.Snapshot(demands);
        DefaultRaceIds = DefinitionCollections.Snapshot(defaultRaceIds);
        DefaultEntityIds = DefinitionCollections.Snapshot(defaultEntityIds);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ContentId PersonalityId { get; }
    public IReadOnlyList<NegotiationQuestionDefinition> Questions { get; }
    public IReadOnlyList<string> FamiliarDialogueLines { get; }
    public IReadOnlyList<NegotiationDemandDefinition> Demands { get; }
    public IReadOnlyList<ContentId> DefaultRaceIds { get; }
    public IReadOnlyList<ContentId> DefaultEntityIds { get; }
}

public sealed record EncounterMemberDefinition(ContentId EntityId, int Level, int Count = 1);

public sealed record EncounterFormationDefinition
{
    public EncounterFormationDefinition(
        int weight,
        bool isBoss,
        IEnumerable<EncounterMemberDefinition>? members = null,
        ContentId? rewardPolicyId = null,
        IEnumerable<KeyValuePair<string, object?>>? rewardParameters = null)
    {
        Weight = weight;
        IsBoss = isBoss;
        Members = DefinitionCollections.Snapshot(members);
        RewardPolicyId = rewardPolicyId;
        RewardParameters = DefinitionCollections.SnapshotParameters(rewardParameters);
    }

    public int Weight { get; }
    public bool IsBoss { get; }
    public IReadOnlyList<EncounterMemberDefinition> Members { get; }
    public ContentId? RewardPolicyId { get; }
    public IReadOnlyDictionary<string, object?> RewardParameters { get; }
}

public sealed record EncounterDefinition
{
    public EncounterDefinition(
        ContentId id,
        string displayName,
        string description,
        ContentId? environmentId = null,
        IEnumerable<EncounterFormationDefinition>? formations = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        EnvironmentId = environmentId;
        Formations = DefinitionCollections.Snapshot(formations);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ContentId? EnvironmentId { get; }
    public IReadOnlyList<EncounterFormationDefinition> Formations { get; }
}

public sealed record DungeonFixedFloorDefinition
{
    public DungeonFixedFloorDefinition(
        int floor,
        DungeonFixedFloorKind kind,
        string description,
        ContentId? encounterId = null,
        ContentId? transitionRuleId = null,
        ContentId? barrierRuleId = null,
        bool hasTerminal = false)
    {
        Floor = floor;
        Kind = kind;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        EncounterId = encounterId;
        TransitionRuleId = transitionRuleId;
        BarrierRuleId = barrierRuleId;
        HasTerminal = hasTerminal;
    }

    public int Floor { get; }
    public DungeonFixedFloorKind Kind { get; }
    public string Description { get; }
    public ContentId? EncounterId { get; }
    public ContentId? TransitionRuleId { get; }
    public ContentId? BarrierRuleId { get; }
    public bool HasTerminal { get; }
}

public sealed record DungeonBlockDefinition
{
    public DungeonBlockDefinition(
        ContentId id,
        string displayName,
        int startFloor,
        int endFloor,
        IEnumerable<ContentId>? encounterPoolIds = null,
        IEnumerable<DungeonFixedFloorDefinition>? fixedFloors = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        StartFloor = startFloor;
        EndFloor = endFloor;
        EncounterPoolIds = DefinitionCollections.Snapshot(encounterPoolIds);
        FixedFloors = DefinitionCollections.Snapshot(fixedFloors);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public int StartFloor { get; }
    public int EndFloor { get; }
    public IReadOnlyList<ContentId> EncounterPoolIds { get; }
    public IReadOnlyList<DungeonFixedFloorDefinition> FixedFloors { get; }
}

public sealed record DungeonDefinition
{
    public DungeonDefinition(
        ContentId id,
        string displayName,
        string description,
        IEnumerable<DungeonBlockDefinition>? blocks = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Blocks = DefinitionCollections.Snapshot(blocks);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<DungeonBlockDefinition> Blocks { get; }
}

public sealed record FusionParentSelectorDefinition(
    FusionParentSelectorKind Kind,
    ContentId Id,
    FusionParentRole Role = FusionParentRole.Participant);

public sealed record FusionResultDefinition
{
    public FusionResultDefinition(
        FusionResultOperationKind operation,
        ContentId? resultEntityId = null,
        int? rankShift = null,
        ContentId? policyId = null,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
    {
        Operation = operation;
        ResultEntityId = resultEntityId;
        RankShift = rankShift;
        PolicyId = policyId;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public FusionResultOperationKind Operation { get; }
    public ContentId? ResultEntityId { get; }
    public int? RankShift { get; }
    public ContentId? PolicyId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}

public sealed record FusionRecipeDefinition
{
    public FusionRecipeDefinition(
        ContentId id,
        string displayName,
        string description,
        IEnumerable<FusionParentSelectorDefinition>? parents,
        FusionResultDefinition result,
        ContentId? accidentPolicyId = null,
        ContentId? mutationPolicyId = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Parents = DefinitionCollections.Snapshot(parents);
        Result = result ?? throw new ArgumentNullException(nameof(result));
        AccidentPolicyId = accidentPolicyId;
        MutationPolicyId = mutationPolicyId;
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<FusionParentSelectorDefinition> Parents { get; }
    public FusionResultDefinition Result { get; }
    public ContentId? AccidentPolicyId { get; }
    public ContentId? MutationPolicyId { get; }
}

public sealed record RulesetDefinition
{
    public RulesetDefinition(
        ContentId id,
        string displayName,
        string description,
        RulesetCategory category,
        ContentId policyId,
        IEnumerable<KeyValuePair<string, object?>>? parameters = null)
    {
        Id = id;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Category = category;
        PolicyId = policyId;
        Parameters = DefinitionCollections.SnapshotParameters(parameters);
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public RulesetCategory Category { get; }
    public ContentId PolicyId { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
}
