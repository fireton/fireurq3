namespace Urql.Runner.MonoGame.Runner;

internal sealed class TranscriptBuffer
{
    private readonly List<TranscriptEntry> _entries = [];

    public IReadOnlyList<TranscriptEntry> Entries => _entries;

    public void Clear() => _entries.Clear();

    public void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _entries.Add(new TranscriptEntry(TranscriptEntryKind.Output, text, DateTime.UtcNow.Ticks));
    }

    public void AppendChoiceEcho(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _entries.Add(new TranscriptEntry(TranscriptEntryKind.ChoiceEcho, text, DateTime.UtcNow.Ticks));
    }

    public void AppendSystem(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _entries.Add(new TranscriptEntry(TranscriptEntryKind.System, text, DateTime.UtcNow.Ticks));
    }
}
