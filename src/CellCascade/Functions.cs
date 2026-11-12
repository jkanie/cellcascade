using System.Collections.Frozen;

namespace CellCascade;

/// <summary>The built-in functions. Names ignore case.</summary>
public static class Functions
{
    private const int Many = 255;

    private static readonly FrozenDictionary<string, (int Min, int Max)> Arity = new Dictionary<string, (int, int)>
    {
        ["SUM"] = (1, Many),
        ["AVERAGE"] = (1, Many),
        ["MIN"] = (1, Many),
        ["MAX"] = (1, Many),
        ["COUNT"] = (1, Many),
        ["COUNTA"] = (1, Many),
        ["AND"] = (1, Many),
        ["OR"] = (1, Many),
        ["NOT"] = (1, 1),
        ["IF"] = (2, 3),
        ["IFERROR"] = (2, 2),
        ["ABS"] = (1, 1),
        ["ROUND"] = (2, 2),
        ["ROUNDUP"] = (2, 2),
        ["ROUNDDOWN"] = (2, 2),
        ["INT"] = (1, 1),
        ["MOD"] = (2, 2),
        ["CONCAT"] = (1, Many),
        ["LEN"] = (1, 1),
        ["UPPER"] = (1, 1),
        ["LOWER"] = (1, 1),
        ["LEFT"] = (1, 2),
        ["RIGHT"] = (1, 2),
        ["MID"] = (3, 3),
        ["TRIM"] = (1, 1),
        ["FIND"] = (2, 3),
        ["SUBSTITUTE"] = (3, 4),
        ["VLOOKUP"] = (3, 4),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The names of the built-in functions, in alphabetical order.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Arity.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// Refuses a call with the wrong number of arguments, as Excel refuses to enter one.
    /// An unknown name passes, because Excel takes it and shows #NAME? in the cell.
    /// </summary>
    public static void CheckArity(string name, int count, int position)
    {
        if (Arity.TryGetValue(name, out var arity) && (count < arity.Min || count > arity.Max))
        {
            var wanted = arity.Min == arity.Max ? $"{arity.Min}" : arity.Max == Many ? $"at least {arity.Min}" : $"{arity.Min} to {arity.Max}";
            throw new FormulaException($"{name} at position {position + 1} takes {wanted} arguments, not {count}.");
        }
    }

    internal static CellValue Call(CallExpr call, IValueSource source)
    {
        var args = call.Arguments;
        return call.Name switch
        {
            "SUM" => Aggregate(args, source, values => values.Sum()),
            "AVERAGE" => Aggregate(args, source, values => values.Count == 0 ? double.NaN : values.Average(), emptyIsDivideByZero: true),
            "MIN" => Aggregate(args, source, values => values.Count == 0 ? 0 : values.Min()),
            "MAX" => Aggregate(args, source, values => values.Count == 0 ? 0 : values.Max()),
            "COUNT" => Count(args, source),
            "COUNTA" => CountA(args, source),
            "AND" => Logical(args, source, all: true),
            "OR" => Logical(args, source, all: false),
            "NOT" => Not(args[0], source),
            "IF" => If(args, source),
            "IFERROR" => IfError(args, source),
            "ABS" => NumberFunction(args[0], source, Math.Abs),
            "ROUND" => Round(args, source, Rounding.HalfAwayFromZero),
            "ROUNDUP" => Round(args, source, Rounding.AwayFromZero),
            "ROUNDDOWN" => Round(args, source, Rounding.TowardZero),
            "INT" => NumberFunction(args[0], source, Math.Floor),
            "MOD" => Mod(args, source),
            "CONCAT" => Concat(args, source),
            "LEN" => TextFunction(args[0], source, text => CellValue.Of(text.Length)),
            "UPPER" => TextFunction(args[0], source, text => CellValue.Of(text.ToUpperInvariant())),
            "LOWER" => TextFunction(args[0], source, text => CellValue.Of(text.ToLowerInvariant())),
            "LEFT" => Take(args, source, fromEnd: false),
            "RIGHT" => Take(args, source, fromEnd: true),
            "MID" => Mid(args, source),
            "TRIM" => TextFunction(args[0], source, text => CellValue.Of(Trim(text))),
            "FIND" => Find(args, source),
            "SUBSTITUTE" => Substitute(args, source),
            "VLOOKUP" => VLookup(args, source),
            _ => CellValue.Error(ErrorKind.Name),
        };
    }

    /// <summary>The cells of an argument that is a range or a single cell reference, else null.</summary>
    private static IEnumerable<CellValue>? CellsOf(Expr arg, IValueSource source) => arg switch
    {
        RangeExpr range => range.Cells().Select(source.ValueAt),
        CellExpr cell => [source.ValueAt(cell.Address)],
        _ => null,
    };

    /// <summary>
    /// SUM, AVERAGE, MIN, MAX. In a range or a cell reference, only numbers count. An error stops the sum.
    /// A value typed straight into the call is turned into a number, so SUM("3", TRUE) is 4.
    /// </summary>
    private static CellValue Aggregate(IReadOnlyList<Expr> args, IValueSource source, Func<List<double>, double> combine, bool emptyIsDivideByZero = false)
    {
        var numbers = new List<double>();
        foreach (var arg in args)
        {
            var cells = CellsOf(arg, source);
            if (cells is not null)
            {
                foreach (var value in cells)
                {
                    switch (value)
                    {
                        case ErrorValue:
                            return value;
                        case NumberValue n:
                            numbers.Add(n.Number);
                            break;
                    }
                }

                continue;
            }

            if (!Evaluator.TryNumber(Evaluator.Evaluate(arg, source), out var number, out var error))
            {
                return error!;
            }

            numbers.Add(number);
        }

        if (emptyIsDivideByZero && numbers.Count == 0)
        {
            return CellValue.Error(ErrorKind.DivideByZero);
        }

        return CellValue.Of(combine(numbers));
    }

    /// <summary>COUNT: numbers in ranges and references, plus typed values that can be read as a number.</summary>
    private static CellValue Count(IReadOnlyList<Expr> args, IValueSource source)
    {
        var count = 0;
        foreach (var arg in args)
        {
            var cells = CellsOf(arg, source);
            if (cells is not null)
            {
                count += cells.Count(value => value is NumberValue);
            }
            else if (Evaluator.TryNumber(Evaluator.Evaluate(arg, source), out _, out _))
            {
                count++;
            }
        }

        return CellValue.Of(count);
    }

    /// <summary>COUNTA: every cell that is not blank (errors included), plus every typed value.</summary>
    private static CellValue CountA(IReadOnlyList<Expr> args, IValueSource source)
    {
        var count = 0;
        foreach (var arg in args)
        {
            var cells = CellsOf(arg, source);
            count += cells is not null ? cells.Count(value => value is not BlankValue) : 1;
        }

        return CellValue.Of(count);
    }

    /// <summary>AND and OR. In ranges, text and blank cells are skipped. With no value to test, the result is #VALUE!.</summary>
    private static CellValue Logical(IReadOnlyList<Expr> args, IValueSource source, bool all)
    {
        var seen = false;
        var result = all;
        foreach (var arg in args)
        {
            var cells = CellsOf(arg, source);
            IEnumerable<CellValue> values = cells is not null
                ? cells.Where(value => value is not TextValue and not BlankValue)
                : [Evaluator.Evaluate(arg, source)];
            foreach (var value in values)
            {
                if (!Evaluator.TryBool(value, out var flag, out var error))
                {
                    return error!;
                }

                seen = true;
                result = all ? result && flag : result || flag;
            }
        }

        return seen ? CellValue.Of(result) : CellValue.Error(ErrorKind.Value);
    }

    private static CellValue Not(Expr arg, IValueSource source) =>
        Evaluator.TryBool(Scalar(arg, source), out var flag, out var error) ? CellValue.Of(!flag) : error!;

    /// <summary>IF evaluates only the branch that it returns, so an error in the other branch does no harm.</summary>
    private static CellValue If(IReadOnlyList<Expr> args, IValueSource source)
    {
        if (!Evaluator.TryBool(Scalar(args[0], source), out var condition, out var error))
        {
            return error!;
        }

        if (condition)
        {
            return Scalar(args[1], source);
        }

        return args.Count > 2 ? Scalar(args[2], source) : CellValue.False;
    }

    /// <summary>IFERROR evaluates its 2nd argument only when the 1st gives an error.</summary>
    private static CellValue IfError(IReadOnlyList<Expr> args, IValueSource source)
    {
        var value = Scalar(args[0], source);
        return value is ErrorValue ? Scalar(args[1], source) : value;
    }

    private static CellValue NumberFunction(Expr arg, IValueSource source, Func<double, double> function) =>
        Evaluator.TryNumber(Scalar(arg, source), out var number, out var error) ? CellValue.Of(function(number)) : error!;

    private static CellValue TextFunction(Expr arg, IValueSource source, Func<string, CellValue> function) =>
        Evaluator.TryText(Scalar(arg, source), out var text, out var error) ? function(text) : error!;

    /// <summary>
    /// LEFT and RIGHT take num_chars characters (1 when it is left out) from the start or the end of the text, or all
    /// of it when num_chars is longer. A fraction is cut toward 0, and a count below 0 gives #VALUE!.
    /// </summary>
    private static CellValue Take(IReadOnlyList<Expr> args, IValueSource source, bool fromEnd)
    {
        if (!Evaluator.TryText(Scalar(args[0], source), out var text, out var error))
        {
            return error!;
        }

        var count = 1.0;
        if (args.Count == 2 && !Evaluator.TryNumber(Scalar(args[1], source), out count, out error))
        {
            return error!;
        }

        if (count < 0)
        {
            return CellValue.Error(ErrorKind.Value);
        }

        var n = (int)Math.Min(Math.Truncate(count), text.Length);
        return CellValue.Of(fromEnd ? text[^n..] : text[..n]);
    }

    /// <summary>
    /// MID(text, start_num, num_chars) counts from 1. A start past the end gives empty text, a start below 1 or a
    /// negative count gives #VALUE!, and a count past the end stops at the end, as Microsoft documents.
    /// </summary>
    private static CellValue Mid(IReadOnlyList<Expr> args, IValueSource source)
    {
        if (!Evaluator.TryText(Scalar(args[0], source), out var text, out var error))
        {
            return error!;
        }

        if (!Evaluator.TryNumber(Scalar(args[1], source), out var start, out error))
        {
            return error!;
        }

        if (!Evaluator.TryNumber(Scalar(args[2], source), out var count, out error))
        {
            return error!;
        }

        start = Math.Truncate(start);
        count = Math.Truncate(count);
        if (start < 1 || count < 0)
        {
            return CellValue.Error(ErrorKind.Value);
        }

        if (start > text.Length)
        {
            return CellValue.Of(string.Empty);
        }

        var from = (int)start - 1;
        return CellValue.Of(text.Substring(from, (int)Math.Min(count, text.Length - from)));
    }

    /// <summary>
    /// FIND(find_text, within_text, start_num) gives the position (from 1) of the 1st find_text in within_text at or
    /// after start_num (1 when it is left out). Case counts, and ? and * are plain characters. As Microsoft
    /// documents, a start_num that is not above 0 or is past the length of within_text gives #VALUE!, an empty
    /// find_text matches at start_num, and text that is not found gives #VALUE!. A fraction is cut toward 0.
    /// </summary>
    private static CellValue Find(IReadOnlyList<Expr> args, IValueSource source)
    {
        if (!Evaluator.TryText(Scalar(args[0], source), out var find, out var error)
            || !Evaluator.TryText(Scalar(args[1], source), out var within, out error))
        {
            return error!;
        }

        var start = 1.0;
        if (args.Count == 3 && !Evaluator.TryNumber(Scalar(args[2], source), out start, out error))
        {
            return error!;
        }

        start = Math.Truncate(start);
        if (start < 1 || start > within.Length)
        {
            return CellValue.Error(ErrorKind.Value);
        }

        var at = within.IndexOf(find, (int)start - 1, StringComparison.Ordinal);
        return at < 0 ? CellValue.Error(ErrorKind.Value) : CellValue.Of(at + 1);
    }

    /// <summary>
    /// SUBSTITUTE(text, old_text, new_text, instance_num) puts new_text in place of every old_text, or only of the
    /// instance_num-th 1 (counted from 1, each match after the end of the last) when it is given. Case counts. An empty old_text, or an
    /// instance that is not there, leaves the text as it is. An instance_num below 1 gives #VALUE!, and a fraction is
    /// cut toward 0.
    /// </summary>
    private static CellValue Substitute(IReadOnlyList<Expr> args, IValueSource source)
    {
        if (!Evaluator.TryText(Scalar(args[0], source), out var text, out var error)
            || !Evaluator.TryText(Scalar(args[1], source), out var old, out error)
            || !Evaluator.TryText(Scalar(args[2], source), out var replacement, out error))
        {
            return error!;
        }

        if (args.Count < 4)
        {
            return CellValue.Of(old.Length == 0 ? text : text.Replace(old, replacement, StringComparison.Ordinal));
        }

        if (!Evaluator.TryNumber(Scalar(args[3], source), out var instance, out error))
        {
            return error!;
        }

        instance = Math.Truncate(instance);
        if (instance < 1)
        {
            return CellValue.Error(ErrorKind.Value);
        }

        if (old.Length == 0)
        {
            return CellValue.Of(text);
        }

        var at = -old.Length;
        for (var seen = 0; seen < instance; seen++)
        {
            at = text.IndexOf(old, at + old.Length, StringComparison.Ordinal);
            if (at < 0)
            {
                return CellValue.Of(text);
            }
        }

        return CellValue.Of(string.Concat(text.AsSpan(0, at), replacement, text.AsSpan(at + old.Length)));
    }

    /// <summary>TRIM drops the spaces (character 32 only) at both ends and leaves 1 space between words.</summary>
    private static string Trim(string text) =>
        string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private enum Rounding
    {
        HalfAwayFromZero,
        AwayFromZero,
        TowardZero,
    }

    /// <summary>
    /// ROUND rounds half away from 0, as Excel does, so ROUND(2.5, 0) is 3 and ROUND(-2.5, 0) is -3.
    /// ROUNDUP rounds away from 0, so ROUNDUP(-3.14159, 1) is -3.2. ROUNDDOWN rounds toward 0, so
    /// ROUNDDOWN(-3.14159, 1) is -3.1. A negative 2nd argument rounds to the left of the decimal point.
    /// </summary>
    private static CellValue Round(IReadOnlyList<Expr> args, IValueSource source, Rounding rounding)
    {
        if (!Evaluator.TryNumber(Scalar(args[0], source), out var number, out var error))
        {
            return error!;
        }

        if (!Evaluator.TryNumber(Scalar(args[1], source), out var places, out error))
        {
            return error!;
        }

        var digits = (int)Math.Truncate(Math.Clamp(places, -308, 308));
        if (digits > 15)
        {
            return CellValue.Of(number);
        }

        var mode = rounding switch
        {
            Rounding.HalfAwayFromZero => MidpointRounding.AwayFromZero,
            Rounding.TowardZero => MidpointRounding.ToZero,
            _ => number < 0 ? MidpointRounding.ToNegativeInfinity : MidpointRounding.ToPositiveInfinity,
        };

        // A double holds 1.005 as 1.00499999..., which rounds to 1. The decimal conversion keeps 15 digits,
        // which gives exactly 1.005, so ROUND(1.005, 2) is 1.01, as in Excel. For the same reason,
        // ROUNDUP(0.1 + 0.2, 1) is 0.3, not 0.4.
        if (digits >= 0)
        {
            if (Math.Abs(number) < 7.9e27)
            {
                return CellValue.Of((double)Math.Round((decimal)number, digits, mode));
            }

            return CellValue.Of(Math.Round(number, digits, mode));
        }

        var factor = Math.Pow(10, -digits);
        return CellValue.Of(Math.Round(number / factor, mode) * factor);
    }

    /// <summary>MOD(number, divisor) has the sign of the divisor, as in Excel, so MOD(-3, 2) is 1. A divisor of 0 gives #DIV/0!.</summary>
    private static CellValue Mod(IReadOnlyList<Expr> args, IValueSource source)
    {
        if (!Evaluator.TryNumber(Scalar(args[0], source), out var number, out var error))
        {
            return error!;
        }

        if (!Evaluator.TryNumber(Scalar(args[1], source), out var divisor, out error))
        {
            return error!;
        }

        if (divisor == 0)
        {
            return CellValue.Error(ErrorKind.DivideByZero);
        }

        return CellValue.Of(number - (divisor * Math.Floor(number / divisor)));
    }

    private static CellValue Concat(IReadOnlyList<Expr> args, IValueSource source)
    {
        var text = new System.Text.StringBuilder();
        foreach (var arg in args)
        {
            var values = CellsOf(arg, source) ?? [Evaluator.Evaluate(arg, source)];
            foreach (var value in values)
            {
                if (value is ErrorValue)
                {
                    return value;
                }

                text.Append(value.Display);
            }
        }

        return CellValue.Of(text.ToString());
    }

    /// <summary>
    /// VLOOKUP(value, range, column, [approximate]). An exact lookup returns the 1st row whose key equals the value.
    /// An approximate lookup (the default, as in Excel) needs the keys sorted from low to high. It returns the
    /// last row whose key is not greater than the value. Keys of another type are skipped.
    /// </summary>
    private static CellValue VLookup(IReadOnlyList<Expr> args, IValueSource source)
    {
        var lookup = Scalar(args[0], source);
        if (lookup is ErrorValue)
        {
            return lookup;
        }

        RangeExpr? range = args[1] switch
        {
            RangeExpr r => r,
            CellExpr c => new RangeExpr(c.Address, c.Address),
            _ => null,
        };
        if (range is null)
        {
            return CellValue.Error(ErrorKind.Value);
        }

        if (!Evaluator.TryNumber(Scalar(args[2], source), out var columnNumber, out var error))
        {
            return error!;
        }

        var column = (int)Math.Truncate(columnNumber);
        if (column < 1)
        {
            return CellValue.Error(ErrorKind.Value);
        }

        if (column > range.Width)
        {
            return CellValue.Error(ErrorKind.Reference);
        }

        var approximate = true;
        if (args.Count > 3 && !Evaluator.TryBool(Scalar(args[3], source), out approximate, out error))
        {
            return error!;
        }

        if (lookup is BlankValue)
        {
            lookup = CellValue.Of(0);
        }

        int? found = null;
        for (var row = range.From.Row; row <= range.To.Row; row++)
        {
            var key = source.ValueAt(new CellAddress(range.From.Column, row));
            if (key is ErrorValue or BlankValue || key.GetType() != lookup.GetType())
            {
                continue;
            }

            var order = Evaluator.Compare(key, lookup);
            if (order == 0)
            {
                found = row;
                if (!approximate)
                {
                    break;
                }
            }
            else if (approximate && order < 0)
            {
                found = row;
            }
            else if (approximate && order > 0)
            {
                break;
            }
        }

        if (found is null)
        {
            return CellValue.Error(ErrorKind.NotAvailable);
        }

        return source.ValueAt(new CellAddress(range.From.Column + column - 1, found.Value));
    }

    /// <summary>A value for a place that takes 1 value: a 1-cell reference gives that cell. A wider range gives #VALUE!.</summary>
    private static CellValue Scalar(Expr arg, IValueSource source) => Evaluator.Evaluate(arg, source);
}
