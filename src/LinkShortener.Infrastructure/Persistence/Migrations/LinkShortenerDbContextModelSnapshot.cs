using LinkShortener.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LinkShortener.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LinkShortenerDbContext))]
public sealed class LinkShortenerDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity("LinkShortener.Domain.Link", entity =>
        {
            entity.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);

            entity.Property<long>("AccessCount")
                .HasColumnType("INTEGER");

            entity.Property<string>("Code")
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnType("TEXT");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("TEXT");

            entity.Property<DateTimeOffset?>("ExpiresAtUtc")
                .HasColumnType("TEXT");

            entity.Property<string>("ManagementKeyHash")
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property<string>("TargetUrl")
                .IsRequired()
                .HasMaxLength(2048)
                .HasColumnType("TEXT");

            entity.HasKey("Id");
            entity.HasIndex("Code").IsUnique();
            entity.ToTable("Links");
        });
    }
}
