namespace JRPGPrototype.Logic.Runtime;

public sealed record RuntimeFieldSnapshot
{
    public RuntimeFieldSnapshot(
        RuntimeNavigationSnapshot navigation,
        RuntimeDungeonTraversalSnapshot? dungeonTraversal = null)
    {
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        DungeonTraversal = dungeonTraversal;
    }

    public RuntimeNavigationSnapshot Navigation { get; }
    public RuntimeDungeonTraversalSnapshot? DungeonTraversal { get; }
}
