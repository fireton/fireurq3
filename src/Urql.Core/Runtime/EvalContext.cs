using System.Globalization;
using System.Text;
using Urql.Core.Diagnostics;

namespace Urql.Core.Runtime;

public sealed class EvalContext
{
    static EvalContext()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private Encoding? _charCodeEncoding;
    private string _charCodeEncodingName = "cp1251";

    public VariableStore Variables { get; } = new();
    public InventoryStore Inventory { get; } = new();
    public DiagnosticBag Diagnostics { get; } = new();
    public string CharCodeEncodingName
    {
        get => _charCodeEncodingName;
        set
        {
            _charCodeEncodingName = string.IsNullOrWhiteSpace(value) ? "cp1251" : value.Trim();
            _charCodeEncoding = null;
        }
    }

    public double ToNumber(UrqlValue value)
    {
        return value.Kind switch
        {
            ValueKind.Number => value.NumberValue,
            ValueKind.String => value.StringValue.Length,
            ValueKind.Bool => value.BoolValue ? 1d : 0d,
            _ => 0d
        };
    }

    public string ToUrqlString(UrqlValue value)
    {
        return value.Kind switch
        {
            ValueKind.String => value.StringValue,
            ValueKind.Number => string.Empty,
            ValueKind.Bool => value.BoolValue ? "1" : "0",
            _ => string.Empty
        };
    }

    public string ToInterpolationString(UrqlValue value)
    {
        return value.Kind switch
        {
            ValueKind.String => value.StringValue,
            ValueKind.Number => value.NumberValue.ToString("G17", CultureInfo.InvariantCulture),
            ValueKind.Bool => value.BoolValue ? "1" : "0",
            _ => string.Empty
        };
    }

    public bool ToBool(UrqlValue value)
    {
        return value.Kind switch
        {
            ValueKind.Number => Math.Abs(value.NumberValue) > 0d,
            ValueKind.String => !string.IsNullOrEmpty(value.StringValue),
            ValueKind.Bool => value.BoolValue,
            _ => false
        };
    }

    public string DecodeByteChar(int code)
    {
        if (UsesUnicodeCharCodes())
        {
            if (code is < 0 or > 0x10FFFF)
            {
                return string.Empty;
            }

            if (code is >= 0xD800 and <= 0xDFFF)
            {
                return string.Empty;
            }

            return char.ConvertFromUtf32(code);
        }

        if (code is < 0 or > 255)
        {
            return string.Empty;
        }

        var encoding = GetCharCodeEncoding();
        return encoding.GetString([(byte)code]);
    }

    public bool UsesUnicodeCharCodes()
    {
        var normalized = CharCodeEncodingName.ToLowerInvariant();
        return normalized is "utf-8" or "utf8" or "utf-16" or "utf-16le" or "utf-16be" or "unicode";
    }

    private Encoding GetCharCodeEncoding()
    {
        if (_charCodeEncoding is not null)
        {
            return _charCodeEncoding;
        }

        var normalized = CharCodeEncodingName.ToLowerInvariant();
        _charCodeEncoding = normalized switch
        {
            "cp1251" or "windows-1251" or "1251" => Encoding.GetEncoding(1251),
            "cp866" or "866" or "ibm866" => Encoding.GetEncoding(866),
            "koi8-r" or "koi8r" => Encoding.GetEncoding("koi8-r"),
            "utf-8" or "utf8" => Encoding.UTF8,
            _ => Encoding.GetEncoding(1251)
        };

        return _charCodeEncoding;
    }
}
