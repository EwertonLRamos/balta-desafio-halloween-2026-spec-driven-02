namespace LinkShortener.Application.Links;

public interface ILinkService
{
    Task<LinkRedirectResult> GetTargetUrlAndIncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken);

    Task<LinkStatisticsResult> GetStatisticsAsync(
        string code,
        string? managementKey,
        CancellationToken cancellationToken);

    Task<LinkCreationResult> CreateAsync(
        string? targetUrl,
        string? code,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken);
}
