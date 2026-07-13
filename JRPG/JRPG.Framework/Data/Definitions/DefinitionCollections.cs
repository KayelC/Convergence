using System.Collections;
using System.Collections.ObjectModel;

namespace JRPGPrototype.Data.Definitions;

internal static class DefinitionCollections
{
    private const int MaximumParameterDepth = 64;

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
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (values is null)
        {
            return new ReadOnlyDictionary<string, object?>(copy);
        }

        var ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach ((string key, object? value) in values)
        {
            if (key is null)
            {
                throw new ArgumentException("Custom parameter keys cannot be null.", nameof(values));
            }

            copy.Add(key, SnapshotParameterValue(value, $"$.{key}", depth: 0, ancestors));
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }

    private static object? SnapshotParameterValue(
        object? value,
        string path,
        int depth,
        HashSet<object> ancestors)
    {
        if (depth > MaximumParameterDepth)
        {
            throw new ArgumentException(
                $"Custom parameter '{path}' exceeds the maximum nesting depth of {MaximumParameterDepth}.");
        }

        return value switch
        {
            null => null,
            bool boolean => boolean,
            string text => text,
            sbyte integer => (long)integer,
            byte integer => (long)integer,
            short integer => (long)integer,
            ushort integer => (long)integer,
            int integer => (long)integer,
            uint integer => (long)integer,
            long integer => integer,
            ulong integer when integer <= long.MaxValue => (long)integer,
            ulong => throw UnsupportedParameter(path, value),
            decimal number => number,
            IEnumerable<KeyValuePair<string, object?>> dictionary =>
                SnapshotParameterDictionary(dictionary, path, depth, ancestors),
            IReadOnlyList<object?> list => SnapshotParameterList(list, list, path, depth, ancestors),
            IList list => SnapshotParameterList(list.Cast<object?>(), list, path, depth, ancestors),
            _ => throw UnsupportedParameter(path, value)
        };
    }

    private static IReadOnlyDictionary<string, object?> SnapshotParameterDictionary(
        IEnumerable<KeyValuePair<string, object?>> values,
        string path,
        int depth,
        HashSet<object> ancestors)
    {
        object container = values;
        EnterContainer(container, path, ancestors);
        try
        {
            var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach ((string key, object? value) in values)
            {
                if (key is null)
                {
                    throw new ArgumentException($"Custom parameter object '{path}' contains a null key.");
                }

                copy.Add(
                    key,
                    SnapshotParameterValue(value, $"{path}.{key}", depth + 1, ancestors));
            }

            return new ReadOnlyDictionary<string, object?>(copy);
        }
        finally
        {
            ancestors.Remove(container);
        }
    }

    private static IReadOnlyList<object?> SnapshotParameterList(
        IEnumerable<object?> values,
        object container,
        string path,
        int depth,
        HashSet<object> ancestors)
    {
        EnterContainer(container, path, ancestors);
        try
        {
            return Array.AsReadOnly(values
                .Select((value, index) => SnapshotParameterValue(
                    value,
                    $"{path}[{index}]",
                    depth + 1,
                    ancestors))
                .ToArray());
        }
        finally
        {
            ancestors.Remove(container);
        }
    }

    private static void EnterContainer(object container, string path, HashSet<object> ancestors)
    {
        if (!ancestors.Add(container))
        {
            throw new ArgumentException($"Custom parameter '{path}' contains a reference cycle.");
        }
    }

    private static ArgumentException UnsupportedParameter(string path, object value) =>
        new(
            $"Custom parameter '{path}' uses unsupported CLR type '{value.GetType().FullName}'. " +
            "Allowed values are null, Boolean, string, integers representable as Int64, decimal, ordered lists, and string-keyed objects.");
}
