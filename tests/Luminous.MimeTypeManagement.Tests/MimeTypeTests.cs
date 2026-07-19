using System;
using FluentAssertions;
using Xunit;

namespace Luminous.MimeTypeManagement.Tests;

public sealed class MimeTypeTests
{
    [Theory]
    [InlineData(
        "Application/Vnd.Example+JSON",
        "application/vnd.example+json",
        "application",
        "vnd.example+json",
        "json"
    )]
    [InlineData("text/plain; charset=utf-8", "text/plain", "text", "plain", null)]
    [InlineData("text/plain \t; ignored", "text/plain", "text", "plain", null)]
    [InlineData("application/example+", "application/example+", "application", "example+", null)]
    public void Parse_normalizes_and_exposes_components(
        string value,
        string expectedValue,
        string expectedTopLevelType,
        string expectedSubType,
        string? expectedSuffix
    )
    {
        var result = MimeType.Parse(value);

        result.Value.Should().Be(expectedValue);
        result.TopLevelType.Should().Be(expectedTopLevelType);
        result.SubType.Should().Be(expectedSubType);
        result.Subtype.Should().Be(expectedSubType);
        result.Suffix.Should().Be(expectedSuffix);
        result.IsDefault.Should().BeFalse();
        result.ToString().Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("text")]
    [InlineData("/plain")]
    [InlineData("text/")]
    [InlineData("text//plain")]
    [InlineData(" text/plain")]
    [InlineData("text/plain ")]
    [InlineData("téxt/plain")]
    [InlineData("text/pl@in")]
    [InlineData("text/*")]
    public void TryParse_rejects_invalid_restricted_names(string value)
    {
        MimeType.TryParse(value, out var result).Should().BeFalse();
        result.Should().Be(default(MimeType));
    }

    [Fact]
    public void Parse_throws_for_invalid_input()
    {
        var action = () => MimeType.Parse("not-a-media-type");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseOptions_enforces_the_component_limit()
    {
        var options = new MimeTypeParseOptions { MaxNameLength = 4 };

        MimeType.TryParse("text/json", options, out var valid).Should().BeTrue();
        valid.Should().Be(MimeType.Parse("text/json"));
        MimeType.TryParse("audio/json", options, out _).Should().BeFalse();
        MimeType.TryParse("text/javascript", options, out _).Should().BeFalse();
    }

    [Fact]
    public void ParseOptions_defaults_to_the_Rfc_limit()
    {
        var nameAtLimit = new string('a', MimeTypeParseOptions.RfcMaxNameLength);
        var nameOverLimit = nameAtLimit + "a";

        MimeType.TryParse($"{nameAtLimit}/{nameAtLimit}", out _).Should().BeTrue();
        MimeType.TryParse($"{nameOverLimit}/plain", out _).Should().BeFalse();
        MimeType.TryParse($"text/{nameOverLimit}", out _).Should().BeFalse();
        MimeTypeParseOptions.Default.MaxNameLength.Should().Be(127);
    }

    [Fact]
    public void ParseOptions_rejects_invalid_limits_and_null_options()
    {
        var invalidOptions = () => new MimeTypeParseOptions { MaxNameLength = 0 };
        var nullOptions = () => MimeType.TryParse("text/plain", null!, out _);

        invalidOptions.Should().Throw<ArgumentOutOfRangeException>();
        nullOptions.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equality_is_value_based()
    {
        var first = MimeType.Parse("IMAGE/JPEG");
        var second = MimeType.Parse("image/jpeg");

        first.Equals((object) second).Should().BeTrue();
        (first == second).Should().BeTrue();
        (first != second).Should().BeFalse();
        first.GetHashCode().Should().Be(second.GetHashCode());
        // ReSharper disable once SuspiciousTypeConversion.Global -- required for testing
        first.Equals("image/jpeg").Should().BeFalse();
        default(MimeType).IsDefault.Should().BeTrue();
        default(MimeType).Value.Should().BeEmpty();
        default(MimeType).GetHashCode().Should().Be(0);
    }
}
