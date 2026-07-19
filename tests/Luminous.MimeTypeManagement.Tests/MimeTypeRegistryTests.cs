using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Luminous.MimeTypeManagement.Tests;

public sealed class MimeTypeRegistryTests
{
    [Fact]
    public void Registry_normalizes_primary_and_alias_members()
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
    }

    [Fact]
    public void Registry_passes_valid_unknown_types_through()
    {
        var registry = CreateRealWorldRegistry();

        registry.Normalize("MODEL/X-EXOTIC; q=1").Value.Should().Be("model/x-exotic");
        registry.TryNormalize("model/x-exotic", out var unknown).Should().BeFalse();
        unknown.Value.Should().Be("model/x-exotic");
        registry.TryGetGroup("model/x-exotic", out _).Should().BeFalse();
        registry.TryGetGroup(MimeType.Parse("model/x-exotic"), out _).Should().BeFalse();
    }

    [Fact]
    public void Registry_try_methods_reject_invalid_values_without_throwing()
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
        var normalizeDefault = () => registry.Normalize(default(MimeType));
        normalizeInvalid.Should().Throw<FormatException>();
        normalizeDefault.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Groups_expose_ordered_extensions_and_extension_lookup_is_bidirectional()
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
        registry.TryGetGroups(FileExtension.Parse("stl"), out var groups).Should().BeTrue();
        groups.Should().HaveCount(2);
        registry.TryGetPreferredGroup("stl", out var preferred).Should().BeTrue();
        preferred.Should().BeSameAs(first);
        registry.GetGroups("unknown").Should().BeEmpty();
        registry.TryGetGroups(FileExtension.Parse("unknown"), out _).Should().BeFalse();
        registry.TryGetPreferredGroup("bad extension", out _).Should().BeFalse();
    }

    [Fact]
    public void Extension_preference_can_override_registration_order()
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
    public void Group_without_extensions_has_no_primary_extension()
    {
        var group = new MimeTypeGroup(MimeType.Parse("application/octet-stream"));

        group.PrimaryExtension.Should().BeNull();
        group.Extensions.Should().BeEmpty();
    }

    [Fact]
    public void Builder_string_overload_uses_its_parse_options()
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
    public void Registry_uses_configured_parse_limit_for_unknown_input()
    {
        var registry = new MimeTypeRegistryBuilder(new MimeTypeParseOptions { MaxNameLength = 4 }).Build();

        registry.Normalize("text/json").Value.Should().Be("text/json");
        registry.TryNormalize("audio/json", out _).Should().BeFalse();
        registry.TryGetGroup("audio/json", out _).Should().BeFalse();
    }

    [Fact]
    public void Concurrent_reads_are_safe()
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
    public void Known_string_normalization_allocates_no_memory_after_warmup()
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
    public void Empty_registry_constructors_create_usable_registries()
    {
        var first = new MimeTypeRegistry();
        var second = new MimeTypeRegistry(new MimeTypeRegistryBuilder());

        first.Normalize("text/plain").Value.Should().Be("text/plain");
        second.IsSubtypeOf("image/png", "application/octet-stream").Should().BeTrue();
        first.Groups.Should().BeEmpty();
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
