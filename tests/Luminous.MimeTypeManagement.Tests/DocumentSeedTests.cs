using System.Linq;
using FluentAssertions;
using Xunit;

namespace Luminous.MimeTypeManagement.Tests;

public sealed class DocumentSeedTests
{
    public static TheoryData<string> XbergExtensions
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var extension in XbergExtensionSnapshot.Extensions)
            {
                data.Add(extension);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(XbergExtensions))]
    public void EveryXbergExtensionResolvesToAtLeastOneGroup(string extension)
    {
        DocumentSeed.Registry.GetGroups(extension).Should().NotBeEmpty(
            "the pinned Xberg snapshot (July 2026) lists '{0}' as a supported format",
            extension
        );
    }

    [Fact]
    public void XbergSnapshotContainsOnlyDistinctExtensions()
    {
        XbergExtensionSnapshot.Extensions.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SeededRegistryMatchesTheMirrorTranscriptionExactly()
    {
        var seeded = DocumentSeed.Registry;
        var mirror = DocumentSeedMirror.BuildRegistry();

        DocumentSeedMirror.Rows.Should().HaveCount(DocumentSeedMirror.ExpectedGroupCount);
        seeded.Groups.Should().HaveCount(DocumentSeedMirror.ExpectedGroupCount);

        foreach (var (group, row) in seeded.Groups.Zip(DocumentSeedMirror.Rows))
        {
            group.PrimaryMimeType.Value.Should().Be(row.Primary);
            group.Aliases.Select(alias => alias.Value).Should().Equal(row.Aliases);
            group.Extensions.Select(extension => extension.Value).Should().Equal(row.Extensions);
        }

        foreach (var row in DocumentSeedMirror.Rows)
        {
            foreach (var alias in row.Aliases)
            {
                seeded.Normalize(alias).Value.Should().Be(
                    row.Primary,
                    "'{0}' is an alias of '{1}'",
                    alias,
                    row.Primary
                );
            }

            foreach (var parent in row.Parents)
            {
                seeded.IsSubtypeOf(row.Primary, parent).Should().BeTrue(
                    "'{0}' has explicit parent '{1}'",
                    row.Primary,
                    parent
                );
            }
        }

        // Any missing or extra hierarchy edge makes the two registries disagree on some pair.
        foreach (var child in DocumentSeedMirror.HierarchyCandidates)
        {
            foreach (var parent in DocumentSeedMirror.HierarchyCandidates)
            {
                seeded.IsSubtypeOf(child, parent).Should().Be(
                    mirror.IsSubtypeOf(child, parent),
                    "seed and mirror must agree on '{0}' deriving from '{1}'",
                    child,
                    parent
                );
            }
        }
    }

    [Theory]
    [InlineData("application/x-zip-compressed", "application/zip")]
    [InlineData("text/xml", "application/xml")]
    [InlineData("image/pjpeg", "image/jpeg")]
    public void LitmusNormalizationsWorkOutOfTheBox(string alias, string expectedPrimary)
    {
        DocumentSeed.Registry.Normalize(alias).Value.Should().Be(expectedPrimary);
    }

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("application/vnd.ms-word.document.macroenabled.12")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.template")]
    [InlineData("application/vnd.ms-word.template.macroenabled.12")]
    [InlineData("application/vnd.oasis.opendocument.text")]
    [InlineData("application/vnd.apple.pages")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("application/vnd.ms-excel.sheet.macroenabled.12")]
    [InlineData("application/vnd.ms-excel.sheet.binary.macroenabled.12")]
    [InlineData("application/vnd.ms-excel.template.macroenabled.12")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.template")]
    [InlineData("application/vnd.ms-excel.addin.macroenabled.12")]
    [InlineData("application/vnd.oasis.opendocument.spreadsheet")]
    [InlineData("application/vnd.apple.numbers")]
    [InlineData("application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("application/vnd.ms-powerpoint.presentation.macroenabled.12")]
    [InlineData("application/vnd.openxmlformats-officedocument.presentationml.slideshow")]
    [InlineData("application/vnd.openxmlformats-officedocument.presentationml.template")]
    [InlineData("application/vnd.ms-powerpoint.template.macroenabled.12")]
    [InlineData("application/vnd.oasis.opendocument.presentation")]
    [InlineData("application/vnd.apple.keynote")]
    public void ZipContainersWithoutZipSuffixDeriveFromZipViaExplicitEdges(string mimeType)
    {
        DocumentSeed.Registry.IsSubtypeOf(mimeType, "application/zip").Should().BeTrue();
    }

    [Theory]
    [InlineData("application/epub+zip", "application/zip")]
    [InlineData("application/hwp+zip", "application/zip")]
    [InlineData("image/svg+xml", "application/xml")]
    [InlineData("application/xhtml+xml", "application/xml")]
    [InlineData("application/x-fictionbook+xml", "application/xml")]
    [InlineData("application/x-jats+xml", "application/xml")]
    [InlineData("application/docbook+xml", "application/xml")]
    [InlineData("text/x-opml+xml", "application/xml")]
    [InlineData("application/x-ipynb+json", "application/json")]
    public void SuffixCarryingTypesReachTheirParentsThroughSuffixRules(string child, string parent)
    {
        DocumentSeed.Registry.IsSubtypeOf(child, parent).Should().BeTrue();
    }

    [Theory]
    [InlineData("application/msword")]
    [InlineData("application/vnd.ms-excel")]
    [InlineData("application/vnd.ms-powerpoint")]
    [InlineData("application/vnd.ms-outlook")]
    [InlineData("application/x-hwp")]
    public void LegacyOleFormatsDeriveFromOleStorage(string mimeType)
    {
        DocumentSeed.Registry.IsSubtypeOf(mimeType, "application/x-ole-storage").Should().BeTrue();
    }

    [Fact]
    public void PstIsItsOwnContainerFormatWithoutOleParent()
    {
        DocumentSeed.Registry.IsSubtypeOf("application/vnd.ms-outlook-pst", "application/x-ole-storage")
           .Should().BeFalse();
    }

    [Fact]
    public void DocxIsSubtypeOfZipWithoutNormalizingToZip()
    {
        const string docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        var registry = DocumentSeed.Registry;

        registry.Normalize(docx).Value.Should().Be(docx);
        registry.IsSubtypeOf(docx, "application/zip").Should().BeTrue();
        registry.TryGetGroup(docx, out var group).Should().BeTrue();
        group.Should().NotBeNull();
        group.PrimaryMimeType.Value.Should().Be(docx);
        group.PrimaryExtension.Should().Be(FileExtension.Parse(".docx"));
    }

    [Fact]
    public void WebmResolvesToAudioAndVideoWithVideoPreferred()
    {
        var registry = DocumentSeed.Registry;

        registry
           .GetGroups(".webm").Select(group => group.PrimaryMimeType.Value)
           .Should().Equal("video/webm", "audio/webm");
        registry.TryGetPreferredGroup(".webm", out var preferred).Should().BeTrue();
        preferred.Should().NotBeNull();
        preferred.PrimaryMimeType.Value.Should().Be("video/webm");
    }

    [Fact]
    public void CompoundExtensionsResolveToTheirGroup()
    {
        DocumentSeed.Registry
           .GetGroups(".tar.gz").Select(group => group.PrimaryMimeType.Value)
           .Should().Equal("application/gzip");
        DocumentSeed.Registry
           .GetGroups(".tgz").Select(group => group.PrimaryMimeType.Value)
           .Should().Equal("application/gzip");
    }

    [Fact]
    public void CreateBuilderBuildsARegistryEquivalentToTheSharedInstance()
    {
        var built = DocumentSeed.CreateBuilder().Build();

        built.Groups.Should().HaveCount(DocumentSeed.Registry.Groups.Length);
        foreach (var (builtGroup, sharedGroup) in built.Groups.Zip(DocumentSeed.Registry.Groups))
        {
            builtGroup.PrimaryMimeType.Value.Should().Be(sharedGroup.PrimaryMimeType.Value);
            builtGroup.Aliases.Select(alias => alias.Value)
               .Should().Equal(sharedGroup.Aliases.Select(alias => alias.Value));
            builtGroup.Extensions.Select(extension => extension.Value)
               .Should().Equal(sharedGroup.Extensions.Select(extension => extension.Value));
        }

        built.Normalize("application/x-zip-compressed").Value.Should().Be("application/zip");
        built.IsSubtypeOf("application/msword", "application/x-ole-storage").Should().BeTrue();
        built
           .GetGroups(".webm").Select(group => group.PrimaryMimeType.Value)
           .Should().Equal("video/webm", "audio/webm");
    }

    [Fact]
    public void CreateBuilderReturnsFreshIndependentBuilders()
    {
        var first = DocumentSeed.CreateBuilder();
        var second = DocumentSeed.CreateBuilder();

        first.Should().NotBeSameAs(second);
        first.RemoveGroup("application/zip").Should().BeTrue();

        second.Build().Groups.Should().HaveCount(DocumentSeedMirror.ExpectedGroupCount);
        DocumentSeed.Registry.Groups.Should().HaveCount(DocumentSeedMirror.ExpectedGroupCount);
    }

    [Fact]
    public void CategoryMethodsComposeCustomSeedsFromSubsets()
    {
        var builder = new MimeTypeRegistryBuilder();
        DocumentSeed.AddOfficeFormats(builder);
        DocumentSeed.AddWebAndDataFormats(builder);
        var composed = builder.Build();

        composed.Groups.Should().HaveCount(45); // 30 office + 15 web and data groups
        composed.TryGetGroup("application/msword", out _).Should().BeTrue();
        composed.TryGetGroup("application/vnd.apple.keynote", out _).Should().BeTrue();
        composed.TryGetGroup("application/json", out _).Should().BeTrue();
        composed.TryGetGroup("text/markdown", out _).Should().BeTrue();
        composed.TryGetGroup("image/png", out _).Should().BeFalse();
        composed.TryGetGroup("application/zip", out _).Should().BeFalse();

        // Explicit parent edges work even when the parent has no group in the subset
        composed
           .IsSubtypeOf(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/zip"
            )
           .Should().BeTrue();
        composed.Normalize("text/xml").Value.Should().Be("application/xml");
    }

    [Fact]
    public void AudioVideoSubsetPrefersVideoWebmLikeTheFullSeed()
    {
        var builder = new MimeTypeRegistryBuilder();
        DocumentSeed.AddAudioVideoFormats(builder);
        var composed = builder.Build();

        composed
           .GetGroups(".webm").Select(group => group.PrimaryMimeType.Value)
           .Should().Equal("video/webm", "audio/webm");
        composed.TryGetPreferredGroup(".webm", out var preferred).Should().BeTrue();
        preferred.Should().NotBeNull();
        preferred.PrimaryMimeType.Value.Should().Be("video/webm");
    }

    [Fact]
    public void RegistryIsASharedSingleton()
    {
        DocumentSeed.Registry.Should().BeSameAs(DocumentSeed.Registry);
    }
}
