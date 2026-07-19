using FluentAssertions;
using Xunit;

namespace Luminous.MimeTypeManagement.Tests;

public sealed class MimeTypeHierarchyTests
{
    [Fact]
    public void Hierarchy_is_reflexive_after_alias_normalization()
    {
        var registry = new MimeTypeRegistryBuilder()
           .AddGroup(MimeType.Parse("application/json"), [MimeType.Parse("text/json")])
           .Build();

        registry.IsSubtypeOf("text/json", "application/json").Should().BeTrue();
        registry.IsSubtypeOf("application/json", "text/json").Should().BeTrue();
    }

    [Fact]
    public void Explicit_hierarchy_is_transitive_and_supports_multiple_parents()
    {
        var registry = new MimeTypeRegistryBuilder()
           .AddParentRelation(
                MimeType.Parse("application/child"),
                MimeType.Parse("application/first")
            )
           .AddParent("application/child", "application/second")
           .AddParent("application/first", "application/root")
           .Build();

        registry.IsSubtypeOf("application/child", "application/first").Should().BeTrue();
        registry.IsSubtypeOf("application/child", "application/second").Should().BeTrue();
        registry.IsSubtypeOf("application/child", "application/root").Should().BeTrue();
        registry.IsSubtypeOf("application/root", "application/child").Should().BeFalse();
    }

    [Theory]
    [InlineData("application/vnd.example+xml", "application/xml")]
    [InlineData("application/vnd.example+json", "application/json")]
    [InlineData("application/vnd.example+zip", "application/zip")]
    [InlineData("text/csv", "text/plain")]
    [InlineData("image/png", "application/octet-stream")]
    public void Default_implicit_rules_are_applied(string child, string parent)
    {
        var registry = new MimeTypeRegistryBuilder().Build();

        registry.IsSubtypeOf(child, parent).Should().BeTrue();
        registry.IsSubtypeOf(child, "application/octet-stream").Should().BeTrue();
    }

    [Fact]
    public void Explicit_relations_take_precedence_over_implicit_rules()
    {
        var registry = new MimeTypeRegistryBuilder()
           .AddParent("application/vnd.document+zip", "application/document")
           .Build();

        registry.IsSubtypeOf("application/vnd.document+zip", "application/document").Should().BeTrue();
        registry.IsSubtypeOf("application/vnd.document+zip", "application/zip").Should().BeFalse();
        registry.IsSubtypeOf("application/vnd.document+zip", "application/octet-stream").Should().BeTrue();
    }

    [Fact]
    public void Implicit_rules_can_be_reconfigured_and_disabled_independently()
    {
        var builder = new MimeTypeRegistryBuilder
        {
            StructuredSyntaxSuffixRulesEnabled = false,
            TextPlainRuleEnabled = false,
            FallbackRuleEnabled = false,
            TextParent = MimeType.Parse("application/custom-text"),
            FallbackParent = MimeType.Parse("application/custom-fallback")
        };
        builder.RemoveSuffixParent("json").Should().BeTrue();
        builder.SetSuffixParent("+yaml", "application/yaml").Should().BeSameAs(builder);
        var disabled = builder.Build();

        disabled.IsSubtypeOf("application/vnd.example+yaml", "application/yaml").Should().BeFalse();
        disabled.IsSubtypeOf("text/csv", "application/custom-text").Should().BeFalse();
        disabled.IsSubtypeOf("image/png", "application/custom-fallback").Should().BeFalse();

        builder.StructuredSyntaxSuffixRulesEnabled = true;
        builder.TextPlainRuleEnabled = true;
        builder.FallbackRuleEnabled = true;
        var enabled = builder.Build();

        enabled.IsSubtypeOf("application/vnd.example+yaml", "application/yaml").Should().BeTrue();
        enabled.IsSubtypeOf("application/vnd.example+json", "application/json").Should().BeFalse();
        enabled.IsSubtypeOf("text/csv", "application/custom-text").Should().BeTrue();
        enabled.IsSubtypeOf("image/png", "application/custom-fallback").Should().BeTrue();
    }

    [Fact]
    public void Self_targeting_implicit_rule_falls_through_to_the_next_rule()
    {
        var builder = new MimeTypeRegistryBuilder();
        builder.SetSuffixParent("json", MimeType.Parse("application/vnd.example+json"));
        var registry = builder.Build();

        registry.IsSubtypeOf("application/vnd.example+json", "application/octet-stream").Should().BeTrue();
    }

    [Fact]
    public void Diamond_hierarchies_traverse_shared_ancestors_only_once()
    {
        var registry = new MimeTypeRegistryBuilder()
           .AddParent("application/child", "application/left")
           .AddParent("application/child", "application/right")
           .AddParent("application/left", "application/root")
           .AddParent("application/right", "application/root")
           .Build();

        registry.IsSubtypeOf("application/child", "application/root").Should().BeTrue();
        registry.IsSubtypeOf("application/child", "application/unrelated").Should().BeFalse();
    }

    [Fact]
    public void Container_relationship_is_hierarchy_not_aliasing()
    {
        var docx = MimeType.Parse("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        var registry = new MimeTypeRegistryBuilder()
           .AddGroup(docx, extensions: [FileExtension.Parse("docx")])
           .AddGroup(MimeType.Parse("application/zip"), extensions: [FileExtension.Parse("zip")])
           .AddParent(docx, MimeType.Parse("application/zip"))
           .Build();

        registry.Normalize(docx).Should().Be(docx);
        registry.IsSubtypeOf(docx, MimeType.Parse("application/zip")).Should().BeTrue();
    }

    [Fact]
    public void Parent_relations_normalize_alias_endpoints()
    {
        var registry = new MimeTypeRegistryBuilder()
           .AddGroup(
                MimeType.Parse("application/canonical"),
                [MimeType.Parse("application/alias")]
            )
           .AddParent("application/alias", "application/root")
           .Build();

        registry.IsSubtypeOf("application/canonical", "application/root").Should().BeTrue();
    }
}
