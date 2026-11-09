using System.Globalization;

namespace CellCascade;

/// <summary>Gives the evaluator the current value of any cell.</summary>
public interface IValueSource
{
    CellValue ValueAt(CellAddress address);
}

/// <summary>
/// Evaluates a parsed formula. The rules for types follow Excel where this file says so,
/// and README.md lists the places where they differ.
/// </summary>
public static class Evaluator
{
    public static CellValue Evaluate(Expr expr, IValueSource source)
    {
        ArgumentNullException.ThrowIfNull(expr);
        ArgumentNullException.ThrowIfNull(source);
        return expr switch
        {
            NumberExpr n => CellValue.Of(n.Number),
            TextExpr t => CellValue.Of(t.Text),
            BoolExpr b => CellValue.Of(b.Value),
            CellExpr c => source.ValueAt(c.Address),

            // A range where 1 value is needed: Excel would pick 1 cell by implicit intersection, which this engine does not do.
            RangeExpr => CellValue.Error(ErrorKind.Value),
            UnaryExpr u => Unary(u, source),
            PercentExpr p => Percent(p, source),
            BinaryExpr b => Binary(b, source),
            CallExpr call => Functions.Call(call, source),
            _ => CellValue.Error(ErrorKind.Value),
        };
    }

    /// <summary>Turns a value into a number: blank is 0, TRUE is 1, text must hold a number.</summary>
    public static bool TryNumber(CellValue value, out double number, out CellValue? error)
    {
        error = null;
        number = 0;
        switch (value)
        {
            case NumberValue n:
                number = n.Number;
                return true;
            case BlankValue:
                return true;
            case BoolValue b:
                number = b.Value ? 1 : 0;
                return true;
            case TextValue t when double.TryParse(t.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed):
                number = parsed;
                return true;
            case ErrorValue:
                error = value;
                return false;
            default:
                error = CellValue.Error(ErrorKind.Value);
                return false;
        }
    }

    /// <summary>Turns a value into text: blank is empty. A number prints as the grid shows it.</summary>
    public static bool TryText(CellValue value, out string text, out CellValue? error)
    {
        error = null;
        text = string.Empty;
        if (value is ErrorValue)
        {
            error = value;
            return false;
        }

        text = value.Display;
        return true;
    }

    /// <summary>Turns a value into TRUE or FALSE: a number is TRUE unless it is 0. Text must say TRUE or FALSE.</summary>
    public static bool TryBool(CellValue value, out bool result, out CellValue? error)
    {
        error = null;
        result = false;
        switch (value)
        {
            case BoolValue b:
                result = b.Value;
                return true;
            case NumberValue n:
                result = n.Number != 0;
                return true;
            case BlankValue:
                return true;
            case TextValue t when t.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase):
                result = true;
                return true;
            case TextValue t when t.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase):
                return true;
            case ErrorValue:
                error = value;
                return false;
            default:
                error = CellValue.Error(ErrorKind.Value);
                return false;
        }
    }

    /// <summary>
    /// Compares 2 values as Excel does: any number is less than any text. Any text is less than any boolean.
    /// Text compares without regard to case. A blank cell counts as 0, as empty text or as FALSE, to match the other side.
    /// </summary>
    public static int Compare(CellValue left, CellValue right)
    {
        left = BlankAs(left, right);
        right = BlankAs(right, left);
        var rank = Rank(left).CompareTo(Rank(right));
        if (rank != 0)
        {
            return rank;
        }

        return (left, right) switch
        {
            (NumberValue a, NumberValue b) => a.Number.CompareTo(b.Number),
            (TextValue a, TextValue b) => string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase),
            (BoolValue a, BoolValue b) => a.Value.CompareTo(b.Value),
            _ => 0,
        };
    }

    private static CellValue BlankAs(CellValue value, CellValue other)
    {
        if (value is not BlankValue)
        {
            return value;
        }

        return other switch
        {
            TextValue => CellValue.Of(string.Empty),
            BoolValue => CellValue.False,
            _ => CellValue.Of(0),
        };
    }

    private static int Rank(CellValue value) => value switch
    {
        NumberValue => 0,
        TextValue => 1,
        BoolValue => 2,
        _ => 0,
    };

    private static CellValue Unary(UnaryExpr u, IValueSource source)
    {
        var value = Evaluate(u.Operand, source);
        if (!TryNumber(value, out var number, out var error))
        {
            return error!;
        }

        return CellValue.Of(u.Operator == '-' ? -number : number);
    }

    private static CellValue Percent(PercentExpr p, IValueSource source)
    {
        var value = Evaluate(p.Operand, source);
        return TryNumber(value, out var number, out var error) ? CellValue.Of(number / 100) : error!;
    }

    private static CellValue Binary(BinaryExpr b, IValueSource source)
    {
        var left = Evaluate(b.Left, source);
        var right = Evaluate(b.Right, source);

        // The left-most error wins, as in Excel.
        if (left is ErrorValue)
        {
            return left;
        }

        if (right is ErrorValue)
        {
            return right;
        }

        switch (b.Operator)
        {
            case "&":
                TryText(left, out var l, out _);
                TryText(right, out var r, out _);
                return CellValue.Of(l + r);
            case "=":
                return CellValue.Of(Compare(left, right) == 0);
            case "<>":
                return CellValue.Of(Compare(left, right) != 0);
            case "<":
                return CellValue.Of(Compare(left, right) < 0);
            case "<=":
                return CellValue.Of(Compare(left, right) <= 0);
            case ">":
                return CellValue.Of(Compare(left, right) > 0);
            case ">=":
                return CellValue.Of(Compare(left, right) >= 0);
        }

        if (!TryNumber(left, out var x, out var errorLeft))
        {
            return errorLeft!;
        }

        if (!TryNumber(right, out var y, out var errorRight))
        {
            return errorRight!;
        }

        return b.Operator switch
        {
            "+" => CellValue.Of(x + y),
            "-" => CellValue.Of(x - y),
            "*" => CellValue.Of(x * y),
            "/" => y == 0 ? CellValue.Error(ErrorKind.DivideByZero) : CellValue.Of(x / y),
            "^" => Power(x, y),
            _ => CellValue.Error(ErrorKind.Value),
        };
    }

    private static CellValue Power(double x, double y)
    {
        if (x == 0 && y == 0)
        {
            return CellValue.Error(ErrorKind.Number);
        }

        if (x == 0 && y < 0)
        {
            return CellValue.Error(ErrorKind.DivideByZero);
        }

        // Math.Pow gives NaN for a negative base with a fraction power. CellValue.Of turns NaN into #NUM!.
        return CellValue.Of(Math.Pow(x, y));
    }
}
