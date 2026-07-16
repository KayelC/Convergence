using System.Diagnostics.CodeAnalysis;

namespace Convergence.Content;

/// <summary>Identifies a local vocabulary value or a pack-qualified content record.</summary>
public readonly record struct ContentId
{
    private readonly string? _value;

    public ContentId(string value)
    {
        _value = Normalize(value);
    }

    public string Value => _value ?? string.Empty;

    public bool IsEmpty => _value is null;

    public bool IsValid => !IsEmpty;

    public bool IsQualified => IsValid && Value.Contains(':', StringComparison.Ordinal);

    public static ContentId Parse(string value) => new(value);

    public static bool TryParse(string? value, out ContentId contentId)
    {
        if (value is null)
        {
            contentId = default;
            return false;
        }

        try
        {
            contentId = new ContentId(value);
            return true;
        }
        catch (ArgumentException)
        {
            contentId = default;
            return false;
        }
    }

    public override string ToString() => Value;

    private static string Normalize([NotNull] string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Content ID cannot be empty.", nameof(value));
        }

        string normalized = value.Trim().ToLowerInvariant();
        int separator = normalized.IndexOf(':');

        if (separator != normalized.LastIndexOf(':'))
        {
            throw new ArgumentException("Content ID may contain at most one namespace separator.", nameof(value));
        }

        if (separator < 0)
        {
            ValidateLocalId(normalized, nameof(value));
            return normalized;
        }

        string packId = normalized[..separator];
        string localId = normalized[(separator + 1)..];
        ValidatePackId(packId, nameof(value));
        ValidateLocalId(localId, nameof(value));
        return $"{packId}:{localId}";
    }

    private static void ValidatePackId(string packId, string parameterName)
    {
        if (packId.Length == 0)
        {
            throw new ArgumentException("Qualified content ID is missing its pack ID.", parameterName);
        }

        foreach (string segment in packId.Split('.'))
        {
            ValidateLocalId(segment, parameterName);
        }
    }

    private static void ValidateLocalId(string localId, string parameterName)
    {
        if (localId.Length == 0)
        {
            throw new ArgumentException("Content ID is missing its local ID.", parameterName);
        }

        string[] segments = localId.Split('_');
        if (segments.Any(segment => segment.Length == 0))
        {
            throw new ArgumentException("Content ID must use lower_snake_case without empty segments.", parameterName);
        }

        foreach (char character in localId)
        {
            bool valid = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_';
            if (!valid)
            {
                throw new ArgumentException("Content ID contains an invalid character.", parameterName);
            }
        }
    }
}
