using System.Text;
using Urql.Core.Diagnostics;

namespace Urql.Core.Syntax.Templates;

public static class InterpolationTemplateParser
{
    private sealed class Frame
    {
        public required int OpenIndex { get; init; }
        public List<TemplatePart> Parts { get; } = [];
        public StringBuilder Literal { get; } = new();
    }

    public static InterpolationTemplate Parse(
        string input,
        DiagnosticBag diagnostics,
        out bool hadInterpolation)
    {
        hadInterpolation = false;
        var positionMap = BuildPositionMap(input);

        var stack = new Stack<Frame>();
        stack.Push(new Frame { OpenIndex = -1 });

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            var current = stack.Peek();

            if (c == '#')
            {
                hadInterpolation = true;
                FlushLiteral(current);
                stack.Push(new Frame { OpenIndex = i });
                continue;
            }

            if (c == '$')
            {
                if (stack.Count == 1)
                {
                    diagnostics.ReportError(
                        DiagnosticCode.UnexpectedCharacter,
                        "Unmatched interpolation close delimiter '$'.",
                        SpanAt(positionMap, i, i));
                    current.Literal.Append(c);
                    continue;
                }

                hadInterpolation = true;
                var finished = stack.Pop();
                FlushLiteral(finished);
                var part = new EmbeddedExpressionPart(
                    new InterpolationTemplate(finished.Parts),
                    SpanAt(positionMap, finished.OpenIndex, i));
                stack.Peek().Parts.Add(part);
                continue;
            }

            current.Literal.Append(c);
        }

        while (stack.Count > 1)
        {
            var unclosed = stack.Pop();
            diagnostics.ReportError(
                DiagnosticCode.UnexpectedCharacter,
                "Unmatched interpolation open delimiter '#'.",
                SpanAt(positionMap, unclosed.OpenIndex, unclosed.OpenIndex));
        }

        var root = stack.Pop();
        FlushLiteral(root);
        return new InterpolationTemplate(root.Parts);
    }

    private static void FlushLiteral(Frame frame)
    {
        if (frame.Literal.Length == 0)
        {
            return;
        }

        frame.Parts.Add(new LiteralTextPart(frame.Literal.ToString()));
        frame.Literal.Clear();
    }

    private static SourceSpan SpanAt(IReadOnlyList<SourcePosition> positions, int startIndex, int endIndex)
    {
        var start = startIndex >= 0 && startIndex < positions.Count ? positions[startIndex] : new SourcePosition(1, 1);
        var end = endIndex >= 0 && endIndex < positions.Count ? positions[endIndex] : start;
        return new SourceSpan(start, end);
    }

    private static List<SourcePosition> BuildPositionMap(string input)
    {
        var map = new List<SourcePosition>(input.Length);
        var line = 1;
        var column = 1;
        foreach (var c in input)
        {
            map.Add(new SourcePosition(line, column));
            if (c == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return map;
    }
}
