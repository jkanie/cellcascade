namespace CellCascade.Tests;

public class AddressTests
{
    [Theory]
    [InlineData("A1", 0, 0)]
    [InlineData("b7", 1, 6)]
    [InlineData("$C$3", 2, 2)]
    [InlineData("Z10", 25, 9)]
    [InlineData("AA1", 26, 0)]
    [InlineData("XFD1048576", 16_383, 1_048_575)]
    public void Parses_addresses(string text, int column, int row)
    {
        Assert.Equal(new CellAddress(column, row), CellAddress.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1")]
    [InlineData("A0")]
    [InlineData("A01")]
    [InlineData("XFE1")]
    [InlineData("A1048577")]
    [InlineData("A1B")]
    [InlineData("AAAA1")]
    public void Refuses_what_is_not_an_address(string text)
    {
        Assert.False(CellAddress.TryParse(text, out _));
    }

    [Fact]
    public void Prints_every_column_name_back_to_the_same_address()
    {
        for (var column = 0; column < CellAddress.MaxColumns; column++)
        {
            var address = new CellAddress(column, 41);
            Assert.Equal(address, CellAddress.Parse(address.ToString()));
        }

        Assert.Equal("XFD", CellAddress.ColumnName(16_383));
        Assert.Equal("AZ", CellAddress.ColumnName(51));
    }

    [Fact]
    public void Orders_by_row_then_column()
    {
        Assert.True(CellAddress.Parse("B1") < CellAddress.Parse("A2"));
        Assert.True(CellAddress.Parse("A2") > CellAddress.Parse("Z1"));
    }
}
