namespace LinkShortener.Application.Links;

public interface ILinkService
{
    Task<string?> GetTargetUrlAsync(string code, CancellationToken cancellationToken);

    Task<LinkCreationResult> CreateAsync(
        string? targetUrl,
        string? code,
        CancellationToken cancellationToken);
}
