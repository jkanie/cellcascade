namespace CellCascade;

/// <summary>Thrown when a cell's input cannot be read as a formula. The message says why and where.</summary>
public sealed class FormulaException : Exception
{
    public FormulaException()
    {
    }

    public FormulaException(string message)
        : base(message)
    {
    }

    public FormulaException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
