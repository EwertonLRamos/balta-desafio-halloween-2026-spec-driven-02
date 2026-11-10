using LinkShortener.Domain;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Infrastructure.Persistence;

public sealed class LinkShortenerDbContext(DbContextOptions<LinkShortenerDbContext> options)
    : DbContext(options)
{
    public DbSet<Link> Links => Set<Link>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Link>(entity =>
        {
            entity.ToTable("Links");
            entity.HasKey(link => link.Id);
            entity.HasIndex(link => link.Code).IsUnique();

            entity.Property(link => link.Code)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(link => link.TargetUrl)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(link => link.CreatedAtUtc)
                .IsRequired();

            entity.Property(link => link.AccessCount)
                .IsRequired();

            entity.Property(link => link.ManagementKeyHash)
                .IsRequired();
        });
    }
}
