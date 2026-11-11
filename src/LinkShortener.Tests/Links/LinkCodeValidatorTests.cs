using LinkShortener.Application.Links;
using Xunit;

namespace LinkShortener.Tests.Links;

public sealed class LinkCodeValidatorTests
{
    private readonly LinkCodeValidator _validator = new();

    [Theory]
    [InlineData("ABCD")]
    [InlineData("1234")]
    [InlineData("A1B2")]
    public void Validate_accepts_only_codes_with_allowed_characters(string code)
    {
        var errorMessage = _validator.Validate(code);

        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData("abCD")]
    [InlineData("ABC0")]
    [InlineData("AB C")]
    [InlineData("ABC!")]
    public void Validate_rejects_lowercase_zero_whitespace_and_special_characters(string code)
    {
        var errorMessage = _validator.Validate(code);

        Assert.Equal(
            "O código deve conter somente letras maiúsculas de A a Z e números de 1 a 9.",
            errorMessage);
    }
}
