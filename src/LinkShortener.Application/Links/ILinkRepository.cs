using LinkShortener.Domain;

namespace LinkShortener.Application.Links;

public interface ILinkRepository
{
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken);

    Task<string?> GetTargetUrlAndIncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken);

    Task<LinkRepositoryAddResult> AddAsync(Link link, CancellationToken cancellationToken);
}
