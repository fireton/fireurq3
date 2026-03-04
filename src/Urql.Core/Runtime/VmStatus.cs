namespace Urql.Core.Runtime;

public enum VmStatus
{
    Running = 0,
    WaitingForChoice = 1,
    Halted = 2,
    Faulted = 3
}
