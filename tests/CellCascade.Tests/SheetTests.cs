namespace CellCascade.Tests;

public class SheetTests
{
    private static CellAddress A(string text) => CellAddress.Parse(text);

    private static Sheet With(params (string Ref, string Input)[] cells)
    {
        var sheet = new Sheet();
        sheet.Set(cells.Select(c => new KeyValuePair<CellAddress, string?>(A(c.Ref), c.Input)));
        return sheet;
    }

    private static string[] Refs(IEnumerable<CellAddress> addresses) => [.. addresses.Select(a => a.ToString())];

    [Fact]
    public void Recalculates_a_chain_in_order()
    {
        var sheet = With(("A1", "1"), ("A2", "=A1+1"), ("A3", "=A2+1"));
        var result = sheet.Set(A("A1"), "10");
        Assert.Equal(["A2", "A3"], Refs(result.Recalculated));
        Assert.Equal(["A1", "A2", "A3"], Refs(result.Changed));
        Assert.Equal("12", sheet.ValueAt(A("A3")).Display);
    }

    [Fact]
    public void Leaves_cells_that_the_change_does_not_reach()
    {
        var sheet = With(("A1", "1"), ("B1", "=A1*2"), ("C1", "=5"), ("D1", "=C1+1"));
        var before = sheet.Evaluations;
        var result = sheet.Set(A("A1"), "2");
        Assert.Equal(["B1"], Refs(result.Recalculated));
        Assert.Equal(1, sheet.Evaluations - before);
    }

    [Fact]
    public void Evaluates_a_diamond_once_and_after_both_sides()
    {
        var sheet = With(("A1", "1"), ("B1", "=A1+1"), ("C1", "=A1*2"), ("D1", "=B1+C1"));
        var result = sheet.Set(A("A1"), "5");
        var order = Refs(result.Recalculated).ToList();
        Assert.Equal(3, order.Count);
        Assert.Equal("D1", order[^1]);
        Assert.Equal("16", sheet.ValueAt(A("D1")).Display);
    }

    [Fact]
    public void A_range_depends_on_its_blank_cells_too()
    {
        var sheet = With(("A1", "=SUM(B1:B3)"));
        var result = sheet.Set(A("B2"), "5");
        Assert.Equal(["A1"], Refs(result.Recalculated));
        Assert.Equal("5", sheet.ValueAt(A("A1")).Display);
    }

    [Fact]
    public void Marks_a_cycle_and_the_cells_that_read_it()
    {
        var sheet = With(("C1", "=A1+1"), ("D1", "=IFERROR(A1,0)"));
        var result = sheet.Set([new(A("A1"), "=B1"), new(A("B1"), "=A1")]);
        Assert.Equal(["A1", "B1"], Refs(result.InCycle));
        Assert.Equal("#CYCLE!", sheet.ValueAt(A("A1")).Display);
        Assert.Equal("#CYCLE!", sheet.ValueAt(A("B1")).Display);
        Assert.Equal("#CYCLE!", sheet.ValueAt(A("C1")).Display);
        Assert.Equal("0", sheet.ValueAt(A("D1")).Display);

        var broken = sheet.Set(A("B1"), "3");
        Assert.Empty(broken.InCycle);
        Assert.Equal("3", sheet.ValueAt(A("A1")).Display);
        Assert.Equal("4", sheet.ValueAt(A("C1")).Display);
        Assert.Equal("3", sheet.ValueAt(A("D1")).Display);
    }

    [Fact]
    public void A_cell_that_reads_itself_is_a_cycle()
    {
        var sheet = new Sheet();
        var result = sheet.Set(A("A1"), "=A1+1");
        Assert.Equal(["A1"], Refs(result.InCycle));
        Assert.Equal("#CYCLE!", sheet.ValueAt(A("A1")).Display);
    }

    [Fact]
    public void Clearing_a_cell_makes_it_blank_for_its_readers()
    {
        var sheet = With(("A1", "1"), ("B1", "=A1"));
        sheet.Set(A("A1"), "");
        Assert.Equal("blank", sheet.ValueAt(A("A1")).TypeName);
        Assert.Equal("0", sheet.ValueAt(A("B1")).Display);
        Assert.Empty(sheet.PrecedentsOf(A("A1")));
        Assert.Equal(["B1"], Refs(sheet.DependentsOf(A("A1"))));
    }

    [Fact]
    public void A_bad_formula_in_a_batch_changes_nothing()
    {
        var sheet = With(("A1", "1"));
        Assert.Throws<FormulaException>(() => sheet.Set([new(A("A1"), "2"), new(A("B1"), "=1+")]));
        Assert.Equal("1", sheet.ValueAt(A("A1")).Display);
        Assert.Equal("blank", sheet.ValueAt(A("B1")).TypeName);
    }

    [Fact]
    public void Changed_lists_only_cells_whose_value_changed()
    {
        var sheet = With(("A1", "1"), ("B1", "=A1>0"));
        var result = sheet.Set(A("A1"), "2");
        Assert.Equal(["B1"], Refs(result.Recalculated));
        Assert.Equal(["A1"], Refs(result.Changed));
    }

    [Fact]
    public void A_new_formula_drops_its_old_references()
    {
        var sheet = With(("A1", "1"), ("B1", "=A1"));
        sheet.Set(A("B1"), "=7");
        Assert.Empty(sheet.DependentsOf(A("A1")));
        Assert.Empty(sheet.Set(A("A1"), "2").Recalculated);
    }

    [Fact]
    public void Lists_precedents_and_dependents()
    {
        var sheet = With(("A1", "1"), ("B1", "=SUM(A1:A2)+C1"), ("D1", "=B1"));
        Assert.Equal(["A1", "C1", "A2"], Refs(sheet.PrecedentsOf(A("B1"))));
        Assert.Equal(["D1"], Refs(sheet.DependentsOf(A("B1"))));
    }

    [Fact]
    public void Handles_a_chain_of_100000_cells_without_running_out_of_stack()
    {
        var sheet = new Sheet();
        var edits = new List<KeyValuePair<CellAddress, string?>> { new(A("A1"), "1") };
        for (var row = 2; row <= 100_000; row++)
        {
            edits.Add(new(new CellAddress(0, row - 1), $"=A{row - 1}+1"));
        }

        sheet.Set(edits);
        Assert.Equal("100000", sheet.ValueAt(A("A100000")).Display);

        var result = sheet.Set(A("A1"), "5");
        Assert.Equal(99_999, result.Recalculated.Count);
        Assert.Equal("100004", sheet.ValueAt(A("A100000")).Display);
    }

    [Fact]
    public void A_cycle_of_100000_cells_is_found_without_running_out_of_stack()
    {
        var sheet = new Sheet();
        var edits = new List<KeyValuePair<CellAddress, string?>> { new(A("A1"), "=A100000+1") };
        for (var row = 2; row <= 100_000; row++)
        {
            edits.Add(new(new CellAddress(0, row - 1), $"=A{row - 1}+1"));
        }

        var result = sheet.Set(edits);
        Assert.Equal(100_000, result.InCycle.Count);
    }
}
