using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace JRPGPrototype.Data.SkillSystem;

public readonly record struct SemanticVersion : IComparable<SemanticVersion>
{
    private SemanticVersion(BigInteger major, BigInteger minor, BigInteger patch, string? preRelease, string? buildMetadata)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        BuildMetadata = buildMetadata;
    }

    public BigInteger Major { get; }
    public BigInteger Minor { get; }
    public BigInteger Patch { get; }
    public string? PreRelease { get; }
    public string? BuildMetadata { get; }

    public static SemanticVersion Parse(string value)
    {
        if (!TryParse(value, out SemanticVersion version))
        {
            throw new ArgumentException($"'{value}' is not a valid Semantic Version 2.0 value.", nameof(value));
        }

        return version;
    }

    public static bool TryParse([NotNullWhen(true)] string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        int buildSeparator = value.IndexOf('+');
        if (buildSeparator != value.LastIndexOf('+'))
        {
            return false;
        }

        string coreAndPreRelease = buildSeparator < 0 ? value : value[..buildSeparator];
        string? buildMetadata = buildSeparator < 0 ? null : value[(buildSeparator + 1)..];
        if (buildMetadata is not null && !ValidIdentifiers(buildMetadata, allowLeadingZeroes: true))
        {
            return false;
        }

        int preReleaseSeparator = coreAndPreRelease.IndexOf('-');
        string core = preReleaseSeparator < 0
            ? coreAndPreRelease
            : coreAndPreRelease[..preReleaseSeparator];
        string? preRelease = preReleaseSeparator < 0
            ? null
            : coreAndPreRelease[(preReleaseSeparator + 1)..];
        if (preRelease is not null && !ValidIdentifiers(preRelease, allowLeadingZeroes: false))
        {
            return false;
        }

        string[] parts = core.Split('.');
        if (parts.Length != 3 ||
            !TryParseCoreNumber(parts[0], out BigInteger major) ||
            !TryParseCoreNumber(parts[1], out BigInteger minor) ||
            !TryParseCoreNumber(parts[2], out BigInteger patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, preRelease, buildMetadata);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        int result = Major.CompareTo(other.Major);
        if (result != 0) return result;
        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;
        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;
        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public override string ToString()
    {
        string value = string.Join('.',
            Major.ToString(CultureInfo.InvariantCulture),
            Minor.ToString(CultureInfo.InvariantCulture),
            Patch.ToString(CultureInfo.InvariantCulture));
        if (PreRelease is not null) value += $"-{PreRelease}";
        if (BuildMetadata is not null) value += $"+{BuildMetadata}";
        return value;
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    private static bool TryParseCoreNumber(string value, out BigInteger number)
    {
        number = 0;
        return value.Length > 0 &&
               (value.Length == 1 || value[0] != '0') &&
               value.All(char.IsAsciiDigit) &&
               BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static bool ValidIdentifiers(string value, bool allowLeadingZeroes)
    {
        string[] identifiers = value.Split('.');
        foreach (string identifier in identifiers)
        {
            if (identifier.Length == 0 || identifier.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                return false;
            }

            if (!allowLeadingZeroes && identifier.All(char.IsAsciiDigit) &&
                identifier.Length > 1 && identifier[0] == '0')
            {
                return false;
            }
        }

        return true;
    }

    private static int ComparePreRelease(string? left, string? right)
    {
        if (left is null) return right is null ? 0 : 1;
        if (right is null) return -1;

        string[] leftParts = left.Split('.');
        string[] rightParts = right.Split('.');
        for (int index = 0; index < Math.Min(leftParts.Length, rightParts.Length); index++)
        {
            string leftPart = leftParts[index];
            string rightPart = rightParts[index];
            bool leftNumeric = leftPart.All(char.IsAsciiDigit);
            bool rightNumeric = rightPart.All(char.IsAsciiDigit);
            int result;
            if (leftNumeric && rightNumeric)
            {
                result = CompareNumericIdentifier(leftPart, rightPart);
            }
            else if (leftNumeric != rightNumeric)
            {
                result = leftNumeric ? -1 : 1;
            }
            else
            {
                result = string.CompareOrdinal(leftPart, rightPart);
            }

            if (result != 0) return result;
        }

        return leftParts.Length.CompareTo(rightParts.Length);
    }

    private static int CompareNumericIdentifier(string left, string right)
    {
        int lengthResult = left.Length.CompareTo(right.Length);
        return lengthResult != 0 ? lengthResult : string.CompareOrdinal(left, right);
    }
}
