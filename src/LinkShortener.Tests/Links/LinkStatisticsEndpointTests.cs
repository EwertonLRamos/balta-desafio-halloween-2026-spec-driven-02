using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Links;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LinkShortener.Tests.Links;

public sealed class LinkStatisticsEndpointTests
{
    [Fact]
    public async Task Get_statistics_returns_access_count_when_management_key_is_valid()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();
            var createResponse = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest("https://example.com/destination", "STATS1"));
            var createdLink = await createResponse.Content.ReadFromJsonAsync<CreateLinkResponse>();

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createdLink);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/links/{createdLink.Code}/stats");
            request.Headers.Add("X-Management-Key", createdLink.ManagementKey);

            var response = await client.SendAsync(request);
            var statistics = await response.Content.ReadFromJsonAsync<LinkStatisticsResponse>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(statistics);
            Assert.Equal(0, statistics.AccessCount);
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
    [InlineData("invalid-management-key")]
    [InlineData(null)]
    public async Task Get_statistics_returns_401_when_management_key_is_invalid_or_missing(
        string? managementKey)
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();
            var createResponse = await client.PostAsJsonAsync(
                "/api/links",
                new CreateLinkRequest("https://example.com/destination", "STATS1"));
            var createdLink = await createResponse.Content.ReadFromJsonAsync<CreateLinkResponse>();

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createdLink);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/links/{createdLink.Code}/stats");
            if (managementKey is not null)
            {
                request.Headers.Add("X-Management-Key", managementKey);
            }

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    [Fact]
    public async Task Get_statistics_returns_not_found_for_unknown_code()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"link-shortener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var databasePath = Path.Combine(testDirectory, "links.db");

        try
        {
            using var factory = new LinkShortenerApiFactory(databasePath);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/links/UNKNOWN/stats");

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
}
