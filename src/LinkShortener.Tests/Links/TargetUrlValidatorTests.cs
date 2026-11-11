using LinkShortener.Application.Links;
using Xunit;

namespace LinkShortener.Tests.Links;

public sealed class TargetUrlValidatorTests
{
    private readonly TargetUrlValidator _validator = new();

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path?query=value")]
    public void Validate_accepts_http_and_https_urls(string targetUrl)
    {
        var result = _validator.Validate(targetUrl);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com")]
    [InlineData("https:///missing-host")]
    [InlineData("https://example.com/path with spaces")]
    [InlineData("http://localhost")]
    [InlineData("http://192.168.1.10")]
    public void Validate_rejects_invalid_or_non_public_urls(string? targetUrl)
    {
        var result = _validator.Validate(targetUrl);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void Validate_accepts_a_url_with_2048_characters()
    {
        var targetUrl = $"https://example.com/{new string('a', 2048 - "https://example.com/".Length)}";

        var result = _validator.Validate(targetUrl);

        Assert.Equal(2048, targetUrl.Length);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_a_url_longer_than_2048_characters()
    {
        var targetUrl = $"https://example.com/{new string('a', 2049 - "https://example.com/".Length)}";

        var result = _validator.Validate(targetUrl);

        Assert.Equal(2049, targetUrl.Length);
        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}
