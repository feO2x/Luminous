using System;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Represents an RFC 6838 media type name, normalized for reliable comparison and lookup.
/// </summary>
/// <remarks>
/// <para>
/// Parsing is case-insensitive and stores the result in lowercase. Parameters such as
/// <c>charset=utf-8</c> are accepted but discarded, so they do not affect identity. Use this type when
/// a complete content type must be reduced to its media type before registry lookup.
/// </para>
/// <para>
/// Wildcard ranges such as <c>image/*</c> are not media type names and are therefore rejected. The
/// default value is an invalid sentinel; create usable values with <see cref="Parse(ReadOnlySpan{char})" />
/// or <see cref="TryParse(ReadOnlySpan{char}, out MimeType)" />.
/// </para>
/// <example>
/// <code>
/// var mimeType = MimeType.Parse("Application/Vnd.Example+JSON; charset=utf-8");
/// // mimeType.Value == "application/vnd.example+json"
/// // mimeType.Suffix == "json"
/// </code>
/// </example>
/// </remarks>
public readonly struct MimeType : IEquatable<MimeType>
{
    /// <summary>
    /// The message of the <see cref="FormatException" /> thrown when a value is not a valid RFC 6838
    /// media type name.
    /// </summary>
    /// <remarks>
    /// Both <see cref="Parse(ReadOnlySpan{char}, MimeTypeParseOptions)" /> and
    /// <see cref="MimeTypeRegistry.Normalize(ReadOnlySpan{char}, bool)" /> use this message.
    /// </remarks>
    public const string InvalidMediaTypeNameMessage = "The value is not a valid RFC 6838 media type name.";

    private readonly string? _value;

    private MimeType(string value, string topLevelType, string subType, string? suffix)
    {
        _value = value;
        TopLevelType = topLevelType;
        SubType = subType;
        Suffix = suffix;
    }

    /// <summary>
    /// Gets the normalized lowercase <c>type/subtype</c> name without parameters.
    /// </summary>
    /// <value>An empty string when this instance is the default value.</value>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Gets the component before the slash, such as <c>application</c> or <c>image</c>.
    /// </summary>
    public string TopLevelType => field ?? string.Empty;

    /// <summary>
    /// Gets the component after the slash, including any structured-syntax suffix.
    /// </summary>
    /// <example><c>vnd.example+json</c> for <c>application/vnd.example+json</c>.</example>
    public string SubType => field ?? string.Empty;

    /// <summary>
    /// Gets the segment after the final <c>+</c> in the subtype, or <see langword="null" /> when no
    /// structured-syntax suffix is present.
    /// </summary>
    public string? Suffix { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is the uninitialized default value.
    /// </summary>
    /// <remarks>A default value is not a valid MIME type and cannot be normalized by a registry.</remarks>
    public bool IsDefault => _value is null;

    /// <summary>
    /// Parses and normalizes a media type name using the RFC 6838 component-length limit.
    /// </summary>
    /// <param name="value">
    /// A <c>type/subtype</c> name, optionally followed by parameters. ASCII casing is ignored.
    /// </param>
    /// <returns>The normalized media type without parameters.</returns>
    /// <exception cref="FormatException"><paramref name="value" /> is not a valid RFC 6838 media type name.</exception>
    public static MimeType Parse(ReadOnlySpan<char> value) => Parse(value, MimeTypeParseOptions.Default);

    /// <summary>
    /// Parses and normalizes a media type name using a caller-defined component-length limit.
    /// </summary>
    /// <param name="value">
    /// A <c>type/subtype</c> name, optionally followed by parameters. ASCII casing is ignored.
    /// </param>
    /// <param name="options">The parsing limits to enforce.</param>
    /// <returns>The normalized media type without parameters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="FormatException"><paramref name="value" /> is not valid under the supplied options.</exception>
    public static MimeType Parse(ReadOnlySpan<char> value, MimeTypeParseOptions options) =>
        !TryParse(value, options, out var mimeType) ?
            throw new FormatException(InvalidMediaTypeNameMessage) :
            mimeType;

    /// <summary>
    /// Attempts to parse and normalize a media type name using the RFC 6838 component-length limit.
    /// </summary>
    /// <param name="value">
    /// A <c>type/subtype</c> name, optionally followed by parameters. ASCII casing is ignored.
    /// </param>
    /// <param name="mimeType">
    /// The normalized media type when parsing succeeds; otherwise, the default value.
    /// </param>
    /// <returns><see langword="true" /> when <paramref name="value" /> is valid; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(ReadOnlySpan<char> value, out MimeType mimeType) =>
        TryParse(value, MimeTypeParseOptions.Default, out mimeType);

    /// <summary>
    /// Attempts to parse and normalize a media type name using a caller-defined component-length limit.
    /// </summary>
    /// <param name="value">
    /// A <c>type/subtype</c> name, optionally followed by parameters. ASCII casing is ignored.
    /// </param>
    /// <param name="options">The parsing limits to enforce.</param>
    /// <param name="mimeType">
    /// The normalized media type when parsing succeeds; otherwise, the default value.
    /// </param>
    /// <returns><see langword="true" /> when <paramref name="value" /> is valid; otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    public static bool TryParse(
        ReadOnlySpan<char> value,
        MimeTypeParseOptions options,
        out MimeType mimeType
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        mimeType = default;

        var mediaType = TrimMediaType(value);
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

    /// <summary>
    /// Determines whether this instance and another instance contain the same normalized media type.
    /// </summary>
    /// <param name="other">The media type to compare with this instance.</param>
    /// <returns><see langword="true" /> when the normalized values are equal.</returns>
    public bool Equals(MimeType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MimeType other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>
    /// Returns the normalized media type name without parameters.
    /// </summary>
    /// <returns><see cref="Value" />.</returns>
    public override string ToString() => Value;

    /// <summary>
    /// Determines whether two instances contain the same normalized media type.
    /// </summary>
    /// <param name="left">The first media type.</param>
    /// <param name="right">The second media type.</param>
    /// <returns><see langword="true" /> when the normalized values are equal.</returns>
    public static bool operator ==(MimeType left, MimeType right) => left.Equals(right);

    /// <summary>
    /// Determines whether two instances contain different normalized media types.
    /// </summary>
    /// <param name="left">The first media type.</param>
    /// <param name="right">The second media type.</param>
    /// <returns><see langword="true" /> when the normalized values differ.</returns>
    public static bool operator !=(MimeType left, MimeType right) => !left.Equals(right);

    /// <summary>
    /// Returns the media type portion of a content type value, without parameters or trailing
    /// optional whitespace.
    /// </summary>
    /// <param name="value">A content type value, such as <c>text/html; charset=utf-8</c>.</param>
    /// <returns>
    /// The part before the first <c>;</c>, trimmed with <see cref="TrimTrailingWhiteSpace" />. When
    /// <paramref name="value" /> contains no parameters, it is returned trimmed as a whole.
    /// </returns>
    public static ReadOnlySpan<char> TrimMediaType(ReadOnlySpan<char> value)
    {
        var parameterIndex = value.IndexOf(';');
        return TrimTrailingWhiteSpace(parameterIndex < 0 ? value : value[..parameterIndex]);
    }

    /// <summary>
    /// Removes trailing SP and HTAB characters (the RFC 7230 OWS production) from a span.
    /// </summary>
    /// <param name="value">The span to trim.</param>
    /// <returns>The span without trailing SP or HTAB characters.</returns>
    // ReSharper disable once MemberCanBePrivate.Global -- we want this to be a public API
    public static ReadOnlySpan<char> TrimTrailingWhiteSpace(ReadOnlySpan<char> value) =>
        // Trim only SP and HTAB via TrimEnd(" \t") instead of the parameterless TrimEnd(), which
        // would also strip '\r', '\n', U+00A0, and every other char.IsWhiteSpace character. Those
        // are invalid in a media type name and must remain in place so that IsRestrictedName
        // rejects the value instead of silently accepting it.
        value.TrimEnd(" \t");

    /// <summary>
    /// Determines whether a value is a valid RFC 6838 structured-syntax suffix name.
    /// </summary>
    /// <param name="suffix">The suffix to validate, without a leading <c>+</c>.</param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="suffix" /> is a non-empty, valid suffix;
    /// otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsValidSuffix(string? suffix) =>
        !string.IsNullOrEmpty(suffix) &&
        TryParse($"application/x+{suffix}", out var mimeType) &&
        string.Equals(mimeType.Suffix, suffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsRestrictedName(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (var character in value[1..])
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
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
}
