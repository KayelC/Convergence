using System.Collections.ObjectModel;

namespace JRPGPrototype.Data.Definitions;

internal static class DefinitionCollections
{
    public static IReadOnlyList<T> Snapshot<T>(IEnumerable<T>? values)
    {
        return Array.AsReadOnly(values?.ToArray() ?? Array.Empty<T>());
    }

    public static IReadOnlyDictionary<TKey, TValue> SnapshotDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var copy = new Dictionary<TKey, TValue>(comparer);
        if (values is not null)
        {
            foreach ((TKey key, TValue value) in values)
            {
                copy.Add(key, value);
            }
        }

        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }

    public static IReadOnlyDictionary<string, object?> SnapshotParameters(
        IEnumerable<KeyValuePair<string, object?>>? values)
    {
        return SnapshotDictionary(values, StringComparer.Ordinal);
    }
}
