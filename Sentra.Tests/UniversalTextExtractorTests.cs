// Юнит-тесты для UniversalTextExtractor с перехватом ошибок и логом

using Sentra.Infrastructure.Crawling;

namespace Sentra.Tests;

public class UniversalTextExtractorTests
{
    private readonly string _testFolder = Path.Combine(Path.GetTempPath(), "SentraTests");

    [Fact]
    public void ExtractText_TxtFile_ReturnsContent()
    {
        try
        {
            Directory.CreateDirectory(_testFolder);
            string path = Path.Combine(_testFolder, "test.txt");
            string expectedText = "Hello Sentra!";
            File.WriteAllText(path, expectedText);

            var extractor = new UniversalTextExtractor();
            var result = extractor.ExtractText(path);

            Assert.Equal(expectedText, result.Trim());
        }
        catch (Exception ex)
        {
            Assert.False(true, $"❌ Ошибка при тестировании TXT-файла: {ex.Message}");
        }
    }

    [Fact]
    public void ExtractText_UnknownBinaryFile_ReturnsEmptyOrCleaned()
    {
        try
        {
            Directory.CreateDirectory(_testFolder);
            string path = Path.Combine(_testFolder, "binary.dat");
            var data = new byte[] { 0x00, 0x01, 0xFF, 0xAB, 0xCD, 0x20, 0x41, 0x42, 0x43 };
            File.WriteAllBytes(path, data);

            var extractor = new UniversalTextExtractor();
            var result = extractor.ExtractText(path);

            Assert.True(result.Length < 10, "⚠️ Ожидалась короткая строка после фильтрации, получено: " + result);
        }
        catch (Exception ex)
        {
            Assert.False(true, $"❌ Ошибка при тестировании бинарного файла: {ex.Message}");
        }
    }

    [Fact(Skip = "DOCX test requires test.docx and OpenXml SDK")]
    public void ExtractText_DocxFile_ReturnsContent()
    {
        try
        {
            string path = Path.Combine(_testFolder, "test.docx");
            if (!File.Exists(path)) throw new FileNotFoundException("DOCX-файл не найден", path);

            var extractor = new UniversalTextExtractor();
            var result = extractor.ExtractText(path);

            Assert.False(string.IsNullOrWhiteSpace(result), "⚠️ DOCX вернул пустой результат");
        }
        catch (Exception ex)
        {
            Assert.False(true, $"❌ Ошибка при тестировании DOCX-файла: {ex.Message}");
        }
    }

    [Fact(Skip = "PDF test requires PdfPig and test.pdf")]
    public void ExtractText_PdfFile_ReturnsContent()
    {
        try
        {
            string path = Path.Combine(_testFolder, "test.pdf");
            if (!File.Exists(path)) throw new FileNotFoundException("PDF-файл не найден", path);

            var extractor = new UniversalTextExtractor();
            var result = extractor.ExtractText(path);

            Assert.Contains("Sentra", result, System.StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Assert.False(true, $"❌ Ошибка при тестировании PDF-файла: {ex.Message}");
        }
    }
}