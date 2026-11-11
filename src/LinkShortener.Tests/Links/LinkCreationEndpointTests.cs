using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinkShortener.Tests.Links;

public sealed class LinkCreationEndpointTests
{
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
                new CreateLinkRequest("https://example.com/destination"));

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

            Assert.Equal("https://example.com/destination", persistedLink.TargetUrl);
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
