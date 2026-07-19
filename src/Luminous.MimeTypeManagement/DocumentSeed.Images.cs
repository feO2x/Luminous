namespace Luminous.MimeTypeManagement;

public static partial class DocumentSeed
{
    /// <summary>
    /// Adds the seed's image groups — raster, JPEG 2000, JBIG2, portable anymap, HEIF/HEIC, AVIF,
    /// and SVG formats — including the aliases, file extensions, and explicit parent edges defined
    /// for them.
    /// </summary>
    /// <param name="builder">The builder to add the category's groups to.</param>
    // ReSharper disable once MemberCanBePrivate.Global -- we want this to be a public API
    public static void AddImageFormats(MimeTypeRegistryBuilder builder)
    {
        AddGroup(builder, "image/png", aliases: ["image/x-png"], extensions: [".png"]);
        AddGroup(
            builder,
            "image/jpeg",
            aliases: ["image/pjpeg", "image/jpg"],
            extensions: [".jpg", ".jpeg"]
        );
        AddGroup(builder, "image/gif", extensions: [".gif"]);
        AddGroup(builder, "image/webp", extensions: [".webp"]);
        AddGroup(
            builder,
            "image/bmp",
            aliases: ["image/x-ms-bmp", "image/x-bmp"],
            extensions: [".bmp"]
        );
        AddGroup(builder, "image/tiff", extensions: [".tiff", ".tif"]);
        AddGroup(builder, "image/jp2", extensions: [".jp2"]);
        AddGroup(builder, "image/jpx", extensions: [".jpx"]);
        AddGroup(builder, "image/jpm", extensions: [".jpm"]);

        // Motion JPEG 2000 is registered under video/ in RFC 3745
        AddGroup(builder, "video/mj2", extensions: [".mj2"]);
        AddGroup(builder, "image/x-jbig2", extensions: [".jbig2", ".jb2"]);
        AddGroup(builder, "image/x-portable-anymap", extensions: [".pnm"]);
        AddGroup(
            builder,
            "image/x-portable-bitmap",
            extensions: [".pbm"],
            parent: "image/x-portable-anymap"
        );
        AddGroup(
            builder,
            "image/x-portable-graymap",
            extensions: [".pgm"],
            parent: "image/x-portable-anymap"
        );
        AddGroup(
            builder,
            "image/x-portable-pixmap",
            extensions: [".ppm"],
            parent: "image/x-portable-anymap"
        );
        AddGroup(builder, "image/heif", extensions: [".heif"]);

        // HEIC is HEIF with HEVC coding
        AddGroup(builder, "image/heic", extensions: [".heic"], parent: "image/heif");
        AddGroup(builder, "image/heic-sequence", extensions: [".heics"]);

        // AVC-coded HEIF image sequence (ISO/IEC 23008-12), not part of the AVIF family
        AddGroup(builder, "image/avcs", extensions: [".avcs"]);
        AddGroup(builder, "image/avif", extensions: [".avif"]);
        AddGroup(builder, "image/svg+xml", extensions: [".svg"]);
    }
}
