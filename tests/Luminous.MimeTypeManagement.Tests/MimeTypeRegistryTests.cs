using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Luminous.MimeTypeManagement.Tests;

public sealed class MimeTypeRegistryTests
{
    [Fact]
    public void RegistryNormalizesPrimaryAndAliasMembers()
    {
        var registry = CreateRealWorldRegistry();

        registry.Normalize("APPLICATION/X-ZIP-COMPRESSED; version=ignored").Value.Should().Be("application/zip");
        registry.Normalize("text/xml").Value.Should().Be("application/xml");
        registry.Normalize("image/pjpeg").Value.Should().Be("image/jpeg");
        registry.Normalize("application/zip").Value.Should().Be("application/zip");
        registry.TryNormalize("image/pjpeg", out var jpeg).Should().BeTrue();
        jpeg.Value.Should().Be("image/jpeg");

        registry.TryGetGroup("TEXT/XML", out var group).Should().BeTrue();
        group.Should().NotBeNull();
        group.PrimaryMimeType.Value.Should().Be("application/xml");
        group.Aliases.Select(alias => alias.Value).Should().Equal("text/xml");

        registry.TryGetGroup(MimeType.Parse("text/xml"), out var parsedGroup).Should().BeTrue();
        parsedGroup.Should().BeSameAs(group);
    }

    [Fact]
    public void RegistryThrowsForValidUnknownTypesByDefaultAndCanPassThemThrough()
    {
        var registry = CreateRealWorldRegistry();
        var parsed = MimeType.Parse("model/x-exotic");

        var normalizeText = () => registry.Normalize("MODEL/X-EXOTIC; q=1");
        var normalizeParsed = () => registry.Normalize(parsed);

        normalizeText.Should().Throw<KeyNotFoundException>()
           .WithMessage("MIME type 'model/x-exotic' is not registered.");
        normalizeParsed.Should().Throw<KeyNotFoundException>()
           .WithMessage("MIME type 'model/x-exotic' is not registered.");
        registry.Normalize("MODEL/X-EXOTIC; q=1", throwWhenUnknown: false).Should().Be(parsed);
        registry.Normalize(parsed, throwWhenUnknown: false).Should().Be(parsed);
        registry.TryNormalize("model/x-exotic", out var unknown).Should().BeFalse();
        unknown.Should().Be(parsed);
        registry.TryGetGroup("model/x-exotic", out _).Should().BeFalse();
        registry.TryGetGroup(parsed, out _).Should().BeFalse();
    }

    [Fact]
    public void RegistryTryMethodsRejectInvalidValuesWithoutThrowing()
    {
        var registry = new MimeTypeRegistryBuilder().Build();

        registry.TryNormalize("invalid", out var normalized).Should().BeFalse();
        normalized.Should().Be(default(MimeType));
        registry.TryNormalize(default(MimeType), out _).Should().BeFalse();
        registry.TryGetGroup("invalid", out _).Should().BeFalse();
        registry.TryGetGroup(default(MimeType), out _).Should().BeFalse();
        registry.IsSubtypeOf(default, MimeType.Parse("text/plain")).Should().BeFalse();
        registry.IsSubtypeOf("invalid", "text/plain").Should().BeFalse();

        var normalizeInvalid = () => registry.Normalize("invalid");
        var normalizeInvalidWithoutThrowingWhenUnknown = () =>
            registry.Normalize("invalid", throwWhenUnknown: false);
        var normalizeDefault = () => registry.Normalize(default(MimeType));
        var normalizeDefaultWithoutThrowingWhenUnknown = () =>
            registry.Normalize(default(MimeType), throwWhenUnknown: false);
        normalizeInvalid.Should().Throw<FormatException>();
        normalizeInvalidWithoutThrowingWhenUnknown.Should().Throw<FormatException>();
        normalizeDefault.Should().Throw<ArgumentException>();
        normalizeDefaultWithoutThrowingWhenUnknown.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GroupsExposeOrderedExtensionsAndExtensionLookupIsBidirectional()
    {
        var first = new MimeTypeGroup(
            MimeType.Parse("model/stl"),
            extensions: [FileExtension.Parse("stl"), FileExtension.Parse("model")]
        );
        var second = new MimeTypeGroup(
            MimeType.Parse("application/vnd.subtitle"),
            extensions: [FileExtension.Parse("stl")]
        );
        var registry = new MimeTypeRegistryBuilder().AddGroup(first).AddGroup(second).Build();

        first.PrimaryExtension.Should().Be(FileExtension.Parse(".stl"));
        first.Extensions.Should().Equal(FileExtension.Parse(".stl"), FileExtension.Parse(".model"));
        second.Aliases.Should().BeEmpty();
        registry.GetGroups("*.STL").Select(group => group.PrimaryMimeType.Value)
           .Should().Equal("model/stl", "application/vnd.subtitle");
        registry.GetGroups(FileExtension.Parse("stl")).Should().Equal(first, second);
        registry.TryGetGroups(FileExtension.Parse("stl"), out var groups).Should().BeTrue();
        groups.Should().HaveCount(2);
        registry.TryGetPreferredGroup("stl", out var preferred).Should().BeTrue();
        preferred.Should().BeSameAs(first);
        registry.GetGroups("unknown").Should().BeEmpty();
        registry.TryGetGroups(FileExtension.Parse("unknown"), out _).Should().BeFalse();
        registry.TryGetPreferredGroup("bad extension", out _).Should().BeFalse();
    }

    [Fact]
    public void ExtensionPreferenceCanOverrideRegistrationOrder()
    {
        var first = Group("model/stl", "stl");
        var second = Group("application/vnd.subtitle", "stl");
        var registry = new MimeTypeRegistryBuilder()
           .AddGroup(first)
           .AddGroup(second)
           .SetExtensionPreference(FileExtension.Parse("stl"), second.PrimaryMimeType)
           .Build();

        registry.GetGroups("stl").Should().Equal(second, first);
        registry.TryGetPreferredGroup(FileExtension.Parse("stl"), out var preferred).Should().BeTrue();
        preferred.Should().BeSameAs(second);
    }

    [Fact]
    public void GroupWithoutExtensionsHasNoPrimaryExtension()
    {
        var group = new MimeTypeGroup(MimeType.Parse("application/octet-stream"));

        group.PrimaryExtension.Should().BeNull();
        group.Extensions.Should().BeEmpty();
    }

    [Fact]
    public void BuilderStringOverloadUsesItsParseOptions()
    {
        var options = new MimeTypeParseOptions { MaxNameLength = 6 };
        var builder = new MimeTypeRegistryBuilder(options)
           .AddGroup("text/custom", ["text/x-old"], ["TXT"]);

        builder.ParseOptions.Should().BeSameAs(options);
        var registry = builder.Build();
        registry.Normalize("text/x-old").Value.Should().Be("text/custom");

        var invalidAssignment = () => builder.ParseOptions = null!;
        invalidAssignment.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegistryUsesConfiguredParseLimitForUnknownInput()
    {
        var options = new MimeTypeParseOptions { MaxNameLength = 4 };
        var registry = new MimeTypeRegistryBuilder(options).Build();

        registry.ParseOptions.Should().BeSameAs(options);
        registry.Normalize("text/json", throwWhenUnknown: false).Value.Should().Be("text/json");
        registry.TryNormalize("audio/json", out _).Should().BeFalse();
        registry.TryGetGroup("audio/json", out _).Should().BeFalse();
    }

    [Fact]
    public void ConcurrentReadsAreSafe()
    {
        var registry = CreateRealWorldRegistry();

        var action = () => Parallel.For(
            0,
            10_000,
            index =>
            {
                registry.Normalize(index % 2 == 0 ? "IMAGE/PJPEG" : "TEXT/XML");
                registry.TryGetPreferredGroup("jpg", out _);
                registry.IsSubtypeOf("application/vnd.example+json", "application/octet-stream");
            }
        );

        action.Should().NotThrow();
    }

    [Fact]
    public void KnownStringNormalizationAllocatesNoMemoryAfterWarmup()
    {
        var registry = CreateRealWorldRegistry();
        const string knownMimeType = "APPLICATION/X-ZIP-COMPRESSED; charset=binary";

        for (var index = 0; index < 100; index++)
        {
            registry.TryNormalize(knownMimeType, out _);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var found = registry.TryNormalize(knownMimeType, out var normalized);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        found.Should().BeTrue();
        normalized.Value.Should().Be("application/zip");
        allocated.Should().Be(0);
    }

    [Fact]
    public void PreferredGroupLookupReportsUnclaimedExtensions()
    {
        var registry = CreateRealWorldRegistry();

        registry.TryGetPreferredGroup(FileExtension.Parse("unknown"), out var group).Should().BeFalse();
        group.Should().BeNull();
    }

    [Fact]
    public void StringLookupIgnoresTrailingWhitespaceBeforeParameters()
    {
        var registry = CreateRealWorldRegistry();

        registry.TryNormalize("APPLICATION/X-ZIP-COMPRESSED \t ; charset=binary", out var withParameters)
           .Should().BeTrue();
        withParameters.Value.Should().Be("application/zip");

        registry.TryNormalize("application/x-zip-compressed \t", out var withoutParameters).Should().BeTrue();
        withoutParameters.Value.Should().Be("application/zip");

        registry.TryNormalize(" \t; charset=binary", out _).Should().BeFalse();
    }

    [Fact]
    public void EmptyBuilderCreatesAUsableRegistry()
    {
        var registry = new MimeTypeRegistryBuilder().Build();

        registry.Normalize("text/plain", throwWhenUnknown: false).Value.Should().Be("text/plain");
        registry.IsSubtypeOf("image/png", "application/octet-stream").Should().BeTrue();
        registry.Groups.Should().BeEmpty();
    }

    private static MimeTypeRegistry CreateRealWorldRegistry() =>
        new MimeTypeRegistryBuilder()
           .AddGroup(
                MimeType.Parse("application/zip"),
                [MimeType.Parse("application/x-zip-compressed")],
                [FileExtension.Parse("zip")]
            )
           .AddGroup(
                MimeType.Parse("application/xml"),
                [MimeType.Parse("text/xml")],
                [FileExtension.Parse("xml")]
            )
           .AddGroup(
                MimeType.Parse("image/jpeg"),
                [MimeType.Parse("image/pjpeg")],
                [FileExtension.Parse("jpg"), FileExtension.Parse("jpeg")]
            )
           .Build();

    private static MimeTypeGroup Group(string mimeType, string extension) =>
        new (
            MimeType.Parse(mimeType),
            extensions: [FileExtension.Parse(extension)]
        );
}
