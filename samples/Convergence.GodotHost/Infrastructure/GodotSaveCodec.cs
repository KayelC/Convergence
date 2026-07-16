using System.Text.Json;
using Convergence.Catalog;
using Convergence.Content;
using Convergence.Encounters;
using Convergence.Execution;
using Convergence.Fusion;
using Convergence.Runtime;

namespace Convergence.GodotHost.Infrastructure;

internal sealed record GodotSaveResource(string Id, decimal Current);

internal sealed record GodotSaveActor(
    string InstanceId,
    string EntityId,
    string TeamId,
    string CommandAuthorityId,
    int Level,
    long Experience,
    long LifetimeExperience,
    int UnspentStatPoints,
    bool IsDeployed,
    IReadOnlyList<GodotSaveResource> Resources,
    IReadOnlyList<string> LearnedSkillIds,
    IReadOnlyList<string> EquippedSkillIds,
    long SkillRevision,
    IReadOnlyList<GodotSavePendingSkillChoice> PendingSkillChoices);

internal sealed record GodotSavePendingSkillChoice(
    long Token,
    int UnlockLevel,
    string SkillId);

internal sealed record GodotSaveSceneInstance(string InstanceId, string NodePath);

internal sealed record GodotSaveDocument(
    int SaveContractVersion,
    string FrameworkVersion,
    string ContentPackId,
    string ContentPackVersion,
    IReadOnlyList<GodotSaveActor> Actors,
    string PartyOwnerInstanceId,
    IReadOnlyList<GodotSaveSceneInstance> SceneInstances);

internal sealed record GodotSaveRestoreResult(
    RuntimeSaveGameSnapshot Snapshot,
    IReadOnlyList<CatalogBattleActor> Actors,
    IReadOnlyList<GodotSaveSceneInstance> SceneInstances,
    RuntimeSaveValidationResult Validation);

internal static class GodotSaveCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Serialize(
        IReadOnlyList<CatalogBattleActor> actors,
        RuntimeInstanceId ownerId,
        ContentPackIdentity pack,
        GodotSceneInstanceRegistry sceneInstances)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(sceneInstances);

        GodotSaveActor[] actorRecords = actors.Select(actor =>
        {
            RuntimeActorSnapshot snapshot = actor.State.ToSnapshot();
            return new GodotSaveActor(
                snapshot.Identity.InstanceId.ToString(),
                snapshot.Identity.EntityDefinitionId.ToString(),
                snapshot.Affiliation.TeamId.ToString(),
                snapshot.Affiliation.CommandAuthorityId.ToString(),
                snapshot.Progression.Level,
                snapshot.Progression.Experience,
                snapshot.Progression.LifetimeExperience,
                snapshot.Progression.UnspentStatPoints,
                snapshot.EncounterPresence.IsDeployed,
                snapshot.Resources
                    .Select(resource => new GodotSaveResource(
                        resource.ResourceId.ToString(),
                        resource.Current))
                    .ToArray(),
                snapshot.Skills.LearnedSkillIds.Select(id => id.ToString()).ToArray(),
                snapshot.Skills.EquippedSkillIds.Select(id => id.ToString()).ToArray(),
                snapshot.Skills.Revision,
                snapshot.Skills.PendingChoices.Select(choice =>
                    new GodotSavePendingSkillChoice(
                        choice.Token.Value,
                        choice.UnlockLevel,
                        choice.SkillId.ToString())).ToArray());
        }).ToArray();

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
            ownerId.ToString(),
            sceneRecords);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static GodotSaveRestoreResult DeserializeAndRestore(
        string json,
        GameDataCatalog catalog,
        ICatalogBattleActorFactory actorFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(actorFactory);

        GodotSaveDocument document = JsonSerializer.Deserialize<GodotSaveDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("The Godot save document was empty.");
        if (document.SaveContractVersion != RuntimeSaveGameSnapshot.CurrentContractVersion)
        {
            throw new InvalidDataException(
                $"Save contract {document.SaveContractVersion} is unsupported; expected {RuntimeSaveGameSnapshot.CurrentContractVersion}.");
        }

        var actors = new List<CatalogBattleActor>(document.Actors.Count);
        foreach (GodotSaveActor actorRecord in document.Actors)
        {
            CatalogBattleActor created = actorFactory.Create(new CatalogBattleActorCreationRequest(
                ContentId.Parse(actorRecord.EntityId),
                RuntimeInstanceId.Parse(actorRecord.InstanceId),
                ContentId.Parse(actorRecord.TeamId),
                actorRecord.Level,
                actorRecord.IsDeployed,
                ContentId.Parse(actorRecord.CommandAuthorityId),
                new RuntimeProgressionSnapshot(
                    actorRecord.Level,
                    actorRecord.Experience,
                    actorRecord.LifetimeExperience,
                    actorRecord.UnspentStatPoints))).RequireActor();
            RuntimeActorSnapshot baseline = created.State.ToSnapshot();
            var savedResourceValues = actorRecord.Resources.ToDictionary(
                resource => ContentId.Parse(resource.Id),
                resource => resource.Current);
            RuntimeResourceSnapshot[] restoredResources = baseline.Resources.Select(resource =>
            {
                decimal current = savedResourceValues.GetValueOrDefault(
                    resource.ResourceId,
                    resource.Current);
                if (current < 0 || current > resource.Maximum)
                {
                    throw new InvalidDataException(
                        $"Saved resource '{resource.ResourceId}' must satisfy " +
                        $"0 <= current <= {resource.Maximum}.");
                }

                return new RuntimeResourceSnapshot(
                    resource.ResourceId,
                    current,
                    resource.Maximum);
            }).ToArray();
            var restoredSnapshot = new RuntimeActorSnapshot(
                baseline.Identity,
                baseline.Affiliation,
                baseline.EncounterPresence,
                baseline.Progression,
                restoredResources,
                baseline.Stats,
                new RuntimeSkillStateSnapshot(
                    actorRecord.LearnedSkillIds.Select(ContentId.Parse),
                    actorRecord.EquippedSkillIds.Select(ContentId.Parse),
                    actorRecord.PendingSkillChoices.Select(choice =>
                        new RuntimePendingSkillChoiceSnapshot(
                            new RuntimeSkillChoiceToken(choice.Token),
                            choice.UnlockLevel,
                            ContentId.Parse(choice.SkillId))),
                    actorRecord.SkillRevision),
                baseline.Equipment,
                baseline.BattleStatus,
                baseline.BattleActivations,
                baseline.BaseResourceValues,
                baseline.VitalResourceId,
                baseline.CapabilityIds);
            actors.Add(actorFactory.Restore(new CatalogBattleActorRestoreRequest(
                restoredSnapshot,
                RuntimeStatSourceKind.Actor,
                MissingHostedEntityBehavior.UseActorBaseStats)).RequireActor());
        }

        RuntimeInstanceId ownerId = RuntimeInstanceId.Parse(document.PartyOwnerInstanceId);
        CatalogBattleActor owner = actors.Single(actor => actor.State.InstanceId == ownerId);
        RuntimeActorSnapshot[] actorSnapshots = actors.Select(actor => actor.State.ToSnapshot()).ToArray();
        RuntimeActorSnapshot ownerSnapshot = owner.State.ToSnapshot();
        var ownerReference = new RuntimeActorReferenceSnapshot(
            ownerSnapshot.Identity.InstanceId,
            ownerSnapshot.Identity.EntityDefinitionId,
            ownerSnapshot.Identity.DisplayName);
        var snapshot = new RuntimeSaveGameSnapshot(
            SemanticVersion.Parse(document.FrameworkVersion),
            [new ContentPackIdentity(document.ContentPackId, SemanticVersion.Parse(document.ContentPackVersion))],
            actorSnapshots,
            new RuntimePartyRosterSnapshot(
                ownerReference,
                activeParty: [ownerReference]),
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
                    ownerId,
                    owner.Entity.Id)
            ]),
            hostContext:
            [
                new KeyValuePair<ContentId, string>(ContentId.Parse("host_kind"), "godot")
            ],
            contractVersion: document.SaveContractVersion);
        RuntimeSaveValidationResult validation = new RuntimeSaveValidator().Validate(snapshot, catalog);
        return new GodotSaveRestoreResult(
            snapshot,
            actors.AsReadOnly(),
            Array.AsReadOnly(document.SceneInstances.ToArray()),
            validation);
    }
}
