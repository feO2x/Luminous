using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Luminous.MimeTypeManagement.Tests;

public sealed class MimeTypeRegistryBuilderTests
{
    [Fact]
    public void Groups_is_a_live_read_only_view_of_the_configuration()
    {
        var first = Group("application/first", "first");
        var replacement = Group("application/replacement", "replacement");
        var builder = new MimeTypeRegistryBuilder().AddGroup(first);
        var groups = builder.Groups;

        groups.Should().ContainSingle().Which.Should().BeSameAs(first);

        builder.ReplaceGroup(first.PrimaryMimeType, replacement);

        groups.Should().ContainSingle().Which.Should().BeSameAs(replacement);
    }

    [Fact]
    public void Build_reports_all_group_membership_duplicate_and_cycle_violations()
    {
        var shared = MimeType.Parse("application/shared");
        var first = new MimeTypeGroup(
            MimeType.Parse("application/first"),
            [shared, shared],
            [FileExtension.Parse("one"), FileExtension.Parse("one")]
        );
        var second = new MimeTypeGroup(shared);
        var builder = new MimeTypeRegistryBuilder()
           .AddGroup(first)
           .AddGroup(second)
           .AddParent("application/a", "application/b")
           .AddParent("application/b", "application/a");

        var act = builder.Build;

        var exception = act.Should().Throw<MimeTypeRegistryValidationException>().Which;
        exception.Violations.Should().HaveCountGreaterThanOrEqualTo(4);
        exception.Violations.Should()
           .Contain(violation => violation.Contains("duplicate MIME type", StringComparison.Ordinal));
        exception.Violations.Should()
           .Contain(violation => violation.Contains("duplicate extension", StringComparison.Ordinal));
        exception.Violations.Should()
           .Contain(violation => violation.Contains("belongs to groups", StringComparison.Ordinal));
        exception.Violations.Should().Contain(violation => violation.Contains("cycle", StringComparison.Ordinal));
        exception.Message.Should().ContainAll(exception.Violations.ToArray());
    }

    [Fact]
    public void Build_detects_cycles_created_by_alias_normalization()
    {
        var builder = new MimeTypeRegistryBuilder()
           .AddGroup(
                MimeType.Parse("application/canonical"),
                [MimeType.Parse("application/alias")]
            )
           .AddParent("application/alias", "application/canonical");

        var act = builder.Build;

        act.Should().Throw<MimeTypeRegistryValidationException>()
           .Which.Violations.Should().ContainSingle(violation => violation.Contains("cycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_validates_default_values_and_invalid_preferences_together()
    {
        var group = new MimeTypeGroup(
            default,
            [default],
            [default]
        );
        var builder = new MimeTypeRegistryBuilder()
           .AddGroup(group)
           .AddParent(default, default(MimeType));
        builder.TextParent = default;
        builder.FallbackParent = default;
        builder.SetSuffixParent("custom", default(MimeType));
        builder.SetExtensionPreference(default, default, default);

        var act = builder.Build;

        act.Should().Throw<MimeTypeRegistryValidationException>()
           .Which.Violations.Should().HaveCountGreaterThan(7);
    }

    [Fact]
    public void Build_validates_preference_claims_and_duplicates()
    {
        var group = new MimeTypeGroup(
            MimeType.Parse("application/example"),
            extensions: [FileExtension.Parse("example")]
        );
        var builder = new MimeTypeRegistryBuilder().AddGroup(group);
        builder.SetExtensionPreference(
            FileExtension.Parse("other"),
            group.PrimaryMimeType,
            group.PrimaryMimeType
        );

        var act = builder.Build;

        act.Should().Throw<MimeTypeRegistryValidationException>()
           .Which.Violations.Should().HaveCount(3);
    }

    [Fact]
    public void ToBuilder_round_trips_and_can_modify_every_configuration_area()
    {
        var first = Group("application/first", "shared");
        var second = Group("application/second", "shared");
        var originalBuilder = new MimeTypeRegistryBuilder()
           .AddGroup(first)
           .AddGroup(second)
           .AddParent("application/first", "application/root")
           .SetExtensionPreference(FileExtension.Parse("shared"), second.PrimaryMimeType);
        originalBuilder.ClearSuffixParents();
        originalBuilder.SetSuffixParent("custom", MimeType.Parse("application/custom"));
        originalBuilder.TextPlainRuleEnabled = false;
        originalBuilder.FallbackRuleEnabled = false;
        var original = originalBuilder.Build();

        var roundTripped = original.ToBuilder().Build();
        roundTripped.GetGroups("shared").Should().Equal(second, first);
        roundTripped.IsSubtypeOf("application/first", "application/root").Should().BeTrue();
        roundTripped.IsSubtypeOf("application/vnd.test+custom", "application/custom").Should().BeTrue();
        roundTripped.IsSubtypeOf("text/csv", "text/plain").Should().BeFalse();

        var modifiedBuilder = original.ToBuilder();
        modifiedBuilder.RemoveGroup(second.PrimaryMimeType).Should().BeTrue();
        modifiedBuilder.ReplaceGroup(
            first.PrimaryMimeType,
            Group("application/replacement", "replacement")
        );
        modifiedBuilder.RemoveParent(
            first.PrimaryMimeType,
            MimeType.Parse("application/root")
        ).Should().BeTrue();
        modifiedBuilder.RemoveParentRelation(
            first.PrimaryMimeType,
            MimeType.Parse("application/missing")
        ).Should().BeFalse();
        modifiedBuilder.ClearParents(first.PrimaryMimeType).Should().Be(0);
        modifiedBuilder.ClearSuffixParents();
        modifiedBuilder.ClearExtensionPreference(FileExtension.Parse("shared")).Should().BeTrue();
        modifiedBuilder.FallbackRuleEnabled = true;
        var modified = modifiedBuilder.Build();

        modified.TryGetGroup("application/second", out _).Should().BeFalse();
        modified.TryGetGroup("application/replacement", out _).Should().BeTrue();
        modified.IsSubtypeOf("application/vnd.test+custom", "application/custom").Should().BeFalse();
        modified.IsSubtypeOf("image/png", "application/octet-stream").Should().BeTrue();

        original.TryGetGroup("application/second", out _).Should().BeTrue();
        original.IsSubtypeOf("application/first", "application/root").Should().BeTrue();
    }

    [Fact]
    public void Parse_options_can_be_replaced_after_construction()
    {
        var builder = new MimeTypeRegistryBuilder();
        var options = new MimeTypeParseOptions { MaxNameLength = 10 };

        builder.ParseOptions = options;

        builder.ParseOptions.Should().BeSameAs(options);
    }

    [Fact]
    public void String_group_overload_allows_omitted_aliases_and_extensions()
    {
        var registry = new MimeTypeRegistryBuilder()
           .AddGroup("application/solo")
           .Build();

        registry.TryGetGroup("application/solo", out var group).Should().BeTrue();
        group.Should().NotBeNull();
        group.Aliases.Should().BeEmpty();
        group.Extensions.Should().BeEmpty();
    }

    [Fact]
    public void Groups_can_be_removed_by_their_primary_name()
    {
        var builder = new MimeTypeRegistryBuilder().AddGroup("application/removable");

        builder.RemoveGroup("application/removable").Should().BeTrue();

        builder.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Build_rejects_preferences_referencing_unknown_groups()
    {
        var builder = new MimeTypeRegistryBuilder()
           .SetExtensionPreference(FileExtension.Parse("example"), MimeType.Parse("application/unknown"));

        var act = builder.Build;

        act.Should().Throw<MimeTypeRegistryValidationException>()
           .Which.Violations.Should().ContainSingle(
                violation => violation.Contains("non-claiming", StringComparison.Ordinal)
            );
    }

    [Fact]
    public void ClearParents_removes_only_relations_of_the_supplied_child()
    {
        var builder = new MimeTypeRegistryBuilder()
           .AddParent("application/child", "application/first")
           .AddParent("application/child", "application/second")
           .AddParent("application/other", "application/first");

        builder.ClearParents(MimeType.Parse("application/child")).Should().Be(2);

        var registry = builder.Build();
        registry.IsSubtypeOf("application/child", "application/first").Should().BeFalse();
        registry.IsSubtypeOf("application/other", "application/first").Should().BeTrue();
    }

    [Fact]
    public void Builder_removal_and_replacement_report_missing_groups()
    {
        var builder = new MimeTypeRegistryBuilder();

        builder.RemoveGroup("not-valid").Should().BeFalse();
        builder.RemoveGroup(MimeType.Parse("application/missing")).Should().BeFalse();
        var act = () => builder.ReplaceGroup(
            MimeType.Parse("application/missing"),
            new MimeTypeGroup(MimeType.Parse("application/new"))
        );
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Builder_guard_clauses_reject_invalid_configuration_calls()
    {
        var builder = new MimeTypeRegistryBuilder();
        var nullGroup = () => builder.AddGroup(null!);
        var nullPrimary = () => builder.AddGroup(null!);
        var nullReplacement = () => builder.ReplaceGroup(MimeType.Parse("application/a"), null!);
        var invalidSuffix = () => builder.SetSuffixParent("not valid", MimeType.Parse("application/a"));
        var emptySuffix = () => builder.RemoveSuffixParent("");
        var nullPreference = () => builder.SetExtensionPreference(FileExtension.Parse("a"), null!);

        nullGroup.Should().Throw<ArgumentNullException>();
        nullPrimary.Should().Throw<ArgumentNullException>();
        nullReplacement.Should().Throw<ArgumentNullException>();
        invalidSuffix.Should().Throw<FormatException>();
        emptySuffix.Should().Throw<ArgumentException>();
        nullPreference.Should().Throw<ArgumentNullException>();
    }

    private static MimeTypeGroup Group(string mimeType, string extension)
    {
        return new MimeTypeGroup(
            MimeType.Parse(mimeType),
            extensions: [FileExtension.Parse(extension)]
        );
    }
}
