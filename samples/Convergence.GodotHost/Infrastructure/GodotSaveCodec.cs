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
    int Level,
    bool IsDeployed,
    IReadOnlyList<GodotSaveResource> Resources);

internal sealed record GodotSaveSceneInstance(string InstanceId, string NodePath);

internal sealed record GodotSaveDocument(
    int SaveContractVersion,
    string FrameworkVersion,
    string ContentPackId,
    string ContentPackVersion,
    IReadOnlyList<GodotSaveActor> Actors,
    string OwnerInstanceId,
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
                snapshot.Ownership.TeamId.ToString(),
                snapshot.Progression.Level,
                snapshot.EncounterPresence.IsDeployed,
                snapshot.Resources
                    .Select(resource => new GodotSaveResource(
                        resource.ResourceId.ToString(),
                        resource.Current))
                    .ToArray());
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
            CatalogBattleActor actor = actorFactory.Create(new CatalogBattleActorCreationRequest(
                ContentId.Parse(actorRecord.EntityId),
                RuntimeInstanceId.Parse(actorRecord.InstanceId),
                ContentId.Parse(actorRecord.TeamId),
                actorRecord.Level,
                actorRecord.IsDeployed)).RequireActor();
            foreach (GodotSaveResource resource in actorRecord.Resources)
            {
                ContentId resourceId = ContentId.Parse(resource.Id);
                BattleResourceState runtimeResource = actor.State.GetRequiredResource(resourceId);
                if (resource.Current < 0 || resource.Current > runtimeResource.Maximum)
                {
                    throw new InvalidDataException(
                        $"Saved resource '{resource.Id}' must satisfy 0 <= current <= {runtimeResource.Maximum}.");
                }

                actor.State.SetResource(resourceId, resource.Current);
            }

            actors.Add(actor);
        }

        RuntimeInstanceId ownerId = RuntimeInstanceId.Parse(document.OwnerInstanceId);
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
                ownerSnapshot.Progression.Level,
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
