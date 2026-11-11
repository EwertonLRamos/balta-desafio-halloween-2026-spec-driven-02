using LinkShortener.Application.Links;
using Xunit;

namespace LinkShortener.Tests.Links;

public sealed class LinkExpirationValidatorTests
{
    private readonly LinkExpirationValidator _validator = new();

    [Fact]
    public void Validate_accepts_a_date_on_the_current_utc_day_through_its_end()
    {
        var currentUtcDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiration = new DateTimeOffset(
            currentUtcDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));

        var result = _validator.Validate(expiration);

        Assert.True(result.IsValid);
        Assert.Equal(
            new DateTimeOffset(currentUtcDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
            result.ExpiresAtUtc);
    }

    [Fact]
    public void Validate_uses_the_utc_date_when_the_input_has_a_non_zero_offset()
    {
        var utcDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var expiration = new DateTimeOffset(
            utcDate.ToDateTime(new TimeOnly(23, 30), DateTimeKind.Unspecified),
            TimeSpan.FromHours(-2));
        var expectedUtcDate = utcDate.AddDays(1);

        var result = _validator.Validate(expiration);

        Assert.True(result.IsValid);
        Assert.Equal(
            new DateTimeOffset(expectedUtcDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
            result.ExpiresAtUtc);
    }

    [Fact]
    public void Validate_rejects_a_date_before_the_current_utc_day()
    {
        var yesterdayUtc = DateTime.UtcNow.Date.AddDays(-1);
        var expiration = new DateTimeOffset(yesterdayUtc, TimeSpan.Zero);

        var result = _validator.Validate(expiration);

        Assert.False(result.IsValid);
        Assert.Null(result.ExpiresAtUtc);
        Assert.Equal("A data de expiração não pode estar no passado.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_accepts_a_missing_expiration()
    {
        var result = _validator.Validate(null);

        Assert.True(result.IsValid);
        Assert.Null(result.ExpiresAtUtc);
        Assert.Null(result.ErrorMessage);
    }
}
