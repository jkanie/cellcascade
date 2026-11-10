using BenchmarkDotNet.Attributes;

namespace CellCascade.Benchmarks;

/// <summary>
/// 100 formulas that each read the same 10,000 cells through =SUM(A1:A10000). A change to 1 of those cells
/// re-calculates all 100 sums, so the run time shows the cost of reading a wide range.
/// </summary>
[MemoryDiagnoser]
public class SumBenchmarks
{
    public const int Values = 10_000;
    public const int Sums = 100;

    private static readonly CellAddress Middle = CellAddress.Parse("A5000");

    private Sheet sheet = new();
    private int next;

    [GlobalSetup]
    public void Setup()
    {
        sheet = new Sheet();
        sheet.Set(Sheets.WideSums(Values, Sums));
        Sheets.Expect(ChangeUnderSums(), Sums, "a change to A5000");
    }

    [Benchmark(Description = "Change A5000 under 100 sums of 10,000 cells")]
    public RecalcResult ChangeUnderSums() =>
        sheet.Set(Middle, (++next).ToString(System.Globalization.CultureInfo.InvariantCulture));
}
