using LinkShortener.Application.Links;
using LinkShortener.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class LinkRepository(
    LinkShortenerDbContext dbContext,
    IMemoryCache memoryCache) : ILinkRepository
{
    private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromHours(24);
    private const int CacheEntrySize = 1;
    private const string TargetUrlCacheKeyPrefix = "link-target:";

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Links.AnyAsync(link => link.Code == code, cancellationToken);

    public async Task<string?> GetTargetUrlAndIncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{TargetUrlCacheKeyPrefix}{code}";
        if (memoryCache.TryGetValue(cacheKey, out string? cachedTargetUrl))
        {
            var updatedLinks = await IncrementAccessCountAsync(code, cancellationToken);
            return updatedLinks == 0 ? null : cachedTargetUrl;
        }

        var updatedLinksOnMiss = await IncrementAccessCountAsync(code, cancellationToken);

        if (updatedLinksOnMiss == 0)
        {
            return null;
        }

        var targetUrl = await dbContext.Links
            .AsNoTracking()
            .Where(link => link.Code == code)
            .Select(link => link.TargetUrl)
            .SingleOrDefaultAsync(cancellationToken);

        if (targetUrl is not null)
        {
            memoryCache.Set(
                cacheKey,
                targetUrl,
                new MemoryCacheEntryOptions()
                    .SetSize(CacheEntrySize)
                    .SetSlidingExpiration(CacheSlidingExpiration)
                    .SetAbsoluteExpiration(CacheAbsoluteExpiration));
        }

        return targetUrl;
    }

    private Task<int> IncrementAccessCountAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Links
            .Where(link => link.Code == code)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    link => link.AccessCount,
                    link => link.AccessCount + 1),
                cancellationToken);

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
