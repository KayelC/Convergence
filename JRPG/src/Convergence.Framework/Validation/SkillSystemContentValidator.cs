using Convergence.Content;

namespace Convergence.Validation;

public sealed class SkillSystemContentValidator : ISkillSystemContentValidator
{
    private const int SupportedSchemaVersion = 1;

    public ContentValidationResult Validate(SkillSystemValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new ValidationContext(request);
        context.ValidateDocuments();
        context.ValidateRecords();

        return context.Errors.Count == 0
            ? new ContentValidationResult([], new ValidatedSkillSystemContentPack(request))
            : new ContentValidationResult(context.Errors, null);
    }

    private sealed class ValidationContext
    {
        private readonly SkillSystemValidationRequest _request;
        private readonly string _packId;
        private readonly SkillSystemRegistrationSnapshot _registrations;
        private readonly List<RecordSource<SkillDefinition>> _skills;
        private readonly List<RecordSource<EntityDefinition>> _entities;
        private readonly List<RecordSource<RaceDefinition>> _races;
        private readonly List<RecordSource<AilmentDefinition>> _ailments;
        private readonly List<RecordSource<ItemDefinition>> _items;
        private readonly List<RecordSource<EquipmentDefinition>> _equipment;
        private readonly List<RecordSource<ShopCatalogDefinition>> _shops;
        private readonly List<RecordSource<NegotiationDefinition>> _negotiations;
        private readonly List<RecordSource<EncounterDefinition>> _encounters;
        private readonly List<RecordSource<DungeonDefinition>> _dungeons;
        private readonly List<RecordSource<FusionRecipeDefinition>> _fusion;
        private readonly List<RecordSource<RulesetDefinition>> _rulesets;
        private readonly Dictionary<ContentId, List<RecordSource<SkillDefinition>>> _skillIndex;
        private readonly Dictionary<ContentId, List<RecordSource<EntityDefinition>>> _entityIndex;
        private readonly Dictionary<ContentId, List<RecordSource<RaceDefinition>>> _raceIndex;
        private readonly Dictionary<ContentId, List<RecordSource<AilmentDefinition>>> _ailmentIndex;
        private readonly Dictionary<ContentId, List<RecordSource<ItemDefinition>>> _itemIndex;
        private readonly Dictionary<ContentId, List<RecordSource<EquipmentDefinition>>> _equipmentIndex;
        private readonly Dictionary<ContentId, List<RecordSource<ShopCatalogDefinition>>> _shopIndex;
        private readonly Dictionary<ContentId, List<RecordSource<NegotiationDefinition>>> _negotiationIndex;
        private readonly Dictionary<ContentId, List<RecordSource<EncounterDefinition>>> _encounterIndex;
        private readonly Dictionary<ContentId, List<RecordSource<DungeonDefinition>>> _dungeonIndex;
        private readonly Dictionary<ContentId, List<RecordSource<FusionRecipeDefinition>>> _fusionIndex;
        private readonly Dictionary<ContentId, List<RecordSource<RulesetDefinition>>> _rulesetIndex;

        public ValidationContext(SkillSystemValidationRequest request)
        {
            _request = request;
            _packId = request.Manifest.Id;
            _registrations = request.Registrations;
            _skills = Flatten(request.SkillDocuments, "skill", "skills", definition => definition.Id);
            _entities = Flatten(request.EntityDocuments, "entity", "entities", definition => definition.Id);
            _races = Flatten(request.RaceDocuments, "race", "races", definition => definition.Id);
            _ailments = Flatten(request.AilmentDocuments, "ailment", "ailments", definition => definition.Id);
            _items = Flatten(request.ItemDocuments, "item", "items", definition => definition.Id);
            _equipment = Flatten(request.EquipmentDocuments, "equipment", "equipment", definition => definition.Id);
            _shops = Flatten(request.ShopDocuments, "shop", "shops", definition => definition.Id);
            _negotiations = Flatten(request.NegotiationDocuments, "negotiation", "negotiations", definition => definition.Id);
            _encounters = Flatten(request.EncounterDocuments, "encounter", "encounters", definition => definition.Id);
            _dungeons = Flatten(request.DungeonDocuments, "dungeon", "dungeons", definition => definition.Id);
            _fusion = Flatten(request.FusionDocuments, "fusion recipe", "fusionRecipes", definition => definition.Id);
            _rulesets = Flatten(request.RulesetDocuments, "ruleset", "rulesets", definition => definition.Id);
            _skillIndex = Index(_skills);
            _entityIndex = Index(_entities);
            _raceIndex = Index(_races);
            _ailmentIndex = Index(_ailments);
            _itemIndex = Index(_items);
            _equipmentIndex = Index(_equipment);
            _shopIndex = Index(_shops);
            _negotiationIndex = Index(_negotiations);
            _encounterIndex = Index(_encounters);
            _dungeonIndex = Index(_dungeons);
            _fusionIndex = Index(_fusion);
            _rulesetIndex = Index(_rulesets);
        }

        public List<ContentValidationError> Errors { get; } = [];

        public void ValidateDocuments()
        {
            if (_request.Manifest.SchemaVersion != SupportedSchemaVersion)
            {
                AddManifestError(
                    "$.schemaVersion",
                    ContentValidationErrorCode.DocumentSchemaVersionUnsupported,
                    $"Manifest schema version {_request.Manifest.SchemaVersion} is not supported.");
            }

            ValidateManifestDocuments();
            ValidateDocumentSet(_request.SkillDocuments, "skills");
            ValidateDocumentSet(_request.EntityDocuments, "entities");
            ValidateDocumentSet(_request.RaceDocuments, "races");
            ValidateDocumentSet(_request.AilmentDocuments, "ailments");
            ValidateDocumentSet(_request.ItemDocuments, "items");
            ValidateDocumentSet(_request.EquipmentDocuments, "equipment");
            ValidateDocumentSet(_request.ShopDocuments, "shops");
            ValidateDocumentSet(_request.NegotiationDocuments, "negotiations");
            ValidateDocumentSet(_request.EncounterDocuments, "encounters");
            ValidateDocumentSet(_request.DungeonDocuments, "dungeons");
            ValidateDocumentSet(_request.FusionDocuments, "fusion");
            ValidateDocumentSet(_request.RulesetDocuments, "rulesets");
        }

        public void ValidateRecords()
        {
            var processedSkills = new HashSet<RecordSource<SkillDefinition>>(ReferenceEqualityComparer.Instance);
            var processedEntities = new HashSet<RecordSource<EntityDefinition>>(ReferenceEqualityComparer.Instance);
            var processedRaces = new HashSet<RecordSource<RaceDefinition>>(ReferenceEqualityComparer.Instance);
            var processedAilments = new HashSet<RecordSource<AilmentDefinition>>(ReferenceEqualityComparer.Instance);
            var processedItems = new HashSet<RecordSource<ItemDefinition>>(ReferenceEqualityComparer.Instance);
            var processedEquipment = new HashSet<RecordSource<EquipmentDefinition>>(ReferenceEqualityComparer.Instance);
            var processedShops = new HashSet<RecordSource<ShopCatalogDefinition>>(ReferenceEqualityComparer.Instance);
            var processedNegotiations = new HashSet<RecordSource<NegotiationDefinition>>(ReferenceEqualityComparer.Instance);
            var processedEncounters = new HashSet<RecordSource<EncounterDefinition>>(ReferenceEqualityComparer.Instance);
            var processedDungeons = new HashSet<RecordSource<DungeonDefinition>>(ReferenceEqualityComparer.Instance);
            var processedFusion = new HashSet<RecordSource<FusionRecipeDefinition>>(ReferenceEqualityComparer.Instance);
            var processedRulesets = new HashSet<RecordSource<RulesetDefinition>>(ReferenceEqualityComparer.Instance);

            foreach (ContentPackDocumentReference document in _request.Manifest.Documents)
            {
                switch (document.Type)
                {
                    case "skills":
                        ValidateDocumentRecords(_skills, document.Path, _skillIndex, ValidateSkill, processedSkills);
                        break;
                    case "entities":
                        ValidateDocumentRecords(_entities, document.Path, _entityIndex, ValidateEntity, processedEntities);
                        break;
                    case "races":
                        ValidateDocumentRecords(_races, document.Path, _raceIndex, ValidateRace, processedRaces);
                        break;
                    case "ailments":
                        ValidateDocumentRecords(_ailments, document.Path, _ailmentIndex, ValidateAilment, processedAilments);
                        break;
                    case "items":
                        ValidateDocumentRecords(_items, document.Path, _itemIndex, ValidateItem, processedItems);
                        break;
                    case "equipment":
                        ValidateDocumentRecords(_equipment, document.Path, _equipmentIndex, ValidateEquipment, processedEquipment);
                        break;
                    case "shops":
                        ValidateDocumentRecords(_shops, document.Path, _shopIndex, ValidateShop, processedShops);
                        break;
                    case "negotiations":
                        ValidateDocumentRecords(_negotiations, document.Path, _negotiationIndex, ValidateNegotiation, processedNegotiations);
                        break;
                    case "encounters":
                        ValidateDocumentRecords(_encounters, document.Path, _encounterIndex, ValidateEncounter, processedEncounters);
                        break;
                    case "dungeons":
                        ValidateDocumentRecords(_dungeons, document.Path, _dungeonIndex, ValidateDungeon, processedDungeons);
                        break;
                    case "fusion":
                        ValidateDocumentRecords(_fusion, document.Path, _fusionIndex, ValidateFusionRecipe, processedFusion);
                        break;
                    case "rulesets":
                        ValidateDocumentRecords(_rulesets, document.Path, _rulesetIndex, ValidateRuleset, processedRulesets);
                        break;
                }
            }

            ValidateRemainingRecords(_skills, _skillIndex, ValidateSkill, processedSkills);
            ValidateRemainingRecords(_entities, _entityIndex, ValidateEntity, processedEntities);
            ValidateRemainingRecords(_races, _raceIndex, ValidateRace, processedRaces);
            ValidateRemainingRecords(_ailments, _ailmentIndex, ValidateAilment, processedAilments);
            ValidateRemainingRecords(_items, _itemIndex, ValidateItem, processedItems);
            ValidateRemainingRecords(_equipment, _equipmentIndex, ValidateEquipment, processedEquipment);
            ValidateRemainingRecords(_shops, _shopIndex, ValidateShop, processedShops);
            ValidateRemainingRecords(_negotiations, _negotiationIndex, ValidateNegotiation, processedNegotiations);
            ValidateRemainingRecords(_encounters, _encounterIndex, ValidateEncounter, processedEncounters);
            ValidateRemainingRecords(_dungeons, _dungeonIndex, ValidateDungeon, processedDungeons);
            ValidateRemainingRecords(_fusion, _fusionIndex, ValidateFusionRecipe, processedFusion);
            ValidateRemainingRecords(_rulesets, _rulesetIndex, ValidateRuleset, processedRulesets);

            ValidateFusionRecipeAmbiguities();
            ValidateMutationFamilies();
        }

        private void ValidateDocumentRecords<TDefinition>(
            IReadOnlyList<RecordSource<TDefinition>> records,
            string manifestPath,
            IReadOnlyDictionary<ContentId, List<RecordSource<TDefinition>>> index,
            Action<RecordSource<TDefinition>> validate,
            ISet<RecordSource<TDefinition>> processed)
        {
            foreach (RecordSource<TDefinition> source in records.Where(source => source.ManifestPath == manifestPath))
            {
                ValidateRecord(source, index, validate, processed);
            }
        }

        private void ValidateRemainingRecords<TDefinition>(
            IReadOnlyList<RecordSource<TDefinition>> records,
            IReadOnlyDictionary<ContentId, List<RecordSource<TDefinition>>> index,
            Action<RecordSource<TDefinition>> validate,
            ISet<RecordSource<TDefinition>> processed)
        {
            foreach (RecordSource<TDefinition> source in records)
            {
                ValidateRecord(source, index, validate, processed);
            }
        }

        private void ValidateRecord<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyDictionary<ContentId, List<RecordSource<TDefinition>>> index,
            Action<RecordSource<TDefinition>> validate,
            ISet<RecordSource<TDefinition>> processed)
        {
            if (!processed.Add(source))
            {
                return;
            }

            ValidateRecordIdentity(source, index);
            validate(source);
        }

        private void ValidateManifestDocuments()
        {
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < _request.Manifest.Documents.Count; index++)
            {
                ContentPackDocumentReference document = _request.Manifest.Documents[index];
                if (!seenPaths.Add(document.Path))
                {
                    AddManifestError(
                        $"$.documents[{index}].path",
                        ContentValidationErrorCode.DocumentDuplicatePath,
                        $"Manifest path '{document.Path}' is declared more than once.");
                }
            }

            ValidateManifestCoverage("skills", _request.SkillDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("entities", _request.EntityDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("races", _request.RaceDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("ailments", _request.AilmentDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("items", _request.ItemDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("equipment", _request.EquipmentDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("shops", _request.ShopDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("negotiations", _request.NegotiationDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("encounters", _request.EncounterDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("dungeons", _request.DungeonDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("fusion", _request.FusionDocuments.Select(document => document.ManifestPath));
            ValidateManifestCoverage("rulesets", _request.RulesetDocuments.Select(document => document.ManifestPath));
        }

        private void ValidateManifestCoverage(string documentType, IEnumerable<string> suppliedPaths)
        {
            var supplied = suppliedPaths.ToHashSet(StringComparer.Ordinal);
            for (int index = 0; index < _request.Manifest.Documents.Count; index++)
            {
                ContentPackDocumentReference reference = _request.Manifest.Documents[index];
                if (reference.Type == documentType && !supplied.Contains(reference.Path))
                {
                    AddManifestError(
                        $"$.documents[{index}].path",
                        ContentValidationErrorCode.DocumentMissing,
                        $"Manifest document '{reference.Path}' was not supplied for validation.");
                }
            }
        }

        private void ValidateDocumentSet<TDefinition>(
            IReadOnlyList<SourceContentDocument<TDefinition>> documents,
            string expectedType)
        {
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceContentDocument<TDefinition> source in documents)
            {
                if (!seenPaths.Add(source.ManifestPath))
                {
                    AddDocumentError(
                        source.SourceName,
                        "$",
                        ContentValidationErrorCode.DocumentDuplicatePath,
                        $"Document path '{source.ManifestPath}' was supplied more than once.");
                }

                List<(ContentPackDocumentReference Reference, int Index)> declarations = _request.Manifest.Documents
                    .Select((reference, index) => (reference, index))
                    .Where(item => item.reference.Path == source.ManifestPath)
                    .Select(item => (item.reference, item.index))
                    .ToList();

                if (declarations.Count == 0)
                {
                    AddDocumentError(
                        source.SourceName,
                        "$",
                        ContentValidationErrorCode.DocumentNotDeclared,
                        $"Document path '{source.ManifestPath}' is not declared by the manifest.");
                }
                else if (declarations.All(item => item.Reference.Type != expectedType))
                {
                    AddDocumentError(
                        source.SourceName,
                        "$",
                        ContentValidationErrorCode.DocumentTypeMismatch,
                        $"Document path '{source.ManifestPath}' is not declared as type '{expectedType}'.");
                }

                if (source.Document.SchemaVersion != SupportedSchemaVersion)
                {
                    AddDocumentError(
                        source.SourceName,
                        "$.schemaVersion",
                        ContentValidationErrorCode.DocumentSchemaVersionUnsupported,
                        $"Document schema version {source.Document.SchemaVersion} is not supported.");
                }
            }
        }

        private void ValidateRecordIdentity<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyDictionary<ContentId, List<RecordSource<TDefinition>>> index)
        {
            if (!source.Id.IsValid)
            {
                Add(
                    source,
                    source.Path + ".id",
                    ContentValidationErrorCode.RecordIdInvalid,
                    $"{Capitalize(source.RecordType)} ID cannot be empty.");
                return;
            }

            if (source.Id.IsQualified)
            {
                Add(
                    source,
                    source.Path + ".id",
                    ContentValidationErrorCode.RecordIdMustBeLocal,
                    $"{Capitalize(source.RecordType)} ID '{source.Id}' must be local and unqualified.");
            }

            if (index[source.Id].Count > 1)
            {
                Add(
                    source,
                    source.Path + ".id",
                    ContentValidationErrorCode.RecordDuplicateId,
                    $"{Capitalize(source.RecordType)} ID '{source.Id}' is declared more than once.");
            }
        }

        private void ValidateSkill(RecordSource<SkillDefinition> source)
        {
            SkillDefinition skill = source.Definition;
            if (skill.Activation == SkillActivation.Active)
            {
                if (skill.MenuGroup is null)
                {
                    Add(source, source.Path + ".menuGroup", ContentValidationErrorCode.SkillActiveMenuGroupRequired,
                        "Active skills require a menu group.");
                }

                if (skill.Effects.Count == 0)
                {
                    Add(source, source.Path + ".effects", ContentValidationErrorCode.SkillActiveEffectsRequired,
                        "Active skills require at least one effect.");
                }

                if (skill.Availability is null || skill.Availability.ContextIds.Count == 0)
                {
                    Add(source, source.Path + ".availability.contexts",
                        ContentValidationErrorCode.SkillActiveAvailabilityRequired,
                        "Active skills require at least one availability context.");
                }

                if (skill.Triggers.Count > 0 || skill.Modifiers.Count > 0)
                {
                    Add(source, source.Path, ContentValidationErrorCode.SkillActivePassiveMembersForbidden,
                        "Active skills cannot declare passive triggers or modifiers.");
                }
            }
            else if (skill.Activation == SkillActivation.Passive)
            {
                if (skill.MenuGroup is not null)
                {
                    Add(source, source.Path + ".menuGroup", ContentValidationErrorCode.SkillPassiveMenuGroupForbidden,
                        "Passive skills cannot declare a menu group.");
                }

                if (skill.Availability is not null)
                {
                    Add(source, source.Path + ".availability",
                        ContentValidationErrorCode.SkillPassiveAvailabilityForbidden,
                        "Passive skills cannot declare availability.");
                }

                if (skill.Targeting is not null || skill.Costs.Count > 0 || skill.Effects.Count > 0)
                {
                    Add(source, source.Path, ContentValidationErrorCode.SkillPassiveActiveMembersForbidden,
                        "Passive skills cannot declare targeting, costs, or active effects.");
                }

                if (skill.InheritanceGroup != InheritanceGroup.Passive)
                {
                    Add(source, source.Path + ".inheritanceGroupId",
                        ContentValidationErrorCode.SkillPassiveInheritanceGroupRequired,
                        "Passive skills must use the passive inheritance group.");
                }

                if (skill.Triggers.Count == 0 && skill.Modifiers.Count == 0)
                {
                    Add(source, source.Path, ContentValidationErrorCode.SkillPassiveBehaviorRequired,
                        "Passive skills require at least one trigger or modifier.");
                }
            }

            ValidateContentReferenceDuplicates(source, skill.Inheritance.ExclusiveOwnerEntityIds,
                source.Path + ".inheritance.exclusiveOwnerEntityIds");
            for (int index = 0; index < skill.Inheritance.ExclusiveOwnerEntityIds.Count; index++)
            {
                ValidateContentReference(source, skill.Inheritance.ExclusiveOwnerEntityIds[index],
                    source.Path + $".inheritance.exclusiveOwnerEntityIds[{index}]", _entityIndex, "entity");
            }

            if (skill.Availability is not null)
            {
                ValidateDuplicates(source, skill.Availability.ContextIds, source.Path + ".availability.contexts");
                for (int index = 0; index < skill.Availability.ContextIds.Count; index++)
                {
                    RequireRegistration(source, skill.Availability.ContextIds[index],
                        source.Path + $".availability.contexts[{index}]", _registrations.ContextIds, "context");
                }
            }

            for (int index = 0; index < skill.Costs.Count; index++)
            {
                SkillCostDefinition cost = skill.Costs[index];
                string path = source.Path + $".costs[{index}]";
                RequireRegistration(source, cost.ResourceId, path + ".resourceId", _registrations.ResourceIds, "resource");
                ValidateAmount(source, cost.Amount, path + ".amount");
            }

            if (skill.Targeting is not null)
            {
                ValidateTargeting(source, skill.Targeting, source.Path + ".targeting");
            }

            ValidateEffects(source, skill.Effects, source.Path + ".effects");
            ValidateTriggers(source, skill.Triggers, source.Path + ".triggers");
            ValidateModifiers(source, skill.Modifiers, source.Path + ".modifiers");

            if (skill.Mutation is not null && !skill.Mutation.FamilyId.IsValid)
            {
                Add(source, source.Path + ".mutation.familyId", ContentValidationErrorCode.ReferenceIdInvalid,
                    "Mutation family ID cannot be empty.");
            }
        }

        private void ValidateEntity(RecordSource<EntityDefinition> source)
        {
            EntityDefinition entity = source.Definition;
            RequirePositive(source, entity.Rank, source.Path + ".rank", "Entity rank");
            RequirePositive(source, entity.BaseLevel, source.Path + ".baseLevel", "Entity base level");
            RequireRegistration(source, entity.EntityKindId, source.Path + ".entityKind",
                _registrations.EntityKindIds, "entity kind");
            ValidateContentReference(source, entity.RaceId, source.Path + ".raceId", _raceIndex, "race");

            foreach ((ContentId statId, int _) in entity.Stats)
            {
                RequireRegistration(source, statId, source.Path + $".stats.{statId}", _registrations.StatIds, "stat");
            }

            foreach ((DamageElement element, _) in entity.ElementalAffinities)
            {
                if (element == DamageElement.Almighty)
                {
                    Add(source, source.Path + ".elementalAffinities.almighty",
                        ContentValidationErrorCode.AlmightyAffinityForbidden,
                        "Almighty cannot have an authored elemental affinity.");
                }
            }

            foreach ((ContentId ailmentId, _) in entity.AilmentResistances)
            {
                ValidateContentReference(source, ailmentId, source.Path + $".ailmentResistances.{ailmentId}",
                    _ailmentIndex, "ailment");
            }

            ValidateDuplicates(source, entity.InheritanceRules.GroupPolicy.GroupIds,
                source.Path + ".inheritanceRules.groupPolicy.groupIds");
            ValidateContentReferenceDuplicates(source, entity.InheritanceRules.BlockedSkillIds,
                source.Path + ".inheritanceRules.blockedSkillIds");
            ValidateContentReferenceDuplicates(source, entity.InheritanceRules.AllowedSkillIds,
                source.Path + ".inheritanceRules.allowedSkillIds");

            var blocked = entity.InheritanceRules.BlockedSkillIds
                .Where(id => id.IsValid)
                .Select(NormalizeContentReference)
                .ToHashSet();
            for (int index = 0; index < entity.InheritanceRules.AllowedSkillIds.Count; index++)
            {
                ContentId skillId = entity.InheritanceRules.AllowedSkillIds[index];
                string path = source.Path + $".inheritanceRules.allowedSkillIds[{index}]";
                if (skillId.IsValid && blocked.Contains(NormalizeContentReference(skillId)))
                {
                    Add(source, path, ContentValidationErrorCode.InheritanceListConflict,
                        $"Skill '{skillId}' appears in both allowed and blocked inheritance lists.");
                }

                RecordSource<SkillDefinition>? allowedSkill = ValidateContentReference(
                    source, skillId, path, _skillIndex, "skill");
                if (allowedSkill is not null &&
                    (!allowedSkill.Definition.Inheritance.IsInheritable ||
                     (allowedSkill.Definition.Inheritance.ExclusiveOwnerEntityIds.Count > 0 &&
                      !allowedSkill.Definition.Inheritance.ExclusiveOwnerEntityIds
                          .Select(NormalizeContentReference)
                          .Contains(entity.Id))))
                {
                    Add(source, path, ContentValidationErrorCode.InheritanceExplicitAllowInvalid,
                        $"Skill '{skillId}' cannot be explicitly allowed because it is non-inheritable or owner-exclusive.");
                }
            }

            for (int index = 0; index < entity.InheritanceRules.BlockedSkillIds.Count; index++)
            {
                ValidateContentReference(source, entity.InheritanceRules.BlockedSkillIds[index],
                    source.Path + $".inheritanceRules.blockedSkillIds[{index}]", _skillIndex, "skill");
            }

            var assignedSkills = new HashSet<ContentId>();
            for (int index = 0; index < entity.BaseSkillIds.Count; index++)
            {
                ContentId skillId = entity.BaseSkillIds[index];
                string path = source.Path + $".baseSkillIds[{index}]";
                if (skillId.IsValid && !assignedSkills.Add(NormalizeContentReference(skillId)))
                {
                    Add(source, path, ContentValidationErrorCode.EntitySkillAssignmentDuplicate,
                        $"Skill '{skillId}' is assigned to the entity more than once.");
                }

                ValidateContentReference(source, skillId, path, _skillIndex, "skill");
            }

            for (int index = 0; index < entity.SkillUnlocks.Count; index++)
            {
                SkillUnlockDefinition unlock = entity.SkillUnlocks[index];
                string path = source.Path + $".skillUnlocks[{index}]";
                if (unlock.Level <= entity.BaseLevel)
                {
                    Add(source, path + ".level", ContentValidationErrorCode.EntityUnlockLevelInvalid,
                        $"Skill unlock level {unlock.Level} must be greater than base level {entity.BaseLevel}.");
                }

                if (unlock.SkillId.IsValid && !assignedSkills.Add(NormalizeContentReference(unlock.SkillId)))
                {
                    Add(source, path + ".skillId", ContentValidationErrorCode.EntitySkillAssignmentDuplicate,
                        $"Skill '{unlock.SkillId}' is assigned to the entity more than once.");
                }

                ValidateContentReference(source, unlock.SkillId, path + ".skillId", _skillIndex, "skill");
            }
        }

        private void ValidateRace(RecordSource<RaceDefinition> source)
        {
            RaceDefinition race = source.Definition;
            ValidateDuplicates(source, race.AlignmentIds, source.Path + ".alignmentIds");
            for (int index = 0; index < race.AlignmentIds.Count; index++)
            {
                RequireRegistration(source, race.AlignmentIds[index], source.Path + $".alignmentIds[{index}]",
                    _registrations.AlignmentIds, "alignment");
            }

            if (race.NegotiationPersonalityId is ContentId personalityId)
            {
                RequireRegistration(source, personalityId, source.Path + ".negotiationPersonalityId",
                    _registrations.NegotiationPersonalityIds, "negotiation personality");
            }
        }

        private void ValidateAilment(RecordSource<AilmentDefinition> source)
        {
            AilmentDefinition ailment = source.Definition;
            ValidateDuration(source, ailment.DefaultDuration, source.Path + ".defaultDuration");
            ValidateAilmentBehavior(source, ailment.TurnBehavior, source.Path + ".turnBehavior");

            ValidateDuplicates(source, ailment.GroupIds, source.Path + ".groupIds");
            for (int index = 0; index < ailment.GroupIds.Count; index++)
            {
                RequireRegistration(source, ailment.GroupIds[index], source.Path + $".groupIds[{index}]",
                    _registrations.AilmentGroupIds, "ailment group");
            }

            if (ailment.ExclusivityGroupId is ContentId exclusivityGroupId)
            {
                RequireRegistration(source, exclusivityGroupId, source.Path + ".exclusivityGroupId",
                    _registrations.AilmentGroupIds, "ailment group");
            }

            RequireNonNegative(source, ailment.Modifiers.EvasionMultiplier, source.Path + ".modifiers.evasionMultiplier",
                "Ailment evasion multiplier");
            RequirePositive(source, ailment.Modifiers.DamageTakenMultiplier,
                source.Path + ".modifiers.damageTakenMultiplier", "Ailment damage-taken multiplier");
            RequirePositive(source, ailment.Modifiers.DamageDealtMultiplier,
                source.Path + ".modifiers.damageDealtMultiplier", "Ailment damage-dealt multiplier");

            ValidateTriggers(source, ailment.Triggers, source.Path + ".triggers");

            if (ailment.Recovery.Natural is NaturalAilmentRecoveryDefinition natural)
            {
                RequirePercentage(source, natural.BaseChance, source.Path + ".recovery.natural.baseChance",
                    "Natural recovery chance");
                RequireRegistration(source, natural.StatId, source.Path + ".recovery.natural.statId",
                    _registrations.StatIds, "stat");
                RequirePositive(source, natural.StatMultiplier, source.Path + ".recovery.natural.statMultiplier",
                    "Natural recovery stat multiplier");
            }

            ValidateDuplicates(source, ailment.Recovery.RemoveOnEventIds, source.Path + ".recovery.removeOnEvents");
            for (int index = 0; index < ailment.Recovery.RemoveOnEventIds.Count; index++)
            {
                RequireRegistration(source, ailment.Recovery.RemoveOnEventIds[index],
                    source.Path + $".recovery.removeOnEvents[{index}]", _registrations.EventIds, "event");
            }
        }

        private void ValidateItem(RecordSource<ItemDefinition> source)
        {
            ItemDefinition item = source.Definition;
            RequirePositive(source, item.StackLimit, source.Path + ".stackLimit", "Item stack limit");
            RequireNonNegative(source, item.BaseValue, source.Path + ".baseValue", "Item base value");

            if (item.ItemKind == ItemKind.Consumable && item.Usage is null)
            {
                Add(source, source.Path + ".usage", ContentValidationErrorCode.ShapeInvalid,
                    "Consumable items require usage.");
                return;
            }

            if (item.ItemKind != ItemKind.Consumable && item.Usage is not null)
            {
                Add(source, source.Path + ".usage", ContentValidationErrorCode.ShapeInvalid,
                    "Only consumable items may declare usage.");
                return;
            }

            if (item.Usage is null)
            {
                return;
            }

            ItemUsageDefinition usage = item.Usage;
            if (usage.ContextIds.Count == 0)
            {
                Add(source, source.Path + ".usage.contexts", ContentValidationErrorCode.ShapeInvalid,
                    "Consumable item usage requires at least one context.");
            }
            ValidateDuplicates(source, usage.ContextIds, source.Path + ".usage.contexts");
            for (int index = 0; index < usage.ContextIds.Count; index++)
            {
                RequireRegistration(source, usage.ContextIds[index],
                    source.Path + $".usage.contexts[{index}]", _registrations.ContextIds, "context");
            }

            ValidateTargeting(source, usage.Targeting, source.Path + ".usage.targeting");
            if (usage.Effects.Count == 0)
            {
                Add(source, source.Path + ".usage.effects", ContentValidationErrorCode.ShapeInvalid,
                    "Consumable item usage requires at least one effect.");
            }
            ValidateEffects(source, usage.Effects, source.Path + ".usage.effects");
        }

        private void ValidateEquipment(RecordSource<EquipmentDefinition> source)
        {
            EquipmentDefinition equipment = source.Definition;
            RequireNonNegative(source, equipment.BaseValue, source.Path + ".baseValue", "Equipment base value");
            ValidateContentReferenceDuplicates(source, equipment.GrantedSkillIds, source.Path + ".grantedSkillIds");
            for (int index = 0; index < equipment.GrantedSkillIds.Count; index++)
            {
                ValidateContentReference(source, equipment.GrantedSkillIds[index],
                    source.Path + $".grantedSkillIds[{index}]", _skillIndex, "skill");
            }

            int profileCount =
                (equipment.Weapon is null ? 0 : 1) +
                (equipment.Armor is null ? 0 : 1) +
                (equipment.Boots is null ? 0 : 1) +
                (equipment.Accessory is null ? 0 : 1);
            if (profileCount != 1)
            {
                Add(source, source.Path, ContentValidationErrorCode.ShapeInvalid,
                    "Equipment records require exactly one slot profile.");
            }

            if ((equipment.Slot == EquipmentSlot.Weapon && equipment.Weapon is null) ||
                (equipment.Slot == EquipmentSlot.Armor && equipment.Armor is null) ||
                (equipment.Slot == EquipmentSlot.Boots && equipment.Boots is null) ||
                (equipment.Slot == EquipmentSlot.Accessory && equipment.Accessory is null))
            {
                Add(source, source.Path, ContentValidationErrorCode.ShapeInvalid,
                    "Equipment slot must match its declared profile.");
            }

            if (equipment.Weapon is EquipmentWeaponProfileDefinition weapon)
            {
                RequireNonNegative(source, weapon.BasicAttack.Power, source.Path + ".weapon.basicAttack.power",
                    "Weapon power");
                RequirePercentage(source, weapon.BasicAttack.Accuracy, source.Path + ".weapon.basicAttack.accuracy",
                    "Weapon accuracy");
            }

            if (equipment.Armor is EquipmentArmorProfileDefinition armor)
            {
                RequireNonNegative(source, armor.Defense, source.Path + ".armor.defense", "Armor defense");
                RequireNonNegative(source, armor.Evasion, source.Path + ".armor.evasion", "Armor evasion");
            }

            if (equipment.Boots is EquipmentBootsProfileDefinition boots)
            {
                RequireNonNegative(source, boots.Evasion, source.Path + ".boots.evasion", "Boot evasion");
            }

            if (equipment.Accessory is EquipmentAccessoryProfileDefinition accessory)
            {
                ValidateStatModifiers(source, accessory.StatModifiers, source.Path + ".accessory.statModifiers");
            }
        }

        private void ValidateShop(RecordSource<ShopCatalogDefinition> source)
        {
            ShopCatalogDefinition shop = source.Definition;
            RequireRegistration(source, shop.CategoryId, source.Path + ".categoryId",
                _registrations.ShopCategoryIds, "shop category");
            ValidateRegisteredList(source, shop.AvailabilityContextIds, source.Path + ".availabilityContexts",
                _registrations.ContextIds, "context");
            if (shop.Offers.Count == 0)
            {
                Add(source, source.Path + ".offers", ContentValidationErrorCode.ShapeInvalid,
                    "Shop catalogs require at least one offer.");
            }

            for (int index = 0; index < shop.Offers.Count; index++)
            {
                ShopOfferDefinition offer = shop.Offers[index];
                string path = source.Path + $".offers[{index}]";
                if (offer.ContentKind == ShopContentKind.Item)
                {
                    ValidateContentReference(source, offer.ContentId, path + ".contentId", _itemIndex, "item");
                }
                else
                {
                    ValidateContentReference(source, offer.ContentId, path + ".contentId",
                        _equipmentIndex, "equipment");
                }

                ValidateShopPrice(source, offer.Price, path + ".price");
                ValidateShopStock(source, offer.Stock, path + ".stock");
            }
        }

        private void ValidateNegotiation(RecordSource<NegotiationDefinition> source)
        {
            NegotiationDefinition negotiation = source.Definition;
            RequireRegistration(source, negotiation.PersonalityId, source.Path + ".personalityId",
                _registrations.NegotiationPersonalityIds, "negotiation personality");
            if (negotiation.Questions.Count == 0)
            {
                Add(source, source.Path + ".questions", ContentValidationErrorCode.ShapeInvalid,
                    "Negotiation definitions require at least one question.");
            }

            long possiblePositiveMood = 0;
            long possibleNegativeMood = 0;
            bool moodRangeExceeded = false;
            for (int questionIndex = 0; questionIndex < negotiation.Questions.Count; questionIndex++)
            {
                NegotiationQuestionDefinition question = negotiation.Questions[questionIndex];
                if (question.Answers.Count == 0)
                {
                    Add(source, source.Path + $".questions[{questionIndex}].answers",
                        ContentValidationErrorCode.ShapeInvalid,
                        "Negotiation questions require at least one answer.");
                    continue;
                }

                int largestAdjustment = question.Answers.Max(answer => answer.Score);
                if (largestAdjustment > 0 &&
                    possiblePositiveMood > NegotiationNumericDomain.MaximumMoodScore - (long)largestAdjustment)
                {
                    moodRangeExceeded = true;
                }
                else if (largestAdjustment > 0)
                {
                    possiblePositiveMood += largestAdjustment;
                }

                int smallestAdjustment = question.Answers.Min(answer => answer.Score);
                if (smallestAdjustment < 0 &&
                    possibleNegativeMood < NegotiationNumericDomain.MinimumMoodScore - (long)smallestAdjustment)
                {
                    moodRangeExceeded = true;
                }
                else if (smallestAdjustment < 0)
                {
                    possibleNegativeMood += smallestAdjustment;
                }
            }

            if (moodRangeExceeded)
            {
                Add(source, source.Path + ".questions", ContentValidationErrorCode.ValueOutOfRange,
                    $"The possible authored mood-score aggregate must remain between " +
                    $"{NegotiationNumericDomain.MinimumMoodScore} and {NegotiationNumericDomain.MaximumMoodScore}.",
                    "Reduce answer-score magnitudes or split the negotiation into separate definitions.");
            }

            long totalDemandWeight = 0;
            bool demandWeightRangeExceeded = false;
            for (int demandIndex = 0; demandIndex < negotiation.Demands.Count; demandIndex++)
            {
                NegotiationDemandDefinition demand = negotiation.Demands[demandIndex];
                string path = source.Path + $".demands[{demandIndex}]";
                RequireRegistration(source, demand.DemandId, path + ".demandId",
                    _registrations.NegotiationDemandIds, "negotiation demand");
                RequirePositive(source, demand.Weight, path + ".weight", "Negotiation demand weight");
                if (demand.Weight > 0 &&
                    totalDemandWeight > NegotiationNumericDomain.MaximumDemandWeightTotal - (long)demand.Weight)
                {
                    demandWeightRangeExceeded = true;
                }
                else if (demand.Weight > 0)
                {
                    totalDemandWeight += demand.Weight;
                }
            }

            if (demandWeightRangeExceeded)
            {
                Add(source, source.Path + ".demands", ContentValidationErrorCode.ValueOutOfRange,
                    $"The aggregate negotiation demand weight cannot exceed " +
                    $"{NegotiationNumericDomain.MaximumDemandWeightTotal}.",
                    "Reduce demand weights while preserving their intended relative proportions.");
            }

            ValidateContentReferenceDuplicates(source, negotiation.DefaultRaceIds, source.Path + ".defaultRaceIds");
            for (int index = 0; index < negotiation.DefaultRaceIds.Count; index++)
            {
                ValidateContentReference(source, negotiation.DefaultRaceIds[index],
                    source.Path + $".defaultRaceIds[{index}]", _raceIndex, "race");
            }

            ValidateContentReferenceDuplicates(source, negotiation.DefaultEntityIds, source.Path + ".defaultEntityIds");
            for (int index = 0; index < negotiation.DefaultEntityIds.Count; index++)
            {
                ValidateContentReference(source, negotiation.DefaultEntityIds[index],
                    source.Path + $".defaultEntityIds[{index}]", _entityIndex, "entity");
            }
        }

        private void ValidateEncounter(RecordSource<EncounterDefinition> source)
        {
            EncounterDefinition encounter = source.Definition;
            if (encounter.EnvironmentId is ContentId environmentId)
            {
                RequireRegistration(source, environmentId, source.Path + ".environmentId",
                    _registrations.EncounterEnvironmentIds, "encounter environment");
            }

            if (encounter.Formations.Count == 0)
            {
                Add(source, source.Path + ".formations", ContentValidationErrorCode.ShapeInvalid,
                    "Encounters require at least one formation.");
            }

            for (int formationIndex = 0; formationIndex < encounter.Formations.Count; formationIndex++)
            {
                EncounterFormationDefinition formation = encounter.Formations[formationIndex];
                string path = source.Path + $".formations[{formationIndex}]";
                RequirePositive(source, formation.Weight, path + ".weight", "Encounter formation weight");
                if (formation.Members.Count == 0)
                {
                    Add(source, path + ".members", ContentValidationErrorCode.ShapeInvalid,
                        "Encounter formations require at least one member.");
                }

                if (formation.RewardPolicyId is ContentId rewardPolicyId)
                {
                    RequireRegistration(source, rewardPolicyId, path + ".rewardPolicyId",
                        _registrations.PolicyIds, "policy");
                }

                for (int memberIndex = 0; memberIndex < formation.Members.Count; memberIndex++)
                {
                    EncounterMemberDefinition member = formation.Members[memberIndex];
                    string memberPath = path + $".members[{memberIndex}]";
                    ValidateContentReference(source, member.EntityId, memberPath + ".entityId", _entityIndex, "entity");
                    RequirePositive(source, member.Level, memberPath + ".level", "Encounter member level");
                    RequirePositive(source, member.Count, memberPath + ".count", "Encounter member count");
                }
            }
        }

        private void ValidateDungeon(RecordSource<DungeonDefinition> source)
        {
            DungeonDefinition dungeon = source.Definition;
            if (dungeon.Blocks.Count == 0)
            {
                Add(source, source.Path + ".blocks", ContentValidationErrorCode.ShapeInvalid,
                    "Dungeons require at least one block.");
            }

            ValidateDuplicates(source, dungeon.Blocks.Select(block => block.Id).ToArray(), source.Path + ".blocks.id");
            for (int blockIndex = 0; blockIndex < dungeon.Blocks.Count; blockIndex++)
            {
                DungeonBlockDefinition block = dungeon.Blocks[blockIndex];
                string blockPath = source.Path + $".blocks[{blockIndex}]";
                if (!block.Id.IsValid)
                {
                    Add(source, blockPath + ".id", ContentValidationErrorCode.RecordIdInvalid,
                        "Dungeon block ID cannot be empty.");
                }
                else if (block.Id.IsQualified)
                {
                    Add(source, blockPath + ".id", ContentValidationErrorCode.RecordIdMustBeLocal,
                        $"Dungeon block ID '{block.Id}' must be local and unqualified.");
                }
                RequirePositive(source, block.StartFloor, blockPath + ".startFloor", "Dungeon block start floor");
                RequirePositive(source, block.EndFloor, blockPath + ".endFloor", "Dungeon block end floor");
                if (block.StartFloor > block.EndFloor)
                {
                    Add(source, blockPath, ContentValidationErrorCode.MinimumExceedsMaximum,
                        "Dungeon block start floor cannot exceed end floor.");
                }

                ValidateContentReferenceDuplicates(source, block.EncounterPoolIds, blockPath + ".encounterPoolIds");
                for (int index = 0; index < block.EncounterPoolIds.Count; index++)
                {
                    ValidateContentReference(source, block.EncounterPoolIds[index],
                        blockPath + $".encounterPoolIds[{index}]", _encounterIndex, "encounter");
                }

                for (int floorIndex = 0; floorIndex < block.FixedFloors.Count; floorIndex++)
                {
                    ValidateFixedFloor(source, block, block.FixedFloors[floorIndex],
                        blockPath + $".fixedFloors[{floorIndex}]");
                }
            }
        }

        private void ValidateFusionRecipe(RecordSource<FusionRecipeDefinition> source)
        {
            FusionRecipeDefinition recipe = source.Definition;
            if (recipe.Parents.Count != 2)
            {
                Add(source, source.Path + ".parents", ContentValidationErrorCode.ShapeInvalid,
                    "Schema v1 fusion recipes require exactly two parents.");
            }

            var seenParents = new HashSet<(FusionParentSelectorKind Kind, ContentId Id)>();
            for (int index = 0; index < recipe.Parents.Count; index++)
            {
                FusionParentSelectorDefinition parent = recipe.Parents[index];
                string path = source.Path + $".parents[{index}]";
                if (parent.Id.IsValid &&
                    !seenParents.Add((parent.Kind, NormalizeContentReference(parent.Id))))
                {
                    Add(source, path + ".id", ContentValidationErrorCode.ListDuplicateValue,
                        $"Fusion parent '{parent.Id}' is listed more than once.");
                }

                if (parent.Kind == FusionParentSelectorKind.Entity)
                {
                    ValidateContentReference(source, parent.Id, path + ".id", _entityIndex, "entity");
                }
                else
                {
                    ValidateContentReference(source, parent.Id, path + ".id", _raceIndex, "race");
                }
            }

            ValidateFusionResult(source, recipe.Result, source.Path + ".result");
            if (recipe.AccidentPolicyId is ContentId accidentPolicyId)
            {
                RequireRegistration(source, accidentPolicyId, source.Path + ".accidentPolicyId",
                    _registrations.PolicyIds, "policy");
            }
            if (recipe.MutationPolicyId is ContentId mutationPolicyId)
            {
                RequireRegistration(source, mutationPolicyId, source.Path + ".mutationPolicyId",
                    _registrations.PolicyIds, "policy");
            }
        }

        private void ValidateFusionRecipeAmbiguities()
        {
            for (int recipeIndex = 0; recipeIndex < _fusion.Count; recipeIndex++)
            {
                RecordSource<FusionRecipeDefinition> recipe = _fusion[recipeIndex];
                if (recipe.Definition.Parents.Count != 2)
                {
                    continue;
                }

                for (int previousIndex = 0; previousIndex < recipeIndex; previousIndex++)
                {
                    RecordSource<FusionRecipeDefinition> previous = _fusion[previousIndex];
                    if (previous.Definition.Parents.Count != 2 ||
                        FusionRecipeSpecificity(previous.Definition) != FusionRecipeSpecificity(recipe.Definition) ||
                        !FusionRecipesOverlap(previous.Definition, recipe.Definition))
                    {
                        continue;
                    }

                    Add(
                        recipe,
                        recipe.Path + ".parents",
                        ContentValidationErrorCode.FusionRecipeAmbiguous,
                        $"Fusion recipe '{recipe.Id}' overlaps equal-specificity recipe '{previous.Id}'.",
                        "Make the parent selectors non-overlapping; schema v1 has no recipe-priority field.");
                    break;
                }
            }
        }

        private bool FusionRecipesOverlap(
            FusionRecipeDefinition first,
            FusionRecipeDefinition second) =>
            (SelectorsIntersect(first.Parents[0], second.Parents[0]) &&
             SelectorsIntersect(first.Parents[1], second.Parents[1])) ||
            (SelectorsIntersect(first.Parents[0], second.Parents[1]) &&
             SelectorsIntersect(first.Parents[1], second.Parents[0]));

        private bool SelectorsIntersect(
            FusionParentSelectorDefinition first,
            FusionParentSelectorDefinition second)
        {
            if (!first.Id.IsValid || !second.Id.IsValid)
            {
                return false;
            }

            if (first.Kind == second.Kind)
            {
                return NormalizeContentReference(first.Id) == NormalizeContentReference(second.Id);
            }

            FusionParentSelectorDefinition entitySelector = first.Kind == FusionParentSelectorKind.Entity
                ? first
                : second;
            FusionParentSelectorDefinition raceSelector = first.Kind == FusionParentSelectorKind.Race
                ? first
                : second;
            if (!TryLocalReference(entitySelector.Id, out ContentId entityId) ||
                !_entityIndex.TryGetValue(entityId, out List<RecordSource<EntityDefinition>>? entities) ||
                entities.Count != 1)
            {
                // Cross-pack entity/race overlap is rechecked by the runtime resolver after qualification.
                return false;
            }

            return NormalizeContentReference(entities[0].Definition.RaceId) ==
                   NormalizeContentReference(raceSelector.Id);
        }

        private static int FusionRecipeSpecificity(FusionRecipeDefinition recipe) =>
            recipe.Parents.Count(parent => parent.Kind == FusionParentSelectorKind.Entity);

        private void ValidateRuleset(RecordSource<RulesetDefinition> source)
        {
            RequireRegistration(source, source.Definition.PolicyId, source.Path + ".policyId",
                _registrations.PolicyIds, "policy");
        }

        private void ValidateStatModifiers<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyList<StatModifierDefinition> modifiers,
            string path)
        {
            ValidateDuplicates(source, modifiers.Select(modifier => modifier.StatId).ToArray(), path + ".statId");
            for (int index = 0; index < modifiers.Count; index++)
            {
                StatModifierDefinition modifier = modifiers[index];
                string modifierPath = path + $"[{index}]";
                RequireRegistration(source, modifier.StatId, modifierPath + ".statId",
                    _registrations.StatIds, "stat");
                if (modifier.Value == 0)
                {
                    Add(source, modifierPath + ".value", ContentValidationErrorCode.ShapeInvalid,
                        "Stat modifiers cannot be zero.");
                }
            }
        }

        private void ValidateShopPrice<TDefinition>(
            RecordSource<TDefinition> source,
            ShopPriceDefinition price,
            string path)
        {
            switch (price)
            {
                case FixedShopPriceDefinition fixedPrice:
                    RequireNonNegative(source, fixedPrice.BasePrice, path + ".basePrice", "Shop price");
                    break;
                case PolicyShopPriceDefinition policy:
                    RequireRegistration(source, policy.PricingPolicyId, path + ".pricingPolicyId",
                        _registrations.PolicyIds, "policy");
                    break;
            }
        }

        private void ValidateShopStock<TDefinition>(
            RecordSource<TDefinition> source,
            ShopStockDefinition stock,
            string path)
        {
            switch (stock)
            {
                case LimitedShopStockDefinition limited:
                    RequirePositive(source, limited.Quantity, path + ".quantity", "Limited shop stock");
                    break;
                case PolicyShopStockDefinition policy:
                    RequireRegistration(source, policy.StockPolicyId, path + ".stockPolicyId",
                        _registrations.PolicyIds, "policy");
                    break;
            }
        }

        private void ValidateFixedFloor(
            RecordSource<DungeonDefinition> source,
            DungeonBlockDefinition block,
            DungeonFixedFloorDefinition floor,
            string path)
        {
            RequirePositive(source, floor.Floor, path + ".floor", "Dungeon fixed floor");
            if (floor.Floor < block.StartFloor || floor.Floor > block.EndFloor)
            {
                Add(source, path + ".floor", ContentValidationErrorCode.ValueOutOfRange,
                    "Fixed floor must be inside its block floor range.");
            }

            if (floor.Kind is DungeonFixedFloorKind.Battle or DungeonFixedFloorKind.Boss &&
                floor.EncounterId is null)
            {
                Add(source, path + ".encounterId", ContentValidationErrorCode.ShapeInvalid,
                    "Battle and boss fixed floors require an encounter ID.");
            }

            if (floor.EncounterId is ContentId encounterId)
            {
                ValidateContentReference(source, encounterId, path + ".encounterId", _encounterIndex, "encounter");
            }

            if (floor.TransitionRuleId is ContentId transitionRuleId)
            {
                RequireRegistration(source, transitionRuleId, path + ".transitionRuleId",
                    _registrations.PolicyIds, "policy");
            }

            if (floor.BarrierRuleId is ContentId barrierRuleId)
            {
                RequireRegistration(source, barrierRuleId, path + ".barrierRuleId",
                    _registrations.PolicyIds, "policy");
            }
        }

        private void ValidateFusionResult(
            RecordSource<FusionRecipeDefinition> source,
            FusionResultDefinition result,
            string path)
        {
            switch (result.Operation)
            {
                case FusionResultOperationKind.CreateEntity:
                    if (result.ResultEntityId is null)
                    {
                        Add(source, path + ".resultEntityId", ContentValidationErrorCode.ShapeInvalid,
                            "Create-entity fusion results require resultEntityId.");
                    }
                    else
                    {
                        ValidateContentReference(source, result.ResultEntityId.Value,
                            path + ".resultEntityId", _entityIndex, "entity");
                    }
                    break;
                case FusionResultOperationKind.RankOffset:
                    if (result.ResultRaceId is null)
                    {
                        Add(source, path + ".resultRaceId", ContentValidationErrorCode.ShapeInvalid,
                            "Rank-offset fusion results require resultRaceId.");
                    }
                    else
                    {
                        ValidateContentReference(source, result.ResultRaceId.Value,
                            path + ".resultRaceId", _raceIndex, "race");
                    }
                    if (result.RankOffset is null or 0)
                    {
                        Add(source, path + ".rankOffset", ContentValidationErrorCode.ShapeInvalid,
                            "Rank-offset fusion results require a nonzero rank offset.");
                    }
                    break;
                case FusionResultOperationKind.StatBoost:
                case FusionResultOperationKind.Special:
                    if (result.PolicyId is null)
                    {
                        Add(source, path + ".policyId", ContentValidationErrorCode.ShapeInvalid,
                            $"{result.Operation} fusion results require a policy ID.");
                    }
                    else
                    {
                        RequireRegistration(source, result.PolicyId.Value, path + ".policyId",
                            _registrations.PolicyIds, "policy");
                    }
                    break;
            }
        }

        private void ValidateEffects<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyList<EffectDefinition> effects,
            string path)
        {
            for (int index = 0; index < effects.Count; index++)
            {
                ValidateEffect(source, effects[index], $"{path}[{index}]");
            }
        }

        private void ValidateEffect<TDefinition>(RecordSource<TDefinition> source, EffectDefinition effect, string path)
        {
            RequireSupportedType(source, effect.GetType(), path, _registrations.SupportedEffectTypes, "effect");
            if (effect.When is not null)
            {
                ValidateCondition(source, effect.When, path + ".when");
            }

            switch (effect)
            {
                case DamageEffectDefinition damage:
                    RequireNonNegative(source, damage.Power, path + ".power", "Damage power");
                    RequirePercentage(source, damage.Accuracy, path + ".accuracy", "Damage accuracy");
                    ValidateCritical(source, damage.Critical, path + ".critical");
                    ValidateHitCount(source, damage.Hits, path + ".hits");
                    break;
                case InstantKillEffectDefinition instantKill:
                    RequirePercentage(source, instantKill.Chance, path + ".chance", "Instant-kill chance");
                    break;
                case ApplyAilmentEffectDefinition apply:
                    RequirePercentage(source, apply.Chance, path + ".chance", "Ailment application chance");
                    ValidateContentReference(source, apply.AilmentId, path + ".ailmentId", _ailmentIndex, "ailment");
                    if (apply.Duration is not null)
                    {
                        ValidateDuration(source, apply.Duration, path + ".duration");
                    }
                    break;
                case RestoreResourceEffectDefinition restore:
                    RequireRegistration(source, restore.ResourceId, path + ".resourceId", _registrations.ResourceIds,
                        "resource");
                    ValidateAmount(source, restore.Amount, path + ".amount");
                    break;
                case RemoveAilmentEffectDefinition removeAilment:
                    ValidateRemoveAilment(source, removeAilment, path);
                    break;
                case ReviveEffectDefinition revive:
                    RequireRegistration(source, revive.ResourceId, path + ".resourceId", _registrations.ResourceIds,
                        "resource");
                    ValidateAmount(source, revive.Amount, path + ".amount");
                    break;
                case ModifyStatStageEffectDefinition statStage:
                    if (statStage.ModifierTrackIds.Count == 0)
                    {
                        Add(source, path + ".modifierTrackIds", ContentValidationErrorCode.ShapeInvalid,
                            "Stat-stage effects require at least one modifier track.");
                    }
                    ValidateDuplicates(source, statStage.ModifierTrackIds, path + ".modifierTrackIds");
                    for (int index = 0; index < statStage.ModifierTrackIds.Count; index++)
                    {
                        RequireRegistration(source, statStage.ModifierTrackIds[index],
                            path + $".modifierTrackIds[{index}]", _registrations.ModifierTrackIds, "modifier track");
                    }
                    if (statStage.StageDelta == 0)
                    {
                        Add(source, path + ".stageDelta", ContentValidationErrorCode.ShapeInvalid,
                            "Stat-stage changes cannot be zero.");
                    }
                    if (statStage.Duration is not null)
                    {
                        ValidateDuration(source, statStage.Duration, path + ".duration");
                    }
                    break;
                case GrantChargeEffectDefinition charge:
                    RequirePositive(source, charge.Multiplier, path + ".multiplier", "Charge multiplier");
                    if (charge.Duration is not null)
                    {
                        ValidateDuration(source, charge.Duration, path + ".duration");
                    }
                    break;
                case GrantShieldEffectDefinition shield when shield.Duration is not null:
                    ValidateDuration(source, shield.Duration, path + ".duration");
                    break;
                case BreakAffinityEffectDefinition affinityBreak:
                    ValidateAffinityElements(
                        source,
                        affinityBreak.Elements,
                        path + ".elementIds",
                        "Affinity Break effects");
                    ValidateDuration(source, affinityBreak.Duration, path + ".duration");
                    break;
                case OverrideAffinityEffectDefinition affinity:
                    ValidateAffinityElements(
                        source,
                        affinity.Elements,
                        path + ".elementIds",
                        "Affinity overrides");
                    ValidateDuration(source, affinity.Duration, path + ".duration");
                    break;
                case RemoveStatusEffectDefinition removeStatus:
                    if (removeStatus.StatusKinds.Count == 0 && removeStatus.StatusIds.Count == 0)
                    {
                        Add(source, path, ContentValidationErrorCode.ShapeInvalid,
                            "Status removal requires at least one status kind or status ID.");
                    }
                    ValidateDuplicates(source, removeStatus.StatusKinds, path + ".statusKinds");
                    ValidateDuplicates(source, removeStatus.StatusIds, path + ".statusIds");
                    for (int index = 0; index < removeStatus.StatusIds.Count; index++)
                    {
                        RequireRegistration(source, removeStatus.StatusIds[index], path + $".statusIds[{index}]",
                            _registrations.StatusIds, "status");
                    }
                    break;
                case ReduceResourceEffectDefinition reduce:
                    RequireRegistration(source, reduce.ResourceId, path + ".resourceId", _registrations.ResourceIds,
                        "resource");
                    ValidateAmount(source, reduce.Amount, path + ".amount");
                    break;
                case SetResourceEffectDefinition set:
                    RequireRegistration(source, set.ResourceId, path + ".resourceId", _registrations.ResourceIds,
                        "resource");
                    ValidateAmount(source, set.Amount, path + ".amount");
                    break;
                case AnalyzeEffectDefinition analyze:
                    if (analyze.Layers.Count == 0)
                    {
                        Add(source, path + ".layers", ContentValidationErrorCode.ShapeInvalid,
                            "Analyze effects require at least one layer.");
                    }
                    ValidateDuplicates(source, analyze.Layers, path + ".layers");
                    break;
                case EscapeEffectDefinition escape:
                    RequireRegistration(source, escape.EligibilityRuleId, path + ".eligibilityRuleId",
                        _registrations.EscapeRuleIds, "escape rule");
                    if (escape.Chance is int chance)
                    {
                        RequirePercentage(source, chance, path + ".chance", "Escape chance");
                    }
                    break;
                case CustomEffectDefinition custom:
                    ValidateParameters(source, custom.HandlerId, custom.Parameters, path + ".parameters",
                        _registrations.CustomEffectValidators, "custom effect handler");
                    break;
            }
        }

        private void ValidateAffinityElements<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyList<DamageElement> elements,
            string path,
            string subject)
        {
            if (elements.Count == 0)
            {
                Add(source, path, ContentValidationErrorCode.ShapeInvalid,
                    $"{subject} require at least one element.");
            }

            ValidateDuplicates(source, elements, path);
            for (int index = 0; index < elements.Count; index++)
            {
                if (elements[index] == DamageElement.Almighty)
                {
                    Add(source, $"{path}[{index}]",
                        ContentValidationErrorCode.AlmightyAffinityForbidden,
                        "Almighty cannot receive an authored affinity change.");
                }
            }
        }

        private void ValidateRemoveAilment<TDefinition>(
            RecordSource<TDefinition> source,
            RemoveAilmentEffectDefinition effect,
            string path)
        {
            if (effect.Scope == AilmentRemovalScope.Selected &&
                effect.AilmentIds.Count == 0 && effect.AilmentGroupIds.Count == 0)
            {
                Add(source, path, ContentValidationErrorCode.ShapeInvalid,
                    "Selected ailment removal requires at least one ailment or ailment group.");
            }

            ValidateContentReferenceDuplicates(source, effect.AilmentIds, path + ".ailmentIds");
            ValidateDuplicates(source, effect.AilmentGroupIds, path + ".ailmentGroupIds");
            for (int index = 0; index < effect.AilmentIds.Count; index++)
            {
                ValidateContentReference(source, effect.AilmentIds[index], path + $".ailmentIds[{index}]",
                    _ailmentIndex, "ailment");
            }
            for (int index = 0; index < effect.AilmentGroupIds.Count; index++)
            {
                RequireRegistration(source, effect.AilmentGroupIds[index], path + $".ailmentGroupIds[{index}]",
                    _registrations.AilmentGroupIds, "ailment group");
            }
        }

        private void ValidateTriggers<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyList<PassiveTriggerDefinition> triggers,
            string path)
        {
            for (int index = 0; index < triggers.Count; index++)
            {
                PassiveTriggerDefinition trigger = triggers[index];
                string triggerPath = $"{path}[{index}]";
                RequireRegistration(source, trigger.EventId, triggerPath + ".event", _registrations.EventIds, "event");
                if (trigger.Effects.Count == 0)
                {
                    Add(source, triggerPath + ".effects", ContentValidationErrorCode.TriggerEffectsRequired,
                        "Passive triggers require at least one effect.");
                }
                if (trigger.When is not null)
                {
                    ValidateCondition(source, trigger.When, triggerPath + ".when");
                }
                ValidateEffects(source, trigger.Effects, triggerPath + ".effects");
            }
        }

        private void ValidateModifiers<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyList<RuleModifierDefinition> modifiers,
            string path)
        {
            for (int index = 0; index < modifiers.Count; index++)
            {
                RuleModifierDefinition modifier = modifiers[index];
                string modifierPath = $"{path}[{index}]";
                RequireSupportedType(source, modifier.GetType(), modifierPath, _registrations.SupportedModifierTypes,
                    "modifier");
                if (modifier.When is not null)
                {
                    ValidateCondition(source, modifier.When, modifierPath + ".when");
                }

                switch (modifier)
                {
                    case NumericRuleModifierDefinition numeric when numeric.Operation == ModifierOperation.Multiply:
                        RequirePositive(source, numeric.Value, modifierPath + ".value", "Multiplicative modifier");
                        break;
                    case ElementalAffinityRuleModifierDefinition affinity when affinity.Element == DamageElement.Almighty:
                        Add(source, modifierPath + ".elementId", ContentValidationErrorCode.AlmightyAffinityForbidden,
                            "Almighty cannot receive an authored passive affinity replacement.");
                        break;
                    case AilmentResistanceRuleModifierDefinition resistance:
                        ValidateContentReference(
                            source,
                            resistance.AilmentId,
                            modifierPath + ".ailmentId",
                            _ailmentIndex,
                            "ailment");
                        break;
                    case BasicAttackRuleModifierDefinition attack
                        when attack.Element is null && attack.Targeting is null && attack.Drain is null:
                        Add(source, modifierPath, ContentValidationErrorCode.ShapeInvalid,
                            "Basic-attack replacements require an element, targeting rule, or drain rule.");
                        break;
                    case BasicAttackRuleModifierDefinition attack when attack.Targeting is not null:
                        ValidateTargeting(source, attack.Targeting, modifierPath + ".targeting");
                        break;
                }
            }
        }

        private void ValidateCondition<TDefinition>(
            RecordSource<TDefinition> source,
            ConditionDefinition condition,
            string path)
        {
            RequireSupportedType(source, condition.GetType(), path, _registrations.SupportedConditionTypes, "condition");
            switch (condition)
            {
                case AllConditionDefinition all:
                    if (all.Conditions.Count == 0)
                    {
                        Add(source, path + ".all", ContentValidationErrorCode.ShapeInvalid,
                            "All-condition nodes must contain at least one condition.");
                    }
                    for (int index = 0; index < all.Conditions.Count; index++)
                    {
                        ValidateCondition(source, all.Conditions[index], path + $".all[{index}]");
                    }
                    break;
                case AnyConditionDefinition any:
                    if (any.Conditions.Count == 0)
                    {
                        Add(source, path + ".any", ContentValidationErrorCode.ShapeInvalid,
                            "Any-condition nodes must contain at least one condition.");
                    }
                    for (int index = 0; index < any.Conditions.Count; index++)
                    {
                        ValidateCondition(source, any.Conditions[index], path + $".any[{index}]");
                    }
                    break;
                case NotConditionDefinition not:
                    ValidateCondition(source, not.Condition, path + ".not");
                    break;
                case ResourcePercentageConditionDefinition resource:
                    RequireRegistration(source, resource.ResourceId, path + ".resourceId", _registrations.ResourceIds,
                        "resource");
                    RequirePercentage(source, resource.Value, path + ".value", "Resource percentage");
                    break;
                case HasAilmentConditionDefinition ailment:
                    if (ailment.AilmentIds.Count == 0)
                    {
                        Add(source, path + ".ailmentIds", ContentValidationErrorCode.ShapeInvalid,
                            "Ailment conditions require at least one ailment.");
                    }
                    ValidateContentReferenceDuplicates(source, ailment.AilmentIds, path + ".ailmentIds");
                    for (int index = 0; index < ailment.AilmentIds.Count; index++)
                    {
                        ValidateContentReference(source, ailment.AilmentIds[index], path + $".ailmentIds[{index}]",
                            _ailmentIndex, "ailment");
                    }
                    break;
                case HasSkillConditionDefinition skill:
                    ValidateContentReference(source, skill.SkillId, path + ".skillId", _skillIndex, "skill");
                    break;
                case HasBuffConditionDefinition buff:
                    RequireRegistration(source, buff.ModifierTrackId, path + ".modifierTrackId",
                        _registrations.ModifierTrackIds, "modifier track");
                    break;
                case HasAffinityConditionDefinition affinity when affinity.Element == DamageElement.Almighty:
                    Add(source, path + ".elementId", ContentValidationErrorCode.AlmightyAffinityForbidden,
                        "Almighty cannot be used in an authored affinity condition.");
                    break;
                case HasCapabilityConditionDefinition capability:
                    RequireRegistration(source, capability.CapabilityId, path + ".capabilityId",
                        _registrations.CapabilityIds, "capability");
                    break;
                case BattleKindConditionDefinition battle:
                    ValidateRegisteredList(source, battle.AllowedBattleKindIds, path + ".allowed",
                        _registrations.BattleKindIds, "battle kind");
                    break;
                case MoonPhaseConditionDefinition moon:
                    ValidateRegisteredList(source, moon.AllowedMoonPhaseIds, path + ".allowed",
                        _registrations.MoonPhaseIds, "moon phase");
                    break;
                case PartySizeConditionDefinition party:
                    RequirePositive(source, party.Value, path + ".value", "Party size");
                    break;
                case ChanceConditionDefinition chance:
                    RequirePercentage(source, chance.Chance, path + ".chance", "Condition chance");
                    break;
                case CustomConditionDefinition custom:
                    ValidateParameters(source, custom.HandlerId, custom.Parameters, path + ".parameters",
                        _registrations.CustomConditionValidators, "custom condition handler");
                    break;
            }
        }

        private void ValidateAilmentBehavior(
            RecordSource<AilmentDefinition> source,
            AilmentTurnBehaviorDefinition behavior,
            string path)
        {
            RequireSupportedType(source, behavior.GetType(), path, _registrations.SupportedAilmentBehaviorTypes,
                "ailment turn behavior");
            switch (behavior)
            {
                case LimitedActionsAilmentTurnBehaviorDefinition limited:
                    ValidateRegisteredList(source, limited.AllowedActionIds, path + ".allowedActionIds",
                        _registrations.ActionIds, "action");
                    break;
                case ChanceSkipAilmentTurnBehaviorDefinition chanceSkip:
                    RequirePercentage(source, chanceSkip.SkipChance, path + ".skipChance", "Skip chance");
                    break;
                case ChanceSkipOrFleeAilmentTurnBehaviorDefinition fear:
                    RequirePercentage(source, fear.SkipChance, path + ".skipChance", "Skip chance");
                    RequirePercentage(source, fear.FleeChance, path + ".fleeChance", "Flee chance");
                    if (fear.SkipChance >= 0 && fear.FleeChance >= 0 && fear.SkipChance + fear.FleeChance > 100)
                    {
                        Add(source, path, ContentValidationErrorCode.ValueOutOfRange,
                            "Skip and flee chances cannot total more than 100.");
                    }
                    break;
                case CustomAilmentTurnBehaviorDefinition custom:
                    ValidateParameters(source, custom.HandlerId, custom.Parameters, path + ".parameters",
                        _registrations.CustomAilmentBehaviorValidators, "custom ailment behavior handler");
                    break;
            }
        }

        private void ValidateAmount<TDefinition>(RecordSource<TDefinition> source, AmountDefinition amount, string path)
        {
            switch (amount)
            {
                case FlatAmountDefinition flat:
                    RequireNonNegative(source, flat.Value, path + ".value", "Flat amount");
                    break;
                case PercentMaximumAmountDefinition maximum:
                    RequireNonNegative(source, maximum.Value, path + ".value", "Percent-maximum amount");
                    break;
                case PercentCurrentAmountDefinition current:
                    RequireNonNegative(source, current.Value, path + ".value", "Percent-current amount");
                    break;
                case PowerAmountDefinition power:
                    RequireNonNegative(source, power.Power, path + ".power", "Power amount");
                    break;
                case FormulaAmountDefinition formula:
                    ValidateParameters(source, formula.FormulaId, formula.Parameters, path + ".parameters",
                        _registrations.FormulaValidators, "formula");
                    break;
            }
        }

        private void ValidateDuration<TDefinition>(
            RecordSource<TDefinition> source,
            DurationDefinition duration,
            string path)
        {
            switch (duration)
            {
                case TurnDurationDefinition turns:
                    RequirePositive(source, turns.Value, path + ".value", "Turn duration");
                    RequireRegistration(source, turns.TickEventId, path + ".tick", _registrations.EventIds, "event");
                    break;
                case PhaseDurationDefinition phase:
                    RequireRegistration(source, phase.PhaseId, path + ".phaseId", _registrations.PhaseIds, "phase");
                    break;
            }
        }

        private void ValidateTargeting<TDefinition>(
            RecordSource<TDefinition> source,
            TargetingDefinition targeting,
            string path)
        {
            bool relationNone = targeting.Relation == TargetRelation.None;
            bool selectionNone = targeting.Selection == TargetSelection.None;
            if (relationNone != selectionNone)
            {
                Add(source, path, ContentValidationErrorCode.ShapeInvalid,
                    "Target relation and selection must both be 'none' or both select targets.");
            }

            if (targeting.Selection == TargetSelection.Random && targeting.Count is null)
            {
                Add(source, path + ".count", ContentValidationErrorCode.ShapeInvalid,
                    "Random targeting requires a target count.");
            }

            if (targeting.Count is TargetCountDefinition count)
            {
                RequirePositive(source, count.Minimum, path + ".count.minimum", "Minimum target count");
                RequirePositive(source, count.Maximum, path + ".count.maximum", "Maximum target count");
                if (count.Minimum > count.Maximum)
                {
                    Add(source, path + ".count", ContentValidationErrorCode.MinimumExceedsMaximum,
                        "Minimum target count cannot exceed maximum target count.");
                }
            }
        }

        private void ValidateHitCount<TDefinition>(
            RecordSource<TDefinition> source,
            HitCountDefinition hits,
            string path)
        {
            RequirePositive(source, hits.Minimum, path + ".minimum", "Minimum hit count");
            RequirePositive(source, hits.Maximum, path + ".maximum", "Maximum hit count");
            if (hits.Minimum > hits.Maximum)
            {
                Add(source, path, ContentValidationErrorCode.MinimumExceedsMaximum,
                    "Minimum hit count cannot exceed maximum hit count.");
            }
            if (hits.Distribution == HitDistribution.Fixed && hits.Minimum != hits.Maximum)
            {
                Add(source, path, ContentValidationErrorCode.ShapeInvalid,
                    "Fixed hit counts require equal minimum and maximum values.");
            }
        }

        private void ValidateCritical<TDefinition>(
            RecordSource<TDefinition> source,
            CriticalDefinition critical,
            string path)
        {
            if (critical is ChanceCriticalDefinition chance)
            {
                RequirePercentage(source, chance.Chance, path + ".chance", "Critical chance");
            }
        }

        private void ValidateMutationFamilies()
        {
            IEnumerable<IGrouping<ContentId, RecordSource<SkillDefinition>>> families = _skills
                .Where(source => source.Definition.Mutation is not null && _skillIndex[source.Id].Count == 1)
                .GroupBy(source => source.Definition.Mutation!.FamilyId);

            foreach (IGrouping<ContentId, RecordSource<SkillDefinition>> family in families)
            {
                List<RecordSource<SkillDefinition>> records = family.ToList();
                foreach (RecordSource<SkillDefinition> source in records)
                {
                    if (source.Definition.Mutation!.Tier <= 0)
                    {
                        Add(source, source.Path + ".mutation.tier", ContentValidationErrorCode.MutationTierInvalid,
                            "Mutation tiers must be positive and start at one.");
                    }
                }

                foreach (IGrouping<int, RecordSource<SkillDefinition>> tierGroup in records
                             .Where(source => source.Definition.Mutation!.Tier > 0)
                             .GroupBy(source => source.Definition.Mutation!.Tier)
                             .Where(group => group.Count() > 1))
                {
                    foreach (RecordSource<SkillDefinition> source in tierGroup)
                    {
                        Add(source, source.Path + ".mutation.tier", ContentValidationErrorCode.MutationTierDuplicate,
                            $"Mutation family '{family.Key}' declares tier {tierGroup.Key} more than once.");
                    }
                }

                int[] tiers = records.Select(source => source.Definition.Mutation!.Tier)
                    .Where(tier => tier > 0)
                    .Distinct()
                    .OrderBy(tier => tier)
                    .ToArray();
                if (tiers.Length > 0)
                {
                    int expected = 1;
                    foreach (int tier in tiers)
                    {
                        if (tier != expected)
                        {
                            RecordSource<SkillDefinition> source = records.First(item => item.Definition.Mutation!.Tier == tier);
                            Add(source, source.Path + ".mutation.tier", ContentValidationErrorCode.MutationTierGap,
                                $"Mutation family '{family.Key}' is missing tier {expected} before tier {tier}.");
                            break;
                        }
                        expected++;
                    }
                }
            }
        }

        private RecordSource<TTarget>? ValidateContentReference<TDefinition, TTarget>(
            RecordSource<TDefinition> source,
            ContentId id,
            string path,
            IReadOnlyDictionary<ContentId, List<RecordSource<TTarget>>> index,
            string targetType)
        {
            if (!id.IsValid)
            {
                Add(source, path, ContentValidationErrorCode.ReferenceIdInvalid,
                    $"{Capitalize(targetType)} reference ID cannot be empty.");
                return null;
            }

            if (!TryLocalReference(id, out ContentId localId))
            {
                return null;
            }

            if (!index.TryGetValue(localId, out List<RecordSource<TTarget>>? matches))
            {
                Add(source, path, ContentValidationErrorCode.ReferenceMissing,
                    $"Unknown {targetType} ID '{id}'.");
                return null;
            }

            if (matches.Count > 1)
            {
                Add(source, path, ContentValidationErrorCode.ReferenceAmbiguous,
                    $"{Capitalize(targetType)} ID '{id}' is ambiguous because it is declared more than once.");
                return null;
            }

            return matches[0];
        }

        private bool TryLocalReference(ContentId id, out ContentId localId)
        {
            if (!id.IsValid)
            {
                localId = default;
                return false;
            }

            if (!id.IsQualified)
            {
                localId = id;
                return true;
            }

            string value = id.ToString();
            int separator = value.IndexOf(':');
            if (!value[..separator].Equals(_packId, StringComparison.Ordinal))
            {
                localId = default;
                return false;
            }

            localId = ContentId.Parse(value[(separator + 1)..]);
            return true;
        }

        private ContentId NormalizeContentReference(ContentId id) =>
            TryLocalReference(id, out ContentId localId) ? localId : id;

        private void ValidateRegisteredList<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyList<ContentId> ids,
            string path,
            IReadOnlySet<ContentId> registrations,
            string registrationKind)
        {
            if (ids.Count == 0)
            {
                Add(source, path, ContentValidationErrorCode.ShapeInvalid,
                    $"At least one {registrationKind} ID is required.");
            }
            ValidateDuplicates(source, ids, path);
            for (int index = 0; index < ids.Count; index++)
            {
                RequireRegistration(source, ids[index], $"{path}[{index}]", registrations, registrationKind);
            }
        }

        private void RequireRegistration<TDefinition>(
            RecordSource<TDefinition> source,
            ContentId id,
            string path,
            IReadOnlySet<ContentId> registrations,
            string registrationKind)
        {
            if (!id.IsValid)
            {
                Add(source, path, ContentValidationErrorCode.RegistrationIdInvalid,
                    $"{Capitalize(registrationKind)} ID cannot be empty.");
                return;
            }

            if (!registrations.Contains(id))
            {
                Add(source, path, ContentValidationErrorCode.RegistrationMissing,
                    $"No {registrationKind} registration exists for '{id}'.");
            }
        }

        private void RequireSupportedType<TDefinition>(
            RecordSource<TDefinition> source,
            Type type,
            string path,
            IReadOnlySet<Type> registrations,
            string typeKind)
        {
            if (!registrations.Contains(type))
            {
                Add(source, path, ContentValidationErrorCode.DefinitionTypeUnsupported,
                    $"The host does not support {typeKind} definition type '{type.Name}'.");
            }
        }

        private void ValidateParameters<TDefinition>(
            RecordSource<TDefinition> source,
            ContentId id,
            IReadOnlyDictionary<string, object?> parameters,
            string path,
            IReadOnlyDictionary<ContentId, IContentParameterValidator> validators,
            string registrationKind)
        {
            if (!id.IsValid)
            {
                Add(source, path, ContentValidationErrorCode.RegistrationIdInvalid,
                    $"{Capitalize(registrationKind)} ID cannot be empty.");
                return;
            }

            if (!validators.TryGetValue(id, out IContentParameterValidator? validator))
            {
                Add(source, path, ContentValidationErrorCode.RegistrationMissing,
                    $"No {registrationKind} registration exists for '{id}'.");
                return;
            }

            IReadOnlyList<ContentParameterValidationIssue> issues = validator.Validate(parameters);
            foreach (ContentParameterValidationIssue issue in issues)
            {
                string issuePath = string.IsNullOrWhiteSpace(issue.ParameterPath)
                    ? path
                    : path + "." + issue.ParameterPath!.TrimStart('.');
                Add(source, issuePath, ContentValidationErrorCode.ParameterValidationFailed,
                    issue.Message, issue.Suggestion);
            }
        }

        private void ValidateDuplicates<TDefinition, TValue>(
            RecordSource<TDefinition> source,
            IReadOnlyList<TValue> values,
            string path)
            where TValue : notnull
        {
            var seen = new HashSet<TValue>();
            for (int index = 0; index < values.Count; index++)
            {
                if (!seen.Add(values[index]))
                {
                    Add(source, $"{path}[{index}]", ContentValidationErrorCode.ListDuplicateValue,
                        $"Value '{values[index]}' is listed more than once.");
                }
            }
        }

        private void ValidateContentReferenceDuplicates<TDefinition>(
            RecordSource<TDefinition> source,
            IReadOnlyList<ContentId> values,
            string path)
        {
            var seen = new HashSet<ContentId>();
            for (int index = 0; index < values.Count; index++)
            {
                if (!values[index].IsValid)
                {
                    continue;
                }

                if (!seen.Add(NormalizeContentReference(values[index])))
                {
                    Add(source, $"{path}[{index}]", ContentValidationErrorCode.ListDuplicateValue,
                        $"Content reference '{values[index]}' resolves to a target already listed here.");
                }
            }
        }

        private void RequirePercentage<TDefinition>(
            RecordSource<TDefinition> source,
            decimal value,
            string path,
            string label)
        {
            if (value is < 0 or > 100)
            {
                Add(source, path, ContentValidationErrorCode.ValueOutOfRange,
                    $"{label} must be between 0 and 100 inclusive.");
            }
        }

        private void RequirePositive<TDefinition>(
            RecordSource<TDefinition> source,
            decimal value,
            string path,
            string label)
        {
            if (value <= 0)
            {
                Add(source, path, ContentValidationErrorCode.ValueMustBePositive,
                    $"{label} must be positive.");
            }
        }

        private void RequireNonNegative<TDefinition>(
            RecordSource<TDefinition> source,
            decimal value,
            string path,
            string label)
        {
            if (value < 0)
            {
                Add(source, path, ContentValidationErrorCode.ValueMustBeNonNegative,
                    $"{label} cannot be negative.");
            }
        }

        private void Add<TDefinition>(
            RecordSource<TDefinition> source,
            string path,
            ContentValidationErrorCode code,
            string message,
            string? suggestion = null)
        {
            Errors.Add(new ContentValidationError(
                _packId,
                source.SourceName,
                source.RecordType,
                source.Id,
                path,
                code,
                message,
                suggestion));
        }

        private void AddManifestError(string path, ContentValidationErrorCode code, string message)
        {
            Errors.Add(new ContentValidationError(
                _packId, _request.ManifestSourceName, "manifest", null, path, code, message));
        }

        private void AddDocumentError(
            string sourceName,
            string path,
            ContentValidationErrorCode code,
            string message)
        {
            Errors.Add(new ContentValidationError(_packId, sourceName, "document", null, path, code, message));
        }

        private static List<RecordSource<TDefinition>> Flatten<TDefinition>(
            IReadOnlyList<SourceContentDocument<TDefinition>> documents,
            string recordType,
            string arrayName,
            Func<TDefinition, ContentId> idSelector)
        {
            var records = new List<RecordSource<TDefinition>>();
            foreach (SourceContentDocument<TDefinition> document in documents)
            {
                for (int index = 0; index < document.Document.Records.Count; index++)
                {
                    TDefinition definition = document.Document.Records[index];
                    records.Add(new RecordSource<TDefinition>(
                        definition,
                        idSelector(definition),
                        recordType,
                        document.ManifestPath,
                        document.SourceName,
                        $"$.{arrayName}[{index}]"));
                }
            }
            return records;
        }

        private static Dictionary<ContentId, List<RecordSource<TDefinition>>> Index<TDefinition>(
            IEnumerable<RecordSource<TDefinition>> records) =>
            records.GroupBy(source => source.Id).ToDictionary(group => group.Key, group => group.ToList());

        private static string Capitalize(string value) =>
            char.ToUpperInvariant(value[0]) + value[1..];
    }

    private sealed record RecordSource<TDefinition>(
        TDefinition Definition,
        ContentId Id,
        string RecordType,
        string ManifestPath,
        string SourceName,
        string Path);
}
