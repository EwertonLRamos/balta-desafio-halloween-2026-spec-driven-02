using LinkShortener.Application.Links;
using Xunit;

namespace LinkShortener.Tests.Links;

public sealed class LinkCodeGeneratorTests
{
    private const string AllowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

    [Fact]
    public void Generate_returns_seven_characters_from_the_allowed_alphabet()
    {
        var code = new LinkCodeGenerator().Generate();

        Assert.Equal(7, code.Length);
        Assert.All(code, character => Assert.Contains(character, AllowedCharacters));
    }
}
