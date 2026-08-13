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
    public void EvidencePolicy_TracksCanonicalBundlesAndLabeledRecoveryWithoutNormalizingBytes()
    {
        string ignore = File.ReadAllText(RepositoryPath(".gitignore"));
        string attributes = File.ReadAllText(RepositoryPath(".gitattributes"));
        string guide = File.ReadAllText(RepositoryPath("docs", "verification-evidence.md"));

        Assert.Contains("/artifacts/*", ignore, StringComparison.Ordinal);
        Assert.Contains("!/artifacts/verification/", ignore, StringComparison.Ordinal);
        Assert.Contains("!/artifacts/verification/**", ignore, StringComparison.Ordinal);
        Assert.Contains("!/artifacts/historical-verification-recovery/", ignore, StringComparison.Ordinal);
        Assert.Contains("!/artifacts/historical-verification-recovery/**", ignore, StringComparison.Ordinal);
        Assert.Contains("!/artifacts/historical-verification-recovery/**/*.log", ignore, StringComparison.Ordinal);
        Assert.Contains("/artifacts/verification/** -text -whitespace", attributes, StringComparison.Ordinal);
        Assert.Contains(
            "/artifacts/historical-verification-recovery/** -text -whitespace",
            attributes,
            StringComparison.Ordinal);
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

    [Fact]
    public void RecoveredHistoricalEvidence_IsExplicitlyNonCanonicalAndChecksummed()
    {
        string recoveryRoot = RepositoryPath(
            "artifacts",
            "historical-verification-recovery",
            "2026-08-13");
        string readme = File.ReadAllText(Path.Combine(recoveryRoot, "README.md"));
        string sourceInventory = Path.Combine(recoveryRoot, "RECOVERED-SOURCES.csv");
        string checksumPath = Path.Combine(recoveryRoot, "SHA256SUMS.txt");

        Assert.Contains("historical recovery, not canonical gate bundles", readme, StringComparison.Ordinal);
        Assert.True(File.Exists(sourceInventory));
        Assert.True(File.Exists(checksumPath));

        string[] recoveredSources = File.ReadAllLines(sourceInventory).Skip(1).ToArray();
        Assert.Equal(34, recoveredSources.Length);
        foreach (string recoveredSource in recoveredSources)
        {
            string[] fields = recoveredSource.Trim('"').Split("\",\"");
            Assert.Equal(6, fields.Length);
            if (!fields[2].Equals("gzip-lossless", StringComparison.Ordinal))
            {
                continue;
            }

            (long bytes, string hash) = InspectDecompressedGzip(
                BundlePath(recoveryRoot, fields[1]));
            Assert.Equal(long.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture), bytes);
            Assert.Equal(fields[4], hash);
        }

        Dictionary<string, string> expected = File.ReadAllLines(checksumPath)
            .Where(line => line.Length != 0)
            .Select(ParseChecksum)
            .ToDictionary(pair => pair.Path, pair => pair.Hash, StringComparer.Ordinal);
        string[] actualFiles = Directory.EnumerateFiles(recoveryRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.Equals(checksumPath, StringComparison.OrdinalIgnoreCase))
            .Select(path => Normalize(Path.GetRelativePath(recoveryRoot, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(actualFiles, expected.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.All(expected, pair => Assert.Equal(pair.Value, HashFile(BundlePath(recoveryRoot, pair.Key))));
    }

    private static void ValidateBundle(string bundleRoot, string manifestPath)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = manifest.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        string status = root.GetProperty("status").GetString()!;
        Assert.Contains(status, new[] { "succeeded", "failed" }, StringComparer.Ordinal);
        Assert.True(root.GetProperty("repositoryWasClean").GetBoolean());

        JsonElement[] commands = root.GetProperty("commands").EnumerateArray().ToArray();
        string[] names = commands.Select(command => command.GetProperty("name").GetString()!).ToArray();
        Assert.All(commands, command =>
        {
            Assert.True(File.Exists(BundlePath(bundleRoot, command.GetProperty("commandFile").GetString()!)));
            Assert.True(File.Exists(BundlePath(bundleRoot, command.GetProperty("outputFile").GetString()!)));
        });

        if (status == "succeeded")
        {
            Assert.Null(root.GetProperty("failure").GetString());
            Assert.All(RequiredCommands, required => Assert.Contains(required, names, StringComparer.Ordinal));
            Assert.All(commands, command => Assert.Equal(0, command.GetProperty("exitCode").GetInt32()));

            JsonElement coverage = root.GetProperty("coverage");
            Assert.True(coverage.GetProperty("lineRate").GetDecimal() >= 0.90m);
            Assert.True(coverage.GetProperty("branchRate").GetDecimal() >= 0.70m);
            string compressedCoverage = BundlePath(
                bundleRoot,
                coverage.GetProperty("compressedFile").GetString()!);
            Assert.True(File.Exists(compressedCoverage));
            Assert.Equal(
                coverage.GetProperty("uncompressedSha256").GetString(),
                InspectDecompressedGzip(compressedCoverage).Hash);
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("failure").GetString()));
            Assert.Contains(commands, command => command.GetProperty("exitCode").GetInt32() != 0);
        }

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

    private static (long Bytes, string Hash) InspectDecompressedGzip(string path)
    {
        using FileStream file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        long bytes = 0;
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) != 0)
        {
            hash.AppendData(buffer, 0, read);
            bytes += read;
        }

        return (bytes, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private static string BundlePath(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string RepositoryPath(params string[] segments) =>
        Path.Combine([RepositoryRoot(), .. segments]);

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
