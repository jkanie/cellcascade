namespace CellCascade.Tests;

public class FunctionTests
{
    private static readonly (string, string)[] Mixed = [("A1", "1"), ("A2", "x"), ("A3", "TRUE"), ("A4", "")];

    private static readonly (string, string)[] Table =
    [
        ("A1", "10"), ("B1", "ten"),
        ("A2", "20"), ("B2", "twenty"),
        ("A3", "30"), ("B3", "thirty"),
        ("A4", "40"), ("B4", "forty"),
        ("A5", "20"), ("B5", "twenty again"),
        ("D1", "apple"), ("E1", "1"),
        ("D2", "Pear"), ("E2", "2"),
    ];

    [Theory]
    [InlineData("=SUM(A1:A4)", "1")]
    [InlineData("=SUM(\"3\",TRUE,2)", "6")]
    [InlineData("=SUM(A2)", "0")]
    [InlineData("=COUNT(A1:A4)", "1")]
    [InlineData("=COUNT(1,\"2\",\"x\",TRUE)", "3")]
    [InlineData("=COUNTA(A1:A4)", "3")]
    [InlineData("=COUNTA(A1:A4,\"\")", "4")]
    [InlineData("=AND(A1:A4)", "TRUE")]
    [InlineData("=AND(A2)", "#VALUE!")]
    [InlineData("=CONCAT(A1:A4,\"!\")", "1xTRUE!")]
    public void Treat_ranges_and_typed_values_as_Excel_does(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, Mixed));
    }

    [Theory]
    [InlineData("=AVERAGE(A1:A3)", "3")]
    [InlineData("=AVERAGE(C1:C3)", "#DIV/0!")]
    [InlineData("=MIN(A1:A3)", "2")]
    [InlineData("=MAX(A1:A3)", "4")]
    [InlineData("=MIN(C1:C3)", "0")]
    [InlineData("=SUM(A1:B2)", "#DIV/0!")]
    [InlineData("=COUNT(A1:B2)", "2")]
    public void Aggregate_numbers(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A1", "2"), ("A2", "4"), ("B1", "=1/0")));
    }

    [Theory]
    [InlineData("=AND(TRUE,1)", "TRUE")]
    [InlineData("=AND(TRUE,0)", "FALSE")]
    [InlineData("=OR(FALSE,0)", "FALSE")]
    [InlineData("=OR(FALSE,\"TRUE\")", "TRUE")]
    [InlineData("=OR(\"maybe\")", "#VALUE!")]
    [InlineData("=NOT(0)", "TRUE")]
    [InlineData("=IF(1>2,\"yes\",\"no\")", "no")]
    [InlineData("=IF(FALSE,1)", "FALSE")]
    [InlineData("=IF(TRUE,1,1/0)", "1")]
    [InlineData("=IF(1/0,1,2)", "#DIV/0!")]
    [InlineData("=IFERROR(1/0,\"none\")", "none")]
    [InlineData("=IFERROR(5,1/0)", "5")]
    public void Logic(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    [Theory]
    [InlineData("=ABS(-3)", "3")]
    [InlineData("=ROUND(2.5,0)", "3")]
    [InlineData("=ROUND(-2.5,0)", "-3")]
    [InlineData("=ROUND(2.675,2)", "2.68")]
    [InlineData("=ROUND(1.005,2)", "1.01")]
    [InlineData("=ROUND(0.285,2)", "0.29")]
    [InlineData("=ROUND(1234.5,-2)", "1200")]
    [InlineData("=ROUND(1250,-2)", "1300")]
    [InlineData("=ROUND(1.23456,1.9)", "1.2")]
    [InlineData("=ROUND(\"x\",1)", "#VALUE!")]
    [InlineData("=ROUNDUP(3.2,0)", "4")]
    [InlineData("=ROUNDUP(76.9,0)", "77")]
    [InlineData("=ROUNDUP(3.14159,3)", "3.142")]
    [InlineData("=ROUNDUP(-3.14159,1)", "-3.2")]
    [InlineData("=ROUNDUP(31415.92654,-2)", "31500")]
    [InlineData("=ROUNDUP(0.1+0.2,1)", "0.3")]
    [InlineData("=ROUNDUP(2,0)", "2")]
    [InlineData("=ROUNDDOWN(3.2,0)", "3")]
    [InlineData("=ROUNDDOWN(76.9,0)", "76")]
    [InlineData("=ROUNDDOWN(3.14159,3)", "3.141")]
    [InlineData("=ROUNDDOWN(-3.14159,1)", "-3.1")]
    [InlineData("=ROUNDDOWN(31415.92654,-2)", "31400")]
    [InlineData("=ROUNDDOWN(\"x\",1)", "#VALUE!")]
    [InlineData("=INT(8.9)", "8")]
    [InlineData("=INT(-8.9)", "-9")]
    [InlineData("=INT(\"7.5\")", "7")]
    [InlineData("=MOD(3,2)", "1")]
    [InlineData("=MOD(-3,2)", "1")]
    [InlineData("=MOD(3,-2)", "-1")]
    [InlineData("=MOD(-3,-2)", "-1")]
    [InlineData("=MOD(7.5,2)", "1.5")]
    [InlineData("=MOD(5,0)", "#DIV/0!")]
    [InlineData("=MOD(1/0,2)", "#DIV/0!")]
    [InlineData("=LEFT(\"Sale Price\",4)", "Sale")]
    [InlineData("=LEFT(\"Sweden\")", "S")]
    [InlineData("=LEFT(\"abc\",10)", "abc")]
    [InlineData("=LEFT(\"abc\",0)", "")]
    [InlineData("=LEFT(\"abc\",2.9)", "ab")]
    [InlineData("=LEFT(\"abc\",-1)", "#VALUE!")]
    [InlineData("=LEFT(12345,2)", "12")]
    [InlineData("=RIGHT(\"Sale Price\",5)", "Price")]
    [InlineData("=RIGHT(\"Stock Number\")", "r")]
    [InlineData("=RIGHT(\"abc\",10)", "abc")]
    [InlineData("=RIGHT(\"abc\",-1)", "#VALUE!")]
    [InlineData("=MID(\"Fluid Flow\",1,5)", "Fluid")]
    [InlineData("=MID(\"Fluid Flow\",7,20)", "Flow")]
    [InlineData("=MID(\"Fluid Flow\",20,5)", "")]
    [InlineData("=MID(\"Fluid Flow\",0,5)", "#VALUE!")]
    [InlineData("=MID(\"Fluid Flow\",1,-1)", "#VALUE!")]
    [InlineData("=TRIM(\" First Quarter   Earnings \")", "First Quarter Earnings")]
    [InlineData("=TRIM(\" Sales report \")", "Sales report")]
    [InlineData("=TRIM(\"   \")", "")]
    [InlineData("=LEN(TRIM(\"\u00A0a\u00A0\"))", "3")]
    [InlineData("=LEFT(1/0)", "#DIV/0!")]
    [InlineData("=LEN(\"hello\")", "5")]
    [InlineData("=LEN(12.5)", "4")]
    [InlineData("=UPPER(\"abc\")", "ABC")]
    [InlineData("=LOWER(\"ABC\")", "abc")]
    public void Numbers_and_text(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    // The 1st 7 rows are Microsoft's examples for FIND and SUBSTITUTE.
    [Theory]
    [InlineData("=FIND(\"M\",\"Miriam McGovern\")", "1")]
    [InlineData("=FIND(\"m\",\"Miriam McGovern\")", "6")]
    [InlineData("=FIND(\"M\",\"Miriam McGovern\",3)", "8")]
    [InlineData("=MID(\"Ceramic Insulators #124-TD45-87\",1,FIND(\" #\",\"Ceramic Insulators #124-TD45-87\",1)-1)", "Ceramic Insulators")]
    [InlineData("=SUBSTITUTE(\"Sales Data\",\"Sales\",\"Cost\")", "Cost Data")]
    [InlineData("=SUBSTITUTE(\"Quarter 1, 2008\",\"1\",\"2\",1)", "Quarter 2, 2008")]
    [InlineData("=SUBSTITUTE(\"Quarter 1, 2011\",\"1\",\"2\",3)", "Quarter 1, 2012")]
    [InlineData("=FIND(\"\",\"abc\")", "1")]
    [InlineData("=FIND(\"\",\"abc\",3)", "3")]
    [InlineData("=FIND(\"x\",\"abc\")", "#VALUE!")]
    [InlineData("=FIND(\"a\",\"abc\",0)", "#VALUE!")]
    [InlineData("=FIND(\"a\",\"abc\",4)", "#VALUE!")]
    [InlineData("=FIND(\"a\",\"abca\",2)", "4")]
    [InlineData("=FIND(\"c\",\"abc\",2.9)", "3")]
    [InlineData("=FIND(\"?\",\"a?b*\")", "2")]
    [InlineData("=FIND(\"*\",\"a?b*\")", "4")]
    [InlineData("=FIND(1/0,\"abc\")", "#DIV/0!")]
    [InlineData("=FIND(5,2025)", "4")]
    [InlineData("=SUBSTITUTE(\"aaaa\",\"aa\",\"b\")", "bb")]
    [InlineData("=SUBSTITUTE(\"aaa\",\"aa\",\"b\",2)", "aaa")]
    [InlineData("=SUBSTITUTE(\"a-b-c\",\"-\",\"\",2)", "a-bc")]
    [InlineData("=SUBSTITUTE(\"a-b-c\",\"-\",\"+\",2.7)", "a-b+c")]
    [InlineData("=SUBSTITUTE(\"abc\",\"\",\"x\")", "abc")]
    [InlineData("=SUBSTITUTE(\"abc\",\"\",\"x\",1)", "abc")]
    [InlineData("=SUBSTITUTE(\"aBc\",\"b\",\"x\")", "aBc")]
    [InlineData("=SUBSTITUTE(\"abc\",\"b\",\"x\",0)", "#VALUE!")]
    [InlineData("=SUBSTITUTE(\"abc\",\"b\",\"x\",1/0)", "#DIV/0!")]
    [InlineData("=SUBSTITUTE(2008,\"0\",\"9\")", "2998")]
    public void Find_and_substitute(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    // The 1st 10 rows are Microsoft's examples for SEARCH and REPLACE, with their data cells written in.
    [Theory]
    [InlineData("=SEARCH(\"e\",\"Statements\",6)", "7")]
    [InlineData("=SEARCH(\"margin\",\"Profit Margin\")", "8")]
    [InlineData("=REPLACE(\"Profit Margin\",SEARCH(\"margin\",\"Profit Margin\"),6,\"Amount\")", "Profit Amount")]
    [InlineData("=MID(\"Profit Margin\",SEARCH(\" \",\"Profit Margin\")+1,4)", "Marg")]
    [InlineData("=SEARCH(\"Y\",\"AYF0093.YoungMensApparel\",8)", "9")]
    [InlineData("=REPLACE(\"abcdefghijk\",6,5,\"*\")", "abcde*k")]
    [InlineData("=REPLACE(\"2009\",3,2,\"10\")", "2010")]
    [InlineData("=REPLACE(\"123456\",1,3,\"@\")", "@456")]
    [InlineData("=SEARCH(\"\"\"\",\"The \"\"boss\"\" is here.\")", "5")]
    [InlineData("=MID(\"The \"\"boss\"\" is here.\",SEARCH(\"\"\"\",\"The \"\"boss\"\" is here.\")+1,SEARCH(\"\"\"\",\"The \"\"boss\"\" is here.\",SEARCH(\"\"\"\",\"The \"\"boss\"\" is here.\")+1)-SEARCH(\"\"\"\",\"The \"\"boss\"\" is here.\")-1)", "boss")]
    [InlineData("=REPLACE(2009,3,2,10)", "2010")]
    [InlineData("=SEARCH(\"b?d\",\"abd\")", "#VALUE!")]
    [InlineData("=SEARCH(\"b?d\",\"aBcD\")", "2")]
    [InlineData("=SEARCH(\"a*d\",\"xAbcD\")", "2")]
    [InlineData("=SEARCH(\"*\",\"abc\")", "1")]
    [InlineData("=SEARCH(\"~?\",\"why? not\")", "4")]
    [InlineData("=SEARCH(\"~*\",\"2*3\")", "2")]
    [InlineData("=SEARCH(\"?\",\"abc\",3)", "3")]
    [InlineData("=SEARCH(\".\",\"a.b\")", "2")]
    [InlineData("=SEARCH(\"x\",\"abc\")", "#VALUE!")]
    [InlineData("=SEARCH(\"a\",\"abc\",0)", "#VALUE!")]
    [InlineData("=SEARCH(\"a\",\"abc\",4)", "#VALUE!")]
    [InlineData("=SEARCH(\"\",\"abc\",2)", "2")]
    [InlineData("=REPLACE(\"abc\",4,1,\"d\")", "abcd")]
    [InlineData("=REPLACE(\"abc\",9,0,\"d\")", "abcd")]
    [InlineData("=REPLACE(\"abc\",2,9,\"X\")", "aX")]
    [InlineData("=REPLACE(\"abc\",2,0,\"X\")", "aXbc")]
    [InlineData("=REPLACE(\"abc\",0,1,\"X\")", "#VALUE!")]
    [InlineData("=REPLACE(\"abc\",1,-1,\"X\")", "#VALUE!")]
    [InlineData("=REPLACE(\"abc\",1.9,1.9,\"X\")", "Xbc")]
    [InlineData("=REPLACE(1/0,1,1,\"X\")", "#DIV/0!")]
    public void Search_and_replace(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    // The 1st 5 rows are Microsoft's examples for PROPER and REPT.
    [Theory]
    [InlineData("=PROPER(\"this is a TITLE\")", "This Is A Title")]
    [InlineData("=PROPER(\"2-way street\")", "2-Way Street")]
    [InlineData("=PROPER(\"76BudGet\")", "76Budget")]
    [InlineData("=REPT(\"*-\",3)", "*-*-*-")]
    [InlineData("=REPT(\"-\",10)", "----------")]
    [InlineData("=PROPER(\"o'neil mcDONALD\")", "O'Neil Mcdonald")]
    [InlineData("=PROPER(\"\u00e9cole \u00c9T\u00c9\")", "\u00c9cole \u00c9t\u00e9")]
    [InlineData("=PROPER(\"\")", "")]
    [InlineData("=PROPER(123)", "123")]
    [InlineData("=PROPER(1/0)", "#DIV/0!")]
    [InlineData("=REPT(\"ab\",0)", "")]
    [InlineData("=REPT(\"ab\",2.9)", "abab")]
    [InlineData("=REPT(\"ab\",-1)", "#VALUE!")]
    [InlineData("=REPT(\"\",100000)", "")]
    [InlineData("=LEN(REPT(\"a\",32767))", "32767")]
    [InlineData("=REPT(\"a\",32768)", "#VALUE!")]
    [InlineData("=REPT(\"ab\",16384)", "#VALUE!")]
    [InlineData("=REPT(1,3)", "111")]
    [InlineData("=REPT(\"a\",1/0)", "#DIV/0!")]
    public void Proper_and_rept(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula));
    }

    [Theory]
    [InlineData("=VLOOKUP(20,A1:B4,2,FALSE)", "twenty")]
    [InlineData("=VLOOKUP(25,A1:B4,2,FALSE)", "#N/A")]
    [InlineData("=VLOOKUP(20,A1:B5,2,FALSE)", "twenty")]
    [InlineData("=VLOOKUP(25,A1:B4,2)", "twenty")]
    [InlineData("=VLOOKUP(40,A1:B4,2)", "forty")]
    [InlineData("=VLOOKUP(99,A1:B4,2,TRUE)", "forty")]
    [InlineData("=VLOOKUP(5,A1:B4,2)", "#N/A")]
    [InlineData("=VLOOKUP(20,A1:B4,3,FALSE)", "#REF!")]
    [InlineData("=VLOOKUP(20,A1:B4,0,FALSE)", "#VALUE!")]
    [InlineData("=VLOOKUP(\"PEAR\",D1:E2,2,FALSE)", "2")]
    [InlineData("=VLOOKUP(\"20\",A1:B4,2,FALSE)", "#N/A")]
    [InlineData("=VLOOKUP(1/0,A1:B4,2)", "#DIV/0!")]
    public void Vlookup(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, Table));
    }

    [Fact]
    public void Function_names_ignore_case()
    {
        Assert.Equal("3", Eval.Display("=sum(1,2)"));
        Assert.Contains("VLOOKUP", Functions.Names);
    }
}
