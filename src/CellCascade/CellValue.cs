using System.Globalization;

namespace CellCascade;

/// <summary>The error values a cell can hold. Each prints as in Excel, except Cycle.</summary>
public enum ErrorKind
{
    /// <summary>#DIV/0! -- a division by 0, or an average of no numbers.</summary>
    DivideByZero,

    /// <summary>#VALUE! -- a value of the wrong type, such as text that is not a number.</summary>
    Value,

    /// <summary>#REF! -- a column index past the edge of a range.</summary>
    Reference,

    /// <summary>#NAME? -- a function that does not exist.</summary>
    Name,

    /// <summary>#NUM! -- a result that is not a finite number.</summary>
    Number,

    /// <summary>#N/A -- a lookup that found nothing.</summary>
    NotAvailable,

    /// <summary>#CYCLE! -- the cell is part of a circular reference. Excel has no such value.</summary>
    Cycle,
}

/// <summary>The value of a cell: a number, text, a boolean, an error, or blank.</summary>
public abstract record CellValue
{
    public static readonly CellValue Blank = new BlankValue();
    public static readonly CellValue True = new BoolValue(true);
    public static readonly CellValue False = new BoolValue(false);

    public static CellValue Of(double number) =>
        double.IsFinite(number) ? new NumberValue(number) : new ErrorValue(ErrorKind.Number);

    public static CellValue Of(bool value) => value ? True : False;

    public static CellValue Of(string text) => new TextValue(text);

    public static CellValue Error(ErrorKind error) => new ErrorValue(error);

    /// <summary>The text that a grid shows for this value.</summary>
    public abstract string Display { get; }

    /// <summary>A short type name for the API: number, text, bool, error or blank.</summary>
    public abstract string TypeName { get; }

    /// <summary>Prints a number as Excel's grid does in the General format: at most 15 significant digits.</summary>
    public static string FormatNumber(double number)
    {
        if (number == 0)
        {
            return "0";
        }

        var text = number.ToString("G15", CultureInfo.InvariantCulture);
        if (text.Contains('E', StringComparison.Ordinal))
        {
            var mantissa = text[..text.IndexOf('E', StringComparison.Ordinal)];
            var exponent = int.Parse(text[(text.IndexOf('E', StringComparison.Ordinal) + 1)..], CultureInfo.InvariantCulture);
            return mantissa + "E" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
        }

        return text;
    }
}

public sealed record NumberValue(double Number) : CellValue
{
    public override string Display => FormatNumber(Number);

    public override string TypeName => "number";
}

public sealed record TextValue(string Text) : CellValue
{
    public override string Display => Text;

    public override string TypeName => "text";
}

public sealed record BoolValue(bool Value) : CellValue
{
    public override string Display => Value ? "TRUE" : "FALSE";

    public override string TypeName => "bool";
}

public sealed record ErrorValue(ErrorKind Kind) : CellValue
{
    public override string Display => Kind switch
    {
        ErrorKind.DivideByZero => "#DIV/0!",
        ErrorKind.Value => "#VALUE!",
        ErrorKind.Reference => "#REF!",
        ErrorKind.Name => "#NAME?",
        ErrorKind.Number => "#NUM!",
        ErrorKind.NotAvailable => "#N/A",
        ErrorKind.Cycle => "#CYCLE!",
        _ => "#VALUE!",
    };

    public override string TypeName => "error";
}

public sealed record BlankValue : CellValue
{
    public override string Display => string.Empty;

    public override string TypeName => "blank";
}
