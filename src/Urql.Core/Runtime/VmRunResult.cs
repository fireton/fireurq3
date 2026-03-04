namespace Urql.Core.Runtime;

public sealed record VmRunResult(
    VmStatus Status,
    int ExecutedInstructions,
    bool HitInstructionLimit);
