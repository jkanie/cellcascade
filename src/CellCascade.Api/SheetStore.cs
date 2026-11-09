using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace CellCascade.Api;

/// <summary>
/// Keeps every sheet in memory, 1 lock per sheet, and saves each sheet's inputs to a database.
/// When the store opens, it creates the database if there is none, then loads every sheet from its inputs,
/// which re-calculates every formula. The values are never saved. A new database starts with a sample sheet called "demo".
/// </summary>
public sealed class SheetStore
{
    public const string SampleName = "demo";

    private readonly IDbContextFactory<SheetDbContext> database;
    private readonly ConcurrentDictionary<string, Entry> sheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock gate = new();

    public SheetStore(IDbContextFactory<SheetDbContext> database)
    {
        ArgumentNullException.ThrowIfNull(database);
        this.database = database;
        using var db = database.CreateDbContext();
        if (db.Database.EnsureCreated())
        {
            db.Sheets.Add(new SheetRow { Name = SampleName });
            db.Cells.AddRange(Sample.Select(pair => new CellRow { SheetName = SampleName, Ref = pair.Key, Input = pair.Value }));
            db.SaveChanges();
        }

        var inputs = db.Cells.AsNoTracking().ToLookup(c => c.SheetName, StringComparer.Ordinal);
        foreach (var name in db.Sheets.AsNoTracking().Select(s => s.Name).ToList())
        {
            sheets[name] = new Entry(name, Load(name, inputs[name]));
        }
    }

    /// <summary>A small made-up budget: 3 lines, a discount, a total, a check against the budget.</summary>
    public static IReadOnlyDictionary<string, string> Sample { get; } = new Dictionary<string, string>
    {
        ["A1"] = "Item",
        ["B1"] = "Qty",
        ["C1"] = "Price",
        ["D1"] = "Total",
        ["A2"] = "Tea",
        ["B2"] = "3",
        ["C2"] = "12.5",
        ["D2"] = "=B2*C2",
        ["A3"] = "Teapot",
        ["B3"] = "1",
        ["C3"] = "64",
        ["D3"] = "=B3*C3",
        ["A4"] = "Mugs",
        ["B4"] = "4",
        ["C4"] = "18",
        ["D4"] = "=B4*C4",
        ["A6"] = "Subtotal",
        ["D6"] = "=SUM(D2:D4)",
        ["A7"] = "Discount",
        ["C7"] = "10%",
        ["D7"] = "=-ROUND(D6*C7,2)",
        ["A8"] = "Total",
        ["D8"] = "=D6+D7",
        ["A9"] = "Budget",
        ["D9"] = "150",
        ["A10"] = "Status",
        ["D10"] = "=IF(D8>D9,\"over by \"&ROUND(D8-D9,2),\"within budget\")",
    };

    public IReadOnlyList<string> Names() => [.. sheets.Keys.Order(StringComparer.OrdinalIgnoreCase)];

    public Sheet? Find(string name) => sheets.TryGetValue(name, out var entry) ? entry.Sheet : null;

    /// <summary>
    /// Sets cells on a sheet, creating the sheet when needed, and saves the new inputs. The answer is built while the
    /// sheet is still locked. A formula that cannot be read throws FormulaException and changes nothing. When the save
    /// fails, the sheet gets its old inputs back and the exception goes to the caller.
    /// </summary>
    public T Edit<T>(string name, IReadOnlyCollection<KeyValuePair<CellAddress, string?>> edits, Func<Sheet, RecalcResult, T> answer)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(answer);
        while (true)
        {
            var entry = GetOrAdd(name);
            lock (entry.Sheet)
            {
                // Remove ran between GetOrAdd and the lock, so this edit goes to a new sheet of the same name.
                if (entry.Removed)
                {
                    continue;
                }

                var before = edits.Select(e => e.Key).Distinct()
                    .Select(a => new KeyValuePair<CellAddress, string?>(a, entry.Sheet.GetInput(a))).ToList();
                var result = entry.Sheet.Set(edits);
                try
                {
                    Save(entry.Name, entry.Sheet, before.Select(pair => pair.Key));
                }
                catch
                {
                    entry.Sheet.Set(before);
                    throw;
                }

                return answer(entry.Sheet, result);
            }
        }
    }

    public bool Remove(string name)
    {
        lock (gate)
        {
            if (!sheets.TryGetValue(name, out var entry))
            {
                return false;
            }

            lock (entry.Sheet)
            {
                using var db = database.CreateDbContext();
                using var transaction = db.Database.BeginTransaction();
                db.Cells.Where(c => c.SheetName == entry.Name).ExecuteDelete();
                db.Sheets.Where(s => s.Name == entry.Name).ExecuteDelete();
                transaction.Commit();
                entry.Removed = true;
                sheets.TryRemove(name, out _);
                return true;
            }
        }
    }

    private static Sheet Load(string name, IEnumerable<CellRow> cells)
    {
        var sheet = new Sheet();
        var edits = new List<KeyValuePair<CellAddress, string?>>();
        foreach (var cell in cells)
        {
            if (!CellAddress.TryParse(cell.Ref, out var address))
            {
                throw new InvalidOperationException($"Sheet '{name}' in the database has a cell called '{cell.Ref}', which is not a cell address.");
            }

            edits.Add(new KeyValuePair<CellAddress, string?>(address, cell.Input));
        }

        try
        {
            sheet.Set(edits);
        }
        catch (FormulaException e)
        {
            throw new InvalidOperationException($"Sheet '{name}' in the database has an input that cannot be read. {e.Message}", e);
        }

        return sheet;
    }

    private Entry GetOrAdd(string name)
    {
        if (sheets.TryGetValue(name, out var found))
        {
            return found;
        }

        lock (gate)
        {
            if (sheets.TryGetValue(name, out found))
            {
                return found;
            }

            using var db = database.CreateDbContext();
            db.Sheets.Add(new SheetRow { Name = name });
            db.SaveChanges();
            var entry = new Entry(name, new Sheet());
            sheets[name] = entry;
            return entry;
        }
    }

    /// <summary>Writes the current input of each cell: a new row, a changed row, or no row for a blank cell.</summary>
    private void Save(string name, Sheet sheet, IEnumerable<CellAddress> addresses)
    {
        var inputs = addresses.ToDictionary(a => a.ToString(), sheet.GetInput, StringComparer.Ordinal);
        var refs = inputs.Keys.ToList();
        using var db = database.CreateDbContext();
        var rows = db.Cells.Where(c => c.SheetName == name && refs.Contains(c.Ref)).ToDictionary(c => c.Ref, StringComparer.Ordinal);
        foreach (var (cell, input) in inputs)
        {
            if (rows.TryGetValue(cell, out var row))
            {
                if (input.Length == 0)
                {
                    db.Cells.Remove(row);
                }
                else
                {
                    row.Input = input;
                }
            }
            else if (input.Length > 0)
            {
                db.Cells.Add(new CellRow { SheetName = name, Ref = cell, Input = input });
            }
        }

        db.SaveChanges();
    }

    private sealed class Entry(string name, Sheet sheet)
    {
        /// <summary>The spelling of the name in the database.</summary>
        public string Name { get; } = name;

        public Sheet Sheet { get; } = sheet;

        /// <summary>Set under the sheet's lock when the sheet is removed.</summary>
        public bool Removed { get; set; }
    }
}
