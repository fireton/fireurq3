using System.Text;
using Urql.Core.Diagnostics;
using Urql.Core.Intermediate;
using Urql.Core.Syntax.Ast;

namespace Urql.Core.Runtime;

public sealed class VirtualMachine
{
    private readonly IrProgram _program;
    private readonly Stack<int> _callStack = new();
    private readonly List<ButtonAction> _buttons = [];
    private readonly StringBuilder _output = new();
    private int _nextButtonId = 1;
    private bool _returnToWaitingAfterUse;

    public VirtualMachine(IrProgram program, EvalContext? context = null)
    {
        _program = program;
        Context = context ?? new EvalContext();
        foreach (var d in program.Diagnostics)
        {
            Context.Diagnostics.Report(d.Code, d.Severity, d.Message, d.Span);
        }
    }

    public EvalContext Context { get; }
    public VmStatus Status { get; private set; } = VmStatus.Running;
    public int InstructionPointer { get; private set; }
    public string OutputText => _output.ToString();
    public IReadOnlyList<ButtonAction> Buttons => _buttons;

    public bool Step()
    {
        if (Status != VmStatus.Running)
        {
            return false;
        }

        if (InstructionPointer < 0 || InstructionPointer >= _program.Instructions.Count)
        {
            Status = VmStatus.Halted;
            return false;
        }

        var instruction = _program.Instructions[InstructionPointer];
        Execute(instruction);
        return true;
    }

    public VmRunResult RunUntilWaitOrHalt(int maxInstructions)
    {
        var executed = 0;
        while (executed < maxInstructions && Status == VmStatus.Running)
        {
            if (!Step())
            {
                break;
            }

            executed++;
        }

        return new VmRunResult(Status, executed, executed >= maxInstructions && Status == VmStatus.Running);
    }

    public VmRunResult RunUntilHalt(int maxInstructions)
    {
        var executed = 0;
        while (executed < maxInstructions && Status is VmStatus.Running or VmStatus.WaitingForChoice)
        {
            if (Status == VmStatus.WaitingForChoice)
            {
                break;
            }

            if (!Step())
            {
                break;
            }

            executed++;
        }

        return new VmRunResult(Status, executed, executed >= maxInstructions && Status == VmStatus.Running);
    }

    public bool ExecuteDynamicSingleStatement(string expandedStatementText)
    {
        return DynamicStatementExecutor.ExecuteSingleStatement(expandedStatementText, this);
    }

    public bool ExpandAndExecuteDynamicSingleStatement(string statementTemplateText)
    {
        var expanded = InterpolationExpander.ExpandInterpolations(statementTemplateText, Context);
        return ExecuteDynamicSingleStatement(expanded);
    }

    public bool ChooseButton(int buttonId)
    {
        if (Status != VmStatus.WaitingForChoice)
        {
            return false;
        }

        var button = _buttons.FirstOrDefault(x => x.Id == buttonId);
        if (button is null)
        {
            return false;
        }

        Context.Variables.Set("last_btn_caption", UrqlValue.String(button.Caption));
        _buttons.Clear();

        if (_program.LabelMap.TryGetValue(button.Target, out var index))
        {
            InstructionPointer = index;
            Status = VmStatus.Running;
            return true;
        }

        Context.Diagnostics.Report(
            CompileDiagnosticCode.UnknownLabel,
            DiagnosticSeverity.Warning,
            $"Unknown label '{button.Target}' selected from button.",
            new Syntax.SourceSpan(new Syntax.SourcePosition(1, 1), new Syntax.SourcePosition(1, 1)));
        Status = VmStatus.Halted;
        return false;
    }

    public bool UseInventoryItem(string itemName, string? actionName = null)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return false;
        }

        if (Context.Inventory.GetCount(itemName) <= 0d)
        {
            return false;
        }

        var label = ResolveUseLabel(itemName, actionName);
        if (label is null)
        {
            return false;
        }

        return InvokeUseLabel(label);
    }

    public bool InvokeUseLabel(string labelName)
    {
        if (Status is VmStatus.Halted or VmStatus.Faulted)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(labelName))
        {
            return false;
        }

        if (!_program.LabelMap.TryGetValue(labelName, out var index))
        {
            Context.Diagnostics.Report(
                CompileDiagnosticCode.UnknownLabel,
                DiagnosticSeverity.Warning,
                $"Unknown label '{labelName}'.",
                new Syntax.SourceSpan(new Syntax.SourcePosition(1, 1), new Syntax.SourcePosition(1, 1)));
            return false;
        }

        _callStack.Push(InstructionPointer);
        _returnToWaitingAfterUse = Status == VmStatus.WaitingForChoice;
        InstructionPointer = index;
        Status = VmStatus.Running;
        return true;
    }

    private void Execute(IrInstruction instruction)
    {
        switch (instruction)
        {
            case AssignInstruction assign:
                ExecuteAssign(assign);
                InstructionPointer++;
                break;

            case PrintInstruction print:
                ExecutePrint(print);
                InstructionPointer++;
                break;

            case AddButtonInstruction btn:
                ExecuteAddButton(btn);
                InstructionPointer++;
                break;

            case PerkillInstruction:
                Context.Variables.Clear();
                InstructionPointer++;
                break;

            case InvkillInstruction invkill:
                ExecuteInvkill(invkill);
                InstructionPointer++;
                break;

            case InvAddInstruction invAdd:
                ExecuteInvDelta(invAdd.CountExpression, invAdd.ItemExpression, true);
                InstructionPointer++;
                break;

            case InvRemoveInstruction invRemove:
                ExecuteInvDelta(invRemove.CountExpression, invRemove.ItemExpression, false);
                InstructionPointer++;
                break;

            case JumpInstruction jump:
                if (jump.TargetIndex < 0 || jump.TargetIndex >= _program.Instructions.Count)
                {
                    Status = VmStatus.Halted;
                }
                else
                {
                    InstructionPointer = jump.TargetIndex;
                }
                break;

            case JumpDynamicInstruction dynamicJump:
            {
                var target = ResolveTarget(dynamicJump.TargetExpression);
                if (_program.LabelMap.TryGetValue(target, out var jumpIndex))
                {
                    InstructionPointer = jumpIndex;
                }
                else
                {
                    Context.Diagnostics.Report(
                        CompileDiagnosticCode.UnknownLabel,
                        DiagnosticSeverity.Warning,
                        $"Unknown label '{target}'.",
                        dynamicJump.Span);
                    InstructionPointer++;
                }
                break;
            }

            case JumpIfFalseInstruction jumpIfFalse:
            {
                var condition = ExpressionEvaluator.Evaluate(jumpIfFalse.ConditionExpression, Context);
                if (!Context.ToBool(condition))
                {
                    InstructionPointer = jumpIfFalse.TargetIndex;
                }
                else
                {
                    InstructionPointer++;
                }
                break;
            }

            case CallInstruction call:
                _callStack.Push(InstructionPointer + 1);
                InstructionPointer = call.TargetIndex;
                break;

            case CallDynamicInstruction dynamicCall:
            {
                var target = ResolveTarget(dynamicCall.TargetExpression);
                if (_program.LabelMap.TryGetValue(target, out var callIndex))
                {
                    _callStack.Push(InstructionPointer + 1);
                    InstructionPointer = callIndex;
                }
                else
                {
                    Context.Diagnostics.Report(
                        CompileDiagnosticCode.UnknownLabel,
                        DiagnosticSeverity.Warning,
                        $"Unknown label '{target}'.",
                        dynamicCall.Span);
                    InstructionPointer++;
                }
                break;
            }

            case ReturnOrHaltInstruction:
                if (_callStack.Count > 0)
                {
                    InstructionPointer = _callStack.Pop();
                    if (_callStack.Count == 0 && _returnToWaitingAfterUse)
                    {
                        _returnToWaitingAfterUse = false;
                        Status = VmStatus.WaitingForChoice;
                    }
                }
                else
                {
                    if (_buttons.Count > 0)
                    {
                        Status = VmStatus.WaitingForChoice;
                    }
                    else
                    {
                        Status = VmStatus.Halted;
                    }
                }
                break;

            case NoOpInstruction:
                InstructionPointer++;
                break;

            default:
                Status = VmStatus.Faulted;
                break;
        }
    }

    private void ExecuteAssign(AssignInstruction assign)
    {
        var value = ExpressionEvaluator.Evaluate(assign.Expression, Context);
        if (assign.ForceString)
        {
            SetVariable(assign.Name, UrqlValue.String(Context.ToUrqlString(value)));
        }
        else
        {
            SetVariable(assign.Name, value);
        }
    }

    private void ExecutePrint(PrintInstruction print)
    {
        var value = ExpressionEvaluator.Evaluate(print.TextExpression, Context);
        AppendOutput(Context.ToUrqlString(value), print.AppendNewline);
    }

    private void ExecuteAddButton(AddButtonInstruction btn)
    {
        var target = ResolveTarget(btn.TargetExpression);
        var caption = ResolveCaption(btn.CaptionExpression);
        _buttons.Add(new ButtonAction(_nextButtonId++, caption, target));
    }

    private void ExecuteInvkill(InvkillInstruction instruction)
    {
        if (instruction.ItemExpression is null)
        {
            Context.Inventory.Clear();
            return;
        }

        var item = Context.ToInterpolationString(ExpressionEvaluator.Evaluate(instruction.ItemExpression, Context)).Trim();
        if (string.IsNullOrEmpty(item))
        {
            Context.Inventory.Clear();
            return;
        }

        Context.Inventory.Clear(item);
    }

    private void ExecuteInvDelta(ExpressionSyntax? countExpression, ExpressionSyntax itemExpression, bool add)
    {
        var item = Context.ToInterpolationString(ExpressionEvaluator.Evaluate(itemExpression, Context)).Trim();
        if (string.IsNullOrEmpty(item))
        {
            return;
        }

        var count = countExpression is null ? 1d : Context.ToNumber(ExpressionEvaluator.Evaluate(countExpression, Context));
        if (add)
        {
            Context.Inventory.Add(item, count);
        }
        else
        {
            Context.Inventory.Remove(item, count);
        }
    }

    private void SetVariable(string name, UrqlValue value)
    {
        if (name.StartsWith("inv_", StringComparison.OrdinalIgnoreCase))
        {
            var item = name[4..];
            Context.Inventory.SetCount(item, Context.ToNumber(value));
            return;
        }

        Context.Variables.Set(name, value);
    }

    private string? ResolveUseLabel(string itemName, string? actionName)
    {
        var item = itemName.Trim();
        var itemUnderscored = item.Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(actionName))
        {
            var labels = new[]
            {
                $"use_{item}",
                $"use_{itemUnderscored}"
            };
            for (var i = 0; i < labels.Length; i++)
            {
                if (_program.LabelMap.ContainsKey(labels[i]))
                {
                    return labels[i];
                }
            }

            return null;
        }

        var action = actionName.Trim();
        var actionUnderscored = action.Replace(' ', '_');
        var actionLabels = new[]
        {
            $"use_{item}_{action}",
            $"use_{item}_{actionUnderscored}",
            $"use_{itemUnderscored}_{action}",
            $"use_{itemUnderscored}_{actionUnderscored}"
        };
        for (var i = 0; i < actionLabels.Length; i++)
        {
            if (_program.LabelMap.ContainsKey(actionLabels[i]))
            {
                return actionLabels[i];
            }
        }

        return null;
    }

    private string ResolveTarget(ExpressionSyntax expression)
    {
        if (expression is IdentifierExpressionSyntax id)
        {
            return id.Name;
        }

        if (expression is StringLiteralExpressionSyntax str)
        {
            return str.Value;
        }

        var value = ExpressionEvaluator.Evaluate(expression, Context);
        return Context.ToInterpolationString(value);
    }

    private string ResolveCaption(ExpressionSyntax expression)
    {
        if (expression is StringLiteralExpressionSyntax str)
        {
            return str.Value;
        }

        if (expression is IdentifierExpressionSyntax id)
        {
            return id.Name;
        }

        return Context.ToInterpolationString(ExpressionEvaluator.Evaluate(expression, Context));
    }

    internal void DynamicExecuteGoto(ExpressionSyntax expression, Syntax.SourceSpan span)
    {
        var target = ResolveTarget(expression);
        if (_program.LabelMap.TryGetValue(target, out var index))
        {
            InstructionPointer = index;
            return;
        }

        Context.Diagnostics.Report(
            CompileDiagnosticCode.UnknownLabel,
            DiagnosticSeverity.Warning,
            $"Unknown label '{target}'.",
            span);
    }

    internal void DynamicExecuteProc(ExpressionSyntax expression, Syntax.SourceSpan span)
    {
        var target = ResolveTarget(expression);
        if (_program.LabelMap.TryGetValue(target, out var index))
        {
            _callStack.Push(InstructionPointer + 1);
            InstructionPointer = index;
            return;
        }

        Context.Diagnostics.Report(
            CompileDiagnosticCode.UnknownLabel,
            DiagnosticSeverity.Warning,
            $"Unknown label '{target}'.",
            span);
    }

    internal void DynamicExecuteEnd()
    {
        if (_callStack.Count > 0)
        {
            InstructionPointer = _callStack.Pop();
            return;
        }

        Status = _buttons.Count > 0 ? VmStatus.WaitingForChoice : VmStatus.Halted;
    }

    internal void DynamicAddButton(ExpressionSyntax targetExpression, ExpressionSyntax captionExpression)
    {
        var target = ResolveTarget(targetExpression);
        var caption = ResolveCaption(captionExpression);
        _buttons.Add(new ButtonAction(_nextButtonId++, caption, target));
    }

    internal void AppendOutput(string text, bool appendNewline)
    {
        _output.Append(text);
        if (appendNewline)
        {
            _output.Append('\n');
        }
    }
}
