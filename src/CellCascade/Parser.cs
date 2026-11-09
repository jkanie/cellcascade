namespace CellCascade;

/// <summary>
/// Parses a formula with Excel's operator order, from loosest to tightest:
/// comparison (= &lt;&gt; &lt; &lt;= &gt; &gt;=), then &amp;, then + and -, then * and /, then ^,
/// then % (after a value), then a leading - or +, then : (a range).
/// Every binary operator groups from the left, so 2^3^2 is 64, as in Excel.
/// </summary>
public static class Parser
{
    /// <summary>The most cells that 1 range may cover. A larger range is refused, because each cell becomes a dependency.</summary>
    public const long MaxRangeCells = 100_000;

    /// <summary>The longest input that a cell takes, as in Excel.</summary>
    public const int MaxInputLength = 8_192;

    public static Expr Parse(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var state = new State(Lexer.Tokenize(formula));
        var expr = state.Comparison();
        if (state.Peek.Kind != TokenKind.End)
        {
            throw new FormulaException($"'{state.Peek.Text}' at position {state.Peek.Position + 1} was not expected.");
        }

        return expr;
    }

    private sealed class State(List<Token> tokens)
    {
        private int index;

        public Token Peek => tokens[index];

        public Expr Comparison()
        {
            var left = Concat();
            while (Peek.Kind == TokenKind.Operator && Peek.Text is "=" or "<>" or "<" or "<=" or ">" or ">=")
            {
                var op = Next().Text;
                left = new BinaryExpr(op, left, Concat());
            }

            return left;
        }

        private Token Next() => tokens[index++];

        private Expr Concat()
        {
            var left = Additive();
            while (Peek.Kind == TokenKind.Operator && Peek.Text == "&")
            {
                Next();
                left = new BinaryExpr("&", left, Additive());
            }

            return left;
        }

        private Expr Additive()
        {
            var left = Multiplicative();
            while (Peek.Kind == TokenKind.Operator && Peek.Text is "+" or "-")
            {
                var op = Next().Text;
                left = new BinaryExpr(op, left, Multiplicative());
            }

            return left;
        }

        private Expr Multiplicative()
        {
            var left = Power();
            while (Peek.Kind == TokenKind.Operator && Peek.Text is "*" or "/")
            {
                var op = Next().Text;
                left = new BinaryExpr(op, left, Power());
            }

            return left;
        }

        private Expr Power()
        {
            var left = Percent();
            while (Peek.Kind == TokenKind.Operator && Peek.Text == "^")
            {
                Next();
                left = new BinaryExpr("^", left, Percent());
            }

            return left;
        }

        private Expr Percent()
        {
            var operand = Unary();
            while (Peek.Kind == TokenKind.Operator && Peek.Text == "%")
            {
                Next();
                operand = new PercentExpr(operand);
            }

            return operand;
        }

        private Expr Unary()
        {
            if (Peek.Kind == TokenKind.Operator && Peek.Text is "-" or "+")
            {
                var op = Next().Text[0];
                return new UnaryExpr(op, Unary());
            }

            return Primary();
        }

        private Expr Primary()
        {
            var token = Next();
            switch (token.Kind)
            {
                case TokenKind.Number:
                    return new NumberExpr(token.Number);
                case TokenKind.Text:
                    return new TextExpr(token.Text);
                case TokenKind.Bool:
                    return new BoolExpr(token.Text == "TRUE");
                case TokenKind.Cell:
                    var from = CellAddress.Parse(token.Text);
                    if (Peek.Kind != TokenKind.Colon)
                    {
                        return new CellExpr(from);
                    }

                    Next();
                    var end = Next();
                    if (end.Kind != TokenKind.Cell)
                    {
                        throw new FormulaException($"A cell address must follow ':' at position {end.Position + 1}.");
                    }

                    return MakeRange(from, CellAddress.Parse(end.Text), token.Position);
                case TokenKind.Name:
                    if (Peek.Kind != TokenKind.LeftParen)
                    {
                        throw new FormulaException($"'{token.Text}' at position {token.Position + 1} is not a cell, a function call or TRUE or FALSE.");
                    }

                    Next();
                    var args = new List<Expr>();
                    if (Peek.Kind != TokenKind.RightParen)
                    {
                        args.Add(Comparison());
                        while (Peek.Kind == TokenKind.Comma)
                        {
                            Next();
                            args.Add(Comparison());
                        }
                    }

                    Expect(TokenKind.RightParen, ")");
                    Functions.CheckArity(token.Text, args.Count, token.Position);
                    return new CallExpr(token.Text, args);
                case TokenKind.LeftParen:
                    var inner = Comparison();
                    Expect(TokenKind.RightParen, ")");
                    return inner;
                case TokenKind.End:
                    throw new FormulaException("The formula ends too early.");
                default:
                    throw new FormulaException($"'{token.Text}' at position {token.Position + 1} was not expected.");
            }
        }

        private void Expect(TokenKind kind, string text)
        {
            var token = Next();
            if (token.Kind != kind)
            {
                var found = token.Kind == TokenKind.End ? "the end" : $"'{token.Text}'";
                throw new FormulaException($"'{text}' was expected at position {token.Position + 1}, but the formula has {found}.");
            }
        }

        private static RangeExpr MakeRange(CellAddress a, CellAddress b, int position)
        {
            var from = new CellAddress(Math.Min(a.Column, b.Column), Math.Min(a.Row, b.Row));
            var to = new CellAddress(Math.Max(a.Column, b.Column), Math.Max(a.Row, b.Row));
            var range = new RangeExpr(from, to);
            if (range.Count > MaxRangeCells)
            {
                throw new FormulaException($"The range at position {position + 1} covers {range.Count:N0} cells, and the limit is {MaxRangeCells:N0}.");
            }

            return range;
        }
    }
}
