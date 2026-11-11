using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinkShortener.Tests.Links;

public sealed class LinkCreationEndpointTests
{
    private const string TargetUrl = "https://example.com/destination";

    [Fact]
    public async Task Post_creates_and_persists_a_link_with_201_created()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var body = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
            Assert.NotNull(body);
            Assert.Equal(7, body.Code.Length);
            Assert.All(body.Code, character => Assert.Contains(character, "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789"));
            Assert.Equal($"http://localhost/{body.Code}", body.ShortUrl);
            Assert.False(string.IsNullOrWhiteSpace(body.ManagementKey));
            Assert.Equal($"/{body.Code}", response.Headers.Location!.OriginalString);

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            var persistedLink = await dbContext.Links
                .AsNoTracking()
                .SingleAsync(link => link.Code == body.Code);

            Assert.Equal(TargetUrl, persistedLink.TargetUrl);
            Assert.Equal(0, persistedLink.AccessCount);
            Assert.Null(persistedLink.ExpiresAtUtc);
            Assert.NotEqual(body.ManagementKey, persistedLink.ManagementKeyHash);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Post_creates_a_link_with_a_custom_code()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl, "MY42"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
            Assert.NotNull(body);
            Assert.Equal("MY42", body.Code);
            Assert.Equal($"http://localhost/{body.Code}", body.ShortUrl);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Post_normalizes_expiration_to_the_end_of_its_utc_date()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");
        var utcDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var requestedExpiration = new DateTimeOffset(
            utcDate.ToDateTime(new TimeOnly(23, 30), DateTimeKind.Unspecified),
            TimeSpan.FromHours(-2));
        var expectedUtcDate = utcDate.AddDays(1);

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl, "EXPIRE1", requestedExpiration));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
            Assert.NotNull(body);

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            var persistedLink = await dbContext.Links
                .AsNoTracking()
                .SingleAsync(link => link.Code == body.Code);

            Assert.Equal(
                new DateTimeOffset(expectedUtcDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)),
                persistedLink.ExpiresAtUtc);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Post_returns_400_when_expiration_is_before_the_current_utc_day()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");
        var yesterdayUtc = DateTime.UtcNow.Date.AddDays(-1);
        var expiration = new DateTimeOffset(yesterdayUtc, TimeSpan.Zero);

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl, "PAST01", expiration));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var validationProblem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
            Assert.NotNull(validationProblem);
            Assert.Contains("expiresAtUtc", validationProblem.Errors.Keys);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Get_by_code_redirects_to_the_link_target()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            var createResponse = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl, "REDIR7"));
            var createdLink = await createResponse.Content.ReadFromJsonAsync<CreateLinkResponse>();

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createdLink);

            var redirectResponse = await client.GetAsync($"/{createdLink.Code}");

            Assert.Equal(HttpStatusCode.Found, redirectResponse.StatusCode);
            Assert.Equal(TargetUrl, redirectResponse.Headers.Location?.ToString());

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            var persistedLink = await dbContext.Links
                .AsNoTracking()
                .SingleAsync(link => link.Code == createdLink.Code);

            Assert.Equal(1, persistedLink.AccessCount);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Concurrent_redirects_increment_the_access_count_without_losing_updates()
    {
        const int requestCount = 40;
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            var createResponse = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl, "COUNT8"));
            var createdLink = await createResponse.Content.ReadFromJsonAsync<CreateLinkResponse>();

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createdLink);

            var redirectResponses = await Task.WhenAll(
                Enumerable.Range(0, requestCount)
                    .Select(_ => client.GetAsync($"/{createdLink.Code}")));

            Assert.All(redirectResponses, response => Assert.Equal(HttpStatusCode.Found, response.StatusCode));

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            var persistedLink = await dbContext.Links
                .AsNoTracking()
                .SingleAsync(link => link.Code == createdLink.Code);

            Assert.Equal(requestCount, persistedLink.AccessCount);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Get_by_unknown_code_returns_not_found()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/UNKNOWN");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Get_by_expired_code_returns_gone_without_incrementing_access_count()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            var createResponse = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl, "EXPIRE1"));
            var createdLink = await createResponse.Content.ReadFromJsonAsync<CreateLinkResponse>();

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createdLink);

            await SetExpirationAsync(factory, createdLink.Code, DateTimeOffset.UtcNow.AddSeconds(-1));

            var redirectResponse = await client.GetAsync($"/{createdLink.Code}");

            Assert.Equal(HttpStatusCode.Gone, redirectResponse.StatusCode);
            Assert.Equal("application/problem+json", redirectResponse.Content.Headers.ContentType?.MediaType);

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            var persistedLink = await dbContext.Links
                .AsNoTracking()
                .SingleAsync(link => link.Code == createdLink.Code);

            Assert.Equal(0, persistedLink.AccessCount);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Cached_redirect_returns_gone_without_incrementing_an_expired_link()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            var createResponse = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl, "CACHE2"));
            var createdLink = await createResponse.Content.ReadFromJsonAsync<CreateLinkResponse>();

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createdLink);

            var firstRedirect = await client.GetAsync($"/{createdLink.Code}");
            Assert.Equal(HttpStatusCode.Found, firstRedirect.StatusCode);
            await SetExpirationAsync(factory, createdLink.Code, DateTimeOffset.UtcNow.AddSeconds(-1));

            var expiredRedirect = await client.GetAsync($"/{createdLink.Code}");

            Assert.Equal(HttpStatusCode.Gone, expiredRedirect.StatusCode);

            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
            var persistedLink = await dbContext.Links
                .AsNoTracking()
                .SingleAsync(link => link.Code == createdLink.Code);

            Assert.Equal(1, persistedLink.AccessCount);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Post_returns_409_when_a_custom_code_is_already_in_use()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();
            var request = new CreateLinkRequest(TargetUrl, "MY42");

            var firstResponse = await client.PostAsJsonAsync("/api/links", request);
            var duplicateResponse = await client.PostAsJsonAsync("/api/links", request);

            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
            Assert.Equal("application/problem+json", duplicateResponse.Content.Headers.ContentType?.MediaType);

            var problem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>();
            Assert.NotNull(problem);
            Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
            Assert.Equal("O código informado já está em uso.", problem.Detail);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public async Task Post_returns_400_when_custom_code_length_is_out_of_range(string code)
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest(TargetUrl, code));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    private sealed class LinkShortenerApiFactory(string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath};Pooling=False"
                });
            });
        }
    }

    private static async Task SetExpirationAsync(
        LinkShortenerApiFactory factory,
        string code,
        DateTimeOffset expiresAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
        await dbContext.Links
            .Where(link => link.Code == code)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                link => link.ExpiresAtUtc,
                expiresAtUtc));
    }

    private sealed record ProblemDetailsResponse(int? Status, string? Detail);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
