using System.Globalization;

namespace CellCascade;

internal enum TokenKind
{
    Number,
    Text,
    Bool,
    Cell,
    Name,
    Operator,
    LeftParen,
    RightParen,
    Comma,
    Colon,
    End,
}

internal readonly record struct Token(TokenKind Kind, string Text, int Position, double Number = 0);

/// <summary>Splits a formula (the part after the leading =) into tokens.</summary>
internal static class Lexer
{
    public static List<Token> Tokenize(ReadOnlySpan<char> source)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            var start = i;
            if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < source.Length && char.IsAsciiDigit(source[i + 1])))
            {
                i = ReadNumber(source, i);
                var text = source[start..i].ToString();
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                {
                    throw new FormulaException($"'{text}' at position {start + 1} is not a number.");
                }

                tokens.Add(new Token(TokenKind.Number, text, start, number));
                continue;
            }

            if (c == '"')
            {
                tokens.Add(ReadText(source, ref i));
                continue;
            }

            if (char.IsAsciiLetter(c) || c == '$' || c == '_')
            {
                while (i < source.Length && (char.IsAsciiLetterOrDigit(source[i]) || source[i] is '$' or '_' or '.'))
                {
                    i++;
                }

                var word = source[start..i].ToString();

                // LOG10 is also a cell address, so a word right before "(" is read as a function name, as Excel does.
                var calls = i < source.Length && source[i] == '(';
                if (!calls && CellAddress.TryParse(word, out _))
                {
                    tokens.Add(new Token(TokenKind.Cell, word, start));
                }
                else if (word.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || word.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Add(new Token(TokenKind.Bool, word.ToUpperInvariant(), start));
                }
                else if (word.Contains('$', StringComparison.Ordinal))
                {
                    throw new FormulaException($"'{word}' at position {start + 1} is not a cell address.");
                }
                else
                {
                    tokens.Add(new Token(TokenKind.Name, word.ToUpperInvariant(), start));
                }

                continue;
            }

            switch (c)
            {
                case '(':
                    tokens.Add(new Token(TokenKind.LeftParen, "(", start));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.RightParen, ")", start));
                    i++;
                    continue;
                case ',':
                    tokens.Add(new Token(TokenKind.Comma, ",", start));
                    i++;
                    continue;
                case ':':
                    tokens.Add(new Token(TokenKind.Colon, ":", start));
                    i++;
                    continue;
                case '+' or '-' or '*' or '/' or '^' or '&' or '%' or '=':
                    tokens.Add(new Token(TokenKind.Operator, c.ToString(), start));
                    i++;
                    continue;
                case '<':
                    if (i + 1 < source.Length && source[i + 1] is '=' or '>')
                    {
                        tokens.Add(new Token(TokenKind.Operator, source.Slice(i, 2).ToString(), start));
                        i += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Operator, "<", start));
                        i++;
                    }

                    continue;
                case '>':
                    if (i + 1 < source.Length && source[i + 1] == '=')
                    {
                        tokens.Add(new Token(TokenKind.Operator, ">=", start));
                        i += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Operator, ">", start));
                        i++;
                    }

                    continue;
                default:
                    throw new FormulaException($"'{c}' at position {start + 1} is not allowed in a formula.");
            }
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, source.Length));
        return tokens;
    }

    private static int ReadNumber(ReadOnlySpan<char> source, int i)
    {
        while (i < source.Length && char.IsAsciiDigit(source[i]))
        {
            i++;
        }

        if (i < source.Length && source[i] == '.')
        {
            i++;
            while (i < source.Length && char.IsAsciiDigit(source[i]))
            {
                i++;
            }
        }

        if (i < source.Length && source[i] is 'e' or 'E')
        {
            var j = i + 1;
            if (j < source.Length && source[j] is '+' or '-')
            {
                j++;
            }

            if (j < source.Length && char.IsAsciiDigit(source[j]))
            {
                i = j;
                while (i < source.Length && char.IsAsciiDigit(source[i]))
                {
                    i++;
                }
            }
        }

        return i;
    }

    private static Token ReadText(ReadOnlySpan<char> source, ref int i)
    {
        var start = i;
        i++;
        var text = new System.Text.StringBuilder();
        while (i < source.Length)
        {
            if (source[i] == '"')
            {
                if (i + 1 < source.Length && source[i + 1] == '"')
                {
                    text.Append('"');
                    i += 2;
                    continue;
                }

                i++;
                return new Token(TokenKind.Text, text.ToString(), start);
            }

            text.Append(source[i]);
            i++;
        }

        throw new FormulaException($"The text that starts at position {start + 1} has no closing quote.");
    }
}
