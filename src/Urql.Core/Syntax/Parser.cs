using Urql.Core.Diagnostics;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Syntax;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly ParserOptions _options;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly Stack<HashSet<TokenKind>> _statementTerminators = new();
    private int _index;

    private Parser(IReadOnlyList<Token> tokens, IReadOnlyList<Diagnostic> lexerDiagnostics, ParserOptions options)
    {
        _tokens = tokens;
        _options = options;
        _diagnostics.AddRange(lexerDiagnostics);
    }

    public static ParseResult Parse(string source)
    {
        return Parse(source, new ParserOptions());
    }

    public static ParseResult Parse(string source, ParserOptions options)
    {
        var lex = Lexer.Lex(source);
        var parser = new Parser(lex.Tokens, lex.Diagnostics, options);
        var program = parser.ParseProgram();
        return new ParseResult(program, parser._diagnostics.Items);
    }

    public static ExpressionParseResult ParseExpressionText(string expressionText)
    {
        var lex = Lexer.Lex(expressionText);
        var parser = new Parser(lex.Tokens, lex.Diagnostics, new ParserOptions());
        var expression = parser.ParseExpression();

        while (parser.Current.Kind == TokenKind.NewLine)
        {
            parser.NextToken();
        }

        if (parser.Current.Kind != TokenKind.EndOfFile)
        {
            parser._diagnostics.ReportError(
                ParseDiagnosticCode.UnexpectedToken,
                $"Unexpected trailing token '{parser.Current.Text}' in expression.",
                parser.Current.Span);
        }

        return new ExpressionParseResult(expression, parser._diagnostics.Items);
    }

    private Token Current => Peek(0);

    private Token Peek(int offset)
    {
        var idx = _index + offset;
        if (idx < 0)
        {
            idx = 0;
        }

        return idx >= _tokens.Count ? _tokens[^1] : _tokens[idx];
    }

    private Token NextToken()
    {
        var current = Current;
        if (_index < _tokens.Count - 1)
        {
            _index++;
        }

        return current;
    }

    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        NextToken();
        return true;
    }

    private ProgramSyntax ParseProgram()
    {
        var lines = new List<StatementLineSyntax>();

        while (Current.Kind != TokenKind.EndOfFile)
        {
            while (Match(TokenKind.NewLine))
            {
            }

            if (Current.Kind == TokenKind.EndOfFile)
            {
                break;
            }

            lines.Add(ParseLine());
        }

        var span = lines.Count == 0
            ? new SourceSpan(new SourcePosition(1, 1), new SourcePosition(1, 1))
            : new SourceSpan(lines[0].Span.Start, lines[^1].Span.End);
        return new ProgramSyntax(lines, span);
    }

    private StatementLineSyntax ParseLine()
    {
        var lineStart = Current.Span.Start;
        LabelSyntax? label = null;
        var labelConsumesLine = false;

        if (Match(TokenKind.Colon))
        {
            label = ParseLabelDeclaration(lineStart, out labelConsumesLine);
        }

        var statements = new List<StatementSyntax>();
        if (!labelConsumesLine && Current.Kind is not (TokenKind.NewLine or TokenKind.EndOfFile))
        {
            statements.Add(ParseStatement());

            while (Match(TokenKind.Ampersand))
            {
                if (Current.Kind is TokenKind.NewLine or TokenKind.EndOfFile)
                {
                    _diagnostics.ReportError(
                        ParseDiagnosticCode.ExpectedToken,
                        "Expected statement after '&'.",
                        PreviousTokenSpan());
                    break;
                }

                statements.Add(ParseStatement());
            }
        }

        if (Current.Kind is not (TokenKind.NewLine or TokenKind.EndOfFile))
        {
            _diagnostics.ReportError(
                ParseDiagnosticCode.UnexpectedToken,
                $"Unexpected token '{Current.Text}' at end of line.",
                Current.Span);
            SyncToLineEnd();
        }

        Match(TokenKind.NewLine);

        var lineEnd = statements.Count > 0
            ? statements[^1].Span.End
            : label?.Span.End ?? lineStart;
        return new StatementLineSyntax(label, statements, new SourceSpan(lineStart, lineEnd));
    }

    private LabelSyntax? ParseLabelDeclaration(SourcePosition lineStart, out bool consumesLine)
    {
        consumesLine = false;
        if (Current.Kind is TokenKind.NewLine or TokenKind.EndOfFile)
        {
            _diagnostics.ReportError(
                ParseDiagnosticCode.ExpectedToken,
                "Expected label identifier after ':'.",
                new SourceSpan(lineStart, lineStart));
            return null;
        }

        var parts = new List<string>();
        var start = Current.Span.Start;
        SourcePosition end = start;
        while (Current.Kind is not (TokenKind.NewLine or TokenKind.EndOfFile))
        {
            var token = NextToken();
            parts.Add(token.Text);
            end = token.Span.End;
        }

        var name = ReconstructRawText(parts).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _diagnostics.ReportError(
                ParseDiagnosticCode.ExpectedToken,
                "Expected label identifier after ':'.",
                new SourceSpan(start, end));
            return null;
        }

        consumesLine = true;
        return new LabelSyntax(name, new SourceSpan(start, end));
    }

    private StatementSyntax ParseStatement()
    {
        var start = Current.Span.Start;
        if (Current.Kind == TokenKind.Identifier && Peek(1).Kind != TokenKind.Equals && HasEqualsBeforeTerminator())
        {
            return ParseAssignmentStatement();
        }

        return Current.Kind switch
        {
            TokenKind.KeywordInstr => ParseInstrStatement(),
            TokenKind.KeywordIf => ParseIfStatement(),
            TokenKind.KeywordGoto => ParseGotoStatement(),
            TokenKind.KeywordProc => ParseProcStatement(),
            TokenKind.KeywordEnd => ParseEndStatement(),
            TokenKind.KeywordP or TokenKind.KeywordPrint => ParsePrintStatement(false),
            TokenKind.KeywordPln or TokenKind.KeywordPrintln => ParsePrintStatement(true),
            TokenKind.KeywordBtn => ParseBtnStatement(),
            TokenKind.Identifier when IsWord(Current, "perkill") => ParsePerkillStatement(),
            TokenKind.Identifier when IsWord(Current, "invkill") => ParseInvkillStatement(),
            TokenKind.Identifier when IsWord(Current, "inv") && Peek(1).Kind == TokenKind.Plus => ParseInvDeltaStatement(true),
            TokenKind.Identifier when IsWord(Current, "inv") && Peek(1).Kind == TokenKind.Minus => ParseInvDeltaStatement(false),
            TokenKind.Identifier when Peek(1).Kind == TokenKind.Equals => ParseAssignmentStatement(),
            _ => ParseUnknownOrInvalid(start)
        };
    }

    private StatementSyntax ParseAssignmentStatement()
    {
        var nameStart = Current.Span.Start;
        var name = ParseVariableNameUntilEquals("Expected '=' in assignment.");
        var expr = ParseExpression();
        return new AssignmentStatementSyntax(name, expr, new SourceSpan(nameStart, expr.Span.End));
    }

    private StatementSyntax ParseInstrStatement()
    {
        var start = NextToken();
        var name = ParseVariableNameUntilEquals("Expected '=' in instr assignment.");
        var expr = ParseExpression();
        return new InstrStatementSyntax(name, expr, new SourceSpan(start.Span.Start, expr.Span.End));
    }

    private StatementSyntax ParseGotoStatement()
    {
        var start = NextToken();
        var expr = ParseExpression();
        return new GotoStatementSyntax(expr, new SourceSpan(start.Span.Start, expr.Span.End));
    }

    private StatementSyntax ParseProcStatement()
    {
        var start = NextToken();
        var expr = ParseExpression();
        return new ProcStatementSyntax(expr, new SourceSpan(start.Span.Start, expr.Span.End));
    }

    private StatementSyntax ParseEndStatement()
    {
        var t = NextToken();
        return new EndStatementSyntax(t.Span);
    }

    private StatementSyntax ParsePrintStatement(bool appendNewline)
    {
        var start = NextToken();
        var expr = ParseRawTextExpression();
        return new PrintStatementSyntax(expr, appendNewline, new SourceSpan(start.Span.Start, expr.Span.End));
    }

    private StatementSyntax ParseBtnStatement()
    {
        var start = NextToken();
        var target = ParseRawTextExpression(TokenKind.Comma);
        _ = Expect(TokenKind.Comma, "Expected ',' after btn target.");
        var caption = ParseRawTextExpression();
        return new BtnStatementSyntax(target, caption, new SourceSpan(start.Span.Start, caption.Span.End));
    }

    private StatementSyntax ParsePerkillStatement()
    {
        var token = NextToken();
        return new PerkillStatementSyntax(token.Span);
    }

    private StatementSyntax ParseInvkillStatement()
    {
        var start = NextToken();
        if (Current.Kind is TokenKind.NewLine or TokenKind.EndOfFile or TokenKind.Ampersand)
        {
            return new InvkillStatementSyntax(null, start.Span);
        }

        var item = ParseRawTextExpression();
        return new InvkillStatementSyntax(item, new SourceSpan(start.Span.Start, item.Span.End));
    }

    private StatementSyntax ParseInvDeltaStatement(bool add)
    {
        var invToken = NextToken();
        var opToken = NextToken();
        if (Current.Kind is TokenKind.NewLine or TokenKind.EndOfFile or TokenKind.Ampersand)
        {
            _diagnostics.ReportError(
                ParseDiagnosticCode.ExpectedToken,
                "Expected inventory item after inv+ or inv-.",
                opToken.Span);
            return new InvalidStatementSyntax("Invalid inventory command.", new SourceSpan(invToken.Span.Start, opToken.Span.End));
        }

        if (TryReadCountAndComma(out var countExpression))
        {
            var itemAfterComma = ParseRawTextExpression();
            if (itemAfterComma is RawTextExpressionSyntax rawAfterComma &&
                string.IsNullOrWhiteSpace(rawAfterComma.RawText))
            {
                _diagnostics.ReportError(
                    ParseDiagnosticCode.ExpectedToken,
                    "Expected inventory item after comma in inv+ or inv-.",
                    opToken.Span);
                return new InvalidStatementSyntax(
                    "Invalid inventory command.",
                    new SourceSpan(invToken.Span.Start, itemAfterComma.Span.End));
            }

            return add
                ? new InvAddStatementSyntax(countExpression, itemAfterComma, new SourceSpan(invToken.Span.Start, itemAfterComma.Span.End))
                : new InvRemoveStatementSyntax(countExpression, itemAfterComma, new SourceSpan(invToken.Span.Start, itemAfterComma.Span.End));
        }

        var itemOnly = ParseRawTextExpression();
        return add
            ? new InvAddStatementSyntax(null, itemOnly, new SourceSpan(invToken.Span.Start, itemOnly.Span.End))
            : new InvRemoveStatementSyntax(null, itemOnly, new SourceSpan(invToken.Span.Start, itemOnly.Span.End));
    }

    private StatementSyntax ParseIfStatement()
    {
        var ifToken = NextToken();
        var condition = ParseExpression();
        _ = Expect(TokenKind.KeywordThen, "Expected 'then' in if statement.");

        var thenStatements = ParseInlineChainUntil(TokenKind.KeywordElse, TokenKind.NewLine, TokenKind.EndOfFile);
        IReadOnlyList<StatementSyntax>? elseStatements = null;

        if (Match(TokenKind.KeywordElse))
        {
            elseStatements = ParseInlineChainUntil(TokenKind.NewLine, TokenKind.EndOfFile);
        }

        var end = elseStatements is { Count: > 0 }
            ? elseStatements[^1].Span.End
            : thenStatements.Count > 0
                ? thenStatements[^1].Span.End
                : condition.Span.End;

        return new IfStatementSyntax(condition, thenStatements, elseStatements, new SourceSpan(ifToken.Span.Start, end));
    }

    private List<StatementSyntax> ParseInlineChainUntil(params TokenKind[] terminators)
    {
        _statementTerminators.Push(new HashSet<TokenKind>(terminators));
        try
        {
            var result = new List<StatementSyntax>();
            if (terminators.Contains(Current.Kind))
            {
                return result;
            }

        result.Add(ParseStatement());
        while (Match(TokenKind.Ampersand))
        {
            if (terminators.Contains(Current.Kind))
            {
                _diagnostics.ReportError(
                    ParseDiagnosticCode.ExpectedToken,
                    "Expected statement after '&'.",
                    PreviousTokenSpan());
                break;
            }

            result.Add(ParseStatement());
        }

            return result;
        }
        finally
        {
            _statementTerminators.Pop();
        }
    }

    private StatementSyntax ParseUnknownOrInvalid(SourcePosition start)
    {
        if (_options.AllowUnknownCommands && IsCommandLike(Current.Kind))
        {
            return ParseUnknownCommandStatement();
        }

        return ParseInvalidStatement(start);
    }

    private StatementSyntax ParseInvalidStatement(SourcePosition start)
    {
        var token = NextToken();
        _diagnostics.ReportError(
            ParseDiagnosticCode.InvalidStatement,
            $"Invalid statement start token '{token.Text}'.",
            token.Span);

        return new InvalidStatementSyntax(
            "Invalid statement.",
            new SourceSpan(start, token.Span.End));
    }

    private StatementSyntax ParseUnknownCommandStatement()
    {
        var command = NextToken();
        var args = ParseRawTextExpression();
        _diagnostics.Report(
            ParseDiagnosticCode.UnknownCommand,
            DiagnosticSeverity.Warning,
            $"Unknown command '{command.Text}' treated as no-op in compatibility mode.",
            command.Span);

        return new UnknownCommandStatementSyntax(
            command.Text,
            args,
            new SourceSpan(command.Span.Start, args.Span.End));
    }

    private Token Expect(TokenKind kind, string message)
    {
        if (Current.Kind == kind)
        {
            return NextToken();
        }

        _diagnostics.ReportError(ParseDiagnosticCode.ExpectedToken, message, Current.Span);
        return new Token(kind, string.Empty, Current.Span);
    }

    private SourceSpan PreviousTokenSpan()
    {
        var idx = Math.Max(0, _index - 1);
        return _tokens[idx].Span;
    }

    private void SyncToLineEnd()
    {
        while (Current.Kind is not (TokenKind.NewLine or TokenKind.EndOfFile))
        {
            NextToken();
        }
    }

    private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        ExpressionSyntax left;

        if (unaryPrecedence > 0 && unaryPrecedence >= parentPrecedence)
        {
            var op = NextToken();
            var operand = ParseExpression(unaryPrecedence);
            left = new UnaryExpressionSyntax(
                op.Kind,
                operand,
                new SourceSpan(op.Span.Start, operand.Span.End));
        }
        else
        {
            left = ParsePrimary();
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence)
            {
                break;
            }

            var op = NextToken();
            var right = ParseExpression(precedence);
            left = new BinaryExpressionSyntax(
                left,
                op.Kind,
                right,
                new SourceSpan(left.Span.Start, right.Span.End));
        }

        return left;
    }

    private ExpressionSyntax ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                NextToken();
                return new NumberLiteralExpressionSyntax(
                    token.Value is double d ? d : 0d,
                    token.Text,
                    token.Span);

            case TokenKind.String:
                NextToken();
                return new StringLiteralExpressionSyntax(
                    token.Value as string ?? string.Empty,
                    token.Text,
                    token.Span);

            case TokenKind.Identifier:
                return ParseIdentifierLikeExpression();

            case TokenKind.OpenParen:
            {
                var open = NextToken();
                var inner = ParseExpression();
                var close = Expect(TokenKind.CloseParen, "Expected ')' to close expression.");
                return new ParenthesizedExpressionSyntax(inner, new SourceSpan(open.Span.Start, close.Span.End));
            }

            default:
                if (IsIdentifierLikeInExpression(token.Kind))
                {
                    return ParseIdentifierLikeExpression();
                }

                _diagnostics.ReportError(
                    ParseDiagnosticCode.UnexpectedToken,
                    $"Expected expression, found '{token.Text}'.",
                    token.Span);
                NextToken();
                return new IdentifierExpressionSyntax(string.Empty, token.Span);
        }
    }

    private static int GetUnaryPrecedence(TokenKind kind) => kind switch
    {
        TokenKind.KeywordNot => 7,
        TokenKind.Plus => 7,
        TokenKind.Minus => 7,
        _ => 0
    };

    private static int GetBinaryPrecedence(TokenKind kind) => kind switch
    {
        TokenKind.Star or TokenKind.Slash or TokenKind.Percent => 6,
        TokenKind.Plus or TokenKind.Minus => 5,
        TokenKind.Equals or TokenKind.NotEquals or TokenKind.DoubleEquals or
        TokenKind.Less or TokenKind.Greater or TokenKind.LessOrEquals or TokenKind.GreaterOrEquals => 4,
        TokenKind.KeywordAnd => 3,
        TokenKind.KeywordOr => 2,
        _ => 0
    };

    private static bool IsIdentifierLikeInExpression(TokenKind kind)
    {
        return kind == TokenKind.Identifier;
    }

    private bool IsCommandLike(TokenKind kind)
    {
        return kind == TokenKind.Identifier || (kind >= TokenKind.KeywordIf && kind <= TokenKind.KeywordBtn);
    }

    private bool HasEqualsBeforeTerminator()
    {
        for (var i = _index; i < _tokens.Count; i++)
        {
            var kind = _tokens[i].Kind;
            if (kind is TokenKind.NewLine or TokenKind.EndOfFile or TokenKind.Ampersand)
            {
                return false;
            }

            if (_statementTerminators.Count > 0 && _statementTerminators.Peek().Contains(kind))
            {
                return false;
            }

            if (kind == TokenKind.Equals)
            {
                return true;
            }
        }

        return false;
    }

    private string ParseVariableNameUntilEquals(string expectedEqualsMessage)
    {
        var parts = new List<string>();
        while (Current.Kind is not (TokenKind.Equals or TokenKind.NewLine or TokenKind.EndOfFile or TokenKind.Ampersand))
        {
            if (_statementTerminators.Count > 0 && _statementTerminators.Peek().Contains(Current.Kind))
            {
                break;
            }

            parts.Add(NextToken().Text);
        }

        _ = Expect(TokenKind.Equals, expectedEqualsMessage);
        var name = ReconstructRawText(parts).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _diagnostics.ReportError(
                ParseDiagnosticCode.ExpectedToken,
                "Expected variable name in assignment.",
                PreviousTokenSpan());
            return string.Empty;
        }

        return name;
    }

    private ExpressionSyntax ParseIdentifierLikeExpression()
    {
        var start = Current.Span.Start;
        var parts = new List<string>();
        SourcePosition end = start;

        while (IsIdentifierLikeInExpression(Current.Kind))
        {
            var token = NextToken();
            parts.Add(token.Text);
            end = token.Span.End;
        }

        var name = ReconstructRawText(parts).Trim();
        return new IdentifierExpressionSyntax(name, new SourceSpan(start, end));
    }

    private bool TryReadCountAndComma(out ExpressionSyntax countExpression)
    {
        var saveIndex = _index;
        var saveDiagnosticsCount = _diagnostics.Items.Count;
        countExpression = ParseExpression();
        if (Current.Kind == TokenKind.Comma)
        {
            NextToken();
            return true;
        }

        _index = saveIndex;
        _diagnostics.TrimToCount(saveDiagnosticsCount);
        countExpression = null!;
        return false;
    }

    private static bool IsWord(Token token, string value)
    {
        return token.Kind == TokenKind.Identifier &&
               string.Equals(token.Text, value, StringComparison.OrdinalIgnoreCase);
    }

    private ExpressionSyntax ParseRawTextExpression(params TokenKind[] additionalTerminators)
    {
        var start = Current.Span.Start;
        var parts = new List<string>();
        var sawAny = false;
        SourcePosition end = start;

        while (Current.Kind != TokenKind.EndOfFile)
        {
            if (Current.Kind == TokenKind.NewLine || Current.Kind == TokenKind.Ampersand)
            {
                break;
            }

            if (additionalTerminators.Contains(Current.Kind))
            {
                break;
            }

            if (_statementTerminators.Count > 0 && _statementTerminators.Peek().Contains(Current.Kind))
            {
                break;
            }

            sawAny = true;
            var token = NextToken();
            end = token.Span.End;
            parts.Add(token.Text);
        }

        var raw = ReconstructRawText(parts);
        if (!sawAny)
        {
            return new RawTextExpressionSyntax(string.Empty, new SourceSpan(start, start));
        }

        return new RawTextExpressionSyntax(raw, new SourceSpan(start, end));
    }

    private static string ReconstructRawText(IReadOnlyList<string> parts)
    {
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                if (!NoSpaceBefore(parts[i]) && !NoSpaceAfter(parts[i - 1]))
                {
                    sb.Append(' ');
                }
            }

            var part = parts[i];
            if (part.Length >= 2 && part[0] == '"' && part[^1] == '"')
            {
                sb.Append(part[1..^1]);
            }
            else
            {
                sb.Append(part);
            }
        }

        return sb.ToString();
    }

    private static bool NoSpaceBefore(string token)
    {
        return token is "," or "." or ":" or ";" or ")" or "]" or "?" or "!" or "$";
    }

    private static bool NoSpaceAfter(string token)
    {
        return token is "(" or "[" or "#" or "%";
    }
}
