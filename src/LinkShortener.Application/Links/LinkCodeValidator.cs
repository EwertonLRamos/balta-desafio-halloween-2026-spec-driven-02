namespace LinkShortener.Application.Links;

public sealed class LinkCodeValidator
{
    private const int MinimumLength = 4;
    private const int MaximumLength = 20;

    public string? Validate(string code)
    {
        if (code.Length is < MinimumLength or > MaximumLength)
        {
            return "O código deve ter entre 4 e 20 caracteres.";
        }

        if (code.Any(character =>
                character is not (>= 'A' and <= 'Z')
                    and not (>= '1' and <= '9')))
        {
            return "O código deve conter somente letras maiúsculas de A a Z e números de 1 a 9.";
        }

        return null;
    }
}
