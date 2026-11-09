namespace CellCascade;

/// <summary>A node of a parsed formula.</summary>
public abstract record Expr;

public sealed record NumberExpr(double Number) : Expr;

public sealed record TextExpr(string Text) : Expr;

public sealed record BoolExpr(bool Value) : Expr;

public sealed record CellExpr(CellAddress Address) : Expr;

/// <summary>A rectangle of cells such as A1:B3. From is the top-left corner and To is the bottom-right one.</summary>
public sealed record RangeExpr(CellAddress From, CellAddress To) : Expr
{
    public int Width => To.Column - From.Column + 1;

    public int Height => To.Row - From.Row + 1;

    public long Count => (long)Width * Height;

    public IEnumerable<CellAddress> Cells()
    {
        for (var row = From.Row; row <= To.Row; row++)
        {
            for (var column = From.Column; column <= To.Column; column++)
            {
                yield return new CellAddress(column, row);
            }
        }
    }
}

public sealed record UnaryExpr(char Operator, Expr Operand) : Expr;

public sealed record PercentExpr(Expr Operand) : Expr;

public sealed record BinaryExpr(string Operator, Expr Left, Expr Right) : Expr;

public sealed record CallExpr(string Name, IReadOnlyList<Expr> Arguments) : Expr;
