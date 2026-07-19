using System.Collections.Generic;
using System.Collections.Immutable;

namespace Luminous.MimeTypeManagement;

public sealed class MimeTypeGroup
{
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

    public MimeType PrimaryMimeType { get; }

    public ImmutableArray<MimeType> Aliases { get; }

    public ImmutableArray<FileExtension> Extensions { get; }

    public FileExtension? PrimaryExtension => Extensions.IsEmpty ? null : Extensions[0];
}
