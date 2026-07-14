namespace Convergence.Content;

public sealed record RaceDefinition
{
    public RaceDefinition(
        ContentId id,
        string displayName,
        IEnumerable<ContentId>? alignmentIds = null,
        ContentId? negotiationPersonalityId = null)
    {
        Id = id;
        DisplayName = displayName;
        AlignmentIds = DefinitionCollections.Snapshot(alignmentIds);
        NegotiationPersonalityId = negotiationPersonalityId;
    }

    public ContentId Id { get; }
    public string DisplayName { get; }
    public IReadOnlyList<ContentId> AlignmentIds { get; }
    public ContentId? NegotiationPersonalityId { get; }
}
