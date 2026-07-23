using System.Text.Json;
using Convergence.Battle;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Fusion;
using Convergence.Runtime;

namespace Convergence.GodotHost.Infrastructure;

internal sealed record GodotSaveResource(string Id, decimal Current, decimal Maximum);

internal sealed record GodotSavePendingSkillChoice(
    long Token,
    int UnlockLevel,
    string SkillId);

internal sealed record GodotSaveTimedState(
    string Id,
    GodotSaveStatusLifetime Lifetime);

internal sealed record GodotSaveStatModifierBoundary(string EventId, long Sequence);

internal sealed record GodotSaveStatModifierContribution(
    long Sequence,
    int StageDelta,
    GodotSaveDuration? Duration,
    GodotSaveStatModifierBoundary? LastLifecycleBoundary);

internal sealed record GodotSaveStatModifierTrack(
    string ModifierTrackId,
    int ResolvedStage,
    IReadOnlyList<GodotSaveStatModifierContribution> Contributions);

internal sealed record GodotSaveStatModifierState(
    string PolicyId,
    IReadOnlyList<GodotSaveStatModifierTrack> Tracks);

internal sealed record GodotSaveCharge(
    string Kind,
    decimal Multiplier,
    GodotSaveStatusLifetime Lifetime);

internal sealed record GodotSaveShield(string Kind, GodotSaveStatusLifetime Lifetime);

internal sealed record GodotSaveAffinityBreak(
    string Element,
    GodotSaveStatusLifetime Lifetime);

internal sealed record GodotSaveAffinityOverride(
    string Element,
    string Affinity,
    GodotSaveStatusLifetime Lifetime);

internal sealed record GodotSaveStatusLifetime(
    GodotSaveDuration Expiration,
    IReadOnlyList<string> AllowedRemovalCauses);

internal sealed record GodotSaveDuration(
    string Kind,
    int? Value = null,
    string? TickEventId = null,
    bool? SuspendWhileReserve = null,
    string? PhaseId = null);

internal sealed record GodotSaveAnalysis(
    string TargetInstanceId,
    IReadOnlyList<string> Layers);

internal sealed record GodotSavePassiveSkillState(string SkillId, bool IsEnabled);

internal sealed record GodotSavePassiveActivation(
    string SkillId,
    string EventId,
    int TriggerIndex,
    int ActivationCount,
    string? TargetInstanceId);

internal sealed record GodotSaveActor(
    string InstanceId,
    string EntityId,
    string ActorKindId,
    string DisplayName,
    string? DisplaySubtitle,
    string TeamId,
    string CommandAuthorityId,
    int Level,
    long Experience,
    long LifetimeExperience,
    int UnspentStatPoints,
    bool IsDeployed,
    bool HasSwappedThisTurn,
    string VitalResourceId,
    IReadOnlyList<GodotSaveResource> Resources,
    IReadOnlyDictionary<string, decimal> BaseResourceValues,
    IReadOnlyDictionary<string, decimal> BaseStats,
    IReadOnlyDictionary<string, decimal> EffectiveStats,
    IReadOnlyList<string> LearnedSkillIds,
    IReadOnlyList<string> EquippedSkillIds,
    long SkillRevision,
    IReadOnlyList<GodotSavePendingSkillChoice> PendingSkillChoices,
    IReadOnlyList<string> CapabilityIds,
    IReadOnlyDictionary<string, string> EquippedItemIds,
    IReadOnlyList<GodotSaveTimedState> Ailments,
    IReadOnlyList<GodotSaveTimedState> Statuses,
    GodotSaveStatModifierState? StatModifiers,
    string? ChargePolicyId,
    IReadOnlyList<GodotSaveCharge> Charges,
    IReadOnlyList<GodotSaveShield> Shields,
    IReadOnlyList<GodotSaveAffinityBreak> AffinityBreaks,
    IReadOnlyList<GodotSaveAffinityOverride> AffinityOverrides,
    bool IsGuarding,
    IReadOnlyList<GodotSaveAnalysis> Analysis,
    IReadOnlyList<GodotSavePassiveSkillState> PassiveSkillStates,
    IReadOnlyList<GodotSavePassiveActivation> PassiveActivations);

internal sealed record GodotSaveActorReference(
    string InstanceId,
    string EntityId,
    string DisplayName);

internal sealed record GodotSavePartyRoster(
    GodotSaveActorReference Owner,
    IReadOnlyList<GodotSaveActorReference> ActiveParty,
    IReadOnlyList<GodotSaveActorReference> ReserveMembers,
    GodotSaveActorReference? ActiveHostedEntity,
    IReadOnlyList<GodotSaveActorReference> HostedEntityRoster,
    IReadOnlyList<GodotSaveActorReference> CompanionRoster,
    int MaxActivePartySize);

internal sealed record GodotSaveSceneInstance(string InstanceId, string NodePath);

internal sealed record GodotSaveDocument(
    int SaveContractVersion,
    string FrameworkVersion,
    string ContentPackId,
    string ContentPackVersion,
    IReadOnlyList<GodotSaveActor> Actors,
    GodotSavePartyRoster PartyRoster,
    IReadOnlyList<GodotSaveSceneInstance> SceneInstances);

internal sealed record GodotSaveRestoreResult(
    RuntimeSaveGameSnapshot DecodedSnapshot,
    RuntimeSessionRestoreResult RestoreResult,
    IReadOnlyList<GodotSaveSceneInstance> SceneInstances)
{
    public bool IsSuccess => RestoreResult.IsSuccess;
    public RuntimeRestoredSession RequireSession() => RestoreResult.RequireSession();
}

internal static class GodotSaveCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Serialize(
        IReadOnlyList<CatalogBattleActor> actors,
        RuntimePartyRosterSnapshot partyRoster,
        ContentPackIdentity pack,
        GodotSceneInstanceRegistry sceneInstances)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(partyRoster);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(sceneInstances);

        GodotSaveActor[] actorRecords = actors
            .Select(actor => ToDto(actor.State.ToSnapshot()))
            .ToArray();
        GodotSaveSceneInstance[] sceneRecords = sceneInstances.Snapshot()
            .Select(pair => new GodotSaveSceneInstance(
                pair.Key.ToString(),
                pair.Value.GetPath().ToString()))
            .ToArray();
        var document = new GodotSaveDocument(
            RuntimeSaveGameSnapshot.CurrentContractVersion,
            "0.1.0",
            pack.Id,
            pack.Version.ToString(),
            actorRecords,
            ToDto(partyRoster),
            sceneRecords);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static GodotSaveRestoreResult DeserializeAndRestore(
        string json,
        GameDataCatalog catalog,
        IRuntimeSessionRestoreService restoreService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(restoreService);

        GodotSaveDocument document = JsonSerializer.Deserialize<GodotSaveDocument>(
            json,
            JsonOptions) ?? throw new InvalidDataException("The Godot save document was empty.");
        if (document.SaveContractVersion != RuntimeSaveGameSnapshot.CurrentContractVersion)
        {
            throw new InvalidDataException(
                $"Save contract {document.SaveContractVersion} is unsupported; expected " +
                $"{RuntimeSaveGameSnapshot.CurrentContractVersion}.");
        }

        RuntimeActorSnapshot[] actorSnapshots = document.Actors
            .Select(FromDto)
            .ToArray();
        RuntimePartyRosterSnapshot partyRoster = FromDto(document.PartyRoster);
        var snapshot = new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse(document.FrameworkVersion),
            [
                new ContentPackIdentity(
                    document.ContentPackId,
                    SemanticVersion.Parse(document.ContentPackVersion))
            ],
            actorSnapshots,
            partyRoster,
            new RuntimeInventorySnapshot(),
            new RuntimeEquipmentSnapshot(),
            new RuntimeWalletSnapshot(0),
            field: null,
            new CompendiumStateSnapshot(),
            new RuntimeKnowledgeSnapshot(),
            new RuntimeSessionProgressSnapshot(),
            new RuntimeCheckpointLogSnapshot(
            [
                new RuntimeCheckpointEntrySnapshot(
                    1,
                    RuntimeCheckpointKind.SaveCreated,
                    "Godot-owned save restored.",
                    partyRoster.Owner.InstanceId,
                    partyRoster.Owner.EntityDefinitionId)
            ]),
            hostContext:
            [
                new KeyValuePair<ContentId, string>(ContentId.Parse("host_kind"), "godot")
            ],
            contractVersion: document.SaveContractVersion);

        RuntimeSessionRestoreResult restored = restoreService.Restore(snapshot, catalog);
        return new GodotSaveRestoreResult(
            snapshot,
            restored,
            restored.IsSuccess
                ? Array.AsReadOnly(document.SceneInstances.ToArray())
                : Array.Empty<GodotSaveSceneInstance>());
    }

    private static GodotSaveActor ToDto(RuntimeActorSnapshot actor) =>
        new(
            actor.Identity.InstanceId.ToString(),
            actor.Identity.EntityDefinitionId.ToString(),
            actor.Identity.ActorKindId.ToString(),
            actor.Identity.DisplayName,
            actor.Identity.DisplaySubtitle,
            actor.Affiliation.TeamId.ToString(),
            actor.Affiliation.CommandAuthorityId.ToString(),
            actor.Progression.Level,
            actor.Progression.Experience,
            actor.Progression.LifetimeExperience,
            actor.Progression.UnspentStatPoints,
            actor.EncounterPresence.IsDeployed,
            actor.EncounterPresence.HasSwappedThisTurn,
            actor.VitalResourceId.ToString(),
            actor.Resources.Select(resource => new GodotSaveResource(
                resource.ResourceId.ToString(),
                resource.Current,
                resource.Maximum)).ToArray(),
            actor.BaseResourceValues.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Stats.BaseStats.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Stats.EffectiveStats.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            actor.Skills.LearnedSkillIds.Select(id => id.ToString()).ToArray(),
            actor.Skills.EquippedSkillIds.Select(id => id.ToString()).ToArray(),
            actor.Skills.Revision,
            actor.Skills.PendingChoices.Select(choice => new GodotSavePendingSkillChoice(
                choice.Token.Value,
                choice.UnlockLevel,
                choice.SkillId.ToString())).ToArray(),
            actor.CapabilityIds.Select(id => id.ToString()).ToArray(),
            actor.Equipment.EquippedItemIds.ToDictionary(
                pair => pair.Key.ToString(),
                pair => pair.Value.ToString()),
            actor.BattleStatus.Ailments.Select(ToDto).ToArray(),
            actor.BattleStatus.Statuses.Select(ToDto).ToArray(),
            ToDto(actor.BattleStatus.StatModifiers),
            actor.BattleStatus.ChargeState?.PolicyId.ToString(),
            actor.BattleStatus.Charges.Select(charge => new GodotSaveCharge(
                charge.Kind.ToString(),
                charge.Multiplier,
                ToLifetimeDto(charge.Lifetime))).ToArray(),
            actor.BattleStatus.Shields.Select(shield => new GodotSaveShield(
                shield.Kind.ToString(),
                ToLifetimeDto(shield.Lifetime))).ToArray(),
            actor.BattleStatus.AffinityBreaks.Select(affinityBreak =>
                new GodotSaveAffinityBreak(
                    affinityBreak.Element.ToString(),
                    ToLifetimeDto(affinityBreak.Lifetime))).ToArray(),
            actor.BattleStatus.AffinityOverrides.Select(affinity =>
                new GodotSaveAffinityOverride(
                    affinity.Element.ToString(),
                    affinity.Affinity.ToString(),
                    ToLifetimeDto(affinity.Lifetime))).ToArray(),
            actor.BattleStatus.IsGuarding,
            actor.BattleStatus.Analysis.Select(analysis => new GodotSaveAnalysis(
                analysis.TargetInstanceId.ToString(),
                analysis.Layers.Select(layer => layer.ToString()).ToArray())).ToArray(),
            actor.BattleActivations.PassiveSkillStates.Select(passive =>
                new GodotSavePassiveSkillState(
                    passive.SkillId.ToString(),
                    passive.IsEnabled)).ToArray(),
            actor.BattleActivations.PassiveActivations.Select(passive =>
                new GodotSavePassiveActivation(
                    passive.SkillId.ToString(),
                    passive.EventId.ToString(),
                    passive.TriggerIndex,
                    passive.ActivationCount,
                    passive.TargetInstanceId?.ToString())).ToArray());

    private static RuntimeActorSnapshot FromDto(GodotSaveActor actor) =>
        new(
            new RuntimeActorIdentitySnapshot(
                Instance(actor.InstanceId),
                Id(actor.EntityId),
                Id(actor.ActorKindId),
                actor.DisplayName,
                actor.DisplaySubtitle),
            new RuntimeActorAffiliationSnapshot(
                Id(actor.CommandAuthorityId),
                Id(actor.TeamId)),
            new RuntimeEncounterPresenceSnapshot(
                actor.IsDeployed,
                actor.HasSwappedThisTurn),
            new RuntimeProgressionSnapshot(
                actor.Level,
                actor.Experience,
                actor.LifetimeExperience,
                actor.UnspentStatPoints),
            actor.Resources.Select(resource => new RuntimeResourceSnapshot(
                Id(resource.Id),
                resource.Current,
                resource.Maximum)),
            new RuntimeStatBlockSnapshot(
                DecimalPairs(actor.BaseStats),
                DecimalPairs(actor.EffectiveStats)),
            new RuntimeSkillStateSnapshot(
                actor.LearnedSkillIds.Select(Id),
                actor.EquippedSkillIds.Select(Id),
                actor.PendingSkillChoices.Select(choice =>
                    new RuntimePendingSkillChoiceSnapshot(
                        new RuntimeSkillChoiceToken(choice.Token),
                        choice.UnlockLevel,
                        Id(choice.SkillId))),
                actor.SkillRevision),
            new RuntimeEquipmentSnapshot(actor.EquippedItemIds.Select(pair =>
                new KeyValuePair<EquipmentSlot, ContentId>(
                    Enum.Parse<EquipmentSlot>(pair.Key),
                    Id(pair.Value)))),
            new RuntimeBattleStatusSnapshot(
                actor.Ailments.Select(FromDto),
                actor.Statuses.Select(FromDto),
                FromDto(actor.StatModifiers),
                FromDto(actor.ChargePolicyId, actor.Charges),
                actor.Shields.Select(shield => new RuntimeShieldSnapshot(
                    Enum.Parse<ShieldKind>(shield.Kind),
                    FromLifetimeDto(shield.Lifetime))),
                actor.AffinityOverrides.Select(affinity => new RuntimeAffinityOverrideSnapshot(
                    Enum.Parse<DamageElement>(affinity.Element),
                    Enum.Parse<ElementalAffinity>(affinity.Affinity),
                    FromLifetimeDto(affinity.Lifetime))),
                actor.IsGuarding,
                actor.Analysis.Select(analysis => new RuntimeAnalysisSnapshot(
                    Instance(analysis.TargetInstanceId),
                    analysis.Layers.Select(layer => Enum.Parse<AnalysisLayer>(layer)))),
                actor.AffinityBreaks.Select(affinityBreak => new RuntimeAffinityBreakSnapshot(
                    Enum.Parse<DamageElement>(affinityBreak.Element),
                    FromLifetimeDto(affinityBreak.Lifetime)))),
            new RuntimeBattleActivationSnapshot(
                actor.PassiveActivations.Select(passive =>
                    new RuntimePassiveActivationSnapshot(
                        Id(passive.SkillId),
                        Id(passive.EventId),
                        passive.TriggerIndex,
                        passive.ActivationCount,
                        passive.TargetInstanceId is null ? null : Instance(passive.TargetInstanceId))),
                actor.PassiveSkillStates.Select(passive =>
                    new RuntimePassiveSkillStateSnapshot(
                        Id(passive.SkillId),
                        passive.IsEnabled))),
            DecimalPairs(actor.BaseResourceValues),
            Id(actor.VitalResourceId),
            actor.CapabilityIds.Select(Id));

    private static GodotSaveStatModifierState? ToDto(RuntimeStatModifierStateSnapshot? state) =>
        state is null
            ? null
            : new GodotSaveStatModifierState(
                state.PolicyId.ToString(),
                state.Tracks.Select(track => new GodotSaveStatModifierTrack(
                    track.ModifierTrackId.ToString(),
                    track.ResolvedStage,
                    track.Contributions.Select(contribution =>
                        new GodotSaveStatModifierContribution(
                            contribution.Sequence,
                            contribution.StageDelta,
                            ToDto(contribution.Duration),
                            contribution.LastLifecycleBoundary is null
                                ? null
                                : new GodotSaveStatModifierBoundary(
                                    contribution.LastLifecycleBoundary.EventId.ToString(),
                                    contribution.LastLifecycleBoundary.Sequence))).ToArray())).ToArray());

    private static RuntimeStatModifierStateSnapshot? FromDto(GodotSaveStatModifierState? state) =>
        state is null
            ? null
            : new RuntimeStatModifierStateSnapshot(
                Id(state.PolicyId),
                state.Tracks.Select(track => new RuntimeStatModifierTrackSnapshot(
                    Id(track.ModifierTrackId),
                    track.ResolvedStage,
                    track.Contributions.Select(contribution =>
                        new RuntimeStatModifierContributionSnapshot(
                            contribution.Sequence,
                            contribution.StageDelta,
                            FromDto(contribution.Duration),
                            contribution.LastLifecycleBoundary is null
                                ? null
                                : new StatModifierLifecycleBoundary(
                                    Id(contribution.LastLifecycleBoundary.EventId),
                                    contribution.LastLifecycleBoundary.Sequence))))));

    private static RuntimeChargeStateSnapshot? FromDto(
        string? policyId,
        IReadOnlyList<GodotSaveCharge> charges)
    {
        ArgumentNullException.ThrowIfNull(charges);
        if (policyId is null)
        {
            if (charges.Count > 0)
            {
                throw new JsonException("Retained charges require a charge-policy ID.");
            }

            return null;
        }

        return new RuntimeChargeStateSnapshot(
            Id(policyId),
            charges.Select(charge => new RuntimeChargeSnapshot(
                Enum.Parse<ChargeKind>(charge.Kind),
                charge.Multiplier,
                FromLifetimeDto(charge.Lifetime))));
    }

    private static GodotSavePartyRoster ToDto(RuntimePartyRosterSnapshot roster) =>
        new(
            ToDto(roster.Owner),
            roster.ActiveParty.Select(ToDto).ToArray(),
            roster.ReserveMembers.Select(ToDto).ToArray(),
            roster.ActiveHostedEntity is null ? null : ToDto(roster.ActiveHostedEntity),
            roster.HostedEntityRoster.Select(ToDto).ToArray(),
            roster.CompanionRoster.Select(ToDto).ToArray(),
            roster.MaxActivePartySize);

    private static RuntimePartyRosterSnapshot FromDto(GodotSavePartyRoster roster) =>
        new(
            FromDto(roster.Owner),
            roster.ActiveParty.Select(FromDto),
            roster.ReserveMembers.Select(FromDto),
            roster.ActiveHostedEntity is null ? null : FromDto(roster.ActiveHostedEntity),
            roster.HostedEntityRoster.Select(FromDto),
            roster.CompanionRoster.Select(FromDto),
            roster.MaxActivePartySize);

    private static GodotSaveActorReference ToDto(RuntimeActorReferenceSnapshot actor) =>
        new(
            actor.InstanceId.ToString(),
            actor.EntityDefinitionId.ToString(),
            actor.DisplayName);

    private static RuntimeActorReferenceSnapshot FromDto(GodotSaveActorReference actor) =>
        new(Instance(actor.InstanceId), Id(actor.EntityId), actor.DisplayName);

    private static GodotSaveTimedState ToDto(RuntimeTimedStateSnapshot timed) =>
        new(timed.Id.ToString(), ToLifetimeDto(timed.Lifetime));

    private static RuntimeTimedStateSnapshot FromDto(GodotSaveTimedState timed) =>
        new(
            Id(timed.Id),
            FromLifetimeDto(timed.Lifetime));

    private static GodotSaveStatusLifetime ToLifetimeDto(StatusLifetimeDefinition lifetime) =>
        new(
            ToDto(lifetime.Expiration)!,
            lifetime.RemovalProfile.AllowedCauses.Select(cause => cause.ToString()).ToArray());

    private static StatusLifetimeDefinition FromLifetimeDto(GodotSaveStatusLifetime lifetime) =>
        new(
            FromDto(lifetime.Expiration) ??
                throw new InvalidDataException("Status lifetime expiration is required."),
            new StatusRemovalProfileDefinition(
                lifetime.AllowedRemovalCauses.Select(Enum.Parse<StatusRemovalCause>)));

    private static GodotSaveDuration? ToDto(DurationDefinition? duration) => duration switch
    {
        null => null,
        InstantDurationDefinition => new GodotSaveDuration(DurationKind.Instant.ToString()),
        TurnDurationDefinition turns => new GodotSaveDuration(
            DurationKind.Turns.ToString(),
            turns.Value,
            turns.TickEventId.ToString(),
            turns.SuspendWhileReserve),
        PhaseDurationDefinition phase => new GodotSaveDuration(
            DurationKind.Phase.ToString(),
            PhaseId: phase.PhaseId.ToString()),
        BattleDurationDefinition => new GodotSaveDuration(DurationKind.Battle.ToString()),
        PermanentDurationDefinition => new GodotSaveDuration(DurationKind.Permanent.ToString()),
        _ => throw new InvalidOperationException(
            $"Unsupported duration type '{duration.GetType().Name}'.")
    };

    private static DurationDefinition? FromDto(GodotSaveDuration? duration) => duration is null
        ? null
        : Enum.Parse<DurationKind>(duration.Kind) switch
        {
            DurationKind.Instant => new InstantDurationDefinition(),
            DurationKind.Turns => new TurnDurationDefinition(
                duration.Value ??
                    throw new InvalidDataException("Turn duration value is required."),
                Id(duration.TickEventId ??
                    throw new InvalidDataException("Turn duration tick event is required.")),
                duration.SuspendWhileReserve ?? false),
            DurationKind.Phase => new PhaseDurationDefinition(
                Id(duration.PhaseId ??
                    throw new InvalidDataException("Phase duration ID is required."))),
            DurationKind.Battle => new BattleDurationDefinition(),
            DurationKind.Permanent => new PermanentDurationDefinition(),
            _ => throw new InvalidDataException(
                $"Unsupported duration kind '{duration.Kind}'.")
        };

    private static IEnumerable<KeyValuePair<ContentId, decimal>> DecimalPairs(
        IReadOnlyDictionary<string, decimal> values) =>
        values.Select(pair => new KeyValuePair<ContentId, decimal>(Id(pair.Key), pair.Value));

    private static ContentId Id(string value) => ContentId.Parse(value);
    private static RuntimeInstanceId Instance(string value) => RuntimeInstanceId.Parse(value);
}
