using System.Text.RegularExpressions;
using Xunit;

namespace Convergence.Framework.Tests.Architecture;

public sealed class TerminologyBoundaryTests
{
    private static readonly Regex IdentifierRegex = new(
        "[A-Za-z][A-Za-z0-9_]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IdentifierPartRegex = new(
        "[A-Z]+(?=[A-Z][a-z]|[0-9]|$)|[A-Z]?[a-z]+|[A-Z]+|[0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] ActiveDirectories = ["src", "samples", "tests", "content", "docs"];

    private static readonly HashSet<string> ScannedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".json",
        ".md",
        ".props",
        ".sln",
        ".targets"
    };

    private static readonly IdentifierRule[] IdentifierRules =
    [
        Rule("retired turn-economy family", "Press", "Turn"),
        Rule("retired owned-actor role", Join("Per", "sona")),
        Rule("retired deployable-actor role", Join("De", "mon")),
        Rule("retired currency name", Join("Mac", "ca")),
        Rule("retired catalyst family", Join("Mita", "ma")),
        Rule("retired location name", Join("Tar", "tarus")),
        Rule("retired battle-exit name", Join("Trae", "sto")),
        Rule("retired field-exit name", Join("Go", "ho")),
        Rule("direct franchise reference", Join("Noc", "turne")),
        Rule("direct franchise reference", Join("At", "lus")),
        Rule("retired fixture name", Join("Or", "pheus")),
        Rule("retired fixture name", Join("Pix", "ie")),
        Rule("retired fixture name", Join("The", "bel")),
        Rule("retired fixture name", Join("Paulo", "wnia")),
        Rule("retired fixture name", Join("Ae", "ros")),
        Rule("retired skill name", Join("Mar", "a", "gi")),
        Rule("retired skill name", Join("A", "gi")),
        Rule("retired skill name", Join("D", "ia")),
        Rule("retired skill name", Join("En", "dure")),
        Rule("retired fixture name", Join("Sli", "me")),
        Rule("retired fixture name", "Jack", "Frost"),
        Rule("direct franchise reference", "Shin", "Megami", "Tensei"),
        Rule("retired roster contract", "Runtime", "Party", "Stock"),
        Rule("retired roster contract", "Runtime", Join("Fo", "rm"), "Stock"),
        Rule("retired active-owned-actor contract", "Active", Join("Fo", "rm")),
        Rule("retired turn-economy property", "Full", "Icons"),
        Rule("retired turn-economy property", "Blinking", "Icons"),
        Rule("retired lifecycle outcome", "Return", "To", "Stock"),
        Rule("retired capacity policy", "Stock", "Capacity", "Policy"),
        Rule("retired roster kind", "Runtime", "Stock", "Kind"),
        Rule("retired roster diagnostic", "Stock", "Placement", "Rejected"),
        Rule("retired roster helper", "Has", "Open", "Stock", "Slot"),
        Rule("retired roster diagnostic", "Stock", "Full"),
        Rule("retired restoration diagnostic", "Missing", "Parent", Join("Fo", "rm")),
        Rule("retired restoration diagnostic", "Duplicate", "Actor", Join("Fo", "rm"), "Reference"),
        Rule("retired lunar-gate diagnostic", "Moon", "Blocked"),
        CaseSensitiveRule("retired actor kind", Join("Hu", "man")),
        CaseSensitiveRule("retired actor kind", Join("Oper", "ator")),
        CaseSensitiveRule("direct franchise reference", Join("S", "M", "T")),
        CaseSensitiveRule("retired fixture name", Join("S", "E", "E", "S"))
    ];

    private static readonly IReadOnlyDictionary<string, IdentifierRule[]> IdentifierRulesByFirstSegment =
        IdentifierRules
            .GroupBy(rule => rule.Segments[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

    private static readonly TextRule[] TextRules =
    [
        Phrase("retired turn-economy name", "Press", "Turn"),
        Phrase("direct franchise reference", "Shin", "Megami", "Tensei"),
        Phrase("retired fixture name", "Jack", "Frost"),
        Phrase("retired fixture name", Join("Go", "ho", "-M")),
        Phrase("retired fixture name", Join("Trae", "sto"), "Gem"),
        Phrase("retired fixture name", Join("Paulo", "wnia"), "Blacksmith"),
        Phrase("retired fixture name", "School", "Uniform"),
        Phrase("retired fixture name", "Childlike", "Negotiation"),
        Phrase("retired fixture phrase", Join("Hee", "-ho")),
        Lexical("retired command wire value", Join("switch_", "fo", "rm")),
        Lexical("retired roster diagnostic wire value", Join("stock_entry_", "not_found")),
        Lexical("retired fixture ID", Join("school_uniform_", "sample")),
        Lexical("retired fixture ID", Join("childlike_", "sample")),
        Lexical("retired fixture ID", Join("legacy_455f", "736c696d65")),
        Quoted("retired actor-kind wire value", Join("hu", "man")),
        Quoted("retired actor-kind wire value", Join("oper", "ator")),
        Quoted("retired owned-actor wire value", Join("fo", "rm"))
    ];

    [Fact]
    public void ActiveProduct_ContainsNoRetiredTerminology()
    {
        Finding[] findings = ActiveFiles()
            .SelectMany(ScanFile)
            .OrderBy(finding => finding.RelativePath, StringComparer.Ordinal)
            .ThenBy(finding => finding.Line)
            .ThenBy(finding => finding.Column)
            .ToArray();

        Assert.True(
            findings.Length == 0,
            "Retired terminology remains in the active product:" + Environment.NewLine +
            string.Join(Environment.NewLine, findings.Select(Format)));
    }

    [Fact]
    public void Scanner_DetectsRetiredSymbolsWireValuesFixturesAndDirectReferences()
    {
        string source = string.Join(
            Environment.NewLine,
            $"public sealed class {Join("Press", "Turn", "Engine")} {{ }}",
            $"var roster = \"{Join("per", "sona", "_", "stock")}\";",
            $"var fixture = \"{Join("jack", "_", "frost")}\";",
            $"// {Join("Shin", " ", "Megami", " ", "Tensei")}",
            $"var kind = \"{Join("oper", "ator")}\";");

        Finding[] findings = ScanText("sample.cs", source).ToArray();

        Assert.Contains(findings, finding => finding.Label == "retired turn-economy family");
        Assert.Contains(findings, finding => finding.Label == "retired owned-actor role");
        Assert.Contains(findings, finding => finding.Label == "retired fixture name");
        Assert.Contains(findings, finding => finding.Label == "direct franchise reference");
        Assert.Contains(findings, finding => finding.Label == "retired actor-kind wire value");
    }

    [Fact]
    public void Scanner_AllowsIncidentalWordsApprovedVocabularyAndRetailStock()
    {
        const string source = """
            Personality and demonstration remain ordinary words in formulas.
            Retail shop stock and limited stock are valid inventory concepts.
            Almighty and Ice Boost are approved generic example vocabulary.
            A host application may publish diagnostics for a hosted entity.
            """;

        Assert.Empty(ScanText("allowed.md", source));
    }

    [Fact]
    public void ScanPolicy_IncludesActiveProductAndExcludesArchiveAndBuildOutputs()
    {
        Assert.True(ShouldScanRelativePath("src/Convergence.Framework/Battle/Action.cs"));
        Assert.True(ShouldScanRelativePath("samples/Convergence.DemoHost/Program.cs"));
        Assert.True(ShouldScanRelativePath("tests/Convergence.Framework.Tests/Rules.cs"));
        Assert.True(ShouldScanRelativePath("content/original/sample.json"));
        Assert.True(ShouldScanRelativePath("docs/architecture.md"));
        Assert.True(ShouldScanRelativePath("README.md"));

        Assert.False(ShouldScanRelativePath("ArchiveDocs/LegacyFramework/old.cs"));
        Assert.False(ShouldScanRelativePath("src/Convergence.Framework/bin/Debug/output.json"));
        Assert.False(ShouldScanRelativePath("tests/Convergence.Framework.Tests/obj/cache.cs"));
        Assert.False(ShouldScanRelativePath("src/Convergence.Framework/readme.txt"));
    }

    [Fact]
    public void Scanner_ExaminesActiveRelativePaths()
    {
        string retiredPath = $"content/demos/{Join("per", "sona", "_sample")}.json";

        Finding finding = Assert.Single(ScanLine(retiredPath, 0, retiredPath));

        Assert.Equal(0, finding.Line);
        Assert.Equal("retired owned-actor role", finding.Label);
    }

    [Fact]
    public void Scanner_ReportsDeterministicOneBasedLocations()
    {
        string source = "safe" + Environment.NewLine + Join("Mac", "ca") + " balance";

        Finding finding = Assert.Single(ScanText("sample.md", source));

        Assert.Equal(2, finding.Line);
        Assert.Equal(1, finding.Column);
        Assert.Equal("retired currency name", finding.Label);
    }

    private static IEnumerable<string> ActiveFiles()
    {
        string root = RepositoryRoot();
        IEnumerable<string> nestedFiles = ActiveDirectories
            .Select(directory => Path.Combine(root, directory))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories));
        IEnumerable<string> rootFiles = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly);

        return nestedFiles
            .Concat(rootFiles)
            .Where(file => ShouldScanRelativePath(Path.GetRelativePath(root, file)))
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static IEnumerable<Finding> ScanFile(string path)
    {
        string relativePath = Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/');
        foreach (Finding finding in ScanLine(relativePath, 0, relativePath))
        {
            yield return finding;
        }

        foreach (Finding finding in ScanText(relativePath, File.ReadAllText(path)))
        {
            yield return finding;
        }
    }

    private static IEnumerable<Finding> ScanText(string relativePath, string text)
    {
        using StringReader reader = new(text);
        int lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            foreach (Finding finding in ScanLine(relativePath, lineNumber, line))
            {
                yield return finding;
            }
        }
    }

    private static IEnumerable<Finding> ScanLine(string relativePath, int lineNumber, string line)
    {
        List<Finding> findings = [];
        foreach (Match identifier in IdentifierRegex.Matches(line))
        {
            IdentifierPart[] parts = IdentifierPartRegex
                .Matches(identifier.Value)
                .Select(match => new IdentifierPart(match.Value, identifier.Index + match.Index, match.Length))
                .ToArray();

            for (int start = 0; start < parts.Length; start++)
            {
                if (!IdentifierRulesByFirstSegment.TryGetValue(parts[start].Value, out IdentifierRule[]? rules))
                {
                    continue;
                }

                foreach (IdentifierRule rule in rules)
                {
                    if (start > parts.Length - rule.Segments.Length ||
                        !SegmentsMatch(parts, start, rule))
                    {
                        continue;
                    }

                    IdentifierPart first = parts[start];
                    IdentifierPart last = parts[start + rule.Segments.Length - 1];
                    int length = last.Index + last.Length - first.Index;
                    findings.Add(new Finding(
                        relativePath,
                        lineNumber,
                        first.Index + 1,
                        rule.Label,
                        line.Substring(first.Index, length)));
                }
            }
        }

        foreach (TextRule rule in TextRules)
        {
            foreach (Match match in rule.Pattern.Matches(line))
            {
                Group value = match.Groups["value"];
                findings.Add(new Finding(
                    relativePath,
                    lineNumber,
                    value.Index + 1,
                    rule.Label,
                    value.Value));
            }
        }

        return findings
            .DistinctBy(finding => (finding.Column, finding.Label, finding.MatchedText))
            .OrderBy(finding => finding.Column)
            .ThenBy(finding => finding.Label, StringComparer.Ordinal);
    }

    private static bool SegmentsMatch(IdentifierPart[] parts, int start, IdentifierRule rule)
    {
        StringComparison comparison = rule.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        for (int index = 0; index < rule.Segments.Length; index++)
        {
            if (!parts[start + index].Value.Equals(rule.Segments[index], comparison))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldScanRelativePath(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment =>
                segment.Equals("ArchiveDocs", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!ScannedExtensions.Contains(Path.GetExtension(normalized)))
        {
            return false;
        }

        return segments.Length == 1 ||
            ActiveDirectories.Contains(segments[0], StringComparer.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "Convergence.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        Assert.NotNull(current);
        return current!;
    }

    private static IdentifierRule Rule(string label, params string[] segments) =>
        new(label, segments, false);

    private static IdentifierRule CaseSensitiveRule(string label, params string[] segments) =>
        new(label, segments, true);

    private static TextRule Phrase(string label, params string[] words)
    {
        string body = string.Join(@"\s+", words.Select(Regex.Escape));
        return new TextRule(label, CreateTextRegex(
            $@"(?<![A-Za-z0-9_])(?<value>{body})(?![A-Za-z0-9_])"));
    }

    private static TextRule Lexical(string label, string value) =>
        new(
            label,
            CreateTextRegex($@"(?<![A-Za-z0-9_])(?<value>{Regex.Escape(value)})(?![A-Za-z0-9_])"));

    private static TextRule Quoted(string label, string value) =>
        new(
            label,
            CreateTextRegex($@"[""'](?<value>{Regex.Escape(value)})[""']"));

    private static Regex CreateTextRegex(string pattern) =>
        new(
            pattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static string Join(params string[] parts) => string.Concat(parts);

    private static string Format(Finding finding) =>
        finding.Line == 0
            ? $"{finding.RelativePath}:path:{finding.Column} [{finding.Label}] {finding.MatchedText}"
            : $"{finding.RelativePath}:{finding.Line}:{finding.Column} [{finding.Label}] {finding.MatchedText}";

    private sealed record IdentifierRule(string Label, string[] Segments, bool CaseSensitive);

    private sealed record TextRule(string Label, Regex Pattern);

    private sealed record IdentifierPart(string Value, int Index, int Length);

    private sealed record Finding(
        string RelativePath,
        int Line,
        int Column,
        string Label,
        string MatchedText);
}
