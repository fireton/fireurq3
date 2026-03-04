namespace Urql.Core.IO;

public sealed record UrqlTextLoadResult(
    string Text,
    string EncodingName,
    double Confidence,
    bool BomDetected);
