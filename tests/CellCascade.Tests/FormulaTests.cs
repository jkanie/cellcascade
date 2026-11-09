namespace CellCascade.Tests;

public class FormulaTests
{
    [Theory]
    [InlineData("=1+2*3", "7")]
    [InlineData("=(1+2)*3", "9")]
    [InlineData("=-2^2", "4")]
    [InlineData("=2^3^2", "64")]
    [InlineData("=2^-1", "0.5")]
    [InlineData("=50%", "0.5")]
    [InlineData("=-50%*2", "-1")]
    [InlineData("=10-2-3", "5")]
    [InlineData("=12/3/2", "2")]
    [InlineData("=\"a\"&1+1", "a2")]
    [InlineData("=1+1=2", "TRUE")]
    [InlineData("=1e3", "1000")]
    [InlineData("=.5", "0.5")]
    [InlineData("=1/3", "0.333333333333333")]
    [InlineData("=10^20", "1E+20")]
    [InlineData("=\"say \"\"hi\"\"\"", "say \"hi\"")]
    [InlineData("= 1 + 2 ", "3")]
    public void Follows_Excel_operator_order(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    [Theory]
    [InlineData("=\"A\"=\"a\"", "TRUE")]
    [InlineData("=1<\"a\"", "TRUE")]
    [InlineData("=\"z\"<TRUE", "TRUE")]
    [InlineData("=FALSE<TRUE", "TRUE")]
    [InlineData("=2>=2", "TRUE")]
    [InlineData("=\"b\"<>\"B\"", "FALSE")]
    [InlineData("=A1=0", "TRUE")]
    [InlineData("=A1=\"\"", "TRUE")]
    public void Compares_across_types_as_Excel_does(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    [Theory]
    [InlineData("=1/0", "#DIV/0!")]
    [InlineData("=0^0", "#NUM!")]
    [InlineData("=0^-1", "#DIV/0!")]
    [InlineData("=(-8)^(1/3)", "#NUM!")]
    [InlineData("=10^400", "#NUM!")]
    [InlineData("=\"x\"+1", "#VALUE!")]
    [InlineData("=A1:A2", "#VALUE!")]
    [InlineData("=FOO(1)", "#NAME?")]
    [InlineData("=(1/0)+(\"x\"+1)", "#DIV/0!")]
    [InlineData("=(\"x\"+1)+(1/0)", "#VALUE!")]
    public void Gives_error_values(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    [Theory]
    [InlineData("=\"3\"+1", "4")]
    [InlineData("=TRUE+1", "2")]
    [InlineData("=A1", "0")]
    [InlineData("=A1&\"x\"", "x")]
    [InlineData("=1&TRUE", "1TRUE")]
    public void Turns_values_into_the_type_an_operator_needs(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    [Fact]
    public void Reads_absolute_references_like_plain_ones()
    {
        Assert.Equal("6", Eval.Display("=$A$1+A$1+$A1", ("A1", "2")));
    }

    [Theory]
    [InlineData("=1+")]
    [InlineData("=(1")]
    [InlineData("=SUM(")]
    [InlineData("=\"abc")]
    [InlineData("=NOT(1,2)")]
    [InlineData("=IF(1)")]
    [InlineData("=VLOOKUP(1,A1:B2)")]
    [InlineData("=A0")]
    [InlineData("=1 2")]
    [InlineData("=FOO")]
    [InlineData("=A1:")]
    [InlineData("=A1:A200000")]
    [InlineData("=1#")]
    [InlineData("=$A")]
    [InlineData("=")]
    [InlineData("=   ")]
    public void Refuses_formulas_that_cannot_be_read(string formula)
    {
        var sheet = new Sheet();
        var error = Assert.Throws<FormulaException>(() => sheet.Set(CellAddress.Parse("B2"), formula));
        Assert.StartsWith("B2: ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Takes_a_range_of_100000_cells_but_not_more()
    {
        Assert.Equal("0", Eval.Display("=SUM(A1:A100000)"));
        Assert.Throws<FormulaException>(() => Eval.Display("=SUM(A1:A100001)"));
    }

    [Fact]
    public void Names_the_position_of_a_fault()
    {
        var error = Assert.Throws<FormulaException>(() => Parser.Parse("1+*2"));
        Assert.Contains("position 3", error.Message, StringComparison.Ordinal);
    }
}
