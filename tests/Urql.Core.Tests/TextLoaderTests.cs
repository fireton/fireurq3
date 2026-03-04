using System.Text;
using Urql.Core.IO;

namespace Urql.Core.Tests;

public sealed class TextLoaderTests
{
    static TextLoaderTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void Decode_ShouldRespectExplicitCp1251()
    {
        const string source = ":старт\npln Привет\nend\n";
        var bytes = Encoding.GetEncoding(1251).GetBytes(source);

        var result = UrqlTextLoader.Decode(bytes, new UrqlTextLoadOptions("cp1251"));

        Assert.Equal("cp1251", result.EncodingName);
        Assert.Equal(source, result.Text);
    }

    [Fact]
    public void Decode_ShouldRespectExplicitCp866()
    {
        const string source = ":старт\npln Привет\nend\n";
        var bytes = Encoding.GetEncoding(866).GetBytes(source);

        var result = UrqlTextLoader.Decode(bytes, new UrqlTextLoadOptions("cp866"));

        Assert.Equal("cp866", result.EncodingName);
        Assert.Equal(source, result.Text);
    }

    [Fact]
    public void Decode_Auto_ShouldPickUtf8ForValidUtf8()
    {
        const string source = ":start\npln Привет\nend\n";
        var bytes = new UTF8Encoding(false).GetBytes(source);

        var result = UrqlTextLoader.Decode(bytes, new UrqlTextLoadOptions("auto"));

        Assert.Equal("utf-8", result.EncodingName);
        Assert.Equal(source, result.Text);
    }

    [Fact]
    public void Decode_Auto_ShouldPickCp1251()
    {
        const string source = ":старт\npln Привет, мир!\nend\n";
        var bytes = Encoding.GetEncoding(1251).GetBytes(source);

        var result = UrqlTextLoader.Decode(bytes, new UrqlTextLoadOptions("auto"));

        Assert.Equal("cp1251", result.EncodingName);
        Assert.Equal(source, result.Text);
    }

    [Fact]
    public void Decode_Auto_ShouldPickCp866()
    {
        const string source = ":старт\npln Привет, мир!\nend\n";
        var bytes = Encoding.GetEncoding(866).GetBytes(source);

        var result = UrqlTextLoader.Decode(bytes, new UrqlTextLoadOptions("auto"));

        Assert.Equal("cp866", result.EncodingName);
        Assert.Equal(source, result.Text);
    }

    [Fact]
    public void Decode_ShouldRejectUnsupportedEncoding()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            UrqlTextLoader.Decode(Array.Empty<byte>(), new UrqlTextLoadOptions("windows-1252")));
        Assert.Contains("Unsupported encoding", ex.Message);
    }

    [Fact]
    public void Decode_ShouldRespectExplicitKoi8R()
    {
        const string source = ":старт\npln Привет\nend\n";
        var bytes = Encoding.GetEncoding("koi8-r").GetBytes(source);

        var result = UrqlTextLoader.Decode(bytes, new UrqlTextLoadOptions("koi8-r"));

        Assert.Equal("koi8-r", result.EncodingName);
        Assert.Equal(source, result.Text);
    }

    [Fact]
    public void Decode_Auto_ShouldHandleKoi8RBytesWithoutThrowing()
    {
        const string source = ":старт\npln Привет, мир!\nend\n";
        var bytes = Encoding.GetEncoding("koi8-r").GetBytes(source);

        var result = UrqlTextLoader.Decode(bytes, new UrqlTextLoadOptions("auto"));

        Assert.NotNull(result.Text);
        Assert.NotEqual(string.Empty, result.EncodingName);
    }
}
