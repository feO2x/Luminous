namespace Luminous.MimeTypeManagement;

public static partial class DocumentSeed
{
    /// <summary>
    /// Adds the seed's office document groups — word processing, spreadsheets, presentations, PDF,
    /// eBooks, database, and Hangul formats — including the aliases, file extensions, and explicit
    /// parent edges defined for them.
    /// </summary>
    /// <param name="builder">The builder to add the category's groups to.</param>
    // ReSharper disable once MemberCanBePrivate.Global -- we want this to be a public API
    public static void AddOfficeFormats(MimeTypeRegistryBuilder builder)
    {
        // Word processing
        AddGroup(
            builder,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            extensions: [".docx"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-word.document.macroenabled.12",
            extensions: [".docm"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
            extensions: [".dotx"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-word.template.macroenabled.12",
            extensions: [".dotm"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/msword",
            aliases: ["application/x-msword"],
            extensions: [".doc", ".dot"],
            parent: "application/x-ole-storage"
        );
        AddGroup(
            builder,
            "application/vnd.oasis.opendocument.text",
            extensions: [".odt"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.apple.pages",
            aliases: ["application/x-iwork-pages-sffpages"],
            extensions: [".pages"],
            parent: "application/zip"
        );

        // Spreadsheets
        AddGroup(
            builder,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            extensions: [".xlsx"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-excel.sheet.macroenabled.12",
            extensions: [".xlsm"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-excel.sheet.binary.macroenabled.12",
            extensions: [".xlsb"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-excel.template.macroenabled.12",
            extensions: [".xltm"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
            extensions: [".xltx"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-excel.addin.macroenabled.12",
            extensions: [".xlam"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-excel",
            aliases: ["application/x-msexcel", "application/excel"],
            extensions: [".xls", ".xlt", ".xla"],
            parent: "application/x-ole-storage"
        );
        AddGroup(
            builder,
            "application/vnd.oasis.opendocument.spreadsheet",
            extensions: [".ods"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.apple.numbers",
            aliases: ["application/x-iwork-numbers-sffnumbers"],
            extensions: [".numbers"],
            parent: "application/zip"
        );

        // Presentations
        AddGroup(
            builder,
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            extensions: [".pptx"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-powerpoint.presentation.macroenabled.12",
            extensions: [".pptm"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
            extensions: [".ppsx"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.openxmlformats-officedocument.presentationml.template",
            extensions: [".potx"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-powerpoint.template.macroenabled.12",
            extensions: [".potm"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.ms-powerpoint",
            aliases: ["application/x-mspowerpoint", "application/powerpoint"],
            extensions: [".ppt", ".pot"],
            parent: "application/x-ole-storage"
        );
        AddGroup(
            builder,
            "application/vnd.oasis.opendocument.presentation",
            extensions: [".odp"],
            parent: "application/zip"
        );
        AddGroup(
            builder,
            "application/vnd.apple.keynote",
            aliases: ["application/x-iwork-keynote-sffkey"],
            extensions: [".key"],
            parent: "application/zip"
        );

        // PDF, eBooks, database, and Hangul
        AddGroup(builder, "application/pdf", aliases: ["application/x-pdf"], extensions: [".pdf"]);
        AddGroup(builder, "application/epub+zip", extensions: [".epub"]);
        AddGroup(builder, "application/x-fictionbook+xml", extensions: [".fb2"]);
        AddGroup(
            builder,
            "application/x-dbf",
            aliases: ["application/dbf", "application/dbase"],
            extensions: [".dbf"]
        );
        AddGroup(
            builder,
            "application/x-hwp",
            aliases: ["application/haansofthwp", "application/vnd.hancom.hwp"],
            extensions: [".hwp"],
            parent: "application/x-ole-storage"
        );
        AddGroup(
            builder,
            "application/hwp+zip",
            aliases: ["application/vnd.hancom.hwpx"],
            extensions: [".hwpx"]
        );
    }
}
