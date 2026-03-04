using Urql.Core.Syntax;

namespace Urql.Core.Diagnostics;

public sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _items = [];

    public IReadOnlyList<Diagnostic> Items => _items;

    public bool HasErrors => _items.Any(x => x.Severity == DiagnosticSeverity.Error);

    public void Report(string code, DiagnosticSeverity severity, string message, SourceSpan span)
    {
        _items.Add(new Diagnostic(code, severity, message, span));
    }

    public void ReportError(string code, string message, SourceSpan span)
    {
        Report(code, DiagnosticSeverity.Error, message, span);
    }

    public void AddRange(IEnumerable<Diagnostic> diagnostics)
    {
        _items.AddRange(diagnostics);
    }

    public void TrimToCount(int count)
    {
        if (count < 0)
        {
            count = 0;
        }

        if (count >= _items.Count)
        {
            return;
        }

        _items.RemoveRange(count, _items.Count - count);
    }
}
