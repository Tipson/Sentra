using Sentra.Infrastructure.Crawling;
using Xunit.Abstractions;

namespace Sentra.Tests;

public class FileCrawlerTests
{
    private readonly ITestOutputHelper _output;

    public FileCrawlerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FindFiles_ShouldReturnFiles()
    {
        var testPath = @"C:\test";
        var files = FileCrawler.FindFiles(testPath);

        _output.WriteLine($"Найдено файлов: {files.Count}");
        foreach (var file in files.Take(5))
        {
            _output.WriteLine(file);
        }

        Assert.NotNull(files);
        Assert.True(files.Count > 0, "Ожидались хотя бы какие-то файлы");
    }
}