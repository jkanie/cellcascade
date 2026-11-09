using System.Globalization;

namespace CellCascade.Tests;

/// <summary>
/// Applies random edits to random sheets and checks the engine against the slow ReferenceSheet after every edit:
/// every value, the exact set of cells re-calculated, their order, the cells in a cycle, the cells that changed.
/// </summary>
public class OracleTests
{
    private const int Columns = 5;
    private const int Rows = 5;

    [Fact]
    public void Incremental_recalculation_matches_a_full_recalculation()
    {
        var edits = 0;
        var cycles = 0;
        for (var seed = 1; seed <= 300; seed++)
        {
            var random = new Random(seed);
            var sheet = new Sheet();
            var reference = new ReferenceSheet();
            for (var step = 0; step < 40; step++)
            {
                var batch = new Dictionary<CellAddress, string?>();
                var size = step == 0 ? Columns * Rows : random.Next(1, 4);
                while (batch.Count < size)
                {
                    batch[RandomCell(random)] = RandomInput(random);
                }

                var before = AllCells().ToDictionary(a => a, sheet.ValueAt);
                var result = sheet.Set(batch.Select(pair => pair));
                foreach (var (address, text) in batch)
                {
                    reference.Set(address, text);
                }

                reference.Recompute();
                Check(sheet, reference, batch.Keys, before, result, $"seed {seed}, step {step}");
                edits++;
                cycles += result.InCycle.Count > 0 ? 1 : 0;
            }
        }

        // The generator must reach the cases that matter, or the test proves little.
        Assert.Equal(12_000, edits);
        Assert.True(cycles > 500, $"only {cycles} edits made a cycle");
    }

    [Fact]
    public void The_order_of_a_batch_does_not_matter()
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var random = new Random(seed);
            var batch = AllCells().Select(a => new KeyValuePair<CellAddress, string?>(a, RandomInput(random))).ToList();
            var first = new Sheet();
            first.Set(batch);
            var second = new Sheet();
            second.Set(batch.OrderBy(_ => random.Next()).ToList());
            foreach (var address in AllCells())
            {
                Assert.Equal(first.ValueAt(address), second.ValueAt(address));
            }
        }
    }

    private static void Check(
        Sheet sheet,
        ReferenceSheet reference,
        IEnumerable<CellAddress> edited,
        Dictionary<CellAddress, CellValue> before,
        RecalcResult result,
        string where)
    {
        foreach (var address in AllCells())
        {
            var expected = reference.ValueAt(address);
            var actual = sheet.ValueAt(address);
            Assert.True(expected == actual, $"{where}: {address} is {actual.Display}, expected {expected.Display}");
        }

        // The cells that the edit reaches: the edited cells plus every cell that reads them, transitively.
        var reached = new HashSet<CellAddress>(edited);
        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var address in reference.Inputs.Keys)
            {
                if (!reached.Contains(address) && reference.ReadsOf(address).Overlaps(reached))
                {
                    reached.Add(address);
                    grew = true;
                }
            }
        }

        var formulas = reached.Where(a => reference.Inputs.TryGetValue(a, out var input) && input.Formula is not null).ToHashSet();
        var expectedRecalculated = formulas.Where(a => !reference.InCycle(a)).ToHashSet();
        var expectedCycle = formulas.Where(reference.InCycle).Order().ToList();

        Assert.True(result.Recalculated.Count == result.Recalculated.Distinct().Count(), $"{where}: a cell was evaluated twice");
        Assert.True(expectedRecalculated.SetEquals(result.Recalculated), $"{where}: re-calculated {string.Join(",", result.Recalculated)}");
        Assert.Equal(expectedCycle, result.InCycle);

        var position = result.Recalculated.Select((a, i) => (a, i)).ToDictionary(p => p.a, p => p.i);
        foreach (var address in result.Recalculated)
        {
            foreach (var read in reference.ReadsOf(address))
            {
                if (position.TryGetValue(read, out var readAt))
                {
                    Assert.True(readAt < position[address], $"{where}: {address} ran before {read}, which it reads");
                }
            }
        }

        var expectedChanged = AllCells().Where(a => before[a] != reference.ValueAt(a)).Order().ToList();
        Assert.Equal(expectedChanged, result.Changed.Where(a => a.Column < Columns && a.Row < Rows).ToList());
    }

    private static IEnumerable<CellAddress> AllCells()
    {
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                yield return new CellAddress(column, row);
            }
        }
    }

    private static CellAddress RandomCell(Random random) => new(random.Next(Columns), random.Next(Rows));

    private static string RandomRange(Random random)
    {
        var a = RandomCell(random);
        var b = RandomCell(random);
        return $"{a}:{b}";
    }

    private static string RandomInput(Random random) => random.Next(20) switch
    {
        < 4 => string.Empty,
        < 7 => random.Next(-5, 21).ToString(CultureInfo.InvariantCulture),
        7 => (random.Next(-50, 51) / 4.0).ToString(CultureInfo.InvariantCulture),
        8 => random.Next(2) == 0 ? "x" : "'10",
        9 => random.Next(2) == 0 ? "TRUE" : "FALSE",
        _ => "=" + RandomFormula(random, 3),
    };

    private static string RandomFormula(Random random, int depth)
    {
        if (depth == 0 || random.Next(4) == 0)
        {
            return random.Next(3) == 0 ? random.Next(0, 10).ToString(CultureInfo.InvariantCulture) : RandomCell(random).ToString();
        }

        string Sub() => RandomFormula(random, depth - 1);
        return random.Next(14) switch
        {
            0 => $"{Sub()}+{Sub()}",
            1 => $"{Sub()}-{Sub()}",
            2 => $"{Sub()}*{Sub()}",
            3 => $"{Sub()}/{Sub()}",
            4 => $"({Sub()})>({Sub()})",
            5 => $"{Sub()}&{Sub()}",
            6 => $"SUM({RandomRange(random)})",
            7 => $"AVERAGE({RandomRange(random)})",
            8 => $"MAX({RandomRange(random)},{Sub()})",
            9 => $"IF({Sub()}>3,{Sub()},{Sub()})",
            10 => $"IFERROR({Sub()},-1)",
            11 => $"ROUND({Sub()}/3,1)",
            12 => $"VLOOKUP({Sub()},{RandomRange(random)},1,{(random.Next(2) == 0 ? "TRUE" : "FALSE")})",
            _ => $"COUNT({RandomRange(random)})+CONCAT({RandomRange(random)})",
        };
    }
}
