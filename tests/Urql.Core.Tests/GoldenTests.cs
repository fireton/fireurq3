using System.Text;
using Urql.Core.Intermediate;
using Urql.Core.Syntax;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Tests;

public sealed class GoldenTests
{
    [Fact]
    public void AstGolden_ShouldMatchSnapshot()
    {
        const string source = """
                              :start
                              a=1
                              if a=1 then goto ok else goto bad
                              :ok
                              end
                              :bad
                              end
                              """;

        var parse = Parser.Parse(source);
        Assert.DoesNotContain(parse.Diagnostics, d => d.Severity == Diagnostics.DiagnosticSeverity.Error);

        var actual = RenderAst(parse.Program).Trim();
        const string expected = """
                                Line(Label=start)
                                Line
                                  Assignment(a = 1)
                                Line
                                  If((a = 1))
                                    Then:
                                      Goto(ok)
                                    Else:
                                      Goto(bad)
                                Line(Label=ok)
                                Line
                                  End
                                Line(Label=bad)
                                Line
                                  End
                                """;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void IrGolden_ShouldMatchSnapshot()
    {
        const string source = """
                              :start
                              a=1
                              if a=1 then goto ok else goto bad
                              :ok
                              end
                              :bad
                              end
                              """;

        var parse = Parser.Parse(source);
        var ir = Compiler.Compile(parse.Program, parse.Diagnostics);

        var actual = RenderIr(ir).Trim();
        const string expected = """
                                Labels:
                                  start -> 0
                                  ok -> 5
                                  bad -> 6
                                Instructions:
                                  [0] ASSIGN a <- 1
                                  [1] JUMP_IF_FALSE (a = 1) -> 4
                                  [2] JUMP -> 5
                                  [3] JUMP -> 5
                                  [4] JUMP -> 6
                                  [5] RETURN_OR_HALT
                                  [6] RETURN_OR_HALT
                                """;

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n").Trim();
    }

    private static string RenderAst(ProgramSyntax program)
    {
        var sb = new StringBuilder();
        foreach (var line in program.Lines)
        {
            if (line.Label is not null)
            {
                sb.AppendLine($"Line(Label={line.Label.Name})");
            }
            else
            {
                sb.AppendLine("Line");
            }

            foreach (var statement in line.Statements)
            {
                RenderStatement(statement, sb, "  ");
            }
        }

        return sb.ToString();
    }

    private static void RenderStatement(StatementSyntax statement, StringBuilder sb, string indent)
    {
        switch (statement)
        {
            case AssignmentStatementSyntax a:
                sb.AppendLine($"{indent}Assignment({a.Name} = {RenderExpr(a.Expression)})");
                break;
            case InstrStatementSyntax i:
                sb.AppendLine($"{indent}Instr({i.Name} = {RenderExpr(i.Expression)})");
                break;
            case IfStatementSyntax f:
                sb.AppendLine($"{indent}If({RenderExpr(f.Condition)})");
                sb.AppendLine($"{indent}  Then:");
                foreach (var s in f.ThenStatements)
                {
                    RenderStatement(s, sb, indent + "    ");
                }

                if (f.ElseStatements is not null)
                {
                    sb.AppendLine($"{indent}  Else:");
                    foreach (var s in f.ElseStatements)
                    {
                        RenderStatement(s, sb, indent + "    ");
                    }
                }

                break;
            case GotoStatementSyntax g:
                sb.AppendLine($"{indent}Goto({RenderExpr(g.Target)})");
                break;
            case ProcStatementSyntax p:
                sb.AppendLine($"{indent}Proc({RenderExpr(p.Target)})");
                break;
            case EndStatementSyntax:
                sb.AppendLine($"{indent}End");
                break;
            case PrintStatementSyntax pr:
                sb.AppendLine($"{indent}{(pr.AppendNewLine ? "Pln" : "P")}({RenderExpr(pr.TextExpression)})");
                break;
            case BtnStatementSyntax b:
                sb.AppendLine($"{indent}Btn({RenderExpr(b.TargetExpression)}, {RenderExpr(b.CaptionExpression)})");
                break;
            default:
                sb.AppendLine($"{indent}{statement.GetType().Name}");
                break;
        }
    }

    private static string RenderExpr(ExpressionSyntax expression)
    {
        return expression switch
        {
            NumberLiteralExpressionSyntax n => n.RawText,
            StringLiteralExpressionSyntax s => $"\"{s.Value}\"",
            IdentifierExpressionSyntax i => i.Name,
            UnaryExpressionSyntax u => $"({u.Operator} {RenderExpr(u.Operand)})",
            BinaryExpressionSyntax b => $"({RenderExpr(b.Left)} {TokenText(b.Operator)} {RenderExpr(b.Right)})",
            ParenthesizedExpressionSyntax p => $"({RenderExpr(p.Inner)})",
            _ => expression.GetType().Name
        };
    }

    private static string TokenText(TokenKind kind) => kind switch
    {
        TokenKind.Equals => "=",
        TokenKind.NotEquals => "<>",
        TokenKind.DoubleEquals => "==",
        TokenKind.Less => "<",
        TokenKind.LessOrEquals => "<=",
        TokenKind.Greater => ">",
        TokenKind.GreaterOrEquals => ">=",
        TokenKind.Plus => "+",
        TokenKind.Minus => "-",
        TokenKind.Star => "*",
        TokenKind.Slash => "/",
        TokenKind.Percent => "%",
        TokenKind.KeywordAnd => "and",
        TokenKind.KeywordOr => "or",
        TokenKind.KeywordNot => "not",
        _ => kind.ToString()
    };

    private static string RenderIr(IrProgram program)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Labels:");
        foreach (var kv in program.LabelMap.OrderBy(k => k.Value))
        {
            sb.AppendLine($"  {kv.Key} -> {kv.Value}");
        }

        sb.AppendLine("Instructions:");
        for (var i = 0; i < program.Instructions.Count; i++)
        {
            sb.AppendLine($"  [{i}] {RenderIrInstruction(program.Instructions[i])}");
        }

        return sb.ToString();
    }

    private static string RenderIrInstruction(IrInstruction instruction)
    {
        return instruction switch
        {
            AssignInstruction a => $"ASSIGN {a.Name} <- {RenderExpr(a.Expression)}",
            JumpInstruction j => $"JUMP -> {j.TargetIndex}",
            JumpDynamicInstruction j => $"JUMP_DYNAMIC {RenderExpr(j.TargetExpression)}",
            JumpIfFalseInstruction j => $"JUMP_IF_FALSE {RenderExpr(j.ConditionExpression)} -> {j.TargetIndex}",
            CallInstruction c => $"CALL -> {c.TargetIndex}",
            CallDynamicInstruction c => $"CALL_DYNAMIC {RenderExpr(c.TargetExpression)}",
            ReturnOrHaltInstruction => "RETURN_OR_HALT",
            PrintInstruction p => $"{(p.AppendNewline ? "PRINTLN" : "PRINT")} {RenderExpr(p.TextExpression)}",
            AddButtonInstruction b => $"ADD_BUTTON {RenderExpr(b.TargetExpression)}, {RenderExpr(b.CaptionExpression)}",
            NoOpInstruction => "NOP",
            _ => instruction.GetType().Name
        };
    }
}
