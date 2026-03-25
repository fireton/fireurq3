using Urql.Core.Syntax;
using Urql.Core.Syntax.Ast;
using Urql.Core.Diagnostics;

namespace Urql.Core.Tests;

public sealed class ParserTests
{
    [Fact]
    public void Parse_ShouldBuildLabelAndStatementChain()
    {
        const string source = """
                              :start
                              a=1 & instr s="x" & p "ok" & pln "done"
                              end
                              """;

        var parse = Parser.Parse(source);

        Assert.DoesNotContain(parse.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal(3, parse.Program.Lines.Count);
        Assert.Equal("start", parse.Program.Lines[0].Label?.Name);
        Assert.Equal(4, parse.Program.Lines[1].Statements.Count);
        Assert.IsType<AssignmentStatementSyntax>(parse.Program.Lines[1].Statements[0]);
        Assert.IsType<InstrStatementSyntax>(parse.Program.Lines[1].Statements[1]);
        Assert.IsType<PrintStatementSyntax>(parse.Program.Lines[1].Statements[2]);
        Assert.IsType<PrintStatementSyntax>(parse.Program.Lines[1].Statements[3]);
        Assert.IsType<EndStatementSyntax>(parse.Program.Lines[2].Statements[0]);
    }

    [Fact]
    public void Parse_PrintRawTail_ShouldPreserveLiteralQuotes()
    {
        const string source = "pln \"done\"";
        var parse = Parser.Parse(source);

        var line = Assert.Single(parse.Program.Lines);
        var print = Assert.IsType<PrintStatementSyntax>(Assert.Single(line.Statements));
        var raw = Assert.IsType<RawTextExpressionSyntax>(print.TextExpression);
        Assert.Equal("\"done\"", raw.RawText);
    }

    [Fact]
    public void Parse_ShouldBuildIfThenElseChains()
    {
        const string source = "if a=1 then p \"x\" & btn n,Go else goto end";
        var parse = Parser.Parse(source);

        var line = Assert.Single(parse.Program.Lines);
        var ifStmt = Assert.IsType<IfStatementSyntax>(Assert.Single(line.Statements));

        Assert.Equal(2, ifStmt.ThenStatements.Count);
        Assert.NotNull(ifStmt.ElseStatements);
        Assert.Single(ifStmt.ElseStatements!);
        Assert.IsType<PrintStatementSyntax>(ifStmt.ThenStatements[0]);
        Assert.IsType<BtnStatementSyntax>(ifStmt.ThenStatements[1]);
        Assert.IsType<GotoStatementSyntax>(ifStmt.ElseStatements![0]);
    }

    [Fact]
    public void Parse_ShouldRespectExpressionPrecedence()
    {
        const string source = "a=1+2*3";
        var parse = Parser.Parse(source);

        var line = Assert.Single(parse.Program.Lines);
        var assign = Assert.IsType<AssignmentStatementSyntax>(Assert.Single(line.Statements));
        var root = Assert.IsType<BinaryExpressionSyntax>(assign.Expression);

        Assert.Equal(TokenKind.Plus, root.Operator);
        Assert.IsType<NumberLiteralExpressionSyntax>(root.Left);
        var right = Assert.IsType<BinaryExpressionSyntax>(root.Right);
        Assert.Equal(TokenKind.Star, right.Operator);
    }

    [Fact]
    public void Parse_DosMode_ShouldTreatTrailingSymbolsAsRawPrintTail()
    {
        const string source = """
                              :a
                              p "ok" ????
                              end
                              """;

        var parse = Parser.Parse(source);

        Assert.Equal(3, parse.Program.Lines.Count);
        Assert.Equal("a", parse.Program.Lines[0].Label?.Name);
        var print = Assert.IsType<PrintStatementSyntax>(parse.Program.Lines[1].Statements[0]);
        var raw = Assert.IsType<RawTextExpressionSyntax>(print.TextExpression);
        Assert.Equal("\"ok\"????", raw.RawText);
        Assert.IsType<EndStatementSyntax>(parse.Program.Lines[2].Statements[0]);
    }

    [Fact]
    public void Parse_ShouldParseGotoProcAndBtn()
    {
        const string source = """
                              proc start
                              btn next,Go
                              goto next
                              """;

        var parse = Parser.Parse(source);
        Assert.Equal(3, parse.Program.Lines.Count);
        Assert.IsType<ProcStatementSyntax>(parse.Program.Lines[0].Statements[0]);
        Assert.IsType<BtnStatementSyntax>(parse.Program.Lines[1].Statements[0]);
        Assert.IsType<GotoStatementSyntax>(parse.Program.Lines[2].Statements[0]);
    }

    [Fact]
    public void Parse_DosMode_ShouldParsePrintRawTail()
    {
        const string source = "pln Привет, мир. Пока!";
        var parse = Parser.Parse(source, new ParserOptions(CompatibilityMode.DosUrq));
        var line = Assert.Single(parse.Program.Lines);
        var print = Assert.IsType<PrintStatementSyntax>(Assert.Single(line.Statements));
        var raw = Assert.IsType<RawTextExpressionSyntax>(print.TextExpression);
        Assert.Equal("Привет, мир. Пока!", raw.RawText);
    }

    [Fact]
    public void Parse_DosMode_ShouldParseInvkillCommand()
    {
        const string source = "invkill";
        var parse = Parser.Parse(source, new ParserOptions(CompatibilityMode.DosUrq));
        var line = Assert.Single(parse.Program.Lines);
        Assert.IsType<InvkillStatementSyntax>(Assert.Single(line.Statements));
        Assert.DoesNotContain(parse.Diagnostics, d => d.Code == Diagnostics.ParseDiagnosticCode.UnknownCommand);
    }

    [Fact]
    public void Parse_DosMode_ShouldParseInventoryDeltaCommands()
    {
        const string source = """
                              inv+ гайка
                              inv- 2,гайка
                              """;
        var parse = Parser.Parse(source, new ParserOptions(CompatibilityMode.DosUrq));

        Assert.Equal(2, parse.Program.Lines.Count);
        Assert.IsType<InvAddStatementSyntax>(Assert.Single(parse.Program.Lines[0].Statements));

        var remove = Assert.IsType<InvRemoveStatementSyntax>(Assert.Single(parse.Program.Lines[1].Statements));
        Assert.NotNull(remove.CountExpression);
    }

    [Fact]
    public void Parse_ShouldSupportVariableNamesWithSpaces()
    {
        const string source = "мы поели = 1";
        var parse = Parser.Parse(source);
        var line = Assert.Single(parse.Program.Lines);
        var assignment = Assert.IsType<AssignmentStatementSyntax>(Assert.Single(line.Statements));
        Assert.Equal("мы поели", assignment.Name);
    }

    [Fact]
    public void Parse_ShouldSupportLabelsWithSpaces()
    {
        const string source = """
                              :use_Топор_Рубить дерево
                              end
                              """;
        var parse = Parser.Parse(source);
        Assert.Equal("use_Топор_Рубить дерево", parse.Program.Lines[0].Label?.Name);
        Assert.IsType<EndStatementSyntax>(Assert.Single(parse.Program.Lines[1].Statements));
    }

    [Fact]
    public void Parse_ShouldTreatPercentMacroAsUnknownNoOpWarning()
    {
        const string source = "%include inc\\more.qst";
        var parse = Parser.Parse(source);

        var line = Assert.Single(parse.Program.Lines);
        var stmt = Assert.IsType<UnknownCommandStatementSyntax>(Assert.Single(line.Statements));
        Assert.Equal("%include", stmt.CommandName, ignoreCase: true);
        Assert.Contains(parse.Diagnostics, d => d.Code == ParseDiagnosticCode.UnknownCommand && d.Severity == DiagnosticSeverity.Warning);
        Assert.DoesNotContain(parse.Diagnostics, d => d.Code == ParseDiagnosticCode.InvalidStatement && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Parse_StrictMode_ShouldReportUnknownCommandAsError()
    {
        const string source = "pause 1000";
        var parse = Parser.Parse(source, new ParserOptions(CompatibilityMode.DosUrq, AllowUnknownCommands: false));

        var line = Assert.Single(parse.Program.Lines);
        Assert.IsType<UnknownCommandStatementSyntax>(Assert.Single(line.Statements));
        Assert.Contains(parse.Diagnostics, d => d.Code == ParseDiagnosticCode.UnknownCommand && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Parse_StrictMode_ShouldReportPercentMacroAsError()
    {
        const string source = "%include inc\\more.qst";
        var parse = Parser.Parse(source, new ParserOptions(CompatibilityMode.DosUrq, AllowUnknownCommands: false));

        var line = Assert.Single(parse.Program.Lines);
        Assert.IsType<UnknownCommandStatementSyntax>(Assert.Single(line.Statements));
        Assert.Contains(parse.Diagnostics, d => d.Code == ParseDiagnosticCode.UnknownCommand && d.Severity == DiagnosticSeverity.Error);
    }
}
