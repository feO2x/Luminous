namespace Luminous.MimeTypeManagement;

public static partial class DocumentSeed
{
    /// <summary>
    /// Adds the seed's web, structured-data, text, and markup groups, including the aliases, file
    /// extensions, and explicit parent edges defined for them.
    /// </summary>
    /// <param name="builder">The builder to add the category's groups to.</param>
    // ReSharper disable once MemberCanBePrivate.Global -- we want this to be a public API
    public static void AddWebAndDataFormats(MimeTypeRegistryBuilder builder)
    {
        // Web and structured data
        AddGroup(builder, "text/html", extensions: [".html", ".htm"]);
        AddGroup(builder, "application/xhtml+xml", extensions: [".xhtml"]);
        AddGroup(builder, "application/xml", aliases: ["text/xml"], extensions: [".xml"]);
        AddGroup(
            builder,
            "application/json",
            aliases: ["text/json", "text/x-json"],
            extensions: [".json"]
        );
        AddGroup(
            builder,
            "application/yaml",
            aliases: ["application/x-yaml", "text/yaml", "text/x-yaml"],
            extensions: [".yaml", ".yml"]
        );
        AddGroup(builder, "application/toml", aliases: ["application/x-toml"], extensions: [".toml"]);
        AddGroup(
            builder,
            "text/csv",
            aliases: ["application/csv", "text/x-csv"],
            extensions: [".csv"]
        );
        AddGroup(builder, "text/tab-separated-values", aliases: ["text/tsv"], extensions: [".tsv"]);

        // Text and markup
        AddGroup(builder, "text/plain", extensions: [".txt"]);
        AddGroup(
            builder,
            "text/markdown",
            aliases: ["text/x-markdown"],
            extensions: [".md", ".markdown"]
        );
        AddGroup(builder, "text/x-djot", extensions: [".djot"]);
        AddGroup(builder, "text/mdx", extensions: [".mdx"]);
        AddGroup(builder, "text/x-rst", extensions: [".rst"]);
        AddGroup(builder, "text/org", aliases: ["text/x-org"], extensions: [".org"]);
        AddGroup(builder, "application/rtf", aliases: ["text/rtf"], extensions: [".rtf"]);
    }
}
