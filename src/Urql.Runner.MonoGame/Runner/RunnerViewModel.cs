using Urql.Core.Diagnostics;
using Urql.Core.Runtime;

namespace Urql.Runner.MonoGame.Runner;

public sealed record RunnerViewModel(
    IReadOnlyList<TranscriptEntry> Transcript,
    IReadOnlyList<ButtonAction> ActiveChoices,
    VmStatus Status,
    bool HitInstructionLimit,
    IReadOnlyList<Diagnostic> Diagnostics);
