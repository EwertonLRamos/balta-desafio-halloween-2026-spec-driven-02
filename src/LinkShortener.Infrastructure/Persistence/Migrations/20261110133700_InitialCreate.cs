using LinkShortener.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LinkShortener.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LinkShortenerDbContext))]
[Migration("20261110133700_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Links",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                TargetUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                AccessCount = table.Column<long>(type: "INTEGER", nullable: false),
                ManagementKeyHash = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Links", link => link.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Links_Code",
            table: "Links",
            column: "Code",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Links");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
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
