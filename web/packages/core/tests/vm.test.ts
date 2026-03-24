import { describe, expect, it } from "vitest";
import { Compiler } from "../src/compiler.js";
import { Parser } from "../src/parser.js";
import { EvalContext, valueKind } from "../src/runtime.js";
import { VirtualMachine, vmStatus } from "../src/vm.js";

describe("VirtualMachine", () => {
  it("executes assignment and if", () => {
    const vm = buildVm(`
:start
a=1+2*3
if a=7 then instr s="ok" else instr s="bad"
end
`.trim());

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    const s = vm.context.variables.tryGet("s");
    expect(s?.kind).toBe(valueKind.string);
    expect(s?.stringValue).toBe("ok");
  });

  it("handles goto", () => {
    const vm = buildVm(`
:start
goto target
instr s="bad"
:target
instr s="ok"
end
`.trim());

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    expect(vm.context.variables.tryGet("s")?.stringValue).toBe("ok");
  });

  it("handles proc and return on end", () => {
    const vm = buildVm(`
:start
proc sub
instr done="yes"
end
:sub
instr x="1"
end
`.trim());

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    expect(vm.context.variables.tryGet("x")?.stringValue).toBe("1");
    expect(vm.context.variables.tryGet("done")?.stringValue).toBe("yes");
  });

  it("pauses at top-level end when buttons exist", () => {
    const vm = buildVm(`
:start
btn next,Go
end
:next
instr s="ok"
end
`.trim());

    const run1 = vm.runUntilWaitOrHalt(500);
    expect(run1.status).toBe(vmStatus.waitingForChoice);
    expect(vm.buttons).toHaveLength(1);
    expect(vm.buttons[0]?.caption).toBe("Go");
    expect(vm.buttons[0]?.target.toLowerCase()).toBe("next");

    expect(vm.chooseButton(vm.buttons[0]!.id)).toBe(true);
    const run2 = vm.runUntilHalt(500);
    expect(run2.status).toBe(vmStatus.halted);
    expect(vm.context.variables.tryGet("s")?.stringValue).toBe("ok");
  });

  it("does not wait on btn until top-level end", () => {
    const vm = buildVm(`
:start
btn next,Go
instr x="set-before-end"
end
:next
end
`.trim());

    expect(vm.step()).toBe(true);
    expect(vm.status).toBe(vmStatus.running);
    expect(vm.buttons).toHaveLength(1);

    expect(vm.step()).toBe(true);
    expect(vm.status).toBe(vmStatus.running);
    expect(vm.context.variables.tryGet("x")?.stringValue).toBe("set-before-end");

    expect(vm.step()).toBe(true);
    expect(vm.status).toBe(vmStatus.waitingForChoice);
  });

  it("respects instruction limit", () => {
    const vm = buildVm(`
:start
goto start
`.trim());

    const run = vm.runUntilHalt(50);

    expect(run.hitInstructionLimit).toBe(true);
    expect(run.status).toBe(vmStatus.running);
  });

  it("handles inventory commands", () => {
    const vm = buildVm(`
:start
inv+ гайка
inv+ 2,гайка
inv- гайка
inv- 1,гайка
end
`.trim());

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    expect(vm.context.inventory.getCount("гайка")).toBe(1);
  });

  it("supports inventory bridge read and write", () => {
    const vm = buildVm(`
:start
inv+ 3,монета
x=inv_монета
inv_монета=5
y=inv_монета
end
`.trim());

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    expect(vm.context.variables.tryGet("x")?.numberValue).toBe(3);
    expect(vm.context.variables.tryGet("y")?.numberValue).toBe(5);
    expect(vm.context.inventory.getCount("монета")).toBe(5);
  });

  it("handles perkill and invkill", () => {
    const vm = buildVm(`
:start
x=10
inv+ 3,монета
perkill
invkill монета
end
`.trim());

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    expect(vm.context.variables.tryGet("x")).toBeNull();
    expect(vm.context.inventory.getCount("монета")).toBe(0);
  });

  it("supports variable names with spaces", () => {
    const vm = buildVm(`
:start
мы поели = 1
if мы поели=1 then instr ok="yes" else instr ok="no"
end
`.trim());

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    expect(vm.context.variables.tryGet("мы поели")?.numberValue).toBe(1);
    expect(vm.context.variables.tryGet("ok")?.stringValue).toBe("yes");
  });

  it("treats bare inventory items as conditions", () => {
    const vm = buildVm(`
:start
inv+ яблоко
if яблоко then instr has="yes" else instr has="no"
inv- яблоко
if not яблоко then instr gone="yes" else instr gone="no"
end
`.trim());

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    expect(vm.context.variables.tryGet("has")?.stringValue).toBe("yes");
    expect(vm.context.variables.tryGet("gone")?.stringValue).toBe("yes");
  });

  it("invokes use labels and returns to waiting state", () => {
    const vm = buildVm(`
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
`.trim());

    const wait = vm.runUntilWaitOrHalt(500);
    expect(wait.status).toBe(vmStatus.waitingForChoice);
    expect(vm.buttons).toHaveLength(1);

    expect(vm.useInventoryItem("Топор", "Рубить дерево")).toBe(true);
    const useRun = vm.runUntilWaitOrHalt(500);
    expect(useRun.status).toBe(vmStatus.waitingForChoice);
    expect(vm.context.variables.tryGet("used")?.stringValue).toBe("1");

    expect(vm.chooseButton(vm.buttons[0]!.id)).toBe(true);
    const final = vm.runUntilHalt(500);
    expect(final.status).toBe(vmStatus.halted);
    expect(vm.context.variables.tryGet("nexted")?.stringValue).toBe("1");
  });

  it("expands legacy interpolation in print and buttons", () => {
    const vm = buildVm(`
:start
instr nextLoc="next"
pln Hello#$world#/$##33$
btn #%nextLoc$,Go#$##33$
end
:next
end
`.trim());

    const run = vm.runUntilWaitOrHalt(500);

    expect(run.status).toBe(vmStatus.waitingForChoice);
    expect(vm.outputText).toBe("Hello world\n!\n");
    expect(vm.buttons).toHaveLength(1);
    expect(vm.buttons[0]?.target.toLowerCase()).toBe("next");
    expect(vm.buttons[0]?.caption).toBe("Go !");
  });

  it("uses configured char code encodings for interpolation", () => {
    const parse = Parser.parse(`
:start
pln ##192$
end
`.trim());
    const ir = Compiler.compile(parse.program, parse.diagnostics);
    const context = new EvalContext();
    context.charCodeEncodingName = "cp866";
    const vm = new VirtualMachine(ir, context);

    const run = vm.runUntilHalt(500);

    expect(run.status).toBe(vmStatus.halted);
    expect(vm.outputText).toBe("└\n");
  });
});

function buildVm(source: string): VirtualMachine {
  const parse = Parser.parse(source);
  const ir = Compiler.compile(parse.program, parse.diagnostics);
  return new VirtualMachine(ir);
}
