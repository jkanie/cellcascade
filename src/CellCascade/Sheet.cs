namespace CellCascade;

/// <summary>What 1 change did: the cells re-calculated (in order), the cells whose value changed, the cells in a cycle.</summary>
public sealed record RecalcResult(
    IReadOnlyList<CellAddress> Recalculated,
    IReadOnlyList<CellAddress> Changed,
    IReadOnlyList<CellAddress> InCycle);

/// <summary>
/// A sheet of cells with a dependency graph. A change re-calculates only the cells that depend on it,
/// directly or through other cells, each 1 time and after every cell it reads. A cell that is part of a
/// circular reference gets #CYCLE!. The class is not thread-safe, so callers lock around it.
/// </summary>
public sealed class Sheet : IValueSource
{
    private readonly Dictionary<CellAddress, Entry> cells = [];
    private readonly Dictionary<CellAddress, HashSet<CellAddress>> dependents = [];

    /// <summary>How many formulas this sheet has evaluated since it was made. Tests use it to prove what ran.</summary>
    public long Evaluations { get; private set; }

    public CellValue ValueAt(CellAddress address) => cells.TryGetValue(address, out var entry) ? entry.Value : CellValue.Blank;

    public string GetInput(CellAddress address) => cells.TryGetValue(address, out var entry) ? entry.Input.Text : string.Empty;

    /// <summary>Every cell that is not blank, by row and then by column.</summary>
    public IEnumerable<(CellAddress Address, string Input, CellValue Value)> Cells() =>
        cells.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value.Input.Text, pair.Value.Value));

    /// <summary>The cells that a cell's formula reads, by row and then by column.</summary>
    public IReadOnlyList<CellAddress> PrecedentsOf(CellAddress address) =>
        cells.TryGetValue(address, out var entry) ? [.. entry.Precedents.Order()] : [];

    /// <summary>The cells whose formulas read this cell directly, by row and then by column.</summary>
    public IReadOnlyList<CellAddress> DependentsOf(CellAddress address) =>
        dependents.TryGetValue(address, out var set) ? [.. set.Order()] : [];

    public RecalcResult Set(CellAddress address, string? input) =>
        Set([new KeyValuePair<CellAddress, string?>(address, input)]);

    /// <summary>
    /// Sets several cells, then re-calculates 1 time. Every input is read before any cell changes, so a
    /// formula with a syntax fault leaves the sheet as it was. The message of the FormulaException names the cell.
    /// </summary>
    public RecalcResult Set(IEnumerable<KeyValuePair<CellAddress, string?>> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var parsed = new Dictionary<CellAddress, CellInput>();
        foreach (var (address, text) in edits)
        {
            try
            {
                parsed[address] = CellInput.Parse(text);
            }
            catch (FormulaException e)
            {
                throw new FormulaException($"{address}: {e.Message}", e);
            }
        }

        var before = new Dictionary<CellAddress, CellValue>();
        foreach (var (address, input) in parsed)
        {
            before[address] = ValueAt(address);
            Unlink(address);
            if (input.IsBlank)
            {
                cells.Remove(address);
                continue;
            }

            var precedents = input.Formula is null ? new HashSet<CellAddress>() : References(input.Formula);
            cells[address] = new Entry(input, input.Literal, precedents);
            foreach (var precedent in precedents)
            {
                if (!dependents.TryGetValue(precedent, out var set))
                {
                    set = [];
                    dependents[precedent] = set;
                }

                set.Add(address);
            }
        }

        return Recalculate(parsed.Keys, before);
    }

    /// <summary>Every cell a formula reads, with each range opened up into its cells.</summary>
    public static HashSet<CellAddress> References(Expr formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var found = new HashSet<CellAddress>();
        var pending = new Stack<Expr>();
        pending.Push(formula);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case CellExpr c:
                    found.Add(c.Address);
                    break;
                case RangeExpr r:
                    found.UnionWith(r.Cells());
                    break;
                case UnaryExpr u:
                    pending.Push(u.Operand);
                    break;
                case PercentExpr p:
                    pending.Push(p.Operand);
                    break;
                case BinaryExpr b:
                    pending.Push(b.Left);
                    pending.Push(b.Right);
                    break;
                case CallExpr call:
                    foreach (var arg in call.Arguments)
                    {
                        pending.Push(arg);
                    }

                    break;
            }
        }

        return found;
    }

    private void Unlink(CellAddress address)
    {
        if (!cells.TryGetValue(address, out var old))
        {
            return;
        }

        foreach (var precedent in old.Precedents)
        {
            if (dependents.TryGetValue(precedent, out var set))
            {
                set.Remove(address);
                if (set.Count == 0)
                {
                    dependents.Remove(precedent);
                }
            }
        }
    }

    private RecalcResult Recalculate(IEnumerable<CellAddress> seeds, Dictionary<CellAddress, CellValue> before)
    {
        // 1. Every cell that the change reaches: the edited cells, plus their dependents, transitively.
        var affected = new HashSet<CellAddress>();
        var queue = new Queue<CellAddress>(seeds);
        while (queue.Count > 0)
        {
            var address = queue.Dequeue();
            if (!affected.Add(address))
            {
                continue;
            }

            if (dependents.TryGetValue(address, out var next))
            {
                foreach (var dependent in next)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        foreach (var address in affected)
        {
            before.TryAdd(address, ValueAt(address));
        }

        // 2. Group the reached cells into strongly connected components, then put them in an order where every
        // cell comes after the cells it reads. A component of more than 1 cell, or a cell that reads itself, is a cycle.
        var order = Components(affected);
        var recalculated = new List<CellAddress>();
        var inCycle = new List<CellAddress>();
        foreach (var component in order)
        {
            var isCycle = component.Count > 1 || (cells.TryGetValue(component[0], out var only) && only.Precedents.Contains(component[0]));
            foreach (var address in component)
            {
                if (!cells.TryGetValue(address, out var entry) || entry.Input.Formula is null)
                {
                    continue;
                }

                if (isCycle)
                {
                    entry.Value = CellValue.Error(ErrorKind.Cycle);
                    inCycle.Add(address);
                    continue;
                }

                Evaluations++;
                var value = Evaluator.Evaluate(entry.Input.Formula, this);

                // A formula that only points at a blank cell shows 0, as in Excel.
                entry.Value = value is BlankValue ? CellValue.Of(0) : value;
                recalculated.Add(address);
            }
        }

        var changed = before.Where(pair => pair.Value != ValueAt(pair.Key)).Select(pair => pair.Key).Order().ToList();
        inCycle.Sort();
        return new RecalcResult(recalculated, changed, inCycle);
    }

    /// <summary>
    /// Tarjan's algorithm with an explicit stack, such that a chain of 100,000 cells cannot overflow the call stack.
    /// It emits each component after every component that depends on it, so the list is reversed at the end.
    /// </summary>
    private List<List<CellAddress>> Components(HashSet<CellAddress> nodes)
    {
        var index = new Dictionary<CellAddress, int>();
        var low = new Dictionary<CellAddress, int>();
        var onStack = new HashSet<CellAddress>();
        var stack = new Stack<CellAddress>();
        var result = new List<List<CellAddress>>();
        var next = 0;

        List<CellAddress> Successors(CellAddress node) =>
            dependents.TryGetValue(node, out var set) ? [.. set.Where(nodes.Contains)] : [];

        foreach (var root in nodes)
        {
            if (index.ContainsKey(root))
            {
                continue;
            }

            var work = new Stack<(CellAddress Node, List<CellAddress> Successors, int Position)>();
            Visit(root);
            while (work.Count > 0)
            {
                var (node, successors, position) = work.Pop();
                if (position < successors.Count)
                {
                    work.Push((node, successors, position + 1));
                    var successor = successors[position];
                    if (!index.ContainsKey(successor))
                    {
                        Visit(successor);
                    }
                    else if (onStack.Contains(successor) && index.TryGetValue(successor, out var successorIndex))
                    {
                        low[node] = Math.Min(low[node], successorIndex);
                    }

                    continue;
                }

                if (work.Count > 0)
                {
                    var parent = work.Peek().Node;
                    low[parent] = Math.Min(low[parent], low[node]);
                }

                if (low[node] == index[node])
                {
                    var component = new List<CellAddress>();
                    CellAddress member;
                    do
                    {
                        member = stack.Pop();
                        onStack.Remove(member);
                        component.Add(member);
                    }
                    while (member != node);
                    result.Add(component);
                }
            }

            void Visit(CellAddress node)
            {
                index[node] = next;
                low[node] = next;
                next++;
                stack.Push(node);
                onStack.Add(node);
                work.Push((node, Successors(node), 0));
            }
        }

        result.Reverse();
        return result;
    }

    private sealed class Entry(CellInput input, CellValue value, HashSet<CellAddress> precedents)
    {
        public CellInput Input { get; } = input;

        public CellValue Value { get; set; } = value;

        public HashSet<CellAddress> Precedents { get; } = precedents;
    }
}
