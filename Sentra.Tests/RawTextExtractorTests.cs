using System.Text;
using Sentra.Infrastructure.Crawling;

namespace Sentra.Tests;

public class RawTextExtractorTests
{
    [Fact]
    public void TryExtract_ShouldReadUtf8TextFile()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "Привет, мир!", Encoding.UTF8);

        var extractor = new RawTextExtractor();
        var result = extractor.TryExtract(path);

        Assert.Contains("Привет", result);
        File.Delete(path);
    }

    [Fact]
    public void TryExtract_ShouldRead1251TextFile()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "Тестовая строка", Encoding.GetEncoding(1251));

        var extractor = new RawTextExtractor();
        var result = extractor.TryExtract(path);

        Assert.Contains("Тестовая", result);
        File.Delete(path);
    }

    [Fact]
    public void TryExtract_ShouldFallbackToBinaryFiltering()
    {
        var path = Path.GetTempFileName();
        var binary = "Header\x00\x01\x02Text\x03\x04Footer"u8.ToArray();
        File.WriteAllBytes(path, binary);

        var extractor = new RawTextExtractor();
        var result = extractor.TryExtract(path);

        Assert.Contains("Header", result);
        Assert.Contains("Text", result);
        Assert.Contains("Footer", result);
        File.Delete(path);
    }
}