using LinkShortener.Application.Links;
using LinkShortener.Domain;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class LinkRepository(LinkShortenerDbContext dbContext) : ILinkRepository
{
    public async Task AddAsync(Link link, CancellationToken cancellationToken)
    {
        await dbContext.Links.AddAsync(link, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
