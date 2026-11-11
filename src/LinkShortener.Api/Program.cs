using LinkShortener.Api.Links;
using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache(options => options.SizeLimit = 10_000);
builder.Services.AddSingleton<ILinkCodeGenerator, LinkCodeGenerator>();
builder.Services.AddSingleton<LinkCodeValidator>();
builder.Services.AddSingleton<LinkExpirationValidator>();
builder.Services.AddSingleton<TargetUrlValidator>();
builder.Services.AddScoped<ILinkRepository, LinkRepository>();
builder.Services.AddScoped<ILinkService, LinkService>();
builder.Services.AddDbContext<LinkShortenerDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=links.db"));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapLinkEndpoints();
app.MapLinkStatisticsEndpoints();

app.Run();

public partial class Program
{
}
