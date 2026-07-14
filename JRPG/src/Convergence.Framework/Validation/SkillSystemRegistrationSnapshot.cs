using System.Collections.ObjectModel;
using Convergence.Content;

namespace Convergence.Validation;

public sealed record SkillSystemRegistrationSnapshot
{
    internal SkillSystemRegistrationSnapshot(SkillSystemRegistrationBuilder builder)
    {
        ContextIds = Snapshot(builder.ContextIds);
        ResourceIds = Snapshot(builder.ResourceIds);
        StatIds = Snapshot(builder.StatIds);
        ModifierTrackIds = Snapshot(builder.ModifierTrackIds);
        EventIds = Snapshot(builder.EventIds);
        PhaseIds = Snapshot(builder.PhaseIds);
        EntityKindIds = Snapshot(builder.EntityKindIds);
        AlignmentIds = Snapshot(builder.AlignmentIds);
        NegotiationPersonalityIds = Snapshot(builder.NegotiationPersonalityIds);
        AilmentGroupIds = Snapshot(builder.AilmentGroupIds);
        BattleKindIds = Snapshot(builder.BattleKindIds);
        MoonPhaseIds = Snapshot(builder.MoonPhaseIds);
        CapabilityIds = Snapshot(builder.CapabilityIds);
        ActionIds = Snapshot(builder.ActionIds);
        StatusIds = Snapshot(builder.StatusIds);
        EscapeRuleIds = Snapshot(builder.EscapeRuleIds);
        ShopCategoryIds = Snapshot(builder.ShopCategoryIds);
        NegotiationDemandIds = Snapshot(builder.NegotiationDemandIds);
        EncounterEnvironmentIds = Snapshot(builder.EncounterEnvironmentIds);
        PolicyIds = Snapshot(builder.PolicyIds);
        FormulaValidators = Snapshot(builder.FormulaValidators);
        CustomEffectValidators = Snapshot(builder.CustomEffectValidators);
        CustomConditionValidators = Snapshot(builder.CustomConditionValidators);
        CustomAilmentBehaviorValidators = Snapshot(builder.CustomAilmentBehaviorValidators);
        SupportedEffectTypes = Snapshot(builder.SupportedEffectTypes);
        SupportedConditionTypes = Snapshot(builder.SupportedConditionTypes);
        SupportedModifierTypes = Snapshot(builder.SupportedModifierTypes);
        SupportedAilmentBehaviorTypes = Snapshot(builder.SupportedAilmentBehaviorTypes);
    }

    public IReadOnlySet<ContentId> ContextIds { get; }
    public IReadOnlySet<ContentId> ResourceIds { get; }
    public IReadOnlySet<ContentId> StatIds { get; }
    public IReadOnlySet<ContentId> ModifierTrackIds { get; }
    public IReadOnlySet<ContentId> EventIds { get; }
    public IReadOnlySet<ContentId> PhaseIds { get; }
    public IReadOnlySet<ContentId> EntityKindIds { get; }
    public IReadOnlySet<ContentId> AlignmentIds { get; }
    public IReadOnlySet<ContentId> NegotiationPersonalityIds { get; }
    public IReadOnlySet<ContentId> AilmentGroupIds { get; }
    public IReadOnlySet<ContentId> BattleKindIds { get; }
    public IReadOnlySet<ContentId> MoonPhaseIds { get; }
    public IReadOnlySet<ContentId> CapabilityIds { get; }
    public IReadOnlySet<ContentId> ActionIds { get; }
    public IReadOnlySet<ContentId> StatusIds { get; }
    public IReadOnlySet<ContentId> EscapeRuleIds { get; }
    public IReadOnlySet<ContentId> ShopCategoryIds { get; }
    public IReadOnlySet<ContentId> NegotiationDemandIds { get; }
    public IReadOnlySet<ContentId> EncounterEnvironmentIds { get; }
    public IReadOnlySet<ContentId> PolicyIds { get; }
    public IReadOnlyDictionary<ContentId, IContentParameterValidator> FormulaValidators { get; }
    public IReadOnlyDictionary<ContentId, IContentParameterValidator> CustomEffectValidators { get; }
    public IReadOnlyDictionary<ContentId, IContentParameterValidator> CustomConditionValidators { get; }
    public IReadOnlyDictionary<ContentId, IContentParameterValidator> CustomAilmentBehaviorValidators { get; }
    public IReadOnlySet<Type> SupportedEffectTypes { get; }
    public IReadOnlySet<Type> SupportedConditionTypes { get; }
    public IReadOnlySet<Type> SupportedModifierTypes { get; }
    public IReadOnlySet<Type> SupportedAilmentBehaviorTypes { get; }

    private static IReadOnlySet<T> Snapshot<T>(IEnumerable<T> values) where T : notnull =>
        new ReadOnlySet<T>(new HashSet<T>(values));

    private static IReadOnlyDictionary<TKey, TValue> Snapshot<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> values)
        where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(values.ToDictionary(pair => pair.Key, pair => pair.Value));

    private sealed class ReadOnlySet<T>(HashSet<T> values) : IReadOnlySet<T>
    {
        public int Count => values.Count;
        public bool Contains(T item) => values.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => values.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => values.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => values.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => values.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => values.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => values.SetEquals(other);
        public IEnumerator<T> GetEnumerator() => values.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

public sealed class SkillSystemRegistrationBuilder
{
    internal HashSet<ContentId> ContextIds { get; } = [];
    internal HashSet<ContentId> ResourceIds { get; } = [];
    internal HashSet<ContentId> StatIds { get; } = [];
    internal HashSet<ContentId> ModifierTrackIds { get; } = [];
    internal HashSet<ContentId> EventIds { get; } = [];
    internal HashSet<ContentId> PhaseIds { get; } = [];
    internal HashSet<ContentId> EntityKindIds { get; } = [];
    internal HashSet<ContentId> AlignmentIds { get; } = [];
    internal HashSet<ContentId> NegotiationPersonalityIds { get; } = [];
    internal HashSet<ContentId> AilmentGroupIds { get; } = [];
    internal HashSet<ContentId> BattleKindIds { get; } = [];
    internal HashSet<ContentId> MoonPhaseIds { get; } = [];
    internal HashSet<ContentId> CapabilityIds { get; } = [];
    internal HashSet<ContentId> ActionIds { get; } = [];
    internal HashSet<ContentId> StatusIds { get; } = [];
    internal HashSet<ContentId> EscapeRuleIds { get; } = [];
    internal HashSet<ContentId> ShopCategoryIds { get; } = [];
    internal HashSet<ContentId> NegotiationDemandIds { get; } = [];
    internal HashSet<ContentId> EncounterEnvironmentIds { get; } = [];
    internal HashSet<ContentId> PolicyIds { get; } = [];
    internal Dictionary<ContentId, IContentParameterValidator> FormulaValidators { get; } = [];
    internal Dictionary<ContentId, IContentParameterValidator> CustomEffectValidators { get; } = [];
    internal Dictionary<ContentId, IContentParameterValidator> CustomConditionValidators { get; } = [];
    internal Dictionary<ContentId, IContentParameterValidator> CustomAilmentBehaviorValidators { get; } = [];
    internal HashSet<Type> SupportedEffectTypes { get; } = [];
    internal HashSet<Type> SupportedConditionTypes { get; } = [];
    internal HashSet<Type> SupportedModifierTypes { get; } = [];
    internal HashSet<Type> SupportedAilmentBehaviorTypes { get; } = [];

    public SkillSystemRegistrationBuilder RegisterContext(params string[] ids) => Add(ContextIds, ids);
    public SkillSystemRegistrationBuilder RegisterResource(params string[] ids) => Add(ResourceIds, ids);
    public SkillSystemRegistrationBuilder RegisterStat(params string[] ids) => Add(StatIds, ids);
    public SkillSystemRegistrationBuilder RegisterModifierTrack(params string[] ids) => Add(ModifierTrackIds, ids);
    public SkillSystemRegistrationBuilder RegisterEvent(params string[] ids) => Add(EventIds, ids);
    public SkillSystemRegistrationBuilder RegisterPhase(params string[] ids) => Add(PhaseIds, ids);
    public SkillSystemRegistrationBuilder RegisterEntityKind(params string[] ids) => Add(EntityKindIds, ids);
    public SkillSystemRegistrationBuilder RegisterAlignment(params string[] ids) => Add(AlignmentIds, ids);
    public SkillSystemRegistrationBuilder RegisterNegotiationPersonality(params string[] ids) =>
        Add(NegotiationPersonalityIds, ids);
    public SkillSystemRegistrationBuilder RegisterAilmentGroup(params string[] ids) => Add(AilmentGroupIds, ids);
    public SkillSystemRegistrationBuilder RegisterBattleKind(params string[] ids) => Add(BattleKindIds, ids);
    public SkillSystemRegistrationBuilder RegisterMoonPhase(params string[] ids) => Add(MoonPhaseIds, ids);
    public SkillSystemRegistrationBuilder RegisterCapability(params string[] ids) => Add(CapabilityIds, ids);
    public SkillSystemRegistrationBuilder RegisterAction(params string[] ids) => Add(ActionIds, ids);
    public SkillSystemRegistrationBuilder RegisterStatus(params string[] ids) => Add(StatusIds, ids);
    public SkillSystemRegistrationBuilder RegisterEscapeRule(params string[] ids) => Add(EscapeRuleIds, ids);
    public SkillSystemRegistrationBuilder RegisterShopCategory(params string[] ids) => Add(ShopCategoryIds, ids);
    public SkillSystemRegistrationBuilder RegisterNegotiationDemand(params string[] ids) =>
        Add(NegotiationDemandIds, ids);
    public SkillSystemRegistrationBuilder RegisterEncounterEnvironment(params string[] ids) =>
        Add(EncounterEnvironmentIds, ids);
    public SkillSystemRegistrationBuilder RegisterPolicy(params string[] ids) => Add(PolicyIds, ids);

    public SkillSystemRegistrationBuilder RegisterFormula(
        string id,
        IContentParameterValidator validator) => AddValidator(FormulaValidators, id, validator);

    public SkillSystemRegistrationBuilder RegisterCustomEffect(
        string id,
        IContentParameterValidator validator) => AddValidator(CustomEffectValidators, id, validator);

    public SkillSystemRegistrationBuilder RegisterCustomCondition(
        string id,
        IContentParameterValidator validator) => AddValidator(CustomConditionValidators, id, validator);

    public SkillSystemRegistrationBuilder RegisterCustomAilmentBehavior(
        string id,
        IContentParameterValidator validator) => AddValidator(CustomAilmentBehaviorValidators, id, validator);

    public SkillSystemRegistrationBuilder SupportEffect<T>() where T : EffectDefinition =>
        AddType(SupportedEffectTypes, typeof(T));

    public SkillSystemRegistrationBuilder SupportCondition<T>() where T : ConditionDefinition =>
        AddType(SupportedConditionTypes, typeof(T));

    public SkillSystemRegistrationBuilder SupportModifier<T>() where T : RuleModifierDefinition =>
        AddType(SupportedModifierTypes, typeof(T));

    public SkillSystemRegistrationBuilder SupportAilmentBehavior<T>() where T : AilmentTurnBehaviorDefinition =>
        AddType(SupportedAilmentBehaviorTypes, typeof(T));

    public SkillSystemRegistrationSnapshot Build() => new(this);

    private SkillSystemRegistrationBuilder Add(HashSet<ContentId> target, IEnumerable<string> ids)
    {
        foreach (string id in ids)
        {
            target.Add(ContentId.Parse(id));
        }

        return this;
    }

    private SkillSystemRegistrationBuilder AddValidator(
        IDictionary<ContentId, IContentParameterValidator> target,
        string id,
        IContentParameterValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        target.Add(ContentId.Parse(id), validator);
        return this;
    }

    private SkillSystemRegistrationBuilder AddType(ISet<Type> target, Type type)
    {
        target.Add(type);
        return this;
    }
}
