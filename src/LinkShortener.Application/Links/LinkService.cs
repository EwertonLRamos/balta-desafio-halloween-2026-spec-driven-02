using System.Security.Cryptography;
using System.Text;
using LinkShortener.Domain;

namespace LinkShortener.Application.Links;

public sealed class LinkService(
    ILinkRepository linkRepository,
    ILinkCodeGenerator linkCodeGenerator,
    TargetUrlValidator targetUrlValidator,
    LinkCodeValidator linkCodeValidator,
    LinkExpirationValidator linkExpirationValidator) : ILinkService
{
    private const int MaximumAutomaticCodeGenerationAttempts = 5;

    public Task<LinkRedirectResult> GetTargetUrlAndIncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken) =>
        linkRepository.GetTargetUrlAndIncrementAccessCountAsync(code, cancellationToken);

    public async Task<LinkStatisticsResult> GetStatisticsAsync(
        string code,
        string? managementKey,
        CancellationToken cancellationToken)
    {
        var statistics = await linkRepository.GetStatisticsDataByCodeAsync(code, cancellationToken);
        if (statistics is null)
        {
            return LinkStatisticsResult.NotFound();
        }

        if (string.IsNullOrEmpty(managementKey))
        {
            return LinkStatisticsResult.Unauthorized();
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(managementKey));
        var storedHash = Convert.FromHexString(statistics.ManagementKeyHash);
        if (!CryptographicOperations.FixedTimeEquals(providedHash, storedHash))
        {
            return LinkStatisticsResult.Unauthorized();
        }

        return LinkStatisticsResult.Found(statistics.AccessCount);
    }

    public async Task<LinkCreationResult> CreateAsync(
        string? targetUrl,
        string? code,
        DateTimeOffset? expiresAtUtc,
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

        var expirationValidation = linkExpirationValidator.Validate(expiresAtUtc);
        if (!expirationValidation.IsValid)
        {
            return LinkCreationResult.Invalid(
                expirationValidation.ErrorMessage
                    ?? throw new InvalidOperationException(
                        "A validação inválida da expiração deve incluir uma mensagem."),
                "expiresAtUtc");
        }

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
        var maximumAttempts = code is null ? MaximumAutomaticCodeGenerationAttempts : 1;
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var linkCode = code ?? linkCodeGenerator.Generate();
            var addResult = await linkRepository.AddAsync(
                new Link
                {
                    Code = linkCode,
                    TargetUrl = validTargetUrl,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    ExpiresAtUtc = expirationValidation.ExpiresAtUtc,
                    AccessCount = 0,
                    ManagementKeyHash = managementKeyHash
                },
                cancellationToken);

            switch (addResult)
            {
                case LinkRepositoryAddResult.Added:
                    return LinkCreationResult.Success(linkCode, validTargetUrl, managementKey);
                case LinkRepositoryAddResult.DuplicateCode when code is not null:
                    return LinkCreationResult.Conflict("O código informado já está em uso.");
                case LinkRepositoryAddResult.DuplicateCode:
                    continue;
                default:
                    throw new InvalidOperationException("Resultado de persistência não reconhecido.");
            }
        }

        return LinkCreationResult.Conflict(
            "Não foi possível gerar um código disponível após várias tentativas.");
    }
}
