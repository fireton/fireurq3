using System.Text;
using System.Text.RegularExpressions;

namespace Urql.Core.IO;

public static class UrqlTextLoader
{
    private sealed record Candidate(
        string Name,
        Encoding Encoding,
        int Priority);

    static UrqlTextLoader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static UrqlTextLoadResult LoadFile(string path, UrqlTextLoadOptions? options = null)
    {
        var bytes = File.ReadAllBytes(path);
        return Decode(bytes, options);
    }

    public static UrqlTextLoadResult Decode(byte[] bytes, UrqlTextLoadOptions? options = null)
    {
        options ??= new UrqlTextLoadOptions();
        var requested = (options.EncodingName ?? "auto").Trim();

        if (!requested.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var explicitEncoding = GetEncodingByName(requested);
            var text = explicitEncoding.GetString(bytes);
            return new UrqlTextLoadResult(text, NormalizeName(requested), 1.0, false);
        }

        if (TryDecodeByBom(bytes, out var bomResult))
        {
            return bomResult;
        }

        var candidates = new List<(string Name, string Text, double Score)>
        {
            TryUtf8Strict(bytes, out var utf8Text)
                ? ("utf-8", utf8Text!, ScoreText(utf8Text!, "utf-8"))
                : default
        };

        var cp1251 = Encoding.GetEncoding(1251);
        var cp866 = Encoding.GetEncoding(866);
        var koi8r = Encoding.GetEncoding("koi8-r");
        var text1251 = cp1251.GetString(bytes);
        var text866 = cp866.GetString(bytes);
        var textKoi8 = koi8r.GetString(bytes);
        candidates.Add(("cp1251", text1251, ScoreText(text1251, "cp1251")));
        candidates.Add(("cp866", text866, ScoreText(text866, "cp866")));
        candidates.Add(("koi8-r", textKoi8, ScoreText(textKoi8, "koi8-r")));

        var ranked = candidates
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .OrderByDescending(c => c.Score)
            .ThenBy(c => PriorityOf(c.Name))
            .ToList();

        var best = ranked.First();

        // If UTF-8 decoding is valid, prefer it when scores are close.
        var utf8Candidate = ranked.FirstOrDefault(c => c.Name == "utf-8");
        if (!string.IsNullOrEmpty(utf8Candidate.Name))
        {
            var nonAsciiBytes = bytes.Count(b => b >= 0x80);
            var margin = nonAsciiBytes > 0 ? 0.75 : 0.15;
            if (utf8Candidate.Score >= best.Score - margin)
            {
                best = utf8Candidate;
            }
        }

        var confidence = ComputeConfidence(candidates.Where(c => !string.IsNullOrEmpty(c.Name)).ToList(), best.Score);
        return new UrqlTextLoadResult(best.Text, best.Name, confidence, false);
    }

    private static bool TryUtf8Strict(byte[] bytes, out string? text)
    {
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            text = utf8.GetString(bytes);
            return true;
        }
        catch
        {
            text = null;
            return false;
        }
    }

    private static bool TryDecodeByBom(byte[] bytes, out UrqlTextLoadResult result)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            result = new UrqlTextLoadResult(text, "utf-8", 1.0, true);
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            var text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            result = new UrqlTextLoadResult(text, "utf-16le", 1.0, true);
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            var text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            result = new UrqlTextLoadResult(text, "utf-16be", 1.0, true);
            return true;
        }

        result = new UrqlTextLoadResult(string.Empty, "unknown", 0, false);
        return false;
    }

    private static Encoding GetEncodingByName(string name)
    {
        return NormalizeName(name) switch
        {
            "utf-8" => Encoding.UTF8,
            "cp1251" => Encoding.GetEncoding(1251),
            "cp866" => Encoding.GetEncoding(866),
            "koi8-r" => Encoding.GetEncoding("koi8-r"),
            _ => throw new ArgumentException($"Unsupported encoding '{name}'. Allowed: auto, utf-8, cp1251, cp866, koi8-r.")
        };
    }

    private static string NormalizeName(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "utf8" => "utf-8",
            "windows-1251" => "cp1251",
            "1251" => "cp1251",
            "cp1251" => "cp1251",
            "ibm866" => "cp866",
            "866" => "cp866",
            "cp866" => "cp866",
            "koi8r" => "koi8-r",
            "koi8-r" => "koi8-r",
            "auto" => "auto",
            "utf-16" => "utf-16le",
            _ => name.Trim().ToLowerInvariant()
        };
    }

    private static int PriorityOf(string name)
    {
        return name switch
        {
            "utf-8" => 0,
            "cp1251" => 1,
            "cp866" => 2,
            "koi8-r" => 3,
            _ => 9
        };
    }

    private static double ScoreText(string text, string encodingName)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var printable = 0;
        var control = 0;
        var cyrillic = 0;
        var boxDrawing = 0;
        var replacement = 0;

        foreach (var ch in text)
        {
            if (ch == '\uFFFD')
            {
                replacement++;
            }

            if (ch is '\r' or '\n' or '\t' || !char.IsControl(ch))
            {
                printable++;
            }
            else
            {
                control++;
            }

            if (ch is >= '\u0400' and <= '\u052F')
            {
                cyrillic++;
            }

            if (ch is >= '\u2500' and <= '\u257F')
            {
                boxDrawing++;
            }
        }

        var len = Math.Max(1.0, text.Length);
        var printableRatio = printable / len;
        var controlRatio = control / len;
        var cyrillicRatio = cyrillic / len;
        var boxRatio = boxDrawing / len;
        var replacementRatio = replacement / len;

        var keywords = Regex.IsMatch(
            text,
            @"(?im)\b(end|if|then|else|goto|proc|btn|instr|print|println|pln)\b")
            ? 0.35
            : 0.0;

        var baseScore = printableRatio * 2.0
                        + cyrillicRatio * 1.4
                        + keywords
                        - controlRatio * 2.0
                        - boxRatio * 4.0
                        - replacementRatio * 3.0;

        if (encodingName == "utf-8")
        {
            baseScore += 0.1;
        }

        return baseScore;
    }

    private static double ComputeConfidence(IReadOnlyList<(string Name, string Text, double Score)> candidates, double bestScore)
    {
        if (candidates.Count == 1)
        {
            return 1.0;
        }

        var second = candidates
            .Select(c => c.Score)
            .OrderByDescending(s => s)
            .Skip(1)
            .FirstOrDefault();

        var gap = Math.Max(0, bestScore - second);
        return Math.Min(1.0, 0.5 + gap);
    }
}
