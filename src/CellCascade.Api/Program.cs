using System.Text.RegularExpressions;
using CellCascade;
using CellCascade.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// This reads the settings when the app first needs the database, not on this line, such that settings that tests add count too.
builder.Services.AddDbContextFactory<SheetDbContext>((services, options) =>
    DatabaseConfig.Configure(options, services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>().ContentRootPath));
builder.Services.AddSingleton<SheetStore>();
var app = builder.Build();

// Opens the database and loads every sheet before the first request, such that a bad setting stops the start.
app.Services.GetRequiredService<SheetStore>();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/functions", () => Functions.Names);

api.MapGet("/sheets", (SheetStore store) => store.Names());

api.MapGet("/sheets/{name}", (string name, SheetStore store) =>
{
    var sheet = store.Find(name);
    if (sheet is null)
    {
        return Results.NotFound(new ErrorDto($"There is no sheet called '{name}'."));
    }

    lock (sheet)
    {
        return Results.Ok(new SheetDto(name, [.. sheet.Cells().Select(c => new CellDto(c.Address.ToString(), c.Input, ValueDto.From(c.Value)))]));
    }
});

api.MapGet("/sheets/{name}/cells/{cell}", (string name, string cell, SheetStore store) =>
{
    var sheet = store.Find(name);
    if (sheet is null)
    {
        return Results.NotFound(new ErrorDto($"There is no sheet called '{name}'."));
    }

    if (!CellAddress.TryParse(cell, out var address))
    {
        return Results.BadRequest(new ErrorDto($"'{cell}' is not a cell address such as A1."));
    }

    lock (sheet)
    {
        return Results.Ok(new CellDetailDto(
            address.ToString(),
            sheet.GetInput(address),
            ValueDto.From(sheet.ValueAt(address)),
            [.. sheet.PrecedentsOf(address).Select(a => a.ToString())],
            [.. sheet.DependentsOf(address).Select(a => a.ToString())]));
    }
});

api.MapPut("/sheets/{name}/cells/{cell}", (string name, string cell, CellEdit edit, SheetStore store) =>
{
    if (!CellAddress.TryParse(cell, out var address))
    {
        return Results.BadRequest(new ErrorDto($"'{cell}' is not a cell address such as A1."));
    }

    return Edit(name, store, [new KeyValuePair<CellAddress, string?>(address, edit.Input)]);
});

api.MapPost("/sheets/{name}/cells", (string name, CellBatch batch, SheetStore store) =>
{
    if (batch.Cells is null || batch.Cells.Count == 0)
    {
        return Results.BadRequest(new ErrorDto("The body needs \"cells\": an object of cell addresses and inputs."));
    }

    if (batch.Cells.Count > Limits.MaxBatch)
    {
        return Results.BadRequest(new ErrorDto($"1 request can set at most {Limits.MaxBatch:N0} cells."));
    }

    var edits = new List<KeyValuePair<CellAddress, string?>>();
    foreach (var (cell, input) in batch.Cells)
    {
        if (!CellAddress.TryParse(cell, out var address))
        {
            return Results.BadRequest(new ErrorDto($"'{cell}' is not a cell address such as A1."));
        }

        edits.Add(new KeyValuePair<CellAddress, string?>(address, input));
    }

    return Edit(name, store, edits);
});

api.MapDelete("/sheets/{name}", (string name, SheetStore store) =>
    store.Remove(name) ? Results.NoContent() : Results.NotFound(new ErrorDto($"There is no sheet called '{name}'.")));

app.Run();

static IResult Edit(string name, SheetStore store, List<KeyValuePair<CellAddress, string?>> edits)
{
    if (!Limits.SheetName().IsMatch(name))
    {
        return Results.BadRequest(new ErrorDto("A sheet name has 1 to 64 letters, digits, - or _."));
    }

    try
    {
        return store.Edit(name, edits, (sheet, result) => Results.Ok(new EditResultDto(
            [.. result.Changed.Select(a => new CellDto(a.ToString(), sheet.GetInput(a), ValueDto.From(sheet.ValueAt(a))))],
            [.. result.Recalculated.Select(a => a.ToString())],
            [.. result.InCycle.Select(a => a.ToString())])));
    }
    catch (FormulaException e)
    {
        return Results.BadRequest(new ErrorDto(e.Message));
    }
}

/// <summary>The entry point. It is public such that the tests can start the app in memory.</summary>
public partial class Program
{
}

internal static partial class Limits
{
    public const int MaxBatch = 10_000;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    public static partial Regex SheetName();
}
