namespace Luminous.MimeTypeManagement;

public static partial class DocumentSeed
{
    /// <summary>
    /// Adds the seed's audio and video groups, including the aliases, file extensions, and
    /// explicit parent edges defined for them.
    /// </summary>
    /// <param name="builder">The builder to add the category's groups to.</param>
    // ReSharper disable once MemberCanBePrivate.Global -- we want this to be a public API
    public static void AddAudioVideoFormats(MimeTypeRegistryBuilder builder)
    {
        AddGroup(
            builder,
            "audio/mpeg",
            aliases: ["audio/mp3", "audio/x-mp3"],
            extensions: [".mp3", ".mpga"]
        );
        AddGroup(builder, "audio/mp4", aliases: ["audio/x-m4a"], extensions: [".m4a"]);

        // IANA-registered primary despite the dominant de-facto audio/wav
        AddGroup(
            builder,
            "audio/vnd.wave",
            aliases: ["audio/wav", "audio/x-wav", "audio/wave"],
            extensions: [".wav"]
        );
        AddGroup(builder, "audio/webm", extensions: [".webm"]);
        AddGroup(builder, "video/mp4", extensions: [".mp4"]);
        AddGroup(builder, "video/mpeg", extensions: [".mpeg"]);
        AddGroup(builder, "video/webm", extensions: [".webm"]);
    }
}
