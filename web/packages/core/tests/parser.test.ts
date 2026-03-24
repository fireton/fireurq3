import { describe, expect, it } from "vitest";
import { parseDiagnosticCode, Parser } from "../src/parser.js";
import { tokenKind } from "../src/token.js";

describe("Parser", () => {
  it("builds label and statement chain", () => {
    const source = `
:start
a=1 & instr s="x" & p "ok" & pln "done"
end
`.trim();

    const parse = Parser.parse(source);

    expect(parse.diagnostics.filter((item) => item.severity === "error")).toHaveLength(0);
    expect(parse.program.lines).toHaveLength(3);
    expect(parse.program.lines[0]?.label?.name).toBe("start");
    expect(parse.program.lines[1]?.statements).toHaveLength(4);
    expect(parse.program.lines[1]?.statements[0]?.kind).toBe("AssignmentStatement");
    expect(parse.program.lines[1]?.statements[1]?.kind).toBe("InstrStatement");
    expect(parse.program.lines[1]?.statements[2]?.kind).toBe("PrintStatement");
    expect(parse.program.lines[1]?.statements[3]?.kind).toBe("PrintStatement");
    expect(parse.program.lines[2]?.statements[0]?.kind).toBe("EndStatement");
  });

  it("builds if then else chains", () => {
    const parse = Parser.parse('if a=1 then p "x" & btn n,Go else goto end');
    const line = parse.program.lines[0]!;
    const ifStatement = line.statements[0];

    expect(ifStatement?.kind).toBe("IfStatement");
    expect(ifStatement && "thenStatements" in ifStatement ? ifStatement.thenStatements.length : 0).toBe(2);
    expect(ifStatement && "elseStatements" in ifStatement ? ifStatement.elseStatements?.length : 0).toBe(1);
  });

  it("respects expression precedence", () => {
    const parse = Parser.parse("a=1+2*3");
    const assignment = parse.program.lines[0]!.statements[0];

    expect(assignment?.kind).toBe("AssignmentStatement");
    if (!assignment || assignment.kind !== "AssignmentStatement") {
      return;
    }

    expect(assignment.expression.kind).toBe("BinaryExpression");
    if (assignment.expression.kind !== "BinaryExpression") {
      return;
    }

    expect(assignment.expression.operator).toBe(tokenKind.plus);
    expect(assignment.expression.right.kind).toBe("BinaryExpression");
    if (assignment.expression.right.kind !== "BinaryExpression") {
      return;
    }

    expect(assignment.expression.right.operator).toBe(tokenKind.star);
  });

  it("treats trailing symbols as raw print tail", () => {
    const parse = Parser.parse(`
:a
p "ok" ????
end
`.trim());

    const print = parse.program.lines[1]!.statements[0];

    expect(print?.kind).toBe("PrintStatement");
    if (!print || print.kind !== "PrintStatement") {
      return;
    }

    expect(print.textExpression.kind).toBe("RawTextExpression");
    if (print.textExpression.kind !== "RawTextExpression") {
      return;
    }

    expect(print.textExpression.rawText).toBe("ok????");
  });

  it("parses goto proc and btn", () => {
    const parse = Parser.parse(`
proc start
btn next,Go
goto next
`.trim());

    expect(parse.program.lines[0]!.statements[0]?.kind).toBe("ProcStatement");
    expect(parse.program.lines[1]!.statements[0]?.kind).toBe("BtnStatement");
    expect(parse.program.lines[2]!.statements[0]?.kind).toBe("GotoStatement");
  });

  it("parses print raw tail in dos mode", () => {
    const parse = Parser.parse("pln Привет, мир. Пока!");
    const print = parse.program.lines[0]!.statements[0];

    expect(print?.kind).toBe("PrintStatement");
    if (!print || print.kind !== "PrintStatement") {
      return;
    }

    expect(print.textExpression.kind).toBe("RawTextExpression");
    if (print.textExpression.kind !== "RawTextExpression") {
      return;
    }

    expect(print.textExpression.rawText).toBe("Привет, мир. Пока!");
  });

  it("parses invkill command", () => {
    const parse = Parser.parse("invkill");

    expect(parse.program.lines[0]!.statements[0]?.kind).toBe("InvkillStatement");
    expect(parse.diagnostics.some((item) => item.code === parseDiagnosticCode.unknownCommand)).toBe(
      false
    );
  });

  it("parses inventory delta commands", () => {
    const parse = Parser.parse(`
inv+ гайка
inv- 2,гайка
`.trim());

    expect(parse.program.lines).toHaveLength(2);
    expect(parse.program.lines[0]!.statements[0]?.kind).toBe("InvAddStatement");
    expect(parse.program.lines[1]!.statements[0]?.kind).toBe("InvRemoveStatement");

    const remove = parse.program.lines[1]!.statements[0];
    expect(remove && "countExpression" in remove ? remove.countExpression : null).not.toBeNull();
  });

  it("supports variable names with spaces", () => {
    const parse = Parser.parse("мы поели = 1");
    const assignment = parse.program.lines[0]!.statements[0];

    expect(assignment?.kind).toBe("AssignmentStatement");
    if (!assignment || assignment.kind !== "AssignmentStatement") {
      return;
    }

    expect(assignment.name).toBe("мы поели");
  });

  it("supports labels with spaces", () => {
    const parse = Parser.parse(`
:use_Топор_Рубить дерево
end
`.trim());

    expect(parse.program.lines[0]?.label?.name).toBe("use_Топор_Рубить дерево");
    expect(parse.program.lines[1]!.statements[0]?.kind).toBe("EndStatement");
  });

  it("treats percent macro as unknown no-op warning", () => {
    const parse = Parser.parse("%include inc\\more.qst");
    const statement = parse.program.lines[0]!.statements[0];

    expect(statement?.kind).toBe("UnknownCommandStatement");
    if (!statement || statement.kind !== "UnknownCommandStatement") {
      return;
    }

    expect(statement.commandName.toLowerCase()).toBe("%include");
    expect(
      parse.diagnostics.some(
        (item) => item.code === parseDiagnosticCode.unknownCommand && item.severity === "warning"
      )
    ).toBe(true);
  });

  it("reports unknown commands as error in strict mode", () => {
    const parse = Parser.parse("pause 1000", {
      allowUnknownCommands: false
    });

    expect(
      parse.diagnostics.some(
        (item) => item.code === parseDiagnosticCode.unknownCommand && item.severity === "error"
      )
    ).toBe(true);
  });
});
