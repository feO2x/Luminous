using System.Collections.Generic;
using System.Collections.Immutable;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Describes equivalent MIME type names for one format and that format's ordered file extensions.
/// </summary>
/// <remarks>
/// Put alternate names for the same format in <see cref="Aliases"/> so registry normalization can
/// replace them with <see cref="PrimaryMimeType"/>. Do not use aliases for containment or handler
/// compatibility: for example, an Office document is based on ZIP but is not an alias of
/// <c>application/zip</c>. Model that relationship with
/// <see cref="MimeTypeRegistryBuilder.AddParent(MimeType, MimeType)"/> instead.
/// </remarks>
public sealed class MimeTypeGroup
{
    /// <summary>
    /// Creates an equivalence group and preserves the supplied alias and extension order.
    /// </summary>
    /// <param name="primaryMimeType">The canonical value returned when any group member is normalized.</param>
    /// <param name="aliases">Alternate names for the same format, or <see langword="null"/> for none.</param>
    /// <param name="extensions">
    /// File extensions in preference order, or <see langword="null"/> for none. The first becomes
    /// <see cref="PrimaryExtension"/>.
    /// </param>
    /// <remarks>
    /// Membership and duplicate checks are deferred until <see cref="MimeTypeRegistryBuilder.Build"/>.
    /// </remarks>
    public MimeTypeGroup(
        MimeType primaryMimeType,
        IEnumerable<MimeType>? aliases = null,
        IEnumerable<FileExtension>? extensions = null
    )
    {
        PrimaryMimeType = primaryMimeType;
        Aliases = aliases?.ToImmutableArray() ?? [];
        Extensions = extensions?.ToImmutableArray() ?? [];
    }

    /// <summary>
    /// Gets the canonical MIME type returned when a primary or alias member is normalized.
    /// </summary>
    public MimeType PrimaryMimeType { get; }

    /// <summary>
    /// Gets the ordered alternate names that identify the same format as <see cref="PrimaryMimeType"/>.
    /// </summary>
    public ImmutableArray<MimeType> Aliases { get; }

    /// <summary>
    /// Gets the format's file extensions in descending preference order.
    /// </summary>
    /// <remarks>
    /// An extension may legitimately belong to several groups. Registry-wide preference for such an
    /// extension is configured separately with <see cref="MimeTypeRegistryBuilder.SetExtensionPreference(FileExtension, MimeType[])"/>.
    /// </remarks>
    public ImmutableArray<FileExtension> Extensions { get; }

    /// <summary>
    /// Gets the first preferred extension, or <see langword="null"/> when the format has no registered extension.
    /// </summary>
    public FileExtension? PrimaryExtension => Extensions.IsEmpty ? null : Extensions[0];
}
