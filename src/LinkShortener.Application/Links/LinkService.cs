using System.Security.Cryptography;
using System.Text;
using LinkShortener.Domain;

namespace LinkShortener.Application.Links;

public sealed class LinkService(
    ILinkRepository linkRepository,
    LinkCodeGenerator linkCodeGenerator,
    TargetUrlValidator targetUrlValidator) : ILinkService
{
    public async Task<LinkCreationResult> CreateAsync(
        string? targetUrl,
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
        var managementKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var managementKeyHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(managementKey)));
        var code = linkCodeGenerator.Generate();

        await linkRepository.AddAsync(
            new Link
            {
                Code = code,
                TargetUrl = validTargetUrl,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                AccessCount = 0,
                ManagementKeyHash = managementKeyHash
            },
            cancellationToken);

        return LinkCreationResult.Success(code, validTargetUrl, managementKey);
    }
}
