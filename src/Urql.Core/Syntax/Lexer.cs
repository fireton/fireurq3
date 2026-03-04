using System.Globalization;
using System.Text;
using Urql.Core.Diagnostics;

namespace Urql.Core.Syntax;

public sealed class Lexer
{
    private readonly struct PhysicalLine(string text, int lineNumber)
    {
        public string Text { get; } = text;
        public int LineNumber { get; } = lineNumber;
    }

    private sealed class NormalizedSource
    {
        public required string Text { get; init; }
        public required IReadOnlyList<SourcePosition> PositionMap { get; init; }
        public required SourcePosition EndPosition { get; init; }
    }

    private static readonly Dictionary<string, TokenKind> KeywordMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["if"] = TokenKind.KeywordIf,
        ["then"] = TokenKind.KeywordThen,
        ["else"] = TokenKind.KeywordElse,
        ["goto"] = TokenKind.KeywordGoto,
        ["proc"] = TokenKind.KeywordProc,
        ["end"] = TokenKind.KeywordEnd,
        ["instr"] = TokenKind.KeywordInstr,
        ["p"] = TokenKind.KeywordP,
        ["print"] = TokenKind.KeywordPrint,
        ["pln"] = TokenKind.KeywordPln,
        ["println"] = TokenKind.KeywordPrintln,
        ["btn"] = TokenKind.KeywordBtn,
        ["and"] = TokenKind.KeywordAnd,
        ["or"] = TokenKind.KeywordOr,
        ["not"] = TokenKind.KeywordNot
    };

    private readonly string _sourceText;
    private readonly NormalizedSource _normalized;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly List<Token> _tokens = [];
    private int _index;

    private Lexer(string sourceText)
    {
        _sourceText = sourceText;
        _normalized = NormalizeSource(sourceText);
    }

    public static LexResult Lex(string sourceText)
    {
        var lexer = new Lexer(sourceText);
        lexer.Tokenize();
        return new LexResult(lexer._tokens, lexer._diagnostics.Items);
    }

    private char Current => _index >= _normalized.Text.Length ? '\0' : _normalized.Text[_index];
    private char Peek(int offset = 1) => _index + offset >= _normalized.Text.Length ? '\0' : _normalized.Text[_index + offset];

    private SourcePosition PositionAt(int index)
    {
        if (_normalized.PositionMap.Count == 0)
        {
            return new SourcePosition(1, 1);
        }

        if (index >= 0 && index < _normalized.PositionMap.Count)
        {
            return _normalized.PositionMap[index];
        }

        return _normalized.EndPosition;
    }

    private SourceSpan SpanFromIndices(int startIndex, int endExclusive)
    {
        var start = PositionAt(startIndex);
        var end = PositionAt(Math.Max(startIndex, endExclusive - 1));
        return new SourceSpan(start, end);
    }

    private void Tokenize()
    {
        while (_index < _normalized.Text.Length)
        {
            if (TrySkipWhitespaceAndComments())
            {
                continue;
            }

            var start = _index;
            var c = Current;

            switch (c)
            {
                case '\n':
                    _index++;
                    AddToken(TokenKind.NewLine, "\n", start, _index);
                    continue;
                case ':':
                    _index++;
                    AddToken(TokenKind.Colon, ":", start, _index);
                    continue;
                case ',':
                    _index++;
                    AddToken(TokenKind.Comma, ",", start, _index);
                    continue;
                case '&':
                    _index++;
                    AddToken(TokenKind.Ampersand, "&", start, _index);
                    continue;
                case '(':
                    _index++;
                    AddToken(TokenKind.OpenParen, "(", start, _index);
                    continue;
                case ')':
                    _index++;
                    AddToken(TokenKind.CloseParen, ")", start, _index);
                    continue;
                case '+':
                    _index++;
                    AddToken(TokenKind.Plus, "+", start, _index);
                    continue;
                case '-':
                    _index++;
                    AddToken(TokenKind.Minus, "-", start, _index);
                    continue;
                case '*':
                    _index++;
                    AddToken(TokenKind.Star, "*", start, _index);
                    continue;
                case '/':
                    _index++;
                    AddToken(TokenKind.Slash, "/", start, _index);
                    continue;
                case '%':
                    _index++;
                    AddToken(TokenKind.Percent, "%", start, _index);
                    continue;
                case '#':
                    _index++;
                    AddToken(TokenKind.Hash, "#", start, _index);
                    continue;
                case '$':
                    _index++;
                    AddToken(TokenKind.Dollar, "$", start, _index);
                    continue;
                case '?':
                    _index++;
                    AddToken(TokenKind.Question, "?", start, _index);
                    continue;
                case '=':
                    if (Peek() == '=')
                    {
                        _index += 2;
                        AddToken(TokenKind.DoubleEquals, "==", start, _index);
                    }
                    else
                    {
                        _index++;
                        AddToken(TokenKind.Equals, "=", start, _index);
                    }
                    continue;
                case '<':
                    if (Peek() == '=')
                    {
                        _index += 2;
                        AddToken(TokenKind.LessOrEquals, "<=", start, _index);
                    }
                    else if (Peek() == '>')
                    {
                        _index += 2;
                        AddToken(TokenKind.NotEquals, "<>", start, _index);
                    }
                    else
                    {
                        _index++;
                        AddToken(TokenKind.Less, "<", start, _index);
                    }
                    continue;
                case '>':
                    if (Peek() == '=')
                    {
                        _index += 2;
                        AddToken(TokenKind.GreaterOrEquals, ">=", start, _index);
                    }
                    else
                    {
                        _index++;
                        AddToken(TokenKind.Greater, ">", start, _index);
                    }
                    continue;
                case '"':
                    ReadStringToken();
                    continue;
                default:
                    if (IsIdentifierStart(c))
                    {
                        ReadIdentifierToken();
                        continue;
                    }

                    if (char.IsDigit(c))
                    {
                        ReadNumberToken();
                        continue;
                    }

                    _index++;
                    _diagnostics.ReportError(
                        DiagnosticCode.UnexpectedCharacter,
                        $"Unexpected character '{c}'.",
                        SpanFromIndices(start, _index));
                    break;
            }
        }

        AddToken(TokenKind.EndOfFile, string.Empty, _normalized.Text.Length, _normalized.Text.Length);
    }

    private bool TrySkipWhitespaceAndComments()
    {
        var progressed = false;

        while (_index < _normalized.Text.Length)
        {
            var c = Current;

            if (c is ' ' or '\t' or '\r' or '\f')
            {
                _index++;
                progressed = true;
                continue;
            }

            if (c == ';')
            {
                progressed = true;
                while (_index < _normalized.Text.Length && Current != '\n')
                {
                    _index++;
                }

                continue;
            }

            if (c == '/' && Peek() == '*')
            {
                progressed = true;
                var start = _index;
                _index += 2;

                while (_index < _normalized.Text.Length && !(Current == '*' && Peek() == '/'))
                {
                    _index++;
                }

                if (_index >= _normalized.Text.Length)
                {
                    _diagnostics.ReportError(
                        DiagnosticCode.UnterminatedBlockComment,
                        "Unterminated block comment.",
                        SpanFromIndices(start, _normalized.Text.Length));
                    return true;
                }

                _index += 2;
                continue;
            }

            break;
        }

        return progressed;
    }

    private void ReadIdentifierToken()
    {
        var start = _index;
        _index++;

        while (IsIdentifierPart(Current))
        {
            _index++;
        }

        var text = _normalized.Text[start.._index];
        var kind = KeywordMap.GetValueOrDefault(text, TokenKind.Identifier);
        AddToken(kind, text, start, _index);
    }

    private void ReadNumberToken()
    {
        var start = _index;
        var hasDot = false;

        if (Current == '0' && (Peek() is 'x' or 'X'))
        {
            _index += 2;
            while (IsHexDigit(Current))
            {
                _index++;
            }

            var hexText = _normalized.Text[start.._index];
            if (!long.TryParse(hexText.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue))
            {
                _diagnostics.ReportError(
                    DiagnosticCode.InvalidNumberLiteral,
                    $"Invalid number literal '{hexText}'.",
                    SpanFromIndices(start, _index));
                AddToken(TokenKind.Number, hexText, start, _index, 0d);
                return;
            }

            AddToken(TokenKind.Number, hexText, start, _index, (double)hexValue);
            return;
        }

        while (char.IsDigit(Current) || Current == '.')
        {
            if (Current == '.')
            {
                if (hasDot)
                {
                    break;
                }

                hasDot = true;
            }

            _index++;
        }

        var text = _normalized.Text[start.._index];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            _diagnostics.ReportError(
                DiagnosticCode.InvalidNumberLiteral,
                $"Invalid number literal '{text}'.",
                SpanFromIndices(start, _index));
            value = 0d;
        }

        AddToken(TokenKind.Number, text, start, _index, value);
    }

    private void ReadStringToken()
    {
        var start = _index;
        _index++; // opening quote
        var builder = new StringBuilder();
        var terminated = false;

        while (_index < _normalized.Text.Length)
        {
            var c = Current;
            if (c == '"')
            {
                _index++;
                terminated = true;
                break;
            }

            if (c == '\\')
            {
                var escapeStart = _index;
                _index++;
                var escaped = Current;
                if (escaped == '\0')
                {
                    break;
                }

                switch (escaped)
                {
                    case '\\':
                        builder.Append('\\');
                        _index++;
                        break;
                    case '"':
                        builder.Append('"');
                        _index++;
                        break;
                    case 'n':
                        builder.Append('\n');
                        _index++;
                        break;
                    case 'r':
                        builder.Append('\r');
                        _index++;
                        break;
                    case 't':
                        builder.Append('\t');
                        _index++;
                        break;
                    case 'x':
                        _index++;
                        var h1 = Current;
                        _index++;
                        var h2 = Current;
                        if (!IsHexDigit(h1) || !IsHexDigit(h2))
                        {
                            _diagnostics.ReportError(
                                DiagnosticCode.InvalidEscapeSequence,
                                "Invalid hex escape sequence. Expected \\xNN.",
                                SpanFromIndices(escapeStart, _index + 1));
                            break;
                        }

                        _index++;
                        var hex = new string([h1, h2]);
                        builder.Append((char)Convert.ToInt32(hex, 16));
                        break;
                    default:
                        _diagnostics.ReportError(
                            DiagnosticCode.InvalidEscapeSequence,
                            $"Invalid escape sequence '\\{escaped}'.",
                            SpanFromIndices(escapeStart, _index + 1));
                        builder.Append(escaped);
                        _index++;
                        break;
                }

                continue;
            }

            if (c == '\n')
            {
                break;
            }

            builder.Append(c);
            _index++;
        }

        if (!terminated)
        {
            _diagnostics.ReportError(
                DiagnosticCode.UnterminatedString,
                "Unterminated string literal.",
                SpanFromIndices(start, _index));
        }

        var text = _normalized.Text[start..Math.Min(_index, _normalized.Text.Length)];
        AddToken(TokenKind.String, text, start, _index, builder.ToString());
    }

    private void AddToken(TokenKind kind, string text, int start, int endExclusive, object? value = null)
    {
        _tokens.Add(new Token(kind, text, SpanFromIndices(start, endExclusive), value));
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') ||
        (c >= 'a' && c <= 'f') ||
        (c >= 'A' && c <= 'F');

    private static NormalizedSource NormalizeSource(string sourceText)
    {
        var lines = SplitPhysicalLines(sourceText);
        var output = new StringBuilder();
        var positions = new List<SourcePosition>(sourceText.Length + 16);
        var hasPreviousLogicalLine = false;

        foreach (var line in lines)
        {
            var firstNonWhitespace = FindFirstNonWhitespace(line.Text);
            var isContinuation = hasPreviousLogicalLine &&
                                 firstNonWhitespace >= 0 &&
                                 line.Text[firstNonWhitespace] == '_';

            if (isContinuation)
            {
                output.Append(' ');
                positions.Add(new SourcePosition(line.LineNumber, firstNonWhitespace + 1));

                var offset = firstNonWhitespace + 1;
                for (var i = offset; i < line.Text.Length; i++)
                {
                    output.Append(line.Text[i]);
                    positions.Add(new SourcePosition(line.LineNumber, i + 1));
                }
            }
            else
            {
                if (hasPreviousLogicalLine)
                {
                    output.Append('\n');
                    positions.Add(new SourcePosition(line.LineNumber, 1));
                }

                for (var i = 0; i < line.Text.Length; i++)
                {
                    output.Append(line.Text[i]);
                    positions.Add(new SourcePosition(line.LineNumber, i + 1));
                }
            }

            hasPreviousLogicalLine = true;
        }

        var endPosition = positions.Count == 0
            ? new SourcePosition(1, 1)
            : new SourcePosition(positions[^1].Line, positions[^1].Column + 1);

        return new NormalizedSource
        {
            Text = output.ToString(),
            PositionMap = positions,
            EndPosition = endPosition
        };
    }

    private static int FindFirstNonWhitespace(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not (' ' or '\t'))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<PhysicalLine> SplitPhysicalLines(string text)
    {
        var lines = new List<PhysicalLine>();
        var start = 0;
        var line = 1;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            var length = i - start;
            if (length > 0 && text[i - 1] == '\r')
            {
                length--;
            }

            lines.Add(new PhysicalLine(text.Substring(start, length), line));
            line++;
            start = i + 1;
        }

        if (start <= text.Length)
        {
            lines.Add(new PhysicalLine(text[start..], line));
        }

        return lines;
    }
}
