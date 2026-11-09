namespace CellCascade.Api;

/// <summary>A value as JSON: the type, the text that the grid shows, the number for a number.</summary>
public sealed record ValueDto(string Type, string Display, double? Number)
{
    public static ValueDto From(CellValue value) =>
        new(value.TypeName, value.Display, value is NumberValue n ? n.Number : null);
}

public sealed record CellDto(string Ref, string Input, ValueDto Value);

public sealed record CellDetailDto(string Ref, string Input, ValueDto Value, IReadOnlyList<string> Precedents, IReadOnlyList<string> Dependents);

public sealed record SheetDto(string Name, IReadOnlyList<CellDto> Cells);

/// <summary>The answer to an edit: each cell whose value changed, the cells re-calculated (in order), the cells in a cycle.</summary>
public sealed record EditResultDto(IReadOnlyList<CellDto> Changed, IReadOnlyList<string> Recalculated, IReadOnlyList<string> InCycle);

public sealed record CellEdit(string? Input);

public sealed record CellBatch(Dictionary<string, string?>? Cells);

public sealed record ErrorDto(string Error);
