using System.Collections.Generic;

namespace Luminous.MimeTypeManagement.Tests;

/// <summary>
/// The pinned Xberg (https://github.com/xberg-io/xberg) supported-format list as of July 2026,
/// transcribed from the "Supported File Formats" section of its README. Stored as static test
/// data — the test suite performs no network I/O, and later Xberg additions do not change this
/// snapshot.
/// </summary>
public static class XbergExtensionSnapshot
{
    public static IReadOnlyList<string> Extensions { get; } =
    [
        // Office — word processing
        ".docx", ".docm", ".doc", ".dotx", ".dotm", ".dot", ".odt", ".pages",

        // Office — spreadsheets
        ".xlsx", ".xlsm", ".xlsb", ".xls", ".xla", ".xlam", ".xltm", ".xltx", ".xlt", ".ods", ".numbers",

        // Office — presentations
        ".pptx", ".pptm", ".ppt", ".ppsx", ".potx", ".potm", ".pot", ".odp", ".key",

        // PDF, eBooks, database, Hangul
        ".pdf", ".epub", ".fb2", ".dbf", ".hwp", ".hwpx",

        // Images — raster
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tiff", ".tif",

        // Images — advanced
        ".jp2", ".jpx", ".jpm", ".mj2", ".jbig2", ".jb2", ".pnm", ".pbm", ".pgm", ".ppm",

        // Images — HEIC family
        ".heic", ".heics", ".heif", ".avif", ".avcs",

        // Images — vector
        ".svg",

        // Audio and video
        ".mp3", ".mpga", ".m4a", ".wav", ".webm", ".mp4", ".mpeg",

        // Web and data — markup
        ".html", ".htm", ".xhtml", ".xml",

        // Web and data — structured data
        ".json", ".yaml", ".yml", ".toml", ".csv", ".tsv",

        // Web and data — text and markdown
        ".txt", ".md", ".markdown", ".djot", ".mdx", ".rst", ".org", ".rtf",

        // Email and archives
        ".eml", ".msg", ".pst", ".zip", ".tar", ".tgz", ".gz", ".7z",

        // Academic — citations
        ".bib", ".ris", ".nbib", ".enw",

        // Academic — scientific
        ".tex", ".latex", ".typ", ".typst", ".jats", ".ipynb",

        // Academic — publishing
        ".docbook", ".dbk", ".docbook4", ".docbook5", ".opml"
    ];
}
