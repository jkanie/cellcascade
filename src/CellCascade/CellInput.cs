using System.Globalization;

namespace CellCascade;

/// <summary>What a person typed into a cell, read the way a spreadsheet reads it.</summary>
public sealed record CellInput(string Text, Expr? Formula, CellValue Literal)
{
    public bool IsBlank => Formula is null && Literal is BlankValue;

    /// <summary>
    /// Reads typed input. "=..." is a formula. A leading ' keeps the rest as text, so '007 stays 007.
    /// TRUE and FALSE are booleans. A number (with an optional % at the end) is a number. The rest is text.
    /// Empty input makes the cell blank.
    /// </summary>
    public static CellInput Parse(string? text)
    {
        text ??= string.Empty;
        if (text.Length > Parser.MaxInputLength)
        {
            throw new FormulaException($"The input has {text.Length:N0} characters, and the limit is {Parser.MaxInputLength:N0}.");
        }

        if (text.Length == 0)
        {
            return new CellInput(text, null, CellValue.Blank);
        }

        if (text[0] == '=')
        {
            if (string.IsNullOrWhiteSpace(text[1..]))
            {
                throw new FormulaException("The formula is empty.");
            }

            return new CellInput(text, Parser.Parse(text[1..]), CellValue.Blank);
        }

        if (text[0] == '\'')
        {
            return new CellInput(text, null, CellValue.Of(text[1..]));
        }

        var trimmed = text.Trim();
        if (trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
        {
            return new CellInput(text, null, CellValue.True);
        }

        if (trimmed.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
        {
            return new CellInput(text, null, CellValue.False);
        }

        var percent = trimmed.EndsWith('%');
        var digits = percent ? trimmed[..^1] : trimmed;
        if (digits.Length > 0
            && double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number))
        {
            return new CellInput(text, null, CellValue.Of(percent ? number / 100 : number));
        }

        return new CellInput(text, null, CellValue.Of(text));
    }
}
