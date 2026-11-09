namespace CellCascade.Tests;

/// <summary>Runs 1 formula on a fresh sheet, after setting the given cells.</summary>
internal static class Eval
{
    public static CellValue Value(string input, params (string Ref, string Input)[] cells)
    {
        var sheet = new Sheet();
        sheet.Set(cells.Select(c => new KeyValuePair<CellAddress, string?>(CellAddress.Parse(c.Ref), c.Input)));
        var target = CellAddress.Parse("Z100");
        sheet.Set(target, input);
        return sheet.ValueAt(target);
    }

    public static string Display(string input, params (string Ref, string Input)[] cells) => Value(input, cells).Display;
}
