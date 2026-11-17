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

    // The 1st 3 rows are Microsoft's examples for PRODUCT, with 5, 15, 30 in A2:A4, and the next 3 are its examples
    // for SIGN.
    [Theory]
    [InlineData("=PRODUCT(A2:A4)", "2250")]
    [InlineData("=PRODUCT(A2:A4, 2)", "4500")]
    [InlineData("=A2*A3*A4", "2250")]
    [InlineData("=SIGN(10)", "1")]
    [InlineData("=SIGN(4-4)", "0")]
    [InlineData("=SIGN(-0.00001)", "-1")]
    [InlineData("=PRODUCT(A1:A6)", "2250")]
    [InlineData("=PRODUCT(B1:B3)", "0")]
    [InlineData("=PRODUCT(\"3\",TRUE,2)", "6")]
    [InlineData("=PRODUCT(A2,0)", "0")]
    [InlineData("=PRODUCT(A2:A4,C1)", "#DIV/0!")]
    [InlineData("=PRODUCT(\"x\")", "#VALUE!")]
    [InlineData("=SIGN(A2)", "1")]
    [InlineData("=SIGN(\"-2\")", "-1")]
    [InlineData("=SIGN(A5)", "#VALUE!")]
    [InlineData("=SIGN(C1)", "#DIV/0!")]
    public void Product_and_sign(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A1", "Data"), ("A2", "5"), ("A3", "15"), ("A4", "30"), ("A5", "x"), ("A6", "TRUE"), ("C1", "=1/0")));
    }

    // The 1st 3 rows are Microsoft's examples for SQRT, with -16 in A2, and the next 3 are its examples for POWER
    // (its results 2401077.222 and 5.656854249 are shown to 3 and 9 decimals, so the test rounds the same way).
    [Theory]
    [InlineData("=SQRT(16)", "4")]
    [InlineData("=SQRT(A2)", "#NUM!")]
    [InlineData("=SQRT(ABS(A2))", "4")]
    [InlineData("=POWER(5,2)", "25")]
    [InlineData("=ROUND(POWER(98.6,3.2),3)", "2401077.222")]
    [InlineData("=ROUND(POWER(4,5/4),9)", "5.656854249")]
    [InlineData("=POWER(A2,0.5)", "#NUM!")]
    [InlineData("=POWER(0,0)", "#NUM!")]
    [InlineData("=POWER(0,-1)", "#DIV/0!")]
    [InlineData("=POWER(2,10)=2^10", "TRUE")]
    [InlineData("=POWER(\"2\",\"3\")", "8")]
    [InlineData("=SQRT(0)", "0")]
    [InlineData("=SQRT(A3)", "#VALUE!")]
    [InlineData("=POWER(C1,2)", "#DIV/0!")]
    public void Sqrt_and_power(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A2", "-16"), ("A3", "x"), ("C1", "=1/0")));
    }

    // The 1st 3 rows are Microsoft's examples for LN and the next 2 its examples for EXP. Microsoft shows the results
    // rounded (LN(2.7182818) as 1, EXP(1) as 2.71828183), so the test rounds to the same decimals.
    [Theory]
    [InlineData("=ROUND(LN(86),7)", "4.4543473")]
    [InlineData("=ROUND(LN(2.7182818),7)", "1")]
    [InlineData("=LN(EXP(3))", "3")]
    [InlineData("=ROUND(EXP(1),8)", "2.71828183")]
    [InlineData("=ROUND(EXP(2),7)", "7.3890561")]
    [InlineData("=EXP(0)", "1")]
    [InlineData("=LN(1)", "0")]
    [InlineData("=LN(0)", "#NUM!")]
    [InlineData("=LN(A2)", "#NUM!")]
    [InlineData("=EXP(1000)", "#NUM!")]
    [InlineData("=LN(A3)", "#VALUE!")]
    [InlineData("=EXP(C1)", "#DIV/0!")]
    public void Exp_and_ln(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A2", "-1"), ("A3", "x"), ("C1", "=1/0")));
    }

    // Microsoft's 4 LOG10 examples and its 3 PI examples, rounded to the decimals Microsoft shows (PI() as 3.141592654).
    // The last row reads cell LOG10 inside a LOG10 call: the name is a function only right before "(".
    [Theory]
    [InlineData("=ROUND(LOG10(86),4)", "1.9345")]
    [InlineData("=LOG10(10)", "1")]
    [InlineData("=LOG10(100000)", "5")]
    [InlineData("=LOG10(10^5)", "5")]
    [InlineData("=ROUND(PI(),9)", "3.141592654")]
    [InlineData("=ROUND(PI()/2,9)", "1.570796327")]
    [InlineData("=ROUND(PI()*(A3^2),8)", "28.27433388")]
    [InlineData("=PI()", "3.14159265358979")]
    [InlineData("=LOG10(1)", "0")]
    [InlineData("=LOG10(0.001)", "-3")]
    [InlineData("=LOG10(0)", "#NUM!")]
    [InlineData("=LOG10(A2)", "#NUM!")]
    [InlineData("=LOG10(A4)", "#VALUE!")]
    [InlineData("=LOG10(C1)", "#DIV/0!")]
    [InlineData("=LOG10(LOG10)", "2")]
    public void Log10_and_pi(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A2", "-1"), ("A3", "3"), ("A4", "x"), ("C1", "=1/0"), ("LOG10", "100")));
    }

    // Microsoft's 4 EVEN examples and 5 ODD examples, then the cases around them.
    [Theory]
    [InlineData("=EVEN(1.5)", "2")]
    [InlineData("=EVEN(3)", "4")]
    [InlineData("=EVEN(2)", "2")]
    [InlineData("=EVEN(-1)", "-2")]
    [InlineData("=EVEN(0)", "0")]
    [InlineData("=EVEN(-2.5)", "-4")]
    [InlineData("=EVEN(A3)", "8")]
    [InlineData("=EVEN(A4)", "#VALUE!")]
    [InlineData("=EVEN(C1)", "#DIV/0!")]
    [InlineData("=ODD(1.5)", "3")]
    [InlineData("=ODD(3)", "3")]
    [InlineData("=ODD(2)", "3")]
    [InlineData("=ODD(-1)", "-1")]
    [InlineData("=ODD(-2)", "-3")]
    [InlineData("=ODD(0)", "1")]
    [InlineData("=ODD(0.2)", "1")]
    [InlineData("=ODD(A3)", "7")]
    [InlineData("=ODD(A4)", "#VALUE!")]
    public void Even_and_odd_round_away_from_zero(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A3", "7"), ("A4", "x"), ("C1", "=1/0")));
    }

    // Microsoft's 2 XOR examples and 5 FACT examples, then the cases around them.
    [Theory]
    [InlineData("=XOR(3>0,2<9)", "FALSE")]
    [InlineData("=XOR(3>12,4>6)", "FALSE")]
    [InlineData("=XOR(TRUE,FALSE)", "TRUE")]
    [InlineData("=XOR(TRUE,TRUE,TRUE)", "TRUE")]
    [InlineData("=XOR(1,0,\"TRUE\")", "FALSE")]
    [InlineData("=XOR(A1:A4)", "TRUE")]
    [InlineData("=XOR(A2:A3)", "#VALUE!")]
    [InlineData("=XOR(\"maybe\")", "#VALUE!")]
    [InlineData("=XOR(C1,TRUE)", "#DIV/0!")]
    [InlineData("=FACT(5)", "120")]
    [InlineData("=FACT(1.9)", "1")]
    [InlineData("=FACT(0)", "1")]
    [InlineData("=FACT(-1)", "#NUM!")]
    [InlineData("=FACT(1)", "1")]
    [InlineData("=FACT(10)", "3628800")]
    [InlineData("=FACT(171)", "#NUM!")]
    [InlineData("=FACT(A4)", "#VALUE!")]
    [InlineData("=FACT(\"3\")", "6")]
    public void Xor_and_fact_match_microsofts_examples(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A1", "TRUE"), ("A2", "x"), ("A4", "y"), ("C1", "=1/0")));
    }

    // Microsoft's 4 GCD examples and 2 LCM examples, then the cases around them. A2:A4 hold 5, 15, 30.
    [Theory]
    [InlineData("=GCD(5, 2)", "1")]
    [InlineData("=GCD(24, 36)", "12")]
    [InlineData("=GCD(7, 1)", "1")]
    [InlineData("=GCD(5, 0)", "5")]
    [InlineData("=LCM(5, 2)", "10")]
    [InlineData("=LCM(24, 36)", "72")]
    [InlineData("=GCD(24.9, 36.5)", "12")]
    [InlineData("=GCD(A2:A4)", "5")]
    [InlineData("=LCM(A2:A4)", "30")]
    [InlineData("=LCM(A2:A4, 4)", "60")]
    [InlineData("=LCM(0, 5)", "0")]
    [InlineData("=GCD(\"12\", 18)", "6")]
    [InlineData("=GCD(-1, 2)", "#NUM!")]
    [InlineData("=LCM(0, -2)", "#NUM!")]
    [InlineData("=GCD(POWER(2,53), 2)", "#NUM!")]
    [InlineData("=LCM(POWER(2,30), POWER(3,20))", "#NUM!")]
    [InlineData("=GCD(\"x\")", "#VALUE!")]
    [InlineData("=LCM(B1:B3)", "#VALUE!")]
    [InlineData("=GCD(C1, 2)", "#DIV/0!")]
    public void Gcd_and_lcm_match_microsofts_examples(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A2", "5"), ("A3", "15"), ("A4", "30"), ("B1", "text"), ("C1", "=1/0")));
    }

    // Microsoft's 1 COMBIN example and 2 PERMUT examples (A2 holds 100, A3 holds 3), then the cases around them.
    [Theory]
    [InlineData("=COMBIN(8,2)", "28")]
    [InlineData("=PERMUT(A2,A3)", "970200")]
    [InlineData("=PERMUT(3,2)", "6")]
    [InlineData("=COMBIN(8.9,2.9)", "28")]
    [InlineData("=COMBIN(5,0)", "1")]
    [InlineData("=COMBIN(5,5)", "1")]
    [InlineData("=COMBIN(0,0)", "1")]
    [InlineData("=COMBIN(52,5)", "2598960")]
    [InlineData("=PERMUT(5,0)", "1")]
    [InlineData("=COMBIN(\"8\",2)", "28")]
    [InlineData("=COMBIN(-1,0)", "#NUM!")]
    [InlineData("=COMBIN(3,4)", "#NUM!")]
    [InlineData("=COMBIN(3,-1)", "#NUM!")]
    [InlineData("=PERMUT(0,0)", "#NUM!")]
    [InlineData("=PERMUT(3,4)", "#NUM!")]
    [InlineData("=PERMUT(400,200)", "#NUM!")]
    [InlineData("=COMBIN(\"x\",2)", "#VALUE!")]
    [InlineData("=PERMUT(C1,2)", "#DIV/0!")]
    public void Combin_and_permut_match_microsofts_examples(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A2", "100"), ("A3", "3"), ("C1", "=1/0")));
    }

    // Microsoft's DEGREES example and its RADIANS example, the latter rounded to the 6 decimals Microsoft shows
    // (3 pi / 2), then the cases around them.
    [Theory]
    [InlineData("=DEGREES(PI())", "180")]
    [InlineData("=ROUND(RADIANS(270),6)", "4.712389")]
    [InlineData("=RADIANS(270)=3*PI()/2", "TRUE")]
    [InlineData("=DEGREES(PI()/2)", "90")]
    [InlineData("=RADIANS(180)", "3.14159265358979")]
    [InlineData("=DEGREES(RADIANS(A2))", "-45")]
    [InlineData("=DEGREES(0)", "0")]
    [InlineData("=RADIANS(\"90\")=PI()/2", "TRUE")]
    [InlineData("=DEGREES(10^307)", "#NUM!")]
    [InlineData("=RADIANS(\"x\")", "#VALUE!")]
    [InlineData("=DEGREES(C1)", "#DIV/0!")]
    public void Degrees_and_radians_match_microsofts_examples(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A2", "-45"), ("C1", "=1/0")));
    }

    // Microsoft's SIN, COS, TAN examples. Where Microsoft rounds (0.0 for the sine of pi, 7 decimals for COS(1.047),
    // 5 for TAN(0.785)), ROUND gives the same number of decimals. Then the cases around them.
    [Theory]
    [InlineData("=ROUND(SIN(PI()),15)", "0")]
    [InlineData("=SIN(PI()/2)", "1")]
    [InlineData("=SIN(30*PI()/180)", "0.5")]
    [InlineData("=SIN(RADIANS(30))", "0.5")]
    [InlineData("=ROUND(COS(1.047),7)", "0.5001711")]
    [InlineData("=COS(60*PI()/180)", "0.5")]
    [InlineData("=COS(RADIANS(60))", "0.5")]
    [InlineData("=ROUND(TAN(0.785),5)", "0.9992")]
    [InlineData("=TAN(45*PI()/180)", "1")]
    [InlineData("=TAN(RADIANS(45))", "1")]
    [InlineData("=COS(0)", "1")]
    [InlineData("=SIN(-A2)=-SIN(A2)", "TRUE")]
    [InlineData("=SIN(A2)^2+COS(A2)^2", "1")]
    [InlineData("=ROUND(TAN(A2)-SIN(A2)/COS(A2),15)", "0")]
    [InlineData("=SIN(\"x\")", "#VALUE!")]
    [InlineData("=COS(C1)", "#DIV/0!")]
    public void Sin_cos_tan_match_microsofts_examples(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A2", "0.7"), ("C1", "=1/0")));
    }

    [Theory]
    [InlineData("EVEN()", "EVEN at position 1 takes 1 argument, not 0.")]
    [InlineData("ODD(1,2)", "ODD at position 1 takes 1 argument, not 2.")]
    [InlineData("FACT(1,2)", "FACT at position 1 takes 1 argument, not 2.")]
    [InlineData("XOR()", "XOR at position 1 takes at least 1 argument, not 0.")]
    [InlineData("GCD()", "GCD at position 1 takes at least 1 argument, not 0.")]
    [InlineData("COMBIN(8)", "COMBIN at position 1 takes 2 arguments, not 1.")]
    [InlineData("DEGREES()", "DEGREES at position 1 takes 1 argument, not 0.")]
    [InlineData("RADIANS(1,2)", "RADIANS at position 1 takes 1 argument, not 2.")]
    [InlineData("SIN()", "SIN at position 1 takes 1 argument, not 0.")]
    [InlineData("TAN(1,2)", "TAN at position 1 takes 1 argument, not 2.")]
    public void Even_and_odd_refuse_the_wrong_number_of_arguments(string formula, string message)
    {
        var error = Assert.Throws<FormulaException>(() => Parser.Parse(formula));
        Assert.Equal(message, error.Message);
    }

    // Microsoft's 3 QUOTIENT examples, then the cases around them.
    [Theory]
    [InlineData("=QUOTIENT(5, 2)", "2")]
    [InlineData("=QUOTIENT(4.5, 3.1)", "1")]
    [InlineData("=QUOTIENT(-10, 3)", "-3")]
    [InlineData("=QUOTIENT(10, -3)", "-3")]
    [InlineData("=QUOTIENT(-10, -3)", "3")]
    [InlineData("=QUOTIENT(2, 5)", "0")]
    [InlineData("=QUOTIENT(A3, 2)", "3")]
    [InlineData("=QUOTIENT(A3, 0)", "#DIV/0!")]
    [InlineData("=QUOTIENT(A4, 2)", "#VALUE!")]
    [InlineData("=QUOTIENT(2, A4)", "#VALUE!")]
    [InlineData("=QUOTIENT(C1, 2)", "#DIV/0!")]
    public void Quotient_keeps_the_whole_part_toward_zero(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A3", "7"), ("A4", "x"), ("C1", "=1/0")));
    }

    [Theory]
    [InlineData("QUOTIENT(1)", "QUOTIENT at position 1 takes 2 arguments, not 1.")]
    [InlineData("QUOTIENT(1,2,3)", "QUOTIENT at position 1 takes 2 arguments, not 3.")]
    public void Quotient_refuses_the_wrong_number_of_arguments(string formula, string message)
    {
        var error = Assert.Throws<FormulaException>(() => Parser.Parse(formula));
        Assert.Equal(message, error.Message);
    }

    // Microsoft's 3 TRUNC examples, then its remark that TRUNC(-4.3) is -4 where INT(-4.3) is -5.
    [Theory]
    [InlineData("=TRUNC(8.9)", "8")]
    [InlineData("=TRUNC(-8.9)", "-8")]
    [InlineData("=TRUNC(0.45)", "0")]
    [InlineData("=TRUNC(-4.3)", "-4")]
    [InlineData("=INT(-4.3)", "-5")]
    [InlineData("=TRUNC(3.14159, 2)", "3.14")]
    [InlineData("=TRUNC(-1.239, 2)", "-1.23")]
    [InlineData("=TRUNC(1234.5, -2)", "1200")]
    [InlineData("=TRUNC(A3, 0)", "3")]
    [InlineData("=TRUNC(A4)", "#VALUE!")]
    [InlineData("=TRUNC(1.5, A4)", "#VALUE!")]
    [InlineData("=TRUNC(C1)", "#DIV/0!")]
    public void Trunc_drops_digits_toward_zero(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A3", "3.99"), ("A4", "x"), ("C1", "=1/0")));
    }

    [Theory]
    [InlineData("TRUNC()", "TRUNC at position 1 takes 1 to 2 arguments, not 0.")]
    [InlineData("TRUNC(1,2,3)", "TRUNC at position 1 takes 1 to 2 arguments, not 3.")]
    public void Trunc_refuses_the_wrong_number_of_arguments(string formula, string message)
    {
        var error = Assert.Throws<FormulaException>(() => Parser.Parse(formula));
        Assert.Equal(message, error.Message);
    }

    // Microsoft's 3 LOG examples, the 3rd rounded to the 7 decimals Microsoft shows.
    [Theory]
    [InlineData("=LOG(10)", "1")]
    [InlineData("=LOG(8, 2)", "3")]
    [InlineData("=ROUND(LOG(86, 2.7182818),7)", "4.4543473")]
    [InlineData("=LOG(1000)", "3")]
    [InlineData("=LOG(0.5, 2)", "-1")]
    [InlineData("=LOG(A3, A3)", "1")]
    [InlineData("=LOG(0)", "#NUM!")]
    [InlineData("=LOG(8, 0)", "#NUM!")]
    [InlineData("=LOG(8, A2)", "#NUM!")]
    [InlineData("=LOG(8, 1)", "#DIV/0!")]
    [InlineData("=LOG(A4, 2)", "#VALUE!")]
    [InlineData("=LOG(8, A4)", "#VALUE!")]
    [InlineData("=LOG(C1, 2)", "#DIV/0!")]
    public void Log_with_and_without_a_base(string formula, string expected)
    {
        Assert.Equal(expected, Eval.Display(formula, ("A2", "-1"), ("A3", "3"), ("A4", "x"), ("C1", "=1/0")));
    }

    [Theory]
    [InlineData("LOG()", "LOG at position 1 takes 1 to 2 arguments, not 0.")]
    [InlineData("LOG(1,2,3)", "LOG at position 1 takes 1 to 2 arguments, not 3.")]
    public void Log_refuses_the_wrong_number_of_arguments(string formula, string message)
    {
        var error = Assert.Throws<FormulaException>(() => Parser.Parse(formula));
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData("PI(1)", "PI at position 1 takes 0 arguments, not 1.")]
    [InlineData("LOG10()", "LOG10 at position 1 takes 1 argument, not 0.")]
    [InlineData("SUM()", "SUM at position 1 takes at least 1 argument, not 0.")]
    public void Pi_and_log10_refuse_the_wrong_number_of_arguments(string formula, string message)
    {
        var error = Assert.Throws<FormulaException>(() => Parser.Parse(formula));
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void Function_names_ignore_case()
    {
        Assert.Equal("3", Eval.Display("=sum(1,2)"));
        Assert.Contains("VLOOKUP", Functions.Names);
    }
}
