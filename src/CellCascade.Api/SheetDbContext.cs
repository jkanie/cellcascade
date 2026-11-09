using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CellCascade.Api;

/// <summary>A saved sheet. Its name is the key, with the spelling it had when it was made.</summary>
public sealed class SheetRow
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>A saved cell: only the input that a person typed. The value is worked out again when the sheet loads.</summary>
public sealed class CellRow
{
    public string SheetName { get; set; } = string.Empty;

    /// <summary>The cell address, such as B7.</summary>
    public string Ref { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;
}

/// <summary>The database of sheets: 1 table of sheet names, 1 table of cell inputs.</summary>
public sealed class SheetDbContext(DbContextOptions<SheetDbContext> options) : DbContext(options)
{
    public DbSet<SheetRow> Sheets => Set<SheetRow>();

    public DbSet<CellRow> Cells => Set<CellRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<SheetRow>(sheet =>
        {
            sheet.HasKey(s => s.Name);
            sheet.Property(s => s.Name).HasMaxLength(64);
            sheet.HasMany<CellRow>().WithOne().HasForeignKey(c => c.SheetName).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CellRow>(cell =>
        {
            cell.HasKey(c => new { c.SheetName, c.Ref });
            cell.Property(c => c.Ref).HasMaxLength(10);
            cell.Property(c => c.Input).HasMaxLength(Parser.MaxInputLength);
        });
    }
}

/// <summary>
/// Picks the database from configuration. Database:Provider is Sqlite (the default) or PostgreSql.
/// SQLite uses the file in Database:SqlitePath, relative to the content root. PostgreSQL uses ConnectionStrings:PostgreSql.
/// </summary>
public static class DatabaseConfig
{
    public const string DefaultSqlitePath = "cellcascade.db";

    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);
        var provider = configuration["Database:Provider"];
        if (string.IsNullOrWhiteSpace(provider) || provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var path = configuration["Database:SqlitePath"];
            if (string.IsNullOrWhiteSpace(path))
            {
                path = DefaultSqlitePath;
            }

            var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(contentRoot, path) };
            options.UseSqlite(connection.ToString());
            return;
        }

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var connection = configuration.GetConnectionString("PostgreSql");
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("Database:Provider is PostgreSql, but ConnectionStrings:PostgreSql is empty.");
            }

            options.UseNpgsql(connection);
            return;
        }

        throw new InvalidOperationException($"Database:Provider is '{provider}'. It must be Sqlite or PostgreSql.");
    }
}
