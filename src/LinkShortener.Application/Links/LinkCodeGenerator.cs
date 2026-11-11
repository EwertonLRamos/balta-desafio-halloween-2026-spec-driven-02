using System.Security.Cryptography;

namespace LinkShortener.Application.Links;

public sealed class LinkCodeGenerator : ILinkCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
    private const int CodeLength = 7;

    public string Generate()
    {
        var code = new char[CodeLength];

        for (var index = 0; index < code.Length; index++)
        {
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
