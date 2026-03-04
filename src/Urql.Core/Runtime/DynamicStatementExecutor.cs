using Urql.Core.Diagnostics;
using Urql.Core.Syntax;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Runtime;

public static class DynamicStatementExecutor
{
    public static bool ExecuteSingleStatement(string text, VirtualMachine vm)
    {
        var parse = Parser.Parse(text);
        foreach (var d in parse.Diagnostics)
        {
            vm.Context.Diagnostics.Report(d.Code, d.Severity, d.Message, d.Span);
        }

        if (parse.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            vm.Context.Diagnostics.Report(
                RuntimeDiagnosticCode.DynamicParseFailed,
                DiagnosticSeverity.Error,
                "Dynamic statement parsing failed.",
                new SourceSpan(new SourcePosition(1, 1), new SourcePosition(1, 1)));
            return false;
        }

        var nonEmptyLines = parse.Program.Lines.Where(l => l.Label is not null || l.Statements.Count > 0).ToList();
        if (nonEmptyLines.Count != 1)
        {
            Reject(vm, "Dynamic execution requires exactly one statement line.");
            return false;
        }

        var line = nonEmptyLines[0];
        if (line.Label is not null)
        {
            Reject(vm, "Labels are not allowed in dynamic single-statement mode.");
            return false;
        }

        if (line.Statements.Count != 1)
        {
            Reject(vm, "Dynamic execution requires exactly one statement.");
            return false;
        }

        var statement = line.Statements[0];
        if (!IsAllowed(statement))
        {
            Reject(vm, $"Statement '{statement.GetType().Name}' is not allowed in dynamic single-statement mode.");
            return false;
        }

        ExecuteAllowed(statement, vm);
        return true;
    }

    private static bool IsAllowed(StatementSyntax statement)
    {
        return statement is AssignmentStatementSyntax
            or InstrStatementSyntax
            or PrintStatementSyntax
            or BtnStatementSyntax
            or GotoStatementSyntax
            or ProcStatementSyntax
            or EndStatementSyntax
            or PerkillStatementSyntax
            or InvkillStatementSyntax
            or InvAddStatementSyntax
            or InvRemoveStatementSyntax;
    }

    private static void ExecuteAllowed(StatementSyntax statement, VirtualMachine vm)
    {
        switch (statement)
        {
            case AssignmentStatementSyntax assign:
            {
                var value = ExpressionEvaluator.Evaluate(assign.Expression, vm.Context);
                SetVariable(vm, assign.Name, value);
                break;
            }

            case InstrStatementSyntax instr:
            {
                var value = ExpressionEvaluator.Evaluate(instr.Expression, vm.Context);
                SetVariable(vm, instr.Name, UrqlValue.String(vm.Context.ToUrqlString(value)));
                break;
            }

            case PrintStatementSyntax print:
            {
                var value = ExpressionEvaluator.Evaluate(print.TextExpression, vm.Context);
                vm.AppendOutput(vm.Context.ToUrqlString(value), print.AppendNewLine);
                break;
            }

            case BtnStatementSyntax btn:
                vm.DynamicAddButton(btn.TargetExpression, btn.CaptionExpression);
                break;

            case GotoStatementSyntax g:
                vm.DynamicExecuteGoto(g.Target, g.Span);
                break;

            case ProcStatementSyntax p:
                vm.DynamicExecuteProc(p.Target, p.Span);
                break;

            case EndStatementSyntax:
                vm.DynamicExecuteEnd();
                break;

            case PerkillStatementSyntax:
                vm.Context.Variables.Clear();
                break;

            case InvkillStatementSyntax invkill:
                ExecuteInvkill(vm, invkill);
                break;

            case InvAddStatementSyntax invAdd:
                ExecuteInvDelta(vm, invAdd.CountExpression, invAdd.ItemExpression, true);
                break;

            case InvRemoveStatementSyntax invRemove:
                ExecuteInvDelta(vm, invRemove.CountExpression, invRemove.ItemExpression, false);
                break;
        }
    }

    private static void ExecuteInvkill(VirtualMachine vm, InvkillStatementSyntax invkill)
    {
        if (invkill.ItemExpression is null)
        {
            vm.Context.Inventory.Clear();
            return;
        }

        var item = vm.Context.ToInterpolationString(ExpressionEvaluator.Evaluate(invkill.ItemExpression, vm.Context)).Trim();
        if (string.IsNullOrEmpty(item))
        {
            vm.Context.Inventory.Clear();
            return;
        }

        vm.Context.Inventory.Clear(item);
    }

    private static void ExecuteInvDelta(
        VirtualMachine vm,
        ExpressionSyntax? countExpression,
        ExpressionSyntax itemExpression,
        bool add)
    {
        var item = vm.Context.ToInterpolationString(ExpressionEvaluator.Evaluate(itemExpression, vm.Context)).Trim();
        if (string.IsNullOrEmpty(item))
        {
            return;
        }

        var count = countExpression is null ? 1d : vm.Context.ToNumber(ExpressionEvaluator.Evaluate(countExpression, vm.Context));
        if (add)
        {
            vm.Context.Inventory.Add(item, count);
        }
        else
        {
            vm.Context.Inventory.Remove(item, count);
        }
    }

    private static void SetVariable(VirtualMachine vm, string name, UrqlValue value)
    {
        if (name.StartsWith("inv_", StringComparison.OrdinalIgnoreCase))
        {
            var item = name[4..];
            vm.Context.Inventory.SetCount(item, vm.Context.ToNumber(value));
            return;
        }

        vm.Context.Variables.Set(name, value);
    }

    private static void Reject(VirtualMachine vm, string message)
    {
        vm.Context.Diagnostics.Report(
            RuntimeDiagnosticCode.DynamicRejected,
            DiagnosticSeverity.Error,
            message,
            new SourceSpan(new SourcePosition(1, 1), new SourcePosition(1, 1)));
    }
}
