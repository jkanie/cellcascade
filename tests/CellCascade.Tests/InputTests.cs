namespace CellCascade.Tests;

public class InputTests
{
    [Theory]
    [InlineData("12", "number", "12")]
    [InlineData(" 12 ", "number", "12")]
    [InlineData("1e3", "number", "1000")]
    [InlineData("-0.5", "number", "-0.5")]
    [InlineData("50%", "number", "0.5")]
    [InlineData("true", "bool", "TRUE")]
    [InlineData("'TRUE", "text", "TRUE")]
    [InlineData("'007", "text", "007")]
    [InlineData("abc", "text", "abc")]
    [InlineData("%", "text", "%")]
    [InlineData("", "blank", "")]
    public void Reads_typed_input(string input, string type, string display)
    {
        var value = CellInput.Parse(input).Literal;
        Assert.Equal(type, value.TypeName);
        Assert.Equal(display, value.Display);
    }

    [Fact]
    public void Refuses_input_over_8192_characters()
    {
        Assert.NotNull(CellInput.Parse(new string('x', 8_192)));
        Assert.Throws<FormulaException>(() => CellInput.Parse(new string('x', 8_193)));
    }
}
