using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class PobbInBuildImporterTests
{
    [Theory]
    [InlineData("https://pobb.in/qO1_QpuQLeDd", "https://pobb.in/qO1_QpuQLeDd/raw")]
    [InlineData("https://www.pobb.in/qO1_QpuQLeDd/", "https://pobb.in/qO1_QpuQLeDd/raw")]
    [InlineData("http://pobb.in/qO1_QpuQLeDd?utm_source=test", "https://pobb.in/qO1_QpuQLeDd/raw")]
    [InlineData("https://pobb.in/qO1_QpuQLeDd/raw", "https://pobb.in/qO1_QpuQLeDd/raw")]
    [InlineData("https://pobb.in/u/PaintMaster/abc_123", "https://pobb.in/u/PaintMaster/abc_123/raw")]
    [InlineData("https://pobb.in/u/PaintMaster/abc_123/raw", "https://pobb.in/u/PaintMaster/abc_123/raw")]
    public void TryCreateRawUrlAcceptsSupportedPobbInUrls(string input, string expected)
    {
        var ok = PobbInBuildImporter.TryCreateRawUrl(input, out var rawUrl);

        Assert.True(ok);
        Assert.Equal(expected, rawUrl.ToString());
    }

    [Theory]
    [InlineData("https://example.com/qO1_QpuQLeDd")]
    [InlineData("https://pobb.in/")]
    [InlineData("https://pobb.in/u/PaintMaster")]
    [InlineData("https://pobb.in/u/PaintMaster/abc_123/extra")]
    [InlineData("not a url")]
    public void TryCreateRawUrlRejectsUnsupportedUrls(string input)
    {
        var ok = PobbInBuildImporter.TryCreateRawUrl(input, out _);

        Assert.False(ok);
    }
}
