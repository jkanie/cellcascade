using System.Collections.Concurrent;

namespace CellCascade.Api;

/// <summary>
/// Keeps the sheets in memory, 1 lock per sheet. The store starts with a sample sheet called "demo".
/// Saving sheets to a database is item 1 in ROADMAP.md.
/// </summary>
public sealed class SheetStore
{
    public const string SampleName = "demo";

    private readonly ConcurrentDictionary<string, Sheet> sheets = new(StringComparer.OrdinalIgnoreCase);

    public SheetStore()
    {
        var sample = GetOrAdd(SampleName);
        sample.Set(Sample.Select(pair => new KeyValuePair<CellAddress, string?>(CellAddress.Parse(pair.Key), pair.Value)));
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

    public Sheet? Find(string name) => sheets.TryGetValue(name, out var sheet) ? sheet : null;

    public Sheet GetOrAdd(string name) => sheets.GetOrAdd(name, _ => new Sheet());

    public bool Remove(string name) => sheets.TryRemove(name, out _);
}
