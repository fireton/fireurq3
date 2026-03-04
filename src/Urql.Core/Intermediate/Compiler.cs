using Urql.Core.Diagnostics;
using Urql.Core.Syntax;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Intermediate;

public static class Compiler
{
    private sealed record PendingPatch(int InstructionIndex, string LabelName, bool IsCall, SourceSpan Span);

    public static IrProgram Compile(ProgramSyntax program, IReadOnlyList<Diagnostic>? parseDiagnostics = null)
    {
        var diagnostics = new DiagnosticBag();
        if (parseDiagnostics is not null)
        {
            diagnostics.AddRange(parseDiagnostics);
        }

        var instructions = new List<IrInstruction>();
        var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<PendingPatch>();

        foreach (var line in program.Lines)
        {
            if (line.Label is not null)
            {
                if (!labels.TryAdd(line.Label.Name, instructions.Count))
                {
                    diagnostics.ReportError(
                        CompileDiagnosticCode.DuplicateLabel,
                        $"Duplicate label '{line.Label.Name}'.",
                        line.Label.Span);
                }
            }

            foreach (var statement in line.Statements)
            {
                EmitStatement(statement, instructions, pending);
            }
        }

        for (var i = 0; i < pending.Count; i++)
        {
            var patch = pending[i];
            if (!labels.TryGetValue(patch.LabelName, out var target))
            {
                diagnostics.Report(
                    CompileDiagnosticCode.UnknownLabel,
                    DiagnosticSeverity.Warning,
                    $"Unknown label '{patch.LabelName}'.",
                    patch.Span);
                continue;
            }

            if (patch.IsCall)
            {
                instructions[patch.InstructionIndex] = new CallInstruction(target, patch.Span);
            }
            else
            {
                instructions[patch.InstructionIndex] = new JumpInstruction(target, patch.Span);
            }
        }

        return new IrProgram(instructions, labels, diagnostics.Items);
    }

    private static void EmitStatement(
        StatementSyntax statement,
        List<IrInstruction> instructions,
        List<PendingPatch> pending)
    {
        switch (statement)
        {
            case AssignmentStatementSyntax assign:
                instructions.Add(new AssignInstruction(assign.Name, assign.Expression, false, assign.Span));
                break;

            case InstrStatementSyntax instr:
                instructions.Add(new AssignInstruction(instr.Name, instr.Expression, true, instr.Span));
                break;

            case PrintStatementSyntax print:
                instructions.Add(new PrintInstruction(print.TextExpression, print.AppendNewLine, print.Span));
                break;

            case BtnStatementSyntax btn:
                instructions.Add(new AddButtonInstruction(btn.TargetExpression, btn.CaptionExpression, btn.Span));
                break;

            case PerkillStatementSyntax perk:
                instructions.Add(new PerkillInstruction(perk.Span));
                break;

            case InvkillStatementSyntax invkill:
                instructions.Add(new InvkillInstruction(invkill.ItemExpression, invkill.Span));
                break;

            case InvAddStatementSyntax invAdd:
                instructions.Add(new InvAddInstruction(invAdd.CountExpression, invAdd.ItemExpression, invAdd.Span));
                break;

            case InvRemoveStatementSyntax invRemove:
                instructions.Add(new InvRemoveInstruction(invRemove.CountExpression, invRemove.ItemExpression, invRemove.Span));
                break;

            case EndStatementSyntax end:
                instructions.Add(new ReturnOrHaltInstruction(end.Span));
                break;

            case GotoStatementSyntax g:
            {
                if (TryStaticLabel(g.Target, out var label))
                {
                    var idx = instructions.Count;
                    instructions.Add(new JumpInstruction(-1, g.Span));
                    pending.Add(new PendingPatch(idx, label, false, g.Span));
                }
                else
                {
                    instructions.Add(new JumpDynamicInstruction(g.Target, g.Span));
                }

                break;
            }

            case ProcStatementSyntax p:
            {
                if (TryStaticLabel(p.Target, out var label))
                {
                    var idx = instructions.Count;
                    instructions.Add(new CallInstruction(-1, p.Span));
                    pending.Add(new PendingPatch(idx, label, true, p.Span));
                }
                else
                {
                    instructions.Add(new CallDynamicInstruction(p.Target, p.Span));
                }

                break;
            }

            case IfStatementSyntax ifs:
                EmitIf(ifs, instructions, pending);
                break;

            case UnknownCommandStatementSyntax:
                instructions.Add(new NoOpInstruction(statement.Span));
                break;

            default:
                instructions.Add(new NoOpInstruction(statement.Span));
                break;
        }
    }

    private static void EmitIf(
        IfStatementSyntax ifs,
        List<IrInstruction> instructions,
        List<PendingPatch> pending)
    {
        var jumpFalseIndex = instructions.Count;
        instructions.Add(new JumpIfFalseInstruction(ifs.Condition, -1, ifs.Span));

        foreach (var s in ifs.ThenStatements)
        {
            EmitStatement(s, instructions, pending);
        }

        if (ifs.ElseStatements is { Count: > 0 })
        {
            var jumpEndIndex = instructions.Count;
            instructions.Add(new JumpInstruction(-1, ifs.Span));

            var elseStart = instructions.Count;
            instructions[jumpFalseIndex] = new JumpIfFalseInstruction(ifs.Condition, elseStart, ifs.Span);

            foreach (var s in ifs.ElseStatements)
            {
                EmitStatement(s, instructions, pending);
            }

            instructions[jumpEndIndex] = new JumpInstruction(instructions.Count, ifs.Span);
        }
        else
        {
            instructions[jumpFalseIndex] = new JumpIfFalseInstruction(ifs.Condition, instructions.Count, ifs.Span);
        }
    }

    private static bool TryStaticLabel(ExpressionSyntax expression, out string label)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax id when !string.IsNullOrWhiteSpace(id.Name):
                label = id.Name;
                return true;

            case StringLiteralExpressionSyntax str when !string.IsNullOrWhiteSpace(str.Value):
                label = str.Value;
                return true;

            default:
                label = string.Empty;
                return false;
        }
    }
}
