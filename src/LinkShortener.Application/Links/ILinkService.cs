namespace LinkShortener.Application.Links;

public interface ILinkService
{
    Task<LinkCreationResult> CreateAsync(
        string? targetUrl,
        string? code,
        CancellationToken cancellationToken);
}
