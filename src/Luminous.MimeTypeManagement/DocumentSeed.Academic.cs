namespace Luminous.MimeTypeManagement;

public static partial class DocumentSeed
{
    /// <summary>
    /// Adds the seed's academic, scientific, and publishing groups, including the aliases, file
    /// extensions, and explicit parent edges defined for them.
    /// </summary>
    /// <param name="builder">The builder to add the category's groups to.</param>
    // ReSharper disable once MemberCanBePrivate.Global -- we want this to be a public API
    public static void AddAcademicFormats(MimeTypeRegistryBuilder builder)
    {
        // Academic and scientific
        AddGroup(builder, "text/x-bibtex", aliases: ["application/x-bibtex"], extensions: [".bib"]);
        AddGroup(builder, "application/x-research-info-systems", extensions: [".ris"]);
        AddGroup(builder, "application/x-nbib", extensions: [".nbib"]);
        AddGroup(builder, "application/x-endnote-refer", extensions: [".enw"]);
        AddGroup(
            builder,
            "text/x-tex",
            aliases: ["application/x-tex", "application/x-latex"],
            extensions: [".tex", ".latex"]
        );
        AddGroup(builder, "text/x-typst", extensions: [".typ", ".typst"]);
        AddGroup(builder, "application/x-jats+xml", extensions: [".jats"]);
        AddGroup(builder, "application/x-ipynb+json", extensions: [".ipynb"]);

        // Publishing — one DocBook group; DocBook 4 and 5 are versions, not formats
        AddGroup(
            builder,
            "application/docbook+xml",
            aliases: ["application/x-docbook+xml"],
            extensions: [".docbook", ".dbk", ".docbook4", ".docbook5"]
        );
        AddGroup(builder, "text/x-opml+xml", aliases: ["text/x-opml"], extensions: [".opml"]);
    }
}
