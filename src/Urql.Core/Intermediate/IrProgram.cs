using Urql.Core.Diagnostics;

namespace Urql.Core.Intermediate;

public sealed record IrProgram(
    IReadOnlyList<IrInstruction> Instructions,
    IReadOnlyDictionary<string, int> LabelMap,
    IReadOnlyList<Diagnostic> Diagnostics);
