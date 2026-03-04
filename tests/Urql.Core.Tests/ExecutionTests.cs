using Urql.Core.Intermediate;
using Urql.Core.Runtime;
using Urql.Core.Syntax;

namespace Urql.Core.Tests;

public sealed class ExecutionTests
{
    [Fact]
    public void Vm_ShouldExecuteAssignmentAndIf()
    {
        const string source = """
                              :start
                              a=1+2*3
                              if a=7 then instr s="ok" else instr s="bad"
                              end
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(500);

        Assert.Equal(VmStatus.Halted, run.Status);
        Assert.True(vm.Context.Variables.TryGet("s", out var s));
        Assert.Equal(ValueKind.String, s.Kind);
        Assert.Equal("ok", s.StringValue);
    }

    [Fact]
    public void Vm_ShouldHandleGoto()
    {
        const string source = """
                              :start
                              goto target
                              instr s="bad"
                              :target
                              instr s="ok"
                              end
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(500);

        Assert.Equal(VmStatus.Halted, run.Status);
        Assert.True(vm.Context.Variables.TryGet("s", out var s));
        Assert.Equal("ok", s.StringValue);
    }

    [Fact]
    public void Vm_ShouldHandleProcAndReturnOnEnd()
    {
        const string source = """
                              :start
                              proc sub
                              instr done="yes"
                              end
                              :sub
                              instr x="1"
                              end
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(500);

        Assert.Equal(VmStatus.Halted, run.Status);
        Assert.True(vm.Context.Variables.TryGet("x", out var x));
        Assert.Equal("1", x.StringValue);
        Assert.True(vm.Context.Variables.TryGet("done", out var done));
        Assert.Equal("yes", done.StringValue);
    }

    [Fact]
    public void Vm_ShouldPauseAtTopLevelEndWhenButtonsExist()
    {
        const string source = """
                              :start
                              btn next,Go
                              end
                              :next
                              instr s="ok"
                              end
                              """;

        var vm = BuildVm(source);
        var run1 = vm.RunUntilWaitOrHalt(500);

        Assert.Equal(VmStatus.WaitingForChoice, run1.Status);
        Assert.Single(vm.Buttons);
        var button = vm.Buttons[0];
        Assert.Equal("Go", button.Caption);
        Assert.Equal("next", button.Target, ignoreCase: true);

        var chosen = vm.ChooseButton(button.Id);
        Assert.True(chosen);

        var run2 = vm.RunUntilHalt(500);
        Assert.Equal(VmStatus.Halted, run2.Status);
        Assert.True(vm.Context.Variables.TryGet("s", out var s));
        Assert.Equal("ok", s.StringValue);
    }

    [Fact]
    public void Vm_ShouldNotWaitOnBtnInstructionOnlyOnTopLevelEnd()
    {
        const string source = """
                              :start
                              btn next,Go
                              instr x="set-before-end"
                              end
                              :next
                              end
                              """;

        var vm = BuildVm(source);

        var steppedBtn = vm.Step();
        Assert.True(steppedBtn);
        Assert.Equal(VmStatus.Running, vm.Status);
        Assert.Single(vm.Buttons);

        var steppedInstr = vm.Step();
        Assert.True(steppedInstr);
        Assert.Equal(VmStatus.Running, vm.Status);
        Assert.True(vm.Context.Variables.TryGet("x", out var x));
        Assert.Equal("set-before-end", x.StringValue);

        var steppedEnd = vm.Step();
        Assert.True(steppedEnd);
        Assert.Equal(VmStatus.WaitingForChoice, vm.Status);
    }

    [Fact]
    public void Vm_ShouldNotWaitOnEndInsideProc_WhenButtonsWereAddedInProc()
    {
        const string source = """
                              :start
                              proc submenu
                              instr done="after-proc"
                              end
                              :submenu
                              btn next,FromProc
                              end
                              :next
                              instr picked="yes"
                              end
                              """;

        var vm = BuildVm(source);

        // proc submenu
        Assert.True(vm.Step());
        Assert.Equal(VmStatus.Running, vm.Status);

        // btn next,FromProc (inside proc)
        Assert.True(vm.Step());
        Assert.Equal(VmStatus.Running, vm.Status);
        Assert.Single(vm.Buttons);
        Assert.Equal("FromProc", vm.Buttons[0].Caption);

        // end (inside proc) should RETURN, not wait
        Assert.True(vm.Step());
        Assert.Equal(VmStatus.Running, vm.Status);

        // instr done="after-proc"
        Assert.True(vm.Step());
        Assert.True(vm.Context.Variables.TryGet("done", out var done));
        Assert.Equal("after-proc", done.StringValue);
        Assert.Equal(VmStatus.Running, vm.Status);

        // top-level end should wait because button exists
        Assert.True(vm.Step());
        Assert.Equal(VmStatus.WaitingForChoice, vm.Status);
    }

    [Fact]
    public void Vm_ShouldRespectInstructionLimit()
    {
        const string source = """
                              :start
                              goto start
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(50);

        Assert.True(run.HitInstructionLimit);
        Assert.Equal(VmStatus.Running, run.Status);
    }

    [Fact]
    public void Vm_ShouldHandleInventoryCommands()
    {
        const string source = """
                              :start
                              inv+ гайка
                              inv+ 2,гайка
                              inv- гайка
                              inv- 1,гайка
                              end
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(500);

        Assert.Equal(VmStatus.Halted, run.Status);
        Assert.Equal(1d, vm.Context.Inventory.GetCount("гайка"), 6);
    }

    [Fact]
    public void Vm_ShouldSupportInvBridgeReadWrite()
    {
        const string source = """
                              :start
                              inv+ 3,монета
                              x=inv_монета
                              inv_монета=5
                              y=inv_монета
                              end
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(500);

        Assert.Equal(VmStatus.Halted, run.Status);
        Assert.True(vm.Context.Variables.TryGet("x", out var x));
        Assert.Equal(3d, x.NumberValue, 6);
        Assert.True(vm.Context.Variables.TryGet("y", out var y));
        Assert.Equal(5d, y.NumberValue, 6);
        Assert.Equal(5d, vm.Context.Inventory.GetCount("монета"), 6);
    }

    [Fact]
    public void Vm_ShouldHandlePerkillAndInvkill()
    {
        const string source = """
                              :start
                              x=10
                              inv+ 3,монета
                              perkill
                              invkill монета
                              end
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(500);

        Assert.Equal(VmStatus.Halted, run.Status);
        Assert.False(vm.Context.Variables.TryGet("x", out _));
        Assert.Equal(0d, vm.Context.Inventory.GetCount("монета"), 6);
    }

    [Fact]
    public void Vm_ShouldSupportVariableNamesWithSpaces()
    {
        const string source = """
                              :start
                              мы поели = 1
                              if мы поели=1 then instr ok="yes" else instr ok="no"
                              end
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(500);

        Assert.Equal(VmStatus.Halted, run.Status);
        Assert.True(vm.Context.Variables.TryGet("мы поели", out var value));
        Assert.Equal(1d, value.NumberValue, 6);
        Assert.True(vm.Context.Variables.TryGet("ok", out var ok));
        Assert.Equal("yes", ok.StringValue);
    }

    [Fact]
    public void Vm_ShouldTreatBareInventoryItemAsCondition()
    {
        const string source = """
                              :start
                              inv+ яблоко
                              if яблоко then instr has="yes" else instr has="no"
                              inv- яблоко
                              if not яблоко then instr gone="yes" else instr gone="no"
                              end
                              """;

        var vm = BuildVm(source);
        var run = vm.RunUntilHalt(500);

        Assert.Equal(VmStatus.Halted, run.Status);
        Assert.True(vm.Context.Variables.TryGet("has", out var has));
        Assert.Equal("yes", has.StringValue);
        Assert.True(vm.Context.Variables.TryGet("gone", out var gone));
        Assert.Equal("yes", gone.StringValue);
    }

    [Fact]
    public void Vm_ShouldInvokeUseLabelAndReturnToWaitingState()
    {
        const string source = """
                              :start
                              inv+ Топор
                              btn next,Go
                              end
                              :use_Топор_Рубить дерево
                              instr used="1"
                              end
                              :next
                              instr nexted="1"
                              end
                              """;

        var vm = BuildVm(source);
        var wait = vm.RunUntilWaitOrHalt(500);
        Assert.Equal(VmStatus.WaitingForChoice, wait.Status);
        Assert.Single(vm.Buttons);

        var invoked = vm.UseInventoryItem("Топор", "Рубить дерево");
        Assert.True(invoked);

        var useRun = vm.RunUntilWaitOrHalt(500);
        Assert.Equal(VmStatus.WaitingForChoice, useRun.Status);
        Assert.True(vm.Context.Variables.TryGet("used", out var used));
        Assert.Equal("1", used.StringValue);

        var chosen = vm.ChooseButton(vm.Buttons[0].Id);
        Assert.True(chosen);
        var final = vm.RunUntilHalt(500);
        Assert.Equal(VmStatus.Halted, final.Status);
        Assert.True(vm.Context.Variables.TryGet("nexted", out var nexted));
        Assert.Equal("1", nexted.StringValue);
    }

    [Fact]
    public void Vm_ShouldInvokeArbitraryUseLabel()
    {
        const string source = """
                              :start
                              btn next,Go
                              end
                              :use_inv_Запись
                              instr saved="1"
                              end
                              :next
                              end
                              """;

        var vm = BuildVm(source);
        var wait = vm.RunUntilWaitOrHalt(500);
        Assert.Equal(VmStatus.WaitingForChoice, wait.Status);

        var invoked = vm.InvokeUseLabel("use_inv_Запись");
        Assert.True(invoked);
        var useRun = vm.RunUntilWaitOrHalt(500);
        Assert.Equal(VmStatus.WaitingForChoice, useRun.Status);
        Assert.True(vm.Context.Variables.TryGet("saved", out var saved));
        Assert.Equal("1", saved.StringValue);
    }

    private static VirtualMachine BuildVm(string source)
    {
        var parse = Parser.Parse(source);
        var ir = Compiler.Compile(parse.Program, parse.Diagnostics);
        return new VirtualMachine(ir);
    }
}
