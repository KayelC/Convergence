namespace Convergence.DemoHost.Tests.TestSupport;

internal static class TestContentPath
{
    public static string Resolve(string root, string logicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);

        string normalized = Normalize(logicalPath);
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

    public static string ResolveManifest(string root, string manifestPath) =>
        Path.GetFullPath(Normalize(manifestPath), root);

    public static string ResolveDocument(string root, string manifestPath, string documentPath)
    {
        string manifestFile = ResolveManifest(root, manifestPath);
        string manifestDirectory = Path.GetDirectoryName(manifestFile) ?? Path.GetFullPath(root);
        return Path.GetFullPath(Normalize(documentPath), manifestDirectory);
    }

    private static string Normalize(string logicalPath) => logicalPath
        .Replace('\\', Path.DirectorySeparatorChar)
        .Replace('/', Path.DirectorySeparatorChar);
}
