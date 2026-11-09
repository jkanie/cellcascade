using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using CellCascade.Api;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CellCascade.Tests;

public sealed class PersistenceTests : IDisposable
{
    /// <summary>Set it to a PostgreSQL connection string to run the PostgreSQL test. The test makes and drops its own database.</summary>
    public const string PostgreSqlVariable = "CELLCASCADE_TEST_POSTGRESQL";

    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Factory factory;

    public PersistenceTests()
    {
        // 1 open in-memory connection for the whole test. Every store made on it shares 1 database, as after a re-start.
        connection.Open();
        factory = new Factory(new DbContextOptionsBuilder<SheetDbContext>().UseSqlite(connection).Options);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => connection.Dispose();

    [Fact]
    public void A_new_store_loads_every_sheet_with_the_same_inputs_and_values() => RoundTrip(factory);

    [Fact]
    public void Saves_only_the_inputs()
    {
        using var db = factory.CreateDbContext();
        var columns = db.Model.FindEntityType(typeof(CellRow))!.GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["Input", "Ref", "SheetName"], columns);
    }

    [Fact]
    public void Values_are_worked_out_from_the_inputs_when_a_sheet_loads()
    {
        _ = new SheetStore(factory);

        // Change an input behind the store's back. Only a re-calculation on load can show its effect.
        using (var db = factory.CreateDbContext())
        {
            db.Cells.Single(c => c.SheetName == SheetStore.SampleName && c.Ref == "B4").Input = "2";
            db.SaveChanges();
        }

        var sheet = new SheetStore(factory).Find(SheetStore.SampleName)!;
        Assert.Equal("36", sheet.ValueAt(A("D4")).Display);
        Assert.Equal("123.75", sheet.ValueAt(A("D8")).Display);
        Assert.Equal("within budget", sheet.ValueAt(A("D10")).Display);
        Assert.Equal(SheetStore.Sample.Values.Count(input => input.StartsWith('=')), sheet.Evaluations);
    }

    [Fact]
    public void Only_a_new_database_gets_the_sample_sheet()
    {
        var first = new SheetStore(factory);
        Assert.Equal([SheetStore.SampleName], first.Names());
        Assert.True(first.Remove(SheetStore.SampleName));
        Assert.Empty(new SheetStore(factory).Names());
    }

    [Fact]
    public void Clearing_a_cell_deletes_its_row()
    {
        var store = new SheetStore(factory);
        Set(store, "s", ("A1", "1"), ("B1", "=A1"));
        Set(store, "s", ("A1", ""));
        using var db = factory.CreateDbContext();
        Assert.Equal(["B1"], db.Cells.Where(c => c.SheetName == "s").Select(c => c.Ref).ToList());
    }

    [Fact]
    public void A_formula_that_cannot_be_read_saves_nothing()
    {
        var store = new SheetStore(factory);
        Set(store, "s", ("A1", "1"));
        Assert.Throws<FormulaException>(() => Set(store, "s", ("A1", "2"), ("B1", "=1+")));
        using var db = factory.CreateDbContext();
        Assert.Equal("1", db.Cells.Single(c => c.SheetName == "s").Input);
    }

    [Fact]
    public void A_failed_save_puts_the_old_inputs_back()
    {
        var store = new SheetStore(factory);
        Set(store, "s", ("A1", "1"), ("B1", "=A1*2"));
        using (var db = factory.CreateDbContext())
        {
            db.Database.ExecuteSqlRaw("DROP TABLE \"Cells\"");
        }

        Assert.ThrowsAny<DbException>(() => Set(store, "s", ("A1", "5")));
        var sheet = store.Find("s")!;
        Assert.Equal("1", sheet.GetInput(A("A1")));
        Assert.Equal("2", sheet.ValueAt(A("B1")).Display);
    }

    [Fact]
    public void Sheet_names_ignore_case_and_keep_their_first_spelling()
    {
        var store = new SheetStore(factory);
        Set(store, "Budget", ("A1", "1"));
        Set(store, "BUDGET", ("A2", "2"));
        var again = new SheetStore(factory);
        Assert.Equal(["Budget", SheetStore.SampleName], again.Names());
        Assert.Equal("2", again.Find("budget")!.GetInput(A("A2")));
    }

    [Fact]
    public void Reads_the_database_from_configuration()
    {
        var root = Path.GetTempPath();
        var file = new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, DatabaseConfig.DefaultSqlitePath) };
        Assert.Equal(file.ToString(), ConnectionString(root));
        Assert.Equal("Data Source=/data/sheets.db", ConnectionString(root, ("Database:SqlitePath", "/data/sheets.db")));
        Assert.Equal(
            "Host=db;Database=sheets",
            ConnectionString(root, ("Database:Provider", "PostgreSql"), ("ConnectionStrings:PostgreSql", "Host=db;Database=sheets")));
        Assert.Throws<InvalidOperationException>(() => ConnectionString(root, ("Database:Provider", "PostgreSql")));
        Assert.Throws<InvalidOperationException>(() => ConnectionString(root, ("Database:Provider", "Oracle")));
    }

    [Fact]
    public async Task A_restart_of_the_app_keeps_every_sheet_and_gives_the_same_values()
    {
        var path = ApiFactory.TempPath();
        try
        {
            string names, demo, other;
            await using (var app = ApiFactory.On(path))
            {
                using var client = app.CreateClient();
                (await client.PutAsJsonAsync("/api/sheets/demo/cells/B4", new CellEdit("2"), Token)).EnsureSuccessStatusCode();
                var batch = new CellBatch(new() { ["A1"] = "=B1", ["B1"] = "=A1", ["C1"] = "'007", ["D1"] = "=SUM(E1:E3)", ["E2"] = "5" });
                (await client.PostAsJsonAsync("/api/sheets/other/cells", batch, Token)).EnsureSuccessStatusCode();
                names = await client.GetStringAsync("/api/sheets", Token);
                demo = await client.GetStringAsync("/api/sheets/demo", Token);
                other = await client.GetStringAsync("/api/sheets/other", Token);
            }

            await using (var app = ApiFactory.On(path))
            {
                using var client = app.CreateClient();
                Assert.Equal("[\"demo\",\"other\"]", await client.GetStringAsync("/api/sheets", Token));
                Assert.Equal(names, await client.GetStringAsync("/api/sheets", Token));
                Assert.Equal(demo, await client.GetStringAsync("/api/sheets/demo", Token));
                Assert.Equal(other, await client.GetStringAsync("/api/sheets/other", Token));
                Assert.Contains("123.75", demo, StringComparison.Ordinal);
                Assert.Contains("#CYCLE!", other, StringComparison.Ordinal);
            }
        }
        finally
        {
            ApiFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task A_deleted_sheet_stays_deleted_after_a_restart()
    {
        var path = ApiFactory.TempPath();
        try
        {
            await using (var app = ApiFactory.On(path))
            {
                using var client = app.CreateClient();
                Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/sheets/demo", Token)).StatusCode);
            }

            await using (var app = ApiFactory.On(path))
            {
                using var client = app.CreateClient();
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/sheets/demo", Token)).StatusCode);
                Assert.Equal("[]", await client.GetStringAsync("/api/sheets", Token));
            }
        }
        finally
        {
            ApiFactory.DeleteDatabase(path);
        }
    }

    [Fact]
    public void PostgreSql_keeps_every_sheet_and_gives_the_same_values()
    {
        var given = Environment.GetEnvironmentVariable(PostgreSqlVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(given), $"{PostgreSqlVariable} is not set, so there is no PostgreSQL server to test.");
        var connectionString = new NpgsqlConnectionStringBuilder(given) { Database = $"cellcascade_test_{Guid.NewGuid():N}" }.ToString();
        var postgres = new Factory(new DbContextOptionsBuilder<SheetDbContext>().UseNpgsql(connectionString).Options);
        try
        {
            RoundTrip(postgres);
        }
        finally
        {
            using var db = postgres.CreateDbContext();
            db.Database.EnsureDeleted();
        }
    }

    /// <summary>Edits sheets in 1 store, then checks that a 2nd store on the same database has the same inputs and values.</summary>
    private static void RoundTrip(Factory database)
    {
        var first = new SheetStore(database);
        Set(first, SheetStore.SampleName, ("B4", "2"));
        Set(first, "prices", ("A1", "10"), ("A2", "=A1*1.5"), ("A3", "=A2&\" each\""), ("B1", "=B1+1"), ("C1", "'TRUE"), ("D1", "=1/0"));
        Set(first, "prices", ("A1", "20"), ("D1", ""));
        Set(first, "gone", ("A1", "1"));
        Assert.True(first.Remove("gone"));

        var second = new SheetStore(database);
        Assert.Equal(first.Names(), second.Names());
        Assert.Equal([SheetStore.SampleName, "prices"], second.Names());
        foreach (var name in second.Names())
        {
            Assert.Equal(Snapshot(first.Find(name)!), Snapshot(second.Find(name)!));
        }

        var prices = second.Find("prices")!;
        Assert.Equal("30 each", prices.ValueAt(A("A3")).Display);
        Assert.Equal("#CYCLE!", prices.ValueAt(A("B1")).Display);
        Assert.Equal("text", prices.ValueAt(A("C1")).TypeName);
        Assert.Equal("blank", prices.ValueAt(A("D1")).TypeName);
        Assert.Equal("123.75", second.Find(SheetStore.SampleName)!.ValueAt(A("D8")).Display);
    }

    private static List<(CellAddress Address, string Input, CellValue Value)> Snapshot(Sheet sheet) => [.. sheet.Cells()];

    private static void Set(SheetStore store, string name, params (string Ref, string Input)[] cells) =>
        store.Edit(name, [.. cells.Select(c => new KeyValuePair<CellAddress, string?>(A(c.Ref), c.Input))], (_, result) => result);

    private static CellAddress A(string text) => CellAddress.Parse(text);

    private static string? ConnectionString(string root, params (string Key, string? Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();
        var options = new DbContextOptionsBuilder<SheetDbContext>();
        DatabaseConfig.Configure(options, configuration, root);
        using var db = new SheetDbContext(options.Options);
        return db.Database.GetConnectionString();
    }

    private sealed class Factory(DbContextOptions<SheetDbContext> options) : IDbContextFactory<SheetDbContext>
    {
        public SheetDbContext CreateDbContext() => new(options);
    }
}
