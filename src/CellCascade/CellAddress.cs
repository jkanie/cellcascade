namespace CellCascade;

/// <summary>
/// A cell address such as B7. Column and row are 0-based inside. The text
/// form is the usual letters plus a 1-based row number.
/// </summary>
public readonly record struct CellAddress(int Column, int Row) : IComparable<CellAddress>
{
    /// <summary>The widest sheet, as in Excel: columns A to XFD.</summary>
    public const int MaxColumns = 16_384;

    /// <summary>The tallest sheet, as in Excel: rows 1 to 1,048,576.</summary>
    public const int MaxRows = 1_048_576;

    public static CellAddress Parse(string text)
    {
        if (TryParse(text, out var address))
        {
            return address;
        }

        throw new FormatException($"'{text}' is not a cell address such as A1.");
    }

    /// <summary>Reads A1, $A$1, a1 and the like. The $ signs are allowed and ignored.</summary>
    public static bool TryParse(string? text, out CellAddress address)
    {
        address = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var i = 0;
        if (i < text.Length && text[i] == '$')
        {
            i++;
        }

        var column = 0;
        var letters = 0;
        while (i < text.Length && char.IsAsciiLetter(text[i]))
        {
            column = (column * 26) + (char.ToUpperInvariant(text[i]) - 'A' + 1);
            letters++;
            i++;
            if (letters > 3)
            {
                return false;
            }
        }

        if (letters == 0)
        {
            return false;
        }

        if (i < text.Length && text[i] == '$')
        {
            i++;
        }

        if (i >= text.Length || text[i] == '0')
        {
            return false;
        }

        var row = 0;
        var digits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            row = (row * 10) + (text[i] - '0');
            digits++;
            i++;
            if (digits > 7)
            {
                return false;
            }
        }

        if (digits == 0 || i != text.Length || column > MaxColumns || row > MaxRows)
        {
            return false;
        }

        address = new CellAddress(column - 1, row - 1);
        return true;
    }

    public static string ColumnName(int column)
    {
        Span<char> buffer = stackalloc char[3];
        var pos = buffer.Length;
        var n = column + 1;
        while (n > 0)
        {
            n--;
            buffer[--pos] = (char)('A' + (n % 26));
            n /= 26;
        }

        return new string(buffer[pos..]);
    }

    public int CompareTo(CellAddress other)
    {
        var byRow = Row.CompareTo(other.Row);
        return byRow != 0 ? byRow : Column.CompareTo(other.Column);
    }

    public static bool operator <(CellAddress left, CellAddress right) => left.CompareTo(right) < 0;

    public static bool operator <=(CellAddress left, CellAddress right) => left.CompareTo(right) <= 0;

    public static bool operator >(CellAddress left, CellAddress right) => left.CompareTo(right) > 0;

    public static bool operator >=(CellAddress left, CellAddress right) => left.CompareTo(right) >= 0;

    public override string ToString() => ColumnName(Column) + (Row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
