using LinkShortener.Domain;

namespace LinkShortener.Application.Links;

public interface ILinkRepository
{
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken);

    Task<LinkRedirectResult> GetTargetUrlAndIncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken);

    Task<LinkStatisticsData?> GetStatisticsDataByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    Task<LinkRepositoryAddResult> AddAsync(Link link, CancellationToken cancellationToken);
}
