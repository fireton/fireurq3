using Urql.Core.Diagnostics;
using Urql.Core.Intermediate;
using Urql.Core.Runtime;
using Urql.Core.Syntax;

namespace Urql.Core.Tests;

public sealed class DynamicExecutionTests
{
    [Fact]
    public void DynamicSingle_ShouldExecuteAssignment()
    {
        var vm = BuildVm(":start\nend");
        var ok = vm.ExecuteDynamicSingleStatement("x=1+2");

        Assert.True(ok);
        Assert.True(vm.Context.Variables.TryGet("x", out var x));
        Assert.Equal(ValueKind.Number, x.Kind);
        Assert.Equal(3d, x.NumberValue, 6);
    }

    [Fact]
    public void DynamicSingle_ShouldExecutePrintln()
    {
        var vm = BuildVm(":start\nend");
        var ok = vm.ExecuteDynamicSingleStatement("pln \"hello\"");

        Assert.True(ok);
        Assert.Equal("hello\n", vm.OutputText);
    }

    [Fact]
    public void DynamicSingle_ShouldRejectStructuralIf()
    {
        var vm = BuildVm(":start\nend");
        var ok = vm.ExecuteDynamicSingleStatement("if 1 then x=1");

        Assert.False(ok);
        Assert.Contains(vm.Context.Diagnostics.Items, d => d.Code == RuntimeDiagnosticCode.DynamicRejected);
    }

    [Fact]
    public void DynamicSingle_ShouldRejectStatementChain()
    {
        var vm = BuildVm(":start\nend");
        var ok = vm.ExecuteDynamicSingleStatement("x=1 & y=2");

        Assert.False(ok);
        Assert.Contains(vm.Context.Diagnostics.Items, d => d.Code == RuntimeDiagnosticCode.DynamicRejected);
        Assert.False(vm.Context.Variables.TryGet("y", out _));
    }

    [Fact]
    public void DynamicSingle_ShouldRejectLabel()
    {
        var vm = BuildVm(":start\nend");
        var ok = vm.ExecuteDynamicSingleStatement(":x");

        Assert.False(ok);
        Assert.Contains(vm.Context.Diagnostics.Items, d => d.Code == RuntimeDiagnosticCode.DynamicRejected);
    }

    [Fact]
    public void DynamicSingle_ShouldExpandThenExecute()
    {
        var vm = BuildVm(":start\nend");
        vm.Context.Variables.Set("money", UrqlValue.Number(7));

        var ok = vm.ExpandAndExecuteDynamicSingleStatement("x=#money+5$");

        Assert.True(ok);
        Assert.True(vm.Context.Variables.TryGet("x", out var x));
        Assert.Equal(12d, x.NumberValue, 6);
    }

    [Fact]
    public void DynamicSingle_ShouldAllowGotoOperator()
    {
        var vm = BuildVm("""
                         :start
                         end
                         :next
                         instr s="ok"
                         end
                         """);

        var ok = vm.ExecuteDynamicSingleStatement("goto next");

        Assert.True(ok);
        Assert.Equal(1, vm.InstructionPointer);
    }

    [Fact]
    public void DynamicSingle_ShouldAllowBtnOperator()
    {
        var vm = BuildVm(":start\nend");

        var ok = vm.ExecuteDynamicSingleStatement("btn next,\"Go\"");

        Assert.True(ok);
        Assert.Single(vm.Buttons);
        Assert.Equal("next", vm.Buttons[0].Target, ignoreCase: true);
        Assert.Equal("Go", vm.Buttons[0].Caption);
    }

    private static VirtualMachine BuildVm(string source)
    {
        var parse = Parser.Parse(source);
        var ir = Compiler.Compile(parse.Program, parse.Diagnostics);
        return new VirtualMachine(ir);
    }
}
