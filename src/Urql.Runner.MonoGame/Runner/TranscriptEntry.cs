namespace Urql.Runner.MonoGame.Runner;

public enum TranscriptEntryKind
{
    Output,
    ChoiceEcho,
    System
}

public sealed record TranscriptEntry(
    TranscriptEntryKind Kind,
    string Text,
    long TimestampTicks);
