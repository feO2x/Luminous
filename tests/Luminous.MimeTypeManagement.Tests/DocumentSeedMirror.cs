using System.Collections.Generic;
using System.Linq;

namespace Luminous.MimeTypeManagement.Tests;

/// <summary>
/// A second, independent transcription of the group table in ai-plans/0003-document-seed.md.
/// The mirror test asserts that <see cref="DocumentSeed" /> matches this static data exactly, so a
/// transcription error must be made identically in both places to go undetected.
/// </summary>
public static class DocumentSeedMirror
{
    public const int ExpectedGroupCount = 90;

    public static IReadOnlyList<(string Primary, string[] Aliases, string[] Extensions, string[] Parents)> Rows { get; }
        =
        [
            // Office — word processing
            ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [], [".docx"],
             ["application/zip"]),
            ("application/vnd.ms-word.document.macroenabled.12", [], [".docm"], ["application/zip"]),
            ("application/vnd.openxmlformats-officedocument.wordprocessingml.template", [], [".dotx"],
             ["application/zip"]),
            ("application/vnd.ms-word.template.macroenabled.12", [], [".dotm"], ["application/zip"]),
            ("application/msword", ["application/x-msword"], [".doc", ".dot"], ["application/x-ole-storage"]),
            ("application/vnd.oasis.opendocument.text", [], [".odt"], ["application/zip"]),
            ("application/vnd.apple.pages", ["application/x-iwork-pages-sffpages"], [".pages"], ["application/zip"]),

            // Office — spreadsheets
            ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [], [".xlsx"], ["application/zip"]),
            ("application/vnd.ms-excel.sheet.macroenabled.12", [], [".xlsm"], ["application/zip"]),
            ("application/vnd.ms-excel.sheet.binary.macroenabled.12", [], [".xlsb"], ["application/zip"]),
            ("application/vnd.ms-excel.template.macroenabled.12", [], [".xltm"], ["application/zip"]),
            ("application/vnd.openxmlformats-officedocument.spreadsheetml.template", [], [".xltx"],
             ["application/zip"]),
            ("application/vnd.ms-excel.addin.macroenabled.12", [], [".xlam"], ["application/zip"]),
            ("application/vnd.ms-excel", ["application/x-msexcel", "application/excel"], [".xls", ".xlt", ".xla"],
             ["application/x-ole-storage"]),
            ("application/vnd.oasis.opendocument.spreadsheet", [], [".ods"], ["application/zip"]),
            ("application/vnd.apple.numbers", ["application/x-iwork-numbers-sffnumbers"], [".numbers"],
             ["application/zip"]),

            // Office — presentations
            ("application/vnd.openxmlformats-officedocument.presentationml.presentation", [], [".pptx"],
             ["application/zip"]),
            ("application/vnd.ms-powerpoint.presentation.macroenabled.12", [], [".pptm"], ["application/zip"]),
            ("application/vnd.openxmlformats-officedocument.presentationml.slideshow", [], [".ppsx"],
             ["application/zip"]),
            ("application/vnd.openxmlformats-officedocument.presentationml.template", [], [".potx"],
             ["application/zip"]),
            ("application/vnd.ms-powerpoint.template.macroenabled.12", [], [".potm"], ["application/zip"]),
            ("application/vnd.ms-powerpoint", ["application/x-mspowerpoint", "application/powerpoint"],
             [".ppt", ".pot"],
             ["application/x-ole-storage"]),
            ("application/vnd.oasis.opendocument.presentation", [], [".odp"], ["application/zip"]),
            ("application/vnd.apple.keynote", ["application/x-iwork-keynote-sffkey"], [".key"], ["application/zip"]),

            // PDF, eBooks, database, Hangul
            ("application/pdf", ["application/x-pdf"], [".pdf"], []),
            ("application/epub+zip", [], [".epub"], []),
            ("application/x-fictionbook+xml", [], [".fb2"], []),
            ("application/x-dbf", ["application/dbf", "application/dbase"], [".dbf"], []),
            ("application/x-hwp", ["application/haansofthwp", "application/vnd.hancom.hwp"], [".hwp"],
             ["application/x-ole-storage"]),
            ("application/hwp+zip", ["application/vnd.hancom.hwpx"], [".hwpx"], []),

            // Images
            ("image/png", ["image/x-png"], [".png"], []),
            ("image/jpeg", ["image/pjpeg", "image/jpg"], [".jpg", ".jpeg"], []),
            ("image/gif", [], [".gif"], []),
            ("image/webp", [], [".webp"], []),
            ("image/bmp", ["image/x-ms-bmp", "image/x-bmp"], [".bmp"], []),
            ("image/tiff", [], [".tiff", ".tif"], []),
            ("image/jp2", [], [".jp2"], []),
            ("image/jpx", [], [".jpx"], []),
            ("image/jpm", [], [".jpm"], []),
            ("video/mj2", [], [".mj2"], []),
            ("image/x-jbig2", [], [".jbig2", ".jb2"], []),
            ("image/x-portable-anymap", [], [".pnm"], []),
            ("image/x-portable-bitmap", [], [".pbm"], ["image/x-portable-anymap"]),
            ("image/x-portable-graymap", [], [".pgm"], ["image/x-portable-anymap"]),
            ("image/x-portable-pixmap", [], [".ppm"], ["image/x-portable-anymap"]),
            ("image/heif", [], [".heif"], []),
            ("image/heic", [], [".heic"], ["image/heif"]),
            ("image/heic-sequence", [], [".heics"], []),
            ("image/avcs", [], [".avcs"], []),
            ("image/avif", [], [".avif"], []),
            ("image/svg+xml", [], [".svg"], []),

            // Audio and video
            ("audio/mpeg", ["audio/mp3", "audio/x-mp3"], [".mp3", ".mpga"], []),
            ("audio/mp4", ["audio/x-m4a"], [".m4a"], []),
            ("audio/vnd.wave", ["audio/wav", "audio/x-wav", "audio/wave"], [".wav"], []),
            ("audio/webm", [], [".webm"], []),
            ("video/mp4", [], [".mp4"], []),
            ("video/mpeg", [], [".mpeg"], []),
            ("video/webm", [], [".webm"], []),

            // Web and data
            ("text/html", [], [".html", ".htm"], []),
            ("application/xhtml+xml", [], [".xhtml"], []),
            ("application/xml", ["text/xml"], [".xml"], []),
            ("application/json", ["text/json", "text/x-json"], [".json"], []),
            ("application/yaml", ["application/x-yaml", "text/yaml", "text/x-yaml"], [".yaml", ".yml"], []),
            ("application/toml", ["application/x-toml"], [".toml"], []),
            ("text/csv", ["application/csv", "text/x-csv"], [".csv"], []),
            ("text/tab-separated-values", ["text/tsv"], [".tsv"], []),

            // Text and markup
            ("text/plain", [], [".txt"], []),
            ("text/markdown", ["text/x-markdown"], [".md", ".markdown"], []),
            ("text/x-djot", [], [".djot"], []),
            ("text/mdx", [], [".mdx"], []),
            ("text/x-rst", [], [".rst"], []),
            ("text/org", ["text/x-org"], [".org"], []),
            ("application/rtf", ["text/rtf"], [".rtf"], []),

            // Email and archives
            ("message/rfc822", [], [".eml"], []),
            ("application/vnd.ms-outlook", [], [".msg"], ["application/x-ole-storage"]),
            ("application/vnd.ms-outlook-pst", [], [".pst"], []),
            ("application/zip", ["application/x-zip-compressed", "application/x-zip"], [".zip"], []),
            ("application/x-tar", [], [".tar"], []),
            ("application/gzip", ["application/x-gzip"], [".gz", ".tgz", ".tar.gz"], []),
            ("application/x-7z-compressed", [], [".7z"], []),

            // Academic and scientific
            ("text/x-bibtex", ["application/x-bibtex"], [".bib"], []),
            ("application/x-research-info-systems", [], [".ris"], []),
            ("application/x-nbib", [], [".nbib"], []),
            ("application/x-endnote-refer", [], [".enw"], []),
            ("text/x-tex", ["application/x-tex", "application/x-latex"], [".tex", ".latex"], []),
            ("text/x-typst", [], [".typ", ".typst"], []),
            ("application/x-jats+xml", [], [".jats"], []),
            ("application/x-ipynb+json", [], [".ipynb"], []),

            // Publishing
            ("application/docbook+xml", ["application/x-docbook+xml"], [".docbook", ".dbk", ".docbook4", ".docbook5"],
             []),
            ("text/x-opml+xml", ["text/x-opml"], [".opml"], [])
        ];

    /// <summary>
    /// Gets every MIME type worth probing in pairwise hierarchy comparisons: all primaries, all
    /// aliases, all explicit parents (including the bare <c>application/x-ole-storage</c> parent),
    /// and the implicit fallback parent.
    /// </summary>
    public static IReadOnlyList<string> HierarchyCandidates { get; } = CreateHierarchyCandidates();

    /// <summary>
    /// Builds a registry directly from the mirror rows, for behavioral comparison against
    /// <see cref="DocumentSeed.Registry" />.
    /// </summary>
    public static MimeTypeRegistry BuildRegistry()
    {
        var builder = new MimeTypeRegistryBuilder();
        foreach (var (primary, aliases, extensions, parents) in Rows)
        {
            builder.AddGroup(primary, aliases, extensions);
            foreach (var parent in parents)
            {
                builder.AddParent(primary, parent);
            }
        }

        builder.SetExtensionPreference(
            FileExtension.Parse(".webm"),
            MimeType.Parse("video/webm"),
            MimeType.Parse("audio/webm")
        );
        return builder.Build();
    }

    private static IReadOnlyList<string> CreateHierarchyCandidates()
    {
        var candidates = new List<string>();
        foreach (var (primary, aliases, _, parents) in Rows)
        {
            candidates.Add(primary);
            candidates.AddRange(aliases);
            candidates.AddRange(parents);
        }

        candidates.Add("application/octet-stream");
        return [.. candidates.Distinct()];
    }
}
