using BenchmarkDotNet.Attributes;

namespace CellCascade.Benchmarks;

/// <summary>
/// A chain of 100,000 formulas: loading it, a change at the top that re-calculates every formula, and a change
/// at the bottom that re-calculates 1. Each change sets a new number, so every run of it does the same work.
/// </summary>
[MemoryDiagnoser]
public class ChainBenchmarks
{
    public const int Length = 100_000;

    private static readonly CellAddress Top = CellAddress.Parse("A1");
    private static readonly CellAddress NextToLast = CellAddress.Parse("A99999");

    private KeyValuePair<CellAddress, string?>[] chain = [];
    private Sheet sheet = new();
    private int next;

    [GlobalSetup]
    public void Setup()
    {
        chain = Sheets.Chain(Length);
        sheet = new Sheet();
        sheet.Set(chain);
        // Each benchmark must do the work its description names.
        Sheets.Expect(ChangeTop(), Length - 1, "a change to A1");
        Sheets.Expect(ChangeBottom(), 1, "a change to A99999");
    }

    [Benchmark(Description = "Load a chain of 100,000 formulas")]
    public Sheet Load()
    {
        var fresh = new Sheet();
        fresh.Set(chain);
        return fresh;
    }

    [Benchmark(Description = "Change A1 (re-calculates 99,999 formulas)")]
    public RecalcResult ChangeTop() => sheet.Set(Top, (++next).ToString(System.Globalization.CultureInfo.InvariantCulture));

    [Benchmark(Description = "Change A99999 (re-calculates 1 formula)")]
    public RecalcResult ChangeBottom() =>
        sheet.Set(NextToLast, (++next).ToString(System.Globalization.CultureInfo.InvariantCulture));
}
