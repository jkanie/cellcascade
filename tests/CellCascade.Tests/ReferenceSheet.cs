namespace CellCascade.Tests;

/// <summary>
/// A slow sheet for the oracle test. It keeps only the inputs and works every value out again from scratch,
/// with no dependency graph. A cell is in a cycle when a walk from it through the cells it reads comes back to it.
/// Every other cell is evaluated by recursion with a memo. It shares the formula evaluator with the engine,
/// because the oracle checks what the engine re-calculates and in what order, not what each function returns.
/// </summary>
internal sealed class ReferenceSheet : IValueSource
{
    private readonly Dictionary<CellAddress, CellInput> inputs = [];
    private readonly Dictionary<CellAddress, CellValue> memo = [];
    private HashSet<CellAddress> cycle = [];

    public void Set(CellAddress address, string? text)
    {
        var input = CellInput.Parse(text);
        if (input.IsBlank)
        {
            inputs.Remove(address);
        }
        else
        {
            inputs[address] = input;
        }
    }

    public IReadOnlyDictionary<CellAddress, CellInput> Inputs => inputs;

    /// <summary>Every cell a formula reads, found by a walk of its own (not the engine's).</summary>
    public static HashSet<CellAddress> Reads(Expr expr)
    {
        var found = new HashSet<CellAddress>();
        void Walk(Expr e)
        {
            switch (e)
            {
                case CellExpr c:
                    found.Add(c.Address);
                    break;
                case RangeExpr r:
                    for (var row = r.From.Row; row <= r.To.Row; row++)
                    {
                        for (var column = r.From.Column; column <= r.To.Column; column++)
                        {
                            found.Add(new CellAddress(column, row));
                        }
                    }

                    break;
                case UnaryExpr u:
                    Walk(u.Operand);
                    break;
                case PercentExpr p:
                    Walk(p.Operand);
                    break;
                case BinaryExpr b:
                    Walk(b.Left);
                    Walk(b.Right);
                    break;
                case CallExpr call:
                    call.Arguments.ToList().ForEach(Walk);
                    break;
            }
        }

        Walk(expr);
        return found;
    }

    public HashSet<CellAddress> ReadsOf(CellAddress address) =>
        inputs.TryGetValue(address, out var input) && input.Formula is not null ? Reads(input.Formula) : [];

    /// <summary>Works out every value again. Call it after a set of changes.</summary>
    public void Recompute()
    {
        memo.Clear();
        cycle = [];
        foreach (var address in inputs.Keys)
        {
            var seen = new HashSet<CellAddress>();
            var pending = new Stack<CellAddress>(ReadsOf(address));
            while (pending.Count > 0)
            {
                var next = pending.Pop();
                if (next == address)
                {
                    cycle.Add(address);
                    break;
                }

                if (seen.Add(next))
                {
                    foreach (var read in ReadsOf(next))
                    {
                        pending.Push(read);
                    }
                }
            }
        }
    }

    public bool InCycle(CellAddress address) => cycle.Contains(address);

    public CellValue ValueAt(CellAddress address)
    {
        if (!inputs.TryGetValue(address, out var input))
        {
            return CellValue.Blank;
        }

        if (input.Formula is null)
        {
            return input.Literal;
        }

        if (cycle.Contains(address))
        {
            return CellValue.Error(ErrorKind.Cycle);
        }

        if (memo.TryGetValue(address, out var known))
        {
            return known;
        }

        var value = Evaluator.Evaluate(input.Formula, this);
        value = value is BlankValue ? CellValue.Of(0) : value;
        memo[address] = value;
        return value;
    }
}
