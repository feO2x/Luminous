using System;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Represents a case-normalized file extension suitable for MIME type registry lookups.
/// </summary>
/// <remarks>
/// Values such as <c>pdf</c>, <c>.PDF</c>, and <c>*.Pdf</c> all normalize to <c>.pdf</c>.
/// Compound extensions such as <c>.tar.gz</c> are preserved. This type represents an extension, not
/// a file path or a general glob pattern.
/// </remarks>
public readonly struct FileExtension : IEquatable<FileExtension>
{
    private readonly string? _value;

    private FileExtension(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the normalized lowercase extension, including its leading dot.
    /// </summary>
    /// <value>An empty string when this instance is the default value.</value>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether this instance is the uninitialized default value.
    /// </summary>
    /// <remarks>A default value is not a valid file extension.</remarks>
    public bool IsDefault => _value is null;

    /// <summary>
    /// Parses and normalizes a simple or compound file extension.
    /// </summary>
    /// <param name="value">
    /// An extension in forms such as <c>pdf</c>, <c>.pdf</c>, or <c>*.tar.gz</c>.
    /// </param>
    /// <returns>The lowercase, dot-prefixed extension.</returns>
    /// <exception cref="FormatException">
    /// <paramref name="value" /> is empty, contains a path separator or whitespace, or is not a valid extension.
    /// </exception>
    public static FileExtension Parse(ReadOnlySpan<char> value) =>
        !TryParse(value, out var extension) ?
            throw new FormatException("The value is not a valid file extension.") :
            extension;

    /// <summary>
    /// Attempts to parse and normalize a simple or compound file extension.
    /// </summary>
    /// <param name="value">
    /// An extension in forms such as <c>pdf</c>, <c>.pdf</c>, or <c>*.tar.gz</c>.
    /// </param>
    /// <param name="extension">
    /// The lowercase, dot-prefixed extension when parsing succeeds; otherwise, the default value.
    /// </param>
    /// <returns><see langword="true" /> when <paramref name="value" /> is a valid extension.</returns>
    public static bool TryParse(ReadOnlySpan<char> value, out FileExtension extension)
    {
        extension = default;

        if (value.StartsWith("*.", StringComparison.Ordinal))
        {
            value = value[2..];
        }
        else if (!value.IsEmpty && value[0] == '.')
        {
            value = value[1..];
        }

        if (value.IsEmpty || value[0] == '.' || value[^1] == '.')
        {
            return false;
        }

        var previousWasDot = false;
        foreach (var character in value)
        {
            if (character == '.')
            {
                if (previousWasDot)
                {
                    return false;
                }

                previousWasDot = true;
                continue;
            }

            previousWasDot = false;
            if (!IsAsciiAlphaNumeric(character) && character is not '-' and not '_' and not '+')
            {
                return false;
            }
        }

        extension = new FileExtension(string.Concat(".", value.ToString().ToLowerInvariant()));
        return true;
    }

    /// <summary>
    /// Determines whether this instance and another instance contain the same normalized extension.
    /// </summary>
    /// <param name="other">The extension to compare with this instance.</param>
    /// <returns><see langword="true" /> when the normalized values are equal.</returns>
    public bool Equals(FileExtension other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FileExtension other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>
    /// Returns the normalized, dot-prefixed extension.
    /// </summary>
    /// <returns><see cref="Value" />.</returns>
    public override string ToString() => Value;

    /// <summary>
    /// Determines whether two instances contain the same normalized extension.
    /// </summary>
    /// <param name="left">The first extension.</param>
    /// <param name="right">The second extension.</param>
    /// <returns><see langword="true" /> when the normalized values are equal.</returns>
    public static bool operator ==(FileExtension left, FileExtension right) => left.Equals(right);

    /// <summary>
    /// Determines whether two instances contain different normalized extensions.
    /// </summary>
    /// <param name="left">The first extension.</param>
    /// <param name="right">The second extension.</param>
    /// <returns><see langword="true" /> when the normalized values differ.</returns>
    public static bool operator !=(FileExtension left, FileExtension right) => !left.Equals(right);

    private static bool IsAsciiAlphaNumeric(char value) =>
        value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
