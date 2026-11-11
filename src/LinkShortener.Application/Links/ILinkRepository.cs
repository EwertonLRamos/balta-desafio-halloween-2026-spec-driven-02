using LinkShortener.Domain;

namespace LinkShortener.Application.Links;

public interface ILinkRepository
{
    Task AddAsync(Link link, CancellationToken cancellationToken);
}
