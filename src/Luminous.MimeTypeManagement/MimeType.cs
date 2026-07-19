using System;

namespace Luminous.MimeTypeManagement;

public readonly struct MimeType : IEquatable<MimeType>
{
    private readonly string? _value;

    private MimeType(string value, string topLevelType, string subType, string? suffix)
    {
        _value = value;
        TopLevelType = topLevelType;
        SubType = subType;
        Suffix = suffix;
    }

    public string Value => _value ?? string.Empty;

    public string TopLevelType => field ?? string.Empty;

    public string SubType => field ?? string.Empty;

    public string Subtype => SubType;

    public string? Suffix { get; }

    public bool IsDefault => _value is null;

    public static MimeType Parse(ReadOnlySpan<char> value) => Parse(value, MimeTypeParseOptions.Default);

    public static MimeType Parse(ReadOnlySpan<char> value, MimeTypeParseOptions options) =>
        !TryParse(value, options, out var mimeType) ?
            throw new FormatException("The value is not a valid RFC 6838 media type name.") :
            mimeType;

    public static bool TryParse(ReadOnlySpan<char> value, out MimeType mimeType) =>
        TryParse(value, MimeTypeParseOptions.Default, out mimeType);

    public static bool TryParse(
        ReadOnlySpan<char> value,
        MimeTypeParseOptions options,
        out MimeType mimeType
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        mimeType = default;

        var mediaTypeLength = value.IndexOf(';');
        if (mediaTypeLength < 0)
        {
            mediaTypeLength = value.Length;
        }
        else
        {
            while (mediaTypeLength > 0 && IsOptionalWhitespace(value[mediaTypeLength - 1]))
            {
                mediaTypeLength--;
            }
        }

        var mediaType = value[..mediaTypeLength];
        var separatorIndex = mediaType.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex != mediaType.LastIndexOf('/'))
        {
            return false;
        }

        var topLevelLength = separatorIndex;
        var subTypeLength = mediaType.Length - separatorIndex - 1;
        if (subTypeLength <= 0 || topLevelLength > options.MaxNameLength || subTypeLength > options.MaxNameLength)
        {
            return false;
        }

        var topLevelType = mediaType[..separatorIndex];
        var subType = mediaType[(separatorIndex + 1)..];
        if (!IsRestrictedName(topLevelType) || !IsRestrictedName(subType))
        {
            return false;
        }

        var normalized = mediaType.ToString().ToLowerInvariant();
        var normalizedTopLevelType = normalized[..separatorIndex];
        var normalizedSubType = normalized[(separatorIndex + 1)..];
        var suffixIndex = normalizedSubType.LastIndexOf('+');
        var suffix = suffixIndex >= 0 && suffixIndex < normalizedSubType.Length - 1 ?
            normalizedSubType[(suffixIndex + 1)..] :
            null;

        mimeType = new MimeType(normalized, normalizedTopLevelType, normalizedSubType, suffix);
        return true;
    }

    public bool Equals(MimeType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is MimeType other && Equals(other);

    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    public override string ToString() => Value;

    public static bool operator ==(MimeType left, MimeType right) => left.Equals(right);

    public static bool operator !=(MimeType left, MimeType right) => !left.Equals(right);

    private static bool IsRestrictedName(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || !IsAsciiAlphaNumeric(value[0]))
        {
            return false;
        }

        foreach (var character in value[1..])
        {
            if (!IsAsciiAlphaNumeric(character) &&
                character is not '!' and
                             not '#' and
                             not '$' and
                             not '&' and
                             not '-' and
                             not '^' and
                             not '_' and
                             not '.' and
                             not '+')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiAlphaNumeric(char value) =>
        value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsOptionalWhitespace(char value) => value is ' ' or '\t';
}
