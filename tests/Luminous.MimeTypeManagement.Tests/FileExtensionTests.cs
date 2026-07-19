using System;
using FluentAssertions;
using Xunit;

namespace Luminous.MimeTypeManagement.Tests;

public sealed class FileExtensionTests
{
    [Theory]
    [InlineData("PDF", ".pdf")]
    [InlineData(".Pdf", ".pdf")]
    [InlineData("*.PDF", ".pdf")]
    [InlineData(".Tar.GZ", ".tar.gz")]
    [InlineData("c++", ".c++")]
    public void ParseNormalizesSupportedForms(string value, string expected)
    {
        var result = FileExtension.Parse(value);

        result.Value.Should().Be(expected);
        result.ToString().Should().Be(expected);
        result.IsDefault.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("*pdf")]
    [InlineData("**.pdf")]
    [InlineData("file*.pdf")]
    [InlineData("tar..gz")]
    [InlineData("pdf ")]
    [InlineData("path/pdf")]
    [InlineData("path\\pdf")]
    [InlineData(".é")]
    public void TryParseRejectsInvalidExtensions(string value)
    {
        FileExtension.TryParse(value, out var result).Should().BeFalse();
        result.Should().Be(default(FileExtension));
    }

    [Fact]
    public void ParseThrowsForInvalidInput()
    {
        var action = () => FileExtension.Parse("*.tar.*");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void EqualityIsValueBased()
    {
        var first = FileExtension.Parse("JPG");
        var second = FileExtension.Parse(".jpg");

        first.Equals((object) second).Should().BeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global -- required in testing scenario
        first.Equals(".jpg").Should().BeFalse();
        (first == second).Should().BeTrue();
        (first != second).Should().BeFalse();
        first.GetHashCode().Should().Be(second.GetHashCode());
        default(FileExtension).IsDefault.Should().BeTrue();
        default(FileExtension).Value.Should().BeEmpty();
        default(FileExtension).GetHashCode().Should().Be(0);
    }
}
