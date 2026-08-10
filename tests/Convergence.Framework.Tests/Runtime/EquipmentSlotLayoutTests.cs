using Convergence.Content;
using Convergence.Runtime;
using Convergence.Validation;
using Xunit;

namespace Convergence.Framework.Tests.Runtime;

public sealed class EquipmentSlotLayoutTests
{
    [Fact]
    public void StandardLayout_ExposesStableImmutableIdsAndMatchesTheFormerFourSlotMatrix()
    {
        IEquipmentSlotLayoutPolicy policy = StandardEquipmentSlotLayoutPolicy.Instance;
        ContentId[] expectedSlots =
        [
            ContentId.Parse("weapon"),
            ContentId.Parse("armor"),
            ContentId.Parse("boots"),
            ContentId.Parse("accessory")
        ];

        Assert.Equal(expectedSlots, policy.SlotIds);
        Assert.Equal(StandardEquipmentSlotIds.Weapon, expectedSlots[0]);
        Assert.Equal(StandardEquipmentSlotIds.Armor, expectedSlots[1]);
        Assert.Equal(StandardEquipmentSlotIds.Boots, expectedSlots[2]);
        Assert.Equal(StandardEquipmentSlotIds.Accessory, expectedSlots[3]);
        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<ContentId>)policy.SlotIds).Add(ContentId.Parse("extra")));

        foreach (ContentId slotId in expectedSlots)
        {
            foreach (EquipmentProfileKind profileKind in Enum.GetValues<EquipmentProfileKind>())
            {
                EquipmentSlotLayoutResult result = policy.ValidateDefinition(
                    Definition(slotId, profileKind));
                bool expected = slotId == SlotFor(profileKind);

                Assert.Equal(expected, result.IsCompatible);
                Assert.Equal(
                    expected
                        ? EquipmentSlotLayoutCode.Compatible
                        : EquipmentSlotLayoutCode.ProfileMismatch,
                    result.Code);
            }

            foreach (ContentId targetSlotId in expectedSlots)
            {
                Assert.Equal(
                    slotId == targetSlotId,
                    policy.ValidateAssignment(slotId, targetSlotId).IsCompatible);
            }
        }

        EquipmentSlotLayoutResult unsupported = policy.ValidateDefinition(
            Definition(ContentId.Parse("relic"), EquipmentProfileKind.Weapon));
        EquipmentSlotLayoutResult ambiguous = policy.ValidateDefinition(
            new EquipmentDefinition(
                ContentId.Parse("ambiguous"),
                "Ambiguous",
                "Two profiles.",
                StandardEquipmentSlotIds.Weapon,
                1,
                weapon: WeaponProfile(),
                accessory: new EquipmentAccessoryProfileDefinition()));

        Assert.Equal(EquipmentSlotLayoutCode.UnsupportedSlot, unsupported.Code);
        Assert.Equal(EquipmentSlotLayoutCode.ProfileMismatch, ambiguous.Code);
    }

    [Fact]
    public void Snapshots_UseAuthoredSlotIdsWithoutWeakeningInstanceUniqueness()
    {
        ContentId relicSlot = ContentId.Parse("relic_socket");
        ContentId secondRelicSlot = ContentId.Parse("second_relic_socket");
        RuntimeInstanceId instanceId = RuntimeInstanceId.Parse("relic-001");
        var instance = new RuntimeEquipmentInstanceSnapshot(
            instanceId,
            ContentId.Parse("test.pack:relic"));
        var inventory = new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    relicSlot,
                    [instance])
            ]);
        var equipment = new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<ContentId, RuntimeInstanceId>(relicSlot, instanceId)
        ]);

        Assert.Same(instance, Assert.Single(inventory.GetEquipmentInstances(relicSlot)));
        Assert.Equal(instanceId, equipment.EquippedInstanceIds[relicSlot]);
        Assert.Throws<ArgumentException>(() => new RuntimeInventorySnapshot(
            ownedEquipmentInstances:
            [
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    relicSlot,
                    [instance]),
                new KeyValuePair<ContentId, IEnumerable<RuntimeEquipmentInstanceSnapshot>>(
                    secondRelicSlot,
                    [instance])
            ]));
        Assert.Throws<ArgumentException>(() => new RuntimeEquipmentSnapshot(
        [
            new KeyValuePair<ContentId, RuntimeInstanceId>(relicSlot, instanceId),
            new KeyValuePair<ContentId, RuntimeInstanceId>(secondRelicSlot, instanceId)
        ]));
    }

    [Fact]
    public void ContentValidation_DelegatesCustomSlotAndProfileCompatibilityToTheSelectedPolicy()
    {
        ContentId relicSlot = ContentId.Parse("relic_socket");
        EquipmentDefinition definition = Definition(relicSlot, EquipmentProfileKind.Accessory);
        SkillSystemValidationRequest request = Request(definition);
        var customPolicy = new RelicEquipmentSlotLayoutPolicy(relicSlot);

        ContentValidationResult standard = new SkillSystemContentValidator().Validate(request);
        ContentValidationResult custom =
            new SkillSystemContentValidator(customPolicy).Validate(request);

        Assert.Contains(standard.Errors, error =>
            error.Code == ContentValidationErrorCode.ShapeInvalid &&
            error.JsonPath == "$.equipment[0].slotId");
        Assert.True(
            custom.IsValid,
            string.Join(Environment.NewLine, custom.Errors.Select(error => error.Message)));
    }

    private static SkillSystemValidationRequest Request(EquipmentDefinition definition)
    {
        var manifest = new ContentPackManifest(
            9,
            "test.pack",
            SemanticVersion.Parse("1.0.0"),
            "Test Pack",
            null,
            null,
            [new ContentPackDocumentReference("equipment", "equipment.json")]);
        var document = new DeserializedContentDocument<EquipmentDefinition>(9, [definition]);
        return new SkillSystemValidationRequest(
            manifest,
            "manifest.json",
            new SkillSystemRegistrationBuilder().Build(),
            equipmentDocuments:
            [
                new SourceContentDocument<EquipmentDefinition>(
                    "equipment.json",
                    "equipment.json",
                    document)
            ]);
    }

    private static EquipmentDefinition Definition(
        ContentId slotId,
        EquipmentProfileKind profileKind) =>
        new(
            ContentId.Parse($"test_{profileKind.ToString().ToLowerInvariant()}"),
            profileKind.ToString(),
            "Test equipment.",
            slotId,
            1,
            weapon: profileKind == EquipmentProfileKind.Weapon ? WeaponProfile() : null,
            armor: profileKind == EquipmentProfileKind.Armor
                ? new EquipmentArmorProfileDefinition(1, 1)
                : null,
            boots: profileKind == EquipmentProfileKind.Boots
                ? new EquipmentBootsProfileDefinition(1)
                : null,
            accessory: profileKind == EquipmentProfileKind.Accessory
                ? new EquipmentAccessoryProfileDefinition()
                : null);

    private static EquipmentWeaponProfileDefinition WeaponProfile() =>
        new(new EquipmentBasicAttackDefinition(
            DamageElement.Physical,
            1,
            100,
            new NeverCriticalDefinition(),
            IsLongRange: false));

    private static ContentId SlotFor(EquipmentProfileKind profileKind) => profileKind switch
    {
        EquipmentProfileKind.Weapon => StandardEquipmentSlotIds.Weapon,
        EquipmentProfileKind.Armor => StandardEquipmentSlotIds.Armor,
        EquipmentProfileKind.Boots => StandardEquipmentSlotIds.Boots,
        EquipmentProfileKind.Accessory => StandardEquipmentSlotIds.Accessory,
        _ => throw new ArgumentOutOfRangeException(nameof(profileKind), profileKind, null)
    };

    private enum EquipmentProfileKind
    {
        Weapon,
        Armor,
        Boots,
        Accessory
    }

    private sealed class RelicEquipmentSlotLayoutPolicy(ContentId relicSlot)
        : IEquipmentSlotLayoutPolicy
    {
        private readonly IReadOnlyList<ContentId> _slotIds = Array.AsReadOnly([relicSlot]);

        public IReadOnlyList<ContentId> SlotIds => _slotIds;

        public EquipmentSlotLayoutResult ValidateDefinition(EquipmentDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return definition.SlotId == relicSlot &&
                   definition.Accessory is not null &&
                   definition.Weapon is null &&
                   definition.Armor is null &&
                   definition.Boots is null
                ? EquipmentSlotLayoutResult.Compatible
                : new EquipmentSlotLayoutResult(
                    EquipmentSlotLayoutCode.ProfileMismatch,
                    "Relic sockets accept accessory profiles only.");
        }

        public EquipmentSlotLayoutResult ValidateAssignment(
            ContentId authoredSlotId,
            ContentId targetSlotId) =>
            authoredSlotId == relicSlot && targetSlotId == relicSlot
                ? EquipmentSlotLayoutResult.Compatible
                : new EquipmentSlotLayoutResult(
                    EquipmentSlotLayoutCode.AssignmentMismatch,
                    "Relics can only be assigned to the authored relic socket.");
    }
}
