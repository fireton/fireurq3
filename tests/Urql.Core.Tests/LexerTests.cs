using Urql.Core.Diagnostics;
using Urql.Core.Syntax;

namespace Urql.Core.Tests;

public sealed class LexerTests
{
    [Fact]
    public void Lex_ShouldRecognizeCoreKeywordsAndSymbols()
    {
        const string source = """
                              :start
                              if a<>1 then p "x" & btn next,Go else goto end
                              end
                              """;

        var result = Lexer.Lex(source);
        var kinds = result.Tokens.Select(x => x.Kind).ToList();

        Assert.Empty(result.Diagnostics);
        Assert.Contains(TokenKind.Colon, kinds);
        Assert.Contains(TokenKind.Identifier, kinds);
        Assert.Contains(TokenKind.KeywordIf, kinds);
        Assert.Contains(TokenKind.NotEquals, kinds);
        Assert.Contains(TokenKind.KeywordThen, kinds);
        Assert.Contains(TokenKind.KeywordP, kinds);
        Assert.Contains(TokenKind.Ampersand, kinds);
        Assert.Contains(TokenKind.KeywordBtn, kinds);
        Assert.Contains(TokenKind.Comma, kinds);
        Assert.Contains(TokenKind.KeywordElse, kinds);
        Assert.Contains(TokenKind.KeywordGoto, kinds);
        Assert.Contains(TokenKind.KeywordEnd, kinds);
        Assert.Equal(TokenKind.EndOfFile, result.Tokens[^1].Kind);
    }

    [Fact]
    public void Lex_ShouldSkipSingleLineAndBlockComments()
    {
        const string source = """
                              ; whole line comment
                              :start ; inline comment
                              /* block
                                 comment */
                              p "ok"
                              """;

        var result = Lexer.Lex(source);
        var texts = result.Tokens.Select(x => x.Text).ToList();

        Assert.Empty(result.Diagnostics);
        Assert.Contains(":start", string.Join(string.Empty, texts));
        Assert.Contains(texts, t => string.Equals(t, "p", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("\"ok\"", texts);
        Assert.DoesNotContain(texts, t => string.Equals(t, "comment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Lex_ShouldMergeLineContinuation_WhenUnderscoreStartsLine()
    {
        const string source = """
                              p "hello"
                                  _ & pln "world"
                              end
                              """;

        var result = Lexer.Lex(source);
        var kinds = result.Tokens.Select(x => x.Kind).ToList();

        Assert.Empty(result.Diagnostics);
        Assert.Contains(TokenKind.Ampersand, kinds);
        Assert.Contains(TokenKind.KeywordPln, kinds);
        Assert.Contains(TokenKind.KeywordEnd, kinds);
    }

    [Fact]
    public void Lex_ShouldParseStringEscapes()
    {
        const string source = "instr s=\"a\\n\\x41\\\"\"";
        var result = Lexer.Lex(source);

        var stringToken = Assert.Single(result.Tokens.Where(t => t.Kind == TokenKind.String));
        Assert.Empty(result.Diagnostics);
        Assert.Equal("a\nA\"", Assert.IsType<string>(stringToken.Value));
    }

    [Fact]
    public void Lex_ShouldParseNumericLiterals_IntFloatHex()
    {
        const string source = "a=1 b=2.5 c=0xFF";
        var result = Lexer.Lex(source);
        var numbers = result.Tokens.Where(t => t.Kind == TokenKind.Number).ToList();

        Assert.Empty(result.Diagnostics);
        Assert.Equal(3, numbers.Count);
        Assert.Equal(1d, Assert.IsType<double>(numbers[0].Value));
        Assert.Equal(2.5d, Assert.IsType<double>(numbers[1].Value));
        Assert.Equal(255d, Assert.IsType<double>(numbers[2].Value));
    }

    [Fact]
    public void Lex_ShouldReportUnexpectedCharacter()
    {
        const string source = "@";
        var result = Lexer.Lex(source);

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedCharacter, diag.Code);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
    }

    [Fact]
    public void Lex_ShouldReportUnterminatedString()
    {
        const string source = "p \"unterminated";
        var result = Lexer.Lex(source);

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.UnterminatedString);
    }

    [Fact]
    public void Lex_ShouldReportUnterminatedBlockComment()
    {
        const string source = "/* missing end";
        var result = Lexer.Lex(source);

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.UnterminatedBlockComment);
    }

    [Fact]
    public void Lex_ShouldReportInvalidEscapeSequence()
    {
        const string source = "p \"bad\\q\"";
        var result = Lexer.Lex(source);

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.InvalidEscapeSequence);
    }

    [Fact]
    public void Lex_ShouldReportInvalidHexNumber()
    {
        const string source = "x=0x";
        var result = Lexer.Lex(source);

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.InvalidNumberLiteral);
    }
}
