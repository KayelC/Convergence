using Convergence.Content;
using Convergence.Validation;

namespace Convergence.Catalog;

public sealed class SkillSystemCatalogLoader : ISkillSystemCatalogLoader
{
    private static readonly HashSet<string> SupportedDocumentTypes =
    [
        "skills",
        "entities",
        "races",
        "ailments",
        "items",
        "equipment",
        "shops",
        "negotiations",
        "encounters",
        "dungeons",
        "fusion",
        "rulesets"
    ];

    private readonly ISkillSystemDocumentDeserializer _deserializer;
    private readonly ISkillSystemContentValidator _validator;

    public SkillSystemCatalogLoader()
        : this(new SkillSystemJsonDeserializer(), new SkillSystemContentValidator())
    {
    }

    public SkillSystemCatalogLoader(
        ISkillSystemDocumentDeserializer deserializer,
        ISkillSystemContentValidator validator)
    {
        ArgumentNullException.ThrowIfNull(deserializer);
        ArgumentNullException.ThrowIfNull(validator);
        _deserializer = deserializer;
        _validator = validator;
    }

    public CatalogLoadResult Load(SkillSystemCatalogLoadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<CatalogLoadDiagnostic>();
        var packs = new List<LoadedPack>();

        for (int bundleIndex = 0; bundleIndex < request.Bundles.Count; bundleIndex++)
        {
            LoadBundle(request.Bundles[bundleIndex], bundleIndex, request.Registrations, diagnostics, packs);
        }

        IReadOnlyList<LoadedPack> uniquePacks = ValidatePackGraph(packs, diagnostics, out List<LoadedPack> loadOrder);
        var ordered = loadOrder.ToHashSet(ReferenceEqualityComparer.Instance);
        IReadOnlyList<LoadedPack> diagnosticOrder = loadOrder
            .Concat(uniquePacks.Where(pack => !ordered.Contains(pack)))
            .ToArray();
        GameDataCatalog candidate = BuildCatalog(
            diagnosticOrder,
            request.Registrations,
            diagnostics);
        ValidateCrossPackReferences(uniquePacks, candidate, diagnostics);

        return diagnostics.Count == 0
            ? new CatalogLoadResult([], candidate)
            : new CatalogLoadResult(diagnostics, null);
    }

    private void LoadBundle(
        ContentPackTextBundle bundle,
        int bundleIndex,
        SkillSystemRegistrationSnapshot registrations,
        List<CatalogLoadDiagnostic> diagnostics,
        List<LoadedPack> packs)
    {
        ContentPackManifest manifest;
        try
        {
            manifest = _deserializer.DeserializeManifest(bundle.ManifestJson, bundle.ManifestSourceName);
        }
        catch (ContentDeserializationException exception)
        {
            diagnostics.Add(DeserializationDiagnostic(
                CatalogLoadDiagnosticCode.ManifestDeserializationFailed,
                null,
                exception));
            return;
        }

        int startingErrorCount = diagnostics.Count;
        var suppliedByPath = new Dictionary<string, ContentDocumentText>(StringComparer.Ordinal);
        foreach (ContentDocumentText document in bundle.Documents)
        {
            if (!IsCanonicalPath(document.Path))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.DocumentPathInvalid, manifest.Id,
                    document.SourceName, "$", $"Document path '{document.Path}' is not a canonical relative path.");
            }

            if (!suppliedByPath.TryAdd(document.Path, document))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.DocumentPathDuplicate, manifest.Id,
                    document.SourceName, "$", $"Document path '{document.Path}' was supplied more than once.");
            }
        }

        var declaredPaths = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < manifest.Documents.Count; index++)
        {
            ContentPackDocumentReference reference = manifest.Documents[index];
            string path = $"$.documents[{index}]";
            if (!IsCanonicalPath(reference.Path))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.DocumentPathInvalid, manifest.Id,
                    bundle.ManifestSourceName, path + ".path",
                    $"Manifest path '{reference.Path}' is not a canonical relative path.");
            }

            if (!declaredPaths.Add(reference.Path))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.DocumentPathDuplicate, manifest.Id,
                    bundle.ManifestSourceName, path + ".path",
                    $"Manifest path '{reference.Path}' is declared more than once.");
            }

            if (!SupportedDocumentTypes.Contains(reference.Type))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.DocumentTypeUnsupported, manifest.Id,
                    bundle.ManifestSourceName, path + ".type",
                    $"Document type '{reference.Type}' is not supported by the skill-system catalog.");
            }

            if (!suppliedByPath.ContainsKey(reference.Path))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.DocumentMissing, manifest.Id,
                    bundle.ManifestSourceName, path + ".path",
                    $"Manifest document '{reference.Path}' was not supplied.");
            }
        }

        foreach (ContentDocumentText document in bundle.Documents)
        {
            if (!declaredPaths.Contains(document.Path))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.DocumentUnexpected, manifest.Id,
                    document.SourceName, "$", $"Document '{document.Path}' is not declared by the manifest.");
            }
        }

        if (diagnostics.Count != startingErrorCount)
        {
            packs.Add(new LoadedPack(bundleIndex, bundle, manifest, null));
            return;
        }

        var skills = new List<SourceContentDocument<SkillDefinition>>();
        var entities = new List<SourceContentDocument<EntityDefinition>>();
        var races = new List<SourceContentDocument<RaceDefinition>>();
        var ailments = new List<SourceContentDocument<AilmentDefinition>>();
        var items = new List<SourceContentDocument<ItemDefinition>>();
        var equipment = new List<SourceContentDocument<EquipmentDefinition>>();
        var shops = new List<SourceContentDocument<ShopCatalogDefinition>>();
        var negotiations = new List<SourceContentDocument<NegotiationDefinition>>();
        var encounters = new List<SourceContentDocument<EncounterDefinition>>();
        var dungeons = new List<SourceContentDocument<DungeonDefinition>>();
        var fusion = new List<SourceContentDocument<FusionRecipeDefinition>>();
        var rulesets = new List<SourceContentDocument<RulesetDefinition>>();
        foreach (ContentPackDocumentReference reference in manifest.Documents)
        {
            ContentDocumentText document = suppliedByPath[reference.Path];
            try
            {
                switch (reference.Type)
                {
                    case "skills":
                        skills.Add(new SourceContentDocument<SkillDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeSkills(document.Json, document.SourceName)));
                        break;
                    case "entities":
                        entities.Add(new SourceContentDocument<EntityDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeEntities(document.Json, document.SourceName)));
                        break;
                    case "races":
                        races.Add(new SourceContentDocument<RaceDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeRaces(document.Json, document.SourceName)));
                        break;
                    case "ailments":
                        ailments.Add(new SourceContentDocument<AilmentDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeAilments(document.Json, document.SourceName)));
                        break;
                    case "items":
                        items.Add(new SourceContentDocument<ItemDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeItems(document.Json, document.SourceName)));
                        break;
                    case "equipment":
                        equipment.Add(new SourceContentDocument<EquipmentDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeEquipment(document.Json, document.SourceName)));
                        break;
                    case "shops":
                        shops.Add(new SourceContentDocument<ShopCatalogDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeShops(document.Json, document.SourceName)));
                        break;
                    case "negotiations":
                        negotiations.Add(new SourceContentDocument<NegotiationDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeNegotiations(document.Json, document.SourceName)));
                        break;
                    case "encounters":
                        encounters.Add(new SourceContentDocument<EncounterDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeEncounters(document.Json, document.SourceName)));
                        break;
                    case "dungeons":
                        dungeons.Add(new SourceContentDocument<DungeonDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeDungeons(document.Json, document.SourceName)));
                        break;
                    case "fusion":
                        fusion.Add(new SourceContentDocument<FusionRecipeDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeFusionRecipes(document.Json, document.SourceName)));
                        break;
                    case "rulesets":
                        rulesets.Add(new SourceContentDocument<RulesetDefinition>(
                            reference.Path, document.SourceName,
                            _deserializer.DeserializeRulesets(document.Json, document.SourceName)));
                        break;
                }
            }
            catch (ContentDeserializationException exception)
            {
                diagnostics.Add(DeserializationDiagnostic(
                    CatalogLoadDiagnosticCode.DocumentDeserializationFailed,
                    manifest.Id,
                    exception));
            }
        }

        ValidatedSkillSystemContentPack? validated = null;
        if (diagnostics.Count == startingErrorCount)
        {
            ContentValidationResult result = _validator.Validate(new SkillSystemValidationRequest(
                manifest,
                bundle.ManifestSourceName,
                registrations,
                skills,
                entities,
                races,
                ailments,
                items,
                equipment,
                shops,
                negotiations,
                encounters,
                dungeons,
                fusion,
                rulesets));
            foreach (ContentValidationError error in result.Errors)
            {
                diagnostics.Add(new CatalogLoadDiagnostic(
                    CatalogLoadDiagnosticCode.ContentValidationFailed,
                    error.PackId,
                    error.SourceName,
                    error.JsonPath,
                    error.Message,
                    error.RecordType,
                    error.RecordId,
                    error.Code,
                    error.Suggestion));
            }

            validated = result.ValidatedContent;
        }

        packs.Add(new LoadedPack(bundleIndex, bundle, manifest, validated));
    }

    private static IReadOnlyList<LoadedPack> ValidatePackGraph(
        IReadOnlyList<LoadedPack> packs,
        List<CatalogLoadDiagnostic> diagnostics,
        out List<LoadedPack> loadOrder)
    {
        var byId = new Dictionary<string, LoadedPack>(StringComparer.Ordinal);
        foreach (LoadedPack pack in packs)
        {
            if (!byId.TryAdd(pack.Manifest.Id, pack))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.PackDuplicate, pack.Manifest.Id,
                    pack.Bundle.ManifestSourceName, "$.id",
                    $"Content pack '{pack.Manifest.Id}' was supplied more than once.");
            }
        }

        List<LoadedPack> uniquePacks = packs
            .Where(pack => ReferenceEquals(byId[pack.Manifest.Id], pack))
            .ToList();
        foreach (LoadedPack pack in uniquePacks)
        {
            var seenDependencies = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < pack.Manifest.Dependencies.Count; index++)
            {
                ContentPackDependency dependency = pack.Manifest.Dependencies[index];
                string path = $"$.dependencies[{index}]";
                if (!seenDependencies.Add(dependency.Id))
                {
                    Add(diagnostics, CatalogLoadDiagnosticCode.DependencyDuplicate, pack.Manifest.Id,
                        pack.Bundle.ManifestSourceName, path + ".id",
                        $"Dependency '{dependency.Id}' is declared more than once.");
                }

                if (dependency.Id == pack.Manifest.Id)
                {
                    Add(diagnostics, CatalogLoadDiagnosticCode.DependencySelfReference, pack.Manifest.Id,
                        pack.Bundle.ManifestSourceName, path + ".id",
                        "A content pack cannot depend on itself.");
                    continue;
                }

                if (!byId.TryGetValue(dependency.Id, out LoadedPack? target))
                {
                    Add(diagnostics, CatalogLoadDiagnosticCode.DependencyMissing, pack.Manifest.Id,
                        pack.Bundle.ManifestSourceName, path + ".id",
                        $"Dependency pack '{dependency.Id}' was not supplied.");
                }
                else if (target.Manifest.Version != dependency.Version)
                {
                    Add(diagnostics, CatalogLoadDiagnosticCode.DependencyVersionMismatch, pack.Manifest.Id,
                        pack.Bundle.ManifestSourceName, path + ".version",
                        $"Dependency '{dependency.Id}' requires exact version {dependency.Version}, " +
                        $"but version {target.Manifest.Version} was supplied.");
                }
            }
        }

        loadOrder = TopologicalOrder(uniquePacks, byId);
        if (loadOrder.Count != uniquePacks.Count)
        {
            foreach (LoadedPack pack in uniquePacks.Where(pack => IsInDependencyCycle(pack, byId)))
            {
                Add(diagnostics, CatalogLoadDiagnosticCode.DependencyCycle, pack.Manifest.Id,
                    pack.Bundle.ManifestSourceName, "$.dependencies",
                    $"Content pack '{pack.Manifest.Id}' participates in a dependency cycle.");
            }
        }

        return uniquePacks;
    }

    private static List<LoadedPack> TopologicalOrder(
        IReadOnlyList<LoadedPack> packs,
        IReadOnlyDictionary<string, LoadedPack> byId)
    {
        var dependencyCount = new Dictionary<LoadedPack, int>(ReferenceEqualityComparer.Instance);
        var dependents = new Dictionary<LoadedPack, List<LoadedPack>>(ReferenceEqualityComparer.Instance);
        foreach (LoadedPack pack in packs)
        {
            dependencyCount.Add(
                pack,
                pack.Manifest.Dependencies
                    .Select(dependency => dependency.Id)
                    .Where(dependencyId => dependencyId != pack.Manifest.Id)
                    .Distinct(StringComparer.Ordinal)
                    .Count(byId.ContainsKey));
            dependents.Add(pack, []);
        }
        foreach (LoadedPack pack in packs)
        {
            foreach (string dependencyId in pack.Manifest.Dependencies
                         .Select(dependency => dependency.Id)
                         .Where(dependencyId => dependencyId != pack.Manifest.Id)
                         .Distinct(StringComparer.Ordinal))
            {
                if (byId.TryGetValue(dependencyId, out LoadedPack? dependency))
                {
                    dependents[dependency].Add(pack);
                }
            }
        }

        var ready = new List<LoadedPack>(packs.Where(pack => dependencyCount[pack] == 0));
        ready.Sort((left, right) => left.BundleIndex.CompareTo(right.BundleIndex));
        var result = new List<LoadedPack>();
        while (ready.Count > 0)
        {
            LoadedPack next = ready[0];
            ready.RemoveAt(0);
            result.Add(next);
            foreach (LoadedPack dependent in dependents[next])
            {
                dependencyCount[dependent]--;
                if (dependencyCount[dependent] == 0)
                {
                    ready.Add(dependent);
                    ready.Sort((left, right) => left.BundleIndex.CompareTo(right.BundleIndex));
                }
            }
        }

        return result;
    }

    private static bool IsInDependencyCycle(
        LoadedPack origin,
        IReadOnlyDictionary<string, LoadedPack> byId)
    {
        var visited = new HashSet<LoadedPack>(ReferenceEqualityComparer.Instance);
        return CanReachOrigin(origin, origin, visited);

        bool CanReachOrigin(
            LoadedPack origin,
            LoadedPack current,
            HashSet<LoadedPack> visited)
        {
            if (!visited.Add(current))
            {
                return false;
            }

            foreach (string dependencyId in current.Manifest.Dependencies
                         .Select(dependency => dependency.Id)
                         .Where(dependencyId => dependencyId != current.Manifest.Id)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!byId.TryGetValue(dependencyId, out LoadedPack? dependency))
                {
                    continue;
                }

                if (ReferenceEquals(dependency, origin) || CanReachOrigin(origin, dependency, visited))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static GameDataCatalog BuildCatalog(
        IReadOnlyList<LoadedPack> loadOrder,
        SkillSystemRegistrationSnapshot registrations,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        var skills = new List<KeyValuePair<ContentId, SkillDefinition>>();
        var entities = new List<KeyValuePair<ContentId, EntityDefinition>>();
        var races = new List<KeyValuePair<ContentId, RaceDefinition>>();
        var ailments = new List<KeyValuePair<ContentId, AilmentDefinition>>();
        var items = new List<KeyValuePair<ContentId, ItemDefinition>>();
        var equipment = new List<KeyValuePair<ContentId, EquipmentDefinition>>();
        var shops = new List<KeyValuePair<ContentId, ShopCatalogDefinition>>();
        var negotiations = new List<KeyValuePair<ContentId, NegotiationDefinition>>();
        var encounters = new List<KeyValuePair<ContentId, EncounterDefinition>>();
        var dungeons = new List<KeyValuePair<ContentId, DungeonDefinition>>();
        var fusion = new List<KeyValuePair<ContentId, FusionRecipeDefinition>>();
        var rulesets = new List<KeyValuePair<ContentId, RulesetDefinition>>();
        var contentPacks = new List<ContentPackIdentity>();

        foreach (LoadedPack pack in loadOrder.Where(pack => pack.Validated is not null))
        {
            string packId = pack.Manifest.Id;
            contentPacks.Add(new ContentPackIdentity(packId, pack.Manifest.Version));
            AddQualified(pack, pack.Validated!.SkillDocuments, skills, definition => DefinitionQualifier.Skill(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.EntityDocuments, entities, definition => DefinitionQualifier.Entity(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.RaceDocuments, races, definition => DefinitionQualifier.Race(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.AilmentDocuments, ailments, definition => DefinitionQualifier.Ailment(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.ItemDocuments, items, definition => DefinitionQualifier.Item(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.EquipmentDocuments, equipment, definition => DefinitionQualifier.Equipment(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.ShopDocuments, shops, definition => DefinitionQualifier.Shop(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.NegotiationDocuments, negotiations, definition => DefinitionQualifier.Negotiation(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.EncounterDocuments, encounters, definition => DefinitionQualifier.Encounter(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.DungeonDocuments, dungeons, definition => DefinitionQualifier.Dungeon(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.FusionDocuments, fusion, definition => DefinitionQualifier.FusionRecipe(packId, definition), diagnostics);
            AddQualified(pack, pack.Validated.RulesetDocuments, rulesets, definition => DefinitionQualifier.Ruleset(packId, definition), diagnostics);
        }

        return new GameDataCatalog(
            contentPacks,
            skills,
            entities,
            races,
            ailments,
            items,
            equipment,
            shops,
            negotiations,
            encounters,
            dungeons,
            fusion,
            rulesets,
            registrations);
    }

    private static void AddQualified<TDefinition>(
        LoadedPack pack,
        IReadOnlyList<SourceContentDocument<TDefinition>> documents,
        ICollection<KeyValuePair<ContentId, TDefinition>> output,
        Func<TDefinition, TDefinition> qualify,
        List<CatalogLoadDiagnostic> diagnostics)
        where TDefinition : class
    {
        var existing = output.Select(pair => pair.Key).ToHashSet();
        foreach (SourceContentDocument<TDefinition> document in documents)
        {
            foreach (TDefinition definition in document.Document.Records)
            {
                TDefinition qualified = qualify(definition);
                ContentId id = DefinitionId(qualified);
                if (!existing.Add(id))
                {
                    Add(diagnostics, CatalogLoadDiagnosticCode.CatalogDuplicateId, pack.Manifest.Id,
                        document.SourceName, "$", $"Catalog ID '{id}' is duplicated.",
                        recordId: id);
                    continue;
                }
                output.Add(new KeyValuePair<ContentId, TDefinition>(id, qualified));
            }
        }
    }

    private static ContentId DefinitionId<TDefinition>(TDefinition definition) => definition switch
    {
        SkillDefinition value => value.Id,
        EntityDefinition value => value.Id,
        RaceDefinition value => value.Id,
        AilmentDefinition value => value.Id,
        ItemDefinition value => value.Id,
        EquipmentDefinition value => value.Id,
        ShopCatalogDefinition value => value.Id,
        NegotiationDefinition value => value.Id,
        EncounterDefinition value => value.Id,
        DungeonDefinition value => value.Id,
        FusionRecipeDefinition value => value.Id,
        RulesetDefinition value => value.Id,
        _ => throw new InvalidOperationException($"Unsupported catalog definition '{typeof(TDefinition).Name}'.")
    };

    private static void ValidateCrossPackReferences(
        IReadOnlyList<LoadedPack> packs,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (LoadedPack pack in packs.Where(pack => pack.Validated is not null))
        {
            CheckSkillReferences(pack, catalog, diagnostics);
            CheckEntityReferences(pack, catalog, diagnostics);
            CheckAilmentReferences(pack, catalog, diagnostics);
            CheckItemReferences(pack, catalog, diagnostics);
            CheckEquipmentReferences(pack, catalog, diagnostics);
            CheckShopReferences(pack, catalog, diagnostics);
            CheckNegotiationReferences(pack, catalog, diagnostics);
            CheckEncounterReferences(pack, catalog, diagnostics);
            CheckDungeonReferences(pack, catalog, diagnostics);
            CheckFusionReferences(pack, catalog, diagnostics);
        }
    }

    private static void CheckEquipmentReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<EquipmentDefinition> document in pack.Validated!.EquipmentDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                EquipmentDefinition equipment = document.Document.Records[recordIndex];
                for (int index = 0; index < equipment.GrantedSkillIds.Count; index++)
                {
                    CheckReference(pack, document.SourceName, "equipment", equipment.Id,
                        $"$.equipment[{recordIndex}].grantedSkillIds[{index}]",
                        equipment.GrantedSkillIds[index], ReferenceKind.Skill, catalog, diagnostics);
                }
            }
        }
    }

    private static void CheckShopReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<ShopCatalogDefinition> document in pack.Validated!.ShopDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                ShopCatalogDefinition shop = document.Document.Records[recordIndex];
                for (int index = 0; index < shop.Offers.Count; index++)
                {
                    ShopOfferDefinition offer = shop.Offers[index];
                    ReferenceKind kind = offer.ContentKind == ShopContentKind.Item
                        ? ReferenceKind.Item
                        : ReferenceKind.Equipment;
                    CheckReference(pack, document.SourceName, "shop", shop.Id,
                        $"$.shops[{recordIndex}].offers[{index}].contentId",
                        offer.ContentId, kind, catalog, diagnostics);
                }
            }
        }
    }

    private static void CheckNegotiationReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<NegotiationDefinition> document in pack.Validated!.NegotiationDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                NegotiationDefinition negotiation = document.Document.Records[recordIndex];
                for (int index = 0; index < negotiation.DefaultRaceIds.Count; index++)
                {
                    CheckReference(pack, document.SourceName, "negotiation", negotiation.Id,
                        $"$.negotiations[{recordIndex}].defaultRaceIds[{index}]",
                        negotiation.DefaultRaceIds[index], ReferenceKind.Race, catalog, diagnostics);
                }

                for (int index = 0; index < negotiation.DefaultEntityIds.Count; index++)
                {
                    CheckReference(pack, document.SourceName, "negotiation", negotiation.Id,
                        $"$.negotiations[{recordIndex}].defaultEntityIds[{index}]",
                        negotiation.DefaultEntityIds[index], ReferenceKind.Entity, catalog, diagnostics);
                }
            }
        }
    }

    private static void CheckEncounterReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<EncounterDefinition> document in pack.Validated!.EncounterDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                EncounterDefinition encounter = document.Document.Records[recordIndex];
                for (int formationIndex = 0; formationIndex < encounter.Formations.Count; formationIndex++)
                {
                    EncounterFormationDefinition formation = encounter.Formations[formationIndex];
                    for (int memberIndex = 0; memberIndex < formation.Members.Count; memberIndex++)
                    {
                        CheckReference(pack, document.SourceName, "encounter", encounter.Id,
                            $"$.encounters[{recordIndex}].formations[{formationIndex}].members[{memberIndex}].entityId",
                            formation.Members[memberIndex].EntityId, ReferenceKind.Entity, catalog, diagnostics);
                    }
                }
            }
        }
    }

    private static void CheckDungeonReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<DungeonDefinition> document in pack.Validated!.DungeonDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                DungeonDefinition dungeon = document.Document.Records[recordIndex];
                for (int blockIndex = 0; blockIndex < dungeon.Blocks.Count; blockIndex++)
                {
                    DungeonBlockDefinition block = dungeon.Blocks[blockIndex];
                    for (int index = 0; index < block.EncounterPoolIds.Count; index++)
                    {
                        CheckReference(pack, document.SourceName, "dungeon", dungeon.Id,
                            $"$.dungeons[{recordIndex}].blocks[{blockIndex}].encounterPoolIds[{index}]",
                            block.EncounterPoolIds[index], ReferenceKind.Encounter, catalog, diagnostics);
                    }

                    for (int floorIndex = 0; floorIndex < block.FixedFloors.Count; floorIndex++)
                    {
                        if (block.FixedFloors[floorIndex].EncounterId is ContentId encounterId)
                        {
                            CheckReference(pack, document.SourceName, "dungeon", dungeon.Id,
                                $"$.dungeons[{recordIndex}].blocks[{blockIndex}].fixedFloors[{floorIndex}].encounterId",
                                encounterId, ReferenceKind.Encounter, catalog, diagnostics);
                        }
                    }
                }
            }
        }
    }

    private static void CheckFusionReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<FusionRecipeDefinition> document in pack.Validated!.FusionDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                FusionRecipeDefinition recipe = document.Document.Records[recordIndex];
                for (int parentIndex = 0; parentIndex < recipe.Parents.Count; parentIndex++)
                {
                    FusionParentSelectorDefinition parent = recipe.Parents[parentIndex];
                    ReferenceKind kind = parent.Kind == FusionParentSelectorKind.Entity
                        ? ReferenceKind.Entity
                        : ReferenceKind.Race;
                    CheckReference(pack, document.SourceName, "fusion recipe", recipe.Id,
                        $"$.fusionRecipes[{recordIndex}].parents[{parentIndex}].id",
                        parent.Id, kind, catalog, diagnostics);
                }

                if (recipe.Result.ResultEntityId is ContentId resultEntityId)
                {
                    CheckReference(pack, document.SourceName, "fusion recipe", recipe.Id,
                        $"$.fusionRecipes[{recordIndex}].result.resultEntityId",
                        resultEntityId, ReferenceKind.Entity, catalog, diagnostics);
                }

                if (recipe.Result.ResultRaceId is ContentId resultRaceId)
                {
                    CheckReference(pack, document.SourceName, "fusion recipe", recipe.Id,
                        $"$.fusionRecipes[{recordIndex}].result.resultRaceId",
                        resultRaceId, ReferenceKind.Race, catalog, diagnostics);
                }
            }
        }
    }

    private static void CheckItemReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<ItemDefinition> document in pack.Validated!.ItemDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                ItemDefinition item = document.Document.Records[recordIndex];
                if (item.Usage is null)
                {
                    continue;
                }

                for (int effectIndex = 0; effectIndex < item.Usage.Effects.Count; effectIndex++)
                {
                    CheckEffect(
                        pack,
                        document.SourceName,
                        "item",
                        item.Id,
                        $"$.items[{recordIndex}].usage.effects[{effectIndex}]",
                        item.Usage.Effects[effectIndex],
                        catalog,
                        diagnostics);
                }
            }
        }
    }

    private static void CheckSkillReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<SkillDefinition> document in pack.Validated!.SkillDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                SkillDefinition skill = document.Document.Records[recordIndex];
                string root = $"$.skills[{recordIndex}]";
                for (int index = 0; index < skill.Inheritance.ExclusiveOwnerEntityIds.Count; index++)
                {
                    CheckReference(pack, document.SourceName, "skill", skill.Id,
                        root + $".inheritance.exclusiveOwnerEntityIds[{index}]",
                        skill.Inheritance.ExclusiveOwnerEntityIds[index], ReferenceKind.Entity, catalog, diagnostics);
                }
                for (int index = 0; index < skill.Effects.Count; index++)
                {
                    CheckEffect(pack, document.SourceName, "skill", skill.Id,
                        root + $".effects[{index}]", skill.Effects[index], catalog, diagnostics);
                }
                for (int index = 0; index < skill.Triggers.Count; index++)
                {
                    PassiveTriggerDefinition trigger = skill.Triggers[index];
                    string path = root + $".triggers[{index}]";
                    if (trigger.When is not null)
                    {
                        CheckCondition(pack, document.SourceName, "skill", skill.Id,
                            path + ".when", trigger.When, catalog, diagnostics);
                    }
                    for (int effectIndex = 0; effectIndex < trigger.Effects.Count; effectIndex++)
                    {
                        CheckEffect(pack, document.SourceName, "skill", skill.Id,
                            path + $".effects[{effectIndex}]", trigger.Effects[effectIndex], catalog, diagnostics);
                    }
                }
                for (int index = 0; index < skill.Modifiers.Count; index++)
                {
                    if (skill.Modifiers[index] is AilmentResistanceRuleModifierDefinition resistance)
                    {
                        CheckReference(pack, document.SourceName, "skill", skill.Id,
                            root + $".modifiers[{index}].ailmentId", resistance.AilmentId,
                            ReferenceKind.Ailment, catalog, diagnostics);
                    }
                    if (skill.Modifiers[index].When is ConditionDefinition when)
                    {
                        CheckCondition(pack, document.SourceName, "skill", skill.Id,
                            root + $".modifiers[{index}].when", when, catalog, diagnostics);
                    }
                }
            }
        }
    }

    private static void CheckEntityReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<EntityDefinition> document in pack.Validated!.EntityDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                EntityDefinition entity = document.Document.Records[recordIndex];
                string root = $"$.entities[{recordIndex}]";
                CheckReference(pack, document.SourceName, "entity", entity.Id, root + ".raceId",
                    entity.RaceId, ReferenceKind.Race, catalog, diagnostics);
                foreach ((ContentId ailmentId, _) in entity.AilmentResistances)
                {
                    CheckReference(pack, document.SourceName, "entity", entity.Id,
                        root + $".ailmentResistances.{ailmentId}", ailmentId,
                        ReferenceKind.Ailment, catalog, diagnostics);
                }

                CheckSkillList(entity.InheritanceRules.BlockedSkillIds, ".inheritanceRules.blockedSkillIds");
                CheckSkillList(entity.InheritanceRules.AllowedSkillIds, ".inheritanceRules.allowedSkillIds", checkInheritance: true);
                CheckSkillList(entity.BaseSkillIds, ".baseSkillIds");
                for (int index = 0; index < entity.SkillUnlocks.Count; index++)
                {
                    CheckReference(pack, document.SourceName, "entity", entity.Id,
                        root + $".skillUnlocks[{index}].skillId", entity.SkillUnlocks[index].SkillId,
                        ReferenceKind.Skill, catalog, diagnostics);
                }

                void CheckSkillList(IReadOnlyList<ContentId> ids, string suffix, bool checkInheritance = false)
                {
                    for (int index = 0; index < ids.Count; index++)
                    {
                        ContentId canonical = DefinitionQualifier.ContentReference(pack.Manifest.Id, ids[index]);
                        CheckReference(pack, document.SourceName, "entity", entity.Id,
                            root + suffix + $"[{index}]", ids[index], ReferenceKind.Skill, catalog, diagnostics);
                        if (checkInheritance && catalog.Skills.TryGetValue(canonical, out SkillDefinition? skill))
                        {
                            ContentId canonicalEntityId = DefinitionQualifier.ContentReference(pack.Manifest.Id, entity.Id);
                            if (!skill.Inheritance.IsInheritable ||
                                (skill.Inheritance.ExclusiveOwnerEntityIds.Count > 0 &&
                                 !skill.Inheritance.ExclusiveOwnerEntityIds.Contains(canonicalEntityId)))
                            {
                                Add(diagnostics, CatalogLoadDiagnosticCode.CrossPackInheritanceInvalid,
                                    pack.Manifest.Id, document.SourceName, root + suffix + $"[{index}]",
                                    $"Skill '{canonical}' cannot be explicitly allowed by entity '{canonicalEntityId}'.",
                                    "entity", entity.Id);
                            }
                        }
                    }
                }
            }
        }
    }

    private static void CheckAilmentReferences(
        LoadedPack pack,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        foreach (SourceContentDocument<AilmentDefinition> document in pack.Validated!.AilmentDocuments)
        {
            for (int recordIndex = 0; recordIndex < document.Document.Records.Count; recordIndex++)
            {
                AilmentDefinition ailment = document.Document.Records[recordIndex];
                string root = $"$.ailments[{recordIndex}]";
                for (int index = 0; index < ailment.Triggers.Count; index++)
                {
                    PassiveTriggerDefinition trigger = ailment.Triggers[index];
                    string path = root + $".triggers[{index}]";
                    if (trigger.When is not null)
                    {
                        CheckCondition(pack, document.SourceName, "ailment", ailment.Id,
                            path + ".when", trigger.When, catalog, diagnostics);
                    }
                    for (int effectIndex = 0; effectIndex < trigger.Effects.Count; effectIndex++)
                    {
                        CheckEffect(pack, document.SourceName, "ailment", ailment.Id,
                            path + $".effects[{effectIndex}]", trigger.Effects[effectIndex], catalog, diagnostics);
                    }
                }
            }
        }
    }

    private static void CheckEffect(
        LoadedPack pack,
        string sourceName,
        string recordType,
        ContentId recordId,
        string path,
        EffectDefinition effect,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        if (effect.When is not null)
        {
            CheckCondition(pack, sourceName, recordType, recordId, path + ".when", effect.When, catalog, diagnostics);
        }

        switch (effect)
        {
            case ApplyAilmentEffectDefinition apply:
                CheckReference(pack, sourceName, recordType, recordId, path + ".ailmentId",
                    apply.AilmentId, ReferenceKind.Ailment, catalog, diagnostics);
                break;
            case RemoveAilmentEffectDefinition remove:
                for (int index = 0; index < remove.AilmentIds.Count; index++)
                {
                    CheckReference(pack, sourceName, recordType, recordId, path + $".ailmentIds[{index}]",
                        remove.AilmentIds[index], ReferenceKind.Ailment, catalog, diagnostics);
                }
                break;
        }
    }

    private static void CheckCondition(
        LoadedPack pack,
        string sourceName,
        string recordType,
        ContentId recordId,
        string path,
        ConditionDefinition condition,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        switch (condition)
        {
            case AllConditionDefinition all:
                for (int index = 0; index < all.Conditions.Count; index++)
                    CheckCondition(pack, sourceName, recordType, recordId, path + $".all[{index}]",
                        all.Conditions[index], catalog, diagnostics);
                break;
            case AnyConditionDefinition any:
                for (int index = 0; index < any.Conditions.Count; index++)
                    CheckCondition(pack, sourceName, recordType, recordId, path + $".any[{index}]",
                        any.Conditions[index], catalog, diagnostics);
                break;
            case NotConditionDefinition not:
                CheckCondition(pack, sourceName, recordType, recordId, path + ".not",
                    not.Condition, catalog, diagnostics);
                break;
            case HasAilmentConditionDefinition ailments:
                for (int index = 0; index < ailments.AilmentIds.Count; index++)
                    CheckReference(pack, sourceName, recordType, recordId, path + $".ailmentIds[{index}]",
                        ailments.AilmentIds[index], ReferenceKind.Ailment, catalog, diagnostics);
                break;
            case HasSkillConditionDefinition skill:
                CheckReference(pack, sourceName, recordType, recordId, path + ".skillId",
                    skill.SkillId, ReferenceKind.Skill, catalog, diagnostics);
                break;
        }
    }

    private static void CheckReference(
        LoadedPack sourcePack,
        string sourceName,
        string recordType,
        ContentId recordId,
        string path,
        ContentId reference,
        ReferenceKind kind,
        GameDataCatalog catalog,
        List<CatalogLoadDiagnostic> diagnostics)
    {
        ContentId canonical = DefinitionQualifier.ContentReference(sourcePack.Manifest.Id, reference);
        string targetPackId = canonical.ToString().Split(':', 2)[0];
        if (targetPackId != sourcePack.Manifest.Id &&
            !sourcePack.Manifest.Dependencies.Any(dependency => dependency.Id == targetPackId))
        {
            Add(diagnostics, CatalogLoadDiagnosticCode.ExternalDependencyNotDeclared,
                sourcePack.Manifest.Id, sourceName, path,
                $"Reference '{canonical}' targets pack '{targetPackId}', which is not a direct dependency.",
                recordType, recordId);
        }

        bool exists = kind switch
        {
            ReferenceKind.Skill => catalog.Skills.ContainsKey(canonical),
            ReferenceKind.Entity => catalog.Entities.ContainsKey(canonical),
            ReferenceKind.Race => catalog.Races.ContainsKey(canonical),
            ReferenceKind.Ailment => catalog.Ailments.ContainsKey(canonical),
            ReferenceKind.Item => catalog.Items.ContainsKey(canonical),
            ReferenceKind.Equipment => catalog.Equipment.ContainsKey(canonical),
            ReferenceKind.Encounter => catalog.Encounters.ContainsKey(canonical),
            _ => false
        };
        if (exists) return;

        bool existsAsOtherType = catalog.Skills.ContainsKey(canonical) ||
                                 catalog.Entities.ContainsKey(canonical) ||
                                 catalog.Races.ContainsKey(canonical) ||
                                 catalog.Ailments.ContainsKey(canonical) ||
                                 catalog.Items.ContainsKey(canonical) ||
                                 catalog.Equipment.ContainsKey(canonical) ||
                                 catalog.Shops.ContainsKey(canonical) ||
                                 catalog.Negotiations.ContainsKey(canonical) ||
                                 catalog.Encounters.ContainsKey(canonical) ||
                                 catalog.Dungeons.ContainsKey(canonical) ||
                                 catalog.FusionRecipes.ContainsKey(canonical) ||
                                 catalog.Rulesets.ContainsKey(canonical);
        Add(diagnostics,
            existsAsOtherType
                ? CatalogLoadDiagnosticCode.ExternalReferenceWrongType
                : CatalogLoadDiagnosticCode.ExternalReferenceMissing,
            sourcePack.Manifest.Id,
            sourceName,
            path,
            existsAsOtherType
                ? $"Reference '{canonical}' does not identify a {kind.ToString().ToLowerInvariant()} definition."
                : $"Referenced {kind.ToString().ToLowerInvariant()} '{canonical}' does not exist.",
            recordType,
            recordId);
    }

    private static bool IsCanonicalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path != path.Trim() || path.StartsWith('/') ||
            path.Contains('\\') || path.Contains(':'))
        {
            return false;
        }

        string[] segments = path.Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static CatalogLoadDiagnostic DeserializationDiagnostic(
        CatalogLoadDiagnosticCode code,
        string? packId,
        ContentDeserializationException exception) =>
        new(
            code,
            packId,
            exception.SourceName,
            exception.JsonPath ?? "$",
            exception.Message);

    private static void Add(
        ICollection<CatalogLoadDiagnostic> diagnostics,
        CatalogLoadDiagnosticCode code,
        string? packId,
        string sourceName,
        string jsonPath,
        string message,
        string? recordType = null,
        ContentId? recordId = null) =>
        diagnostics.Add(new CatalogLoadDiagnostic(
            code, packId, sourceName, jsonPath, message, recordType, recordId));

    private sealed record LoadedPack(
        int BundleIndex,
        ContentPackTextBundle Bundle,
        ContentPackManifest Manifest,
        ValidatedSkillSystemContentPack? Validated);

    private enum ReferenceKind
    {
        Skill,
        Entity,
        Race,
        Ailment,
        Item,
        Equipment,
        Encounter
    }
}
