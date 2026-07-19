using System;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Configures the input-length guard used while parsing MIME type names.
/// </summary>
/// <remarks>
/// The limit is applied independently to the top-level type and subtype before strings are allocated.
/// Keep the RFC default for untrusted <c>Content-Type</c> input unless an integration has a documented
/// need for longer, non-standard names.
/// </remarks>
public sealed class MimeTypeParseOptions
{
    /// <summary>
    /// The RFC 6838 maximum length of either the top-level type or subtype component.
    /// </summary>
    public const int RfcMaxNameLength = 127;

    /// <summary>
    /// Gets the shared options instance that enforces the RFC 6838 limit of 127 characters per component.
    /// </summary>
    public static MimeTypeParseOptions Default { get; } = new ();

    /// <summary>
    /// Gets the maximum permitted length of each component in a MIME type name.
    /// </summary>
    /// <value>A positive number of characters; the default is <see cref="RfcMaxNameLength"/>.</value>
    /// <exception cref="ArgumentOutOfRangeException">The assigned value is less than one.</exception>
    public int MaxNameLength
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = RfcMaxNameLength;
}
