using System.Data.Common;
using LinkShortener.Application.Links;
using LinkShortener.Domain;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace LinkShortener.Tests.Links;

public sealed class LinkRepositoryCacheTests
{
    [Fact]
    public async Task Cached_redirect_skips_the_target_lookup_but_still_increments_access_count()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var interceptor = new QueryCountingInterceptor();
        var options = new DbContextOptionsBuilder<LinkShortenerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        await using var dbContext = new LinkShortenerDbContext(options);
        await dbContext.Database.MigrateAsync();
        dbContext.Links.Add(new Link
        {
            Code = "CACHE1",
            TargetUrl = "https://example.com/destination",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            AccessCount = 0,
            ManagementKeyHash = "management-key-hash"
        });
        await dbContext.SaveChangesAsync();

        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000 });
        var repository = new LinkRepository(dbContext, memoryCache);
        interceptor.Reset();

        var firstResult = await repository.GetTargetUrlAndIncrementAccessCountAsync(
            "CACHE1",
            CancellationToken.None);
        var secondResult = await repository.GetTargetUrlAndIncrementAccessCountAsync(
            "CACHE1",
            CancellationToken.None);

        Assert.Equal(LinkRedirectStatus.Found, firstResult.Status);
        Assert.Equal("https://example.com/destination", firstResult.TargetUrl);
        Assert.Equal(firstResult, secondResult);
        Assert.Equal(1, interceptor.ReaderCommands);
        Assert.Equal(2, interceptor.NonQueryCommands);

        var link = await dbContext.Links
            .AsNoTracking()
            .SingleAsync(item => item.Code == "CACHE1");
        Assert.Equal(2, link.AccessCount);
    }

    private sealed class QueryCountingInterceptor : DbCommandInterceptor
    {
        private int _readerCommands;
        private int _nonQueryCommands;

        public int ReaderCommands => Volatile.Read(ref _readerCommands);

        public int NonQueryCommands => Volatile.Read(ref _nonQueryCommands);

        public void Reset()
        {
            Interlocked.Exchange(ref _readerCommands, 0);
            Interlocked.Exchange(ref _nonQueryCommands, 0);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readerCommands);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _nonQueryCommands);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
