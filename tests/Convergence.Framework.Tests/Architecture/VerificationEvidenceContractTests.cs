using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class VerificationEvidenceContractTests
{
    private static readonly string[] RequiredCommands =
    [
        "00-dotnet-info",
        "01-restore-audit",
        "02-format",
        "03-framework-build",
        "04-solution-build",
        "05-focused-framework-tests",
        "06-focused-demohost-tests",
        "07-architecture-tests",
        "08-full-tests",
        "09-framework-coverage",
        "10-coverage-threshold",
        "11-content-validation",
        "12-demo-battle",
        "13-demo-field",
        "14-demo-save",
        "15-demo-training-annex",
        "16-demo-training-annex-play",
        "17-godot-build",
        "18-godot-smoke",
        "19-trimming-analysis",
        "20-diff-check"
    ];

    [Fact]
    public void EvidencePolicy_TracksOnlyCanonicalBundlesAndPreservesRawBytes()
    {
        string ignore = File.ReadAllText(RepositoryPath(".gitignore"));
        string attributes = File.ReadAllText(RepositoryPath(".gitattributes"));
        string guide = File.ReadAllText(RepositoryPath("docs", "verification-evidence.md"));

        Assert.Contains("/artifacts/*", ignore, StringComparison.Ordinal);
        Assert.Contains("!/artifacts/verification/", ignore, StringComparison.Ordinal);
        Assert.Contains("!/artifacts/verification/**", ignore, StringComparison.Ordinal);
        Assert.Contains("/artifacts/verification/** -text", attributes, StringComparison.Ordinal);
        Assert.Contains(
            "artifacts/verification/<checkpoint>/<tested-commit>/",
            guide,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceRunner_RequiresCleanSourceAndCapturesTheCompleteLocalGate()
    {
        string runner = File.ReadAllText(RepositoryPath("eng", "Invoke-VerificationEvidence.ps1"));

        string[] tokens =
        [
            "Verification evidence requires a clean worktree.",
            "Verification evidence destination already exists",
            "GodotExecutable",
            "dotnet restore Convergence.sln --locked-mode",
            "dotnet test Convergence.sln",
            "XPlat Code Coverage",
            "--clean-battle-demo",
            "--clean-field-demo",
            "--clean-save-demo",
            "--clean-training-annex-demo",
            "--clean-training-annex-play",
            "--convergence-smoke",
            "EnableTrimAnalyzer=true",
            "git diff --check",
            "manifest.json",
            "SHA256SUMS.txt"
        ];

        Assert.All(tokens, token => Assert.Contains(token, runner, StringComparison.Ordinal));
        Assert.DoesNotContain("SkipGodot", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("Overwrite", runner, StringComparison.Ordinal);
    }

    [Fact]
    public void CommittedEvidenceBundles_HaveSuccessfulCommandsCompleteChecksumsAndValidCoverage()
    {
        string evidenceRoot = RepositoryPath("artifacts", "verification");
        if (!Directory.Exists(evidenceRoot))
        {
            return;
        }

        string[] manifests = Directory.EnumerateFiles(
                evidenceRoot,
                "manifest.json",
                SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(manifests);

        foreach (string manifestPath in manifests)
        {
            ValidateBundle(Path.GetDirectoryName(manifestPath)!, manifestPath);
        }
    }

    private static void ValidateBundle(string bundleRoot, string manifestPath)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = manifest.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("succeeded", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("repositoryWasClean").GetBoolean());
        Assert.Null(root.GetProperty("failure").GetString());

        JsonElement[] commands = root.GetProperty("commands").EnumerateArray().ToArray();
        string[] names = commands.Select(command => command.GetProperty("name").GetString()!).ToArray();
        Assert.All(RequiredCommands, required => Assert.Contains(required, names, StringComparer.Ordinal));
        Assert.All(commands, command =>
        {
            Assert.Equal(0, command.GetProperty("exitCode").GetInt32());
            Assert.True(File.Exists(BundlePath(bundleRoot, command.GetProperty("commandFile").GetString()!)));
            Assert.True(File.Exists(BundlePath(bundleRoot, command.GetProperty("outputFile").GetString()!)));
        });

        JsonElement coverage = root.GetProperty("coverage");
        Assert.True(coverage.GetProperty("lineRate").GetDecimal() >= 0.90m);
        Assert.True(coverage.GetProperty("branchRate").GetDecimal() >= 0.70m);
        string compressedCoverage = BundlePath(
            bundleRoot,
            coverage.GetProperty("compressedFile").GetString()!);
        Assert.True(File.Exists(compressedCoverage));
        Assert.Equal(
            coverage.GetProperty("uncompressedSha256").GetString(),
            HashDecompressedGzip(compressedCoverage));

        string checksumPath = Path.Combine(bundleRoot, "SHA256SUMS.txt");
        Assert.True(File.Exists(checksumPath));
        Dictionary<string, string> expected = File.ReadAllLines(checksumPath)
            .Where(line => line.Length != 0)
            .Select(ParseChecksum)
            .ToDictionary(pair => pair.Path, pair => pair.Hash, StringComparer.Ordinal);
        string[] actualFiles = Directory.EnumerateFiles(bundleRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Equals(checksumPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => Normalize(Path.GetRelativePath(bundleRoot, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(actualFiles, expected.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.All(expected, pair => Assert.Equal(pair.Value, HashFile(BundlePath(bundleRoot, pair.Key))));
    }

    private static (string Hash, string Path) ParseChecksum(string line)
    {
        int separator = line.IndexOf("  ", StringComparison.Ordinal);
        Assert.True(separator == 64, $"Malformed SHA256SUMS entry: {line}");
        return (line[..separator], line[(separator + 2)..]);
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string HashDecompressedGzip(string path)
    {
        using FileStream file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        return Convert.ToHexString(SHA256.HashData(gzip)).ToLowerInvariant();
    }

    private static string BundlePath(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string RepositoryPath(params string[] segments) =>
        Path.Combine([RepositoryRoot(), .. segments]);

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
