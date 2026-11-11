using LinkShortener.Application.Links;
using LinkShortener.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class LinkRepository(LinkShortenerDbContext dbContext) : ILinkRepository
{
    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Links.AnyAsync(link => link.Code == code, cancellationToken);

    public async Task<string?> GetTargetUrlAndIncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var updatedLinks = await dbContext.Links
            .Where(link => link.Code == code)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    link => link.AccessCount,
                    link => link.AccessCount + 1),
                cancellationToken);

        if (updatedLinks == 0)
        {
            return null;
        }

        return await dbContext.Links
            .AsNoTracking()
            .Where(link => link.Code == code)
            .Select(link => link.TargetUrl)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<LinkStatisticsData?> GetStatisticsDataByCodeAsync(
        string code,
        CancellationToken cancellationToken) =>
        dbContext.Links
            .AsNoTracking()
            .Where(link => link.Code == code)
            .Select(link => new LinkStatisticsData(link.ManagementKeyHash, link.AccessCount))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<LinkRepositoryAddResult> AddAsync(
        Link link,
        CancellationToken cancellationToken)
    {
        await dbContext.Links.AddAsync(link, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return LinkRepositoryAddResult.Added;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return LinkRepositoryAddResult.DuplicateCode;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };
}
