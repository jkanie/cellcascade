using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace CellCascade.Tests;

/// <summary>
/// Starts the app in memory on a SQLite file of its own in the temp folder. Made with On(path), it uses that file and
/// leaves it in place, such that a 2nd factory on the same path acts as a re-start of the app.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string path;
    private readonly bool ownsFile;

    public ApiFactory()
        : this(TempPath(), ownsFile: true)
    {
    }

    private ApiFactory(string path, bool ownsFile)
    {
        this.path = path;
        this.ownsFile = ownsFile;
    }

    /// <summary>A factory on a SQLite file that the caller owns.</summary>
    public static ApiFactory On(string path) => new(path, ownsFile: false);

    public static string TempPath() => Path.Combine(Path.GetTempPath(), $"cellcascade-test-{Guid.NewGuid():N}.db");

    /// <summary>Deletes a SQLite file and the 2 files that write-ahead logging keeps beside it.</summary>
    public static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { path, path + "-wal", path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Another test still has the file open. It is a temp file, so it can stay.
            }
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (ownsFile)
        {
            DeleteDatabase(path);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:SqlitePath", path);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && ownsFile)
        {
            DeleteDatabase(path);
        }
    }
}
