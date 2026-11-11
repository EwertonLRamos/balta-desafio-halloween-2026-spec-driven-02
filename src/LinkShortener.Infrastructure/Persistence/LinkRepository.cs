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

    public async Task<LinkRedirectResult> GetTargetUrlAndIncrementAccessCountAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{TargetUrlCacheKeyPrefix}{code}";
        if (memoryCache.TryGetValue(cacheKey, out string? cachedTargetUrl))
        {
            var currentUtc = DateTimeOffset.UtcNow;
            var updatedLinks = await IncrementAccessCountAsync(code, currentUtc, cancellationToken);
            return updatedLinks == 0
                ? await GetUnavailableLinkResultAsync(code, currentUtc, cancellationToken)
                : LinkRedirectResult.Found(cachedTargetUrl
                    ?? throw new InvalidOperationException("Um destino em cache não pode ser nulo."));
        }

        var currentUtcOnMiss = DateTimeOffset.UtcNow;
        var updatedLinksOnMiss = await IncrementAccessCountAsync(
            code,
            currentUtcOnMiss,
            cancellationToken);

        if (updatedLinksOnMiss == 0)
        {
            return await GetUnavailableLinkResultAsync(code, currentUtcOnMiss, cancellationToken);
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

        return targetUrl is null
            ? LinkRedirectResult.NotFound()
            : LinkRedirectResult.Found(targetUrl);
    }

    private async Task<LinkRedirectResult> GetUnavailableLinkResultAsync(
        string code,
        DateTimeOffset currentUtc,
        CancellationToken cancellationToken)
    {
        var link = await dbContext.Links
            .AsNoTracking()
            .Where(link => link.Code == code)
            .Select(link => new { link.ExpiresAtUtc })
            .SingleOrDefaultAsync(cancellationToken);

        return link?.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc < currentUtc
            ? LinkRedirectResult.Expired()
            : LinkRedirectResult.NotFound();
    }

    private Task<int> IncrementAccessCountAsync(
        string code,
        DateTimeOffset currentUtc,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "Links"
            SET "AccessCount" = "AccessCount" + 1
            WHERE "Code" = {code}
              AND ("ExpiresAtUtc" IS NULL OR "ExpiresAtUtc" >= {currentUtc})
            """,
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
            dbContext.Entry(link).State = EntityState.Detached;
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
