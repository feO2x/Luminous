using System;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Provides a hand-curated seed registry of MIME types for document formats commonly found in
/// document management systems.
/// </summary>
/// <remarks>
/// <para>
/// The seed covers the format list of the Xberg document intelligence framework as of July 2026:
/// office documents, images, audio and video, web and data formats, email and archives, and
/// academic and publishing formats. The coverage target is pinned to that snapshot; the seed is
/// curated by hand and does not track later upstream additions automatically.
/// </para>
/// <para>
/// The primary MIME type of each group follows a canonical-selection policy: when a current RFC or
/// the IANA registry designates a preferred type, it wins (e.g. <c>application/xml</c> over
/// <c>text/xml</c>); otherwise the IANA-registered type wins over legacy names (e.g.
/// <c>application/gzip</c> over <c>application/x-gzip</c>); unregistered formats use the most
/// widespread de-facto type, typically <c>x-</c>-prefixed (e.g. <c>application/x-tar</c>). Legacy
/// and de-facto variants observed in the wild are registered as aliases, so normalization always
/// moves from legacy to standard names. Consumers who prefer a different primary swap it via
/// <see cref="MimeTypeRegistryBuilder.ReplaceGroup(MimeType, MimeTypeGroup)" />.
/// </para>
/// <para>
/// <see cref="Registry" /> is a shared, lazily created singleton because <see cref="MimeTypeRegistry" />
/// is immutable and thread-safe. <see cref="CreateBuilder" /> returns a fresh, fully populated
/// builder per call because builders are mutable and not thread-safe. The name is deliberately
/// neither "Default" (which would over-promise universal coverage) nor "Xberg" (which would couple
/// the public API to a third-party project); future seeds with different scopes can sit alongside
/// it under the same naming pattern.
/// </para>
/// <para>
/// To compose a custom seed from a subset of categories, call the category methods
/// (<see cref="AddOfficeFormats" />, <see cref="AddImageFormats" />, <see cref="AddAudioVideoFormats" />,
/// <see cref="AddWebAndDataFormats" />, <see cref="AddEmailAndArchiveFormats" />, and
/// <see cref="AddAcademicFormats" />) on a fresh <see cref="MimeTypeRegistryBuilder" /> instead of
/// starting from <see cref="CreateBuilder" />.
/// </para>
/// </remarks>
public static partial class DocumentSeed
{
    private static readonly Lazy<MimeTypeRegistry> SharedRegistry = new (() => CreateBuilder().Build());

    /// <summary>
    /// Gets the shared immutable registry populated with the document seed.
    /// </summary>
    /// <remarks>The instance is created lazily on first access and is safe to share across threads.</remarks>
    public static MimeTypeRegistry Registry => SharedRegistry.Value;

    /// <summary>
    /// Creates a new builder fully populated with the document seed.
    /// </summary>
    /// <returns>
    /// A fresh <see cref="MimeTypeRegistryBuilder" /> containing every group, alias, extension,
    /// hierarchy edge, and extension preference of the seed, which callers can modify before calling
    /// <see cref="MimeTypeRegistryBuilder.Build" />.
    /// </returns>
    /// <remarks>
    /// A new builder is returned per call because builders are mutable and not thread-safe. To
    /// customize an already built seed registry instead, call <see cref="MimeTypeRegistry.ToBuilder" />
    /// on <see cref="Registry" />.
    /// </remarks>
    public static MimeTypeRegistryBuilder CreateBuilder()
    {
        var builder = new MimeTypeRegistryBuilder();
        AddOfficeFormats(builder);
        AddImageFormats(builder);
        AddAudioVideoFormats(builder);
        AddWebAndDataFormats(builder);
        AddEmailAndArchiveFormats(builder);
        AddAcademicFormats(builder);
        builder.SetExtensionPreference(
            FileExtension.Parse(".webm"),
            MimeType.Parse("video/webm"),
            MimeType.Parse("audio/webm")
        );
        return builder;
    }

    // Adds one group row of the seed table: the group itself plus its explicit parent edge when
    // the row lists one. Parents reachable through the structured-syntax suffix rules are not
    // listed in the seed and therefore never passed here.
    private static void AddGroup(
        MimeTypeRegistryBuilder builder,
        string primary,
        string[]? aliases = null,
        string[]? extensions = null,
        string? parent = null
    )
    {
        builder.AddGroup(primary, aliases, extensions);
        if (parent is not null)
        {
            builder.AddParent(primary, parent);
        }
    }
}
