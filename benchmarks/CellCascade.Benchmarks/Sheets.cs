namespace CellCascade.Benchmarks;

/// <summary>The sheets the benchmarks measure, built the same way each time.</summary>
public static class Sheets
{
    /// <summary>A1 holds 1, and each cell below adds 1 to the cell above it: A2 is =A1+1, and so on.</summary>
    public static KeyValuePair<CellAddress, string?>[] Chain(int length)
    {
        var edits = new KeyValuePair<CellAddress, string?>[length];
        edits[0] = new(CellAddress.Parse("A1"), "1");
        for (var row = 2; row <= length; row++)
        {
            edits[row - 1] = new(CellAddress.Parse($"A{row}"), $"=A{row - 1}+1");
        }

        return edits;
    }

    /// <summary>
    /// Column A holds the numbers 1 to <paramref name="values"/>. Each of the cells B1 to B<paramref name="sums"/>
    /// sums the whole of column A, so a change in column A re-calculates every one of them.
    /// </summary>
    public static KeyValuePair<CellAddress, string?>[] WideSums(int values, int sums)
    {
        var edits = new List<KeyValuePair<CellAddress, string?>>(values + sums);
        for (var row = 1; row <= values; row++)
        {
            edits.Add(new(CellAddress.Parse($"A{row}"), row.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        for (var row = 1; row <= sums; row++)
        {
            edits.Add(new(CellAddress.Parse($"B{row}"), $"=SUM(A1:A{values})"));
        }

        return [.. edits];
    }

    /// <summary>Stops the run when a change re-calculated another number of formulas than the benchmark claims.</summary>
    public static void Expect(RecalcResult result, int recalculated, string what)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Recalculated.Count != recalculated)
        {
            throw new InvalidOperationException($"{what} re-calculated {result.Recalculated.Count} formulas, not {recalculated}.");
        }
    }
}
