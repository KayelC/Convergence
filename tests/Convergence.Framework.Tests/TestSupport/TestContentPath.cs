namespace Convergence.Framework.Tests.TestSupport;

internal static class TestContentPath
{
    public static string Resolve(string root, string logicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);

        string normalized = logicalPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (normalized.Contains(Path.DirectorySeparatorChar))
        {
            return Path.GetFullPath(normalized, root);
        }

        string[] matches = Directory.GetFiles(root, normalized, SearchOption.AllDirectories);
        return matches.Length switch
        {
            1 => matches[0],
            0 => Path.Combine(root, normalized),
            _ => throw new InvalidOperationException(
                $"Content fixture '{logicalPath}' is ambiguous. Use its pack-relative path.")
        };
    }
}
