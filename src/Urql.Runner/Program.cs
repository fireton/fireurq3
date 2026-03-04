using Urql.Core;
using Urql.Core.IO;
using Urql.Core.Intermediate;
using Urql.Core.Runtime;
using Urql.Core.Syntax;

Console.WriteLine($"FireURQ3 runner scaffold. Core version: {AssemblyInfo.Version}");

if (args.Length == 0)
{
    return;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.WriteLine($"Input file not found: {path}");
    return;
}

var load = UrqlTextLoader.LoadFile(path, new UrqlTextLoadOptions("auto"));
Console.WriteLine($"Detected encoding: {load.EncodingName} (confidence {load.Confidence:F2}, bom={load.BomDetected})");
var source = load.Text;
var lex = Lexer.Lex(source);
Console.WriteLine($"Lexed tokens: {lex.Tokens.Count}, diagnostics: {lex.Diagnostics.Count}");
var parse = Parser.Parse(source, new ParserOptions(CompatibilityMode.DosUrq));
Console.WriteLine($"Parsed lines: {parse.Program.Lines.Count}, diagnostics: {parse.Diagnostics.Count}");
var ir = Compiler.Compile(parse.Program, parse.Diagnostics);
var vm = new VirtualMachine(ir);
var run = vm.RunUntilWaitOrHalt(10_000);
Console.WriteLine($"VM status: {run.Status}, executed: {run.ExecutedInstructions}, output-len: {vm.OutputText.Length}, buttons: {vm.Buttons.Count}");
