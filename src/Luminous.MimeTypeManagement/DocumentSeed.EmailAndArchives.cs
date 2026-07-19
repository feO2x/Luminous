namespace Luminous.MimeTypeManagement;

public static partial class DocumentSeed
{
    /// <summary>
    /// Adds the seed's email and archive groups, including the aliases, file extensions, and
    /// explicit parent edges defined for them.
    /// </summary>
    /// <param name="builder">The builder to add the category's groups to.</param>
    // ReSharper disable once MemberCanBePrivate.Global -- we want this to be a public API
    public static void AddEmailAndArchiveFormats(MimeTypeRegistryBuilder builder)
    {
        AddGroup(builder, "message/rfc822", extensions: [".eml"]);
        AddGroup(
            builder,
            "application/vnd.ms-outlook",
            extensions: [".msg"],
            parent: "application/x-ole-storage"
        );

        // PST is its own container format, not CFB — deliberately no OLE parent
        AddGroup(builder, "application/vnd.ms-outlook-pst", extensions: [".pst"]);
        AddGroup(
            builder,
            "application/zip",
            aliases: ["application/x-zip-compressed", "application/x-zip"],
            extensions: [".zip"]
        );
        AddGroup(builder, "application/x-tar", extensions: [".tar"]);
        AddGroup(
            builder,
            "application/gzip",
            aliases: ["application/x-gzip"],
            extensions: [".gz", ".tgz", ".tar.gz"]
        );
        AddGroup(builder, "application/x-7z-compressed", extensions: [".7z"]);
    }
}
