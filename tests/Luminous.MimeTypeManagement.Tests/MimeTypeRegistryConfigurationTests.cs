using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using FluentAssertions;
using Xunit;

namespace Luminous.MimeTypeManagement.Tests;

public sealed class MimeTypeRegistryConfigurationTests
{
    [Fact]
    public void RegistryCanBeCreatedDirectlyFromAConfiguration()
    {
        var configuration = CreateConfiguration(
            groups:
            [
                new MimeTypeGroup(
                    MimeType.Parse("application/zip"),
                    [MimeType.Parse("application/x-zip-compressed")],
                    [FileExtension.Parse("zip")]
                )
            ],
            parentRelations:
            [
                (MimeType.Parse("application/vnd.example"), MimeType.Parse("application/zip"))
            ]
        );

        var registry = new MimeTypeRegistry(configuration);

        registry.Normalize("application/x-zip-compressed").Value.Should().Be("application/zip");
        registry.IsSubtypeOf("application/vnd.example", "application/zip").Should().BeTrue();
        registry.TryGetPreferredGroup("zip", out var group).Should().BeTrue();
        group.Should().NotBeNull();
        group.PrimaryMimeType.Value.Should().Be("application/zip");
    }

    [Fact]
    public void RegistryConstructorRejectsANullConfiguration()
    {
        var act = () => new MimeTypeRegistry(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegistryConstructorValidatesTheConfiguration()
    {
        var shared = MimeType.Parse("application/shared");
        var configuration = CreateConfiguration(
            groups: [new MimeTypeGroup(shared), new MimeTypeGroup(shared)]
        );

        var act = () => new MimeTypeRegistry(configuration);

        act.Should().Throw<MimeTypeRegistryValidationException>()
           .Which.Violations.Should().ContainSingle(
                violation => violation.Contains("belongs to groups", StringComparison.Ordinal)
            );
    }

    [Fact]
    public void DefaultArraysAreNormalizedToEmptyArrays()
    {
        var configuration = CreateConfiguration();

        configuration.Groups.IsEmpty.Should().BeTrue();
        configuration.ParentRelations.IsEmpty.Should().BeTrue();
        configuration.SuffixParents.IsEmpty.Should().BeTrue();
        configuration.ExtensionPreferences.IsEmpty.Should().BeTrue();

        var registry = new MimeTypeRegistry(configuration);
        registry.Groups.Should().BeEmpty();
        registry.Normalize("text/plain", throwWhenUnknown: false).Value.Should().Be("text/plain");
    }

    [Fact]
    public void ParseOptionsCannotBeAssignedNull()
    {
        var act = () => new MimeTypeRegistryConfiguration
        {
            Groups = [],
            ParentRelations = [],
            SuffixParents = [],
            ExtensionPreferences = [],
            ParseOptions = null!,
            StructuredSyntaxSuffixRulesEnabled = true,
            TextPlainRuleEnabled = true,
            FallbackRuleEnabled = true,
            TextParent = MimeType.Parse("text/plain"),
            FallbackParent = MimeType.Parse("application/octet-stream")
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullGroupEntriesAreReportedAsViolations()
    {
        var configuration = CreateConfiguration(
            groups: [new MimeTypeGroup(MimeType.Parse("application/zip")), null!]
        );

        var act = () => new MimeTypeRegistry(configuration);

        act.Should().Throw<MimeTypeRegistryValidationException>()
           .Which.Violations.Should().Equal("Group 2 is null.");
    }

    [Fact]
    public void InvalidAndDuplicateSuffixKeysAreReportedAsViolations()
    {
        var configuration = CreateConfiguration(
            suffixParents:
            [
                new KeyValuePair<string, MimeType>("+xml", MimeType.Parse("application/xml")),
                new KeyValuePair<string, MimeType>("json", MimeType.Parse("application/json")),
                new KeyValuePair<string, MimeType>("JSON", MimeType.Parse("text/json")),
                new KeyValuePair<string, MimeType>(null!, MimeType.Parse("application/octet-stream"))
            ]
        );

        var act = () => new MimeTypeRegistry(configuration);

        act.Should().Throw<MimeTypeRegistryValidationException>()
           .Which.Violations.Should().BeEquivalentTo(
                "'+xml' is not a valid structured-syntax suffix.",
                "The '+JSON' suffix is configured more than once.",
                "'' is not a valid structured-syntax suffix."
            );
    }

    [Fact]
    public void DuplicateAndIncompleteExtensionPreferencesAreReportedAsViolations()
    {
        var stl = FileExtension.Parse("stl");
        var configuration = CreateConfiguration(
            groups: [new MimeTypeGroup(MimeType.Parse("model/stl"), extensions: [stl])],
            extensionPreferences:
            [
                new KeyValuePair<FileExtension, ImmutableArray<MimeType>>(stl, [MimeType.Parse("model/stl")]),
                new KeyValuePair<FileExtension, ImmutableArray<MimeType>>(stl, [MimeType.Parse("model/stl")]),
                new KeyValuePair<FileExtension, ImmutableArray<MimeType>>(FileExtension.Parse("gz"), default)
            ]
        );

        var act = () => new MimeTypeRegistry(configuration);

        act.Should().Throw<MimeTypeRegistryValidationException>()
           .Which.Violations.Should().BeEquivalentTo(
                "The preference for '.stl' is configured more than once.",
                "The preference for '.gz' has no MIME type list."
            );
    }

    [Fact]
    public void ConfigurationsFromABuilderRoundTripThroughToBuilder()
    {
        var configuration = CreateConfiguration(
            groups: [new MimeTypeGroup(MimeType.Parse("application/zip"), extensions: [FileExtension.Parse("zip")])],
            suffixParents: [new KeyValuePair<string, MimeType>("CUSTOM", MimeType.Parse("application/custom"))]
        );

        var roundTripped = new MimeTypeRegistry(configuration).ToBuilder().Build();

        roundTripped.TryGetGroup("application/zip", out _).Should().BeTrue();
        roundTripped.IsSubtypeOf("application/vnd.example+custom", "application/custom").Should().BeTrue();
    }

    private static MimeTypeRegistryConfiguration CreateConfiguration(
        ImmutableArray<MimeTypeGroup> groups = default,
        ImmutableArray<(MimeType Child, MimeType Parent)> parentRelations = default,
        ImmutableArray<KeyValuePair<string, MimeType>> suffixParents = default,
        ImmutableArray<KeyValuePair<FileExtension, ImmutableArray<MimeType>>> extensionPreferences = default
    ) =>
        new ()
        {
            Groups = groups,
            ParentRelations = parentRelations,
            SuffixParents = suffixParents,
            ExtensionPreferences = extensionPreferences,
            ParseOptions = MimeTypeParseOptions.Default,
            StructuredSyntaxSuffixRulesEnabled = true,
            TextPlainRuleEnabled = true,
            FallbackRuleEnabled = true,
            TextParent = MimeType.Parse("text/plain"),
            FallbackParent = MimeType.Parse("application/octet-stream")
        };
}
