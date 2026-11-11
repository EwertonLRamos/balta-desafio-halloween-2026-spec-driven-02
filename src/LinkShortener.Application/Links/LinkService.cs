using System.Security.Cryptography;
using System.Text;
using LinkShortener.Domain;

namespace LinkShortener.Application.Links;

public sealed class LinkService(
    ILinkRepository linkRepository,
    LinkCodeGenerator linkCodeGenerator,
    TargetUrlValidator targetUrlValidator,
    LinkCodeValidator linkCodeValidator) : ILinkService
{
    public Task<string?> GetTargetUrlAndIncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken) =>
        linkRepository.GetTargetUrlAndIncrementAccessCountAsync(code, cancellationToken);

    public async Task<LinkCreationResult> CreateAsync(
        string? targetUrl,
        string? code,
        CancellationToken cancellationToken)
    {
        var validation = targetUrlValidator.Validate(targetUrl);
        if (!validation.IsValid)
        {
            return LinkCreationResult.Invalid(
                validation.ErrorMessage
                    ?? throw new InvalidOperationException("A validação inválida deve incluir uma mensagem."));
        }

        var validTargetUrl = targetUrl
            ?? throw new InvalidOperationException("A validação válida deve incluir uma URL de destino.");

        if (code is not null)
        {
            var codeValidationError = linkCodeValidator.Validate(code);
            if (codeValidationError is not null)
            {
                return LinkCreationResult.Invalid(codeValidationError, "code");
            }

            if (await linkRepository.ExistsByCodeAsync(code, cancellationToken))
            {
                return LinkCreationResult.Conflict("O código informado já está em uso.");
            }
        }

        var managementKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var managementKeyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(managementKey)));
        var linkCode = code ?? linkCodeGenerator.Generate();

        var addResult = await linkRepository.AddAsync(
            new Link
            {
                Code = linkCode,
                TargetUrl = validTargetUrl,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                AccessCount = 0,
                ManagementKeyHash = managementKeyHash
            },
            cancellationToken);

        return addResult switch
        {
            LinkRepositoryAddResult.Added =>
                LinkCreationResult.Success(linkCode, validTargetUrl, managementKey),
            LinkRepositoryAddResult.DuplicateCode =>
                LinkCreationResult.Conflict(code is null
                    ? "Não foi possível gerar um código disponível."
                    : "O código informado já está em uso."),
            _ => throw new InvalidOperationException("Resultado de persistência não reconhecido.")
        };
    }
}
