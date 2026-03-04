using Urql.Core.Syntax;

namespace Urql.Core.Diagnostics;

public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    SourceSpan Span);
