using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MimeKit;
using UglyToad.PdfPig;
// PdfPig
// OpenXML SDK
// HTML parser
using HtmlDocument = HtmlAgilityPack.HtmlDocument; // Email parser

namespace Sentra.Infrastructure.Crawling;

public class UniversalTextExtractor
{
    public string ExtractText(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        try
        {
            switch (ext)
            {
                case ".txt":
                case ".csv":
                    return File.ReadAllText(filePath, Encoding.UTF8);

                case ".json":
                case ".xml":
                    // можно также парсить, но просто возвращаем содержимое
                    return File.ReadAllText(filePath, Encoding.UTF8);

                case ".docx":
                case ".pptx":
                case ".xlsx":
                    return ExtractOpenXml(filePath);

                case ".pdf":
                    return ExtractPdf(filePath);

                case ".html":
                case ".htm":
                    return ExtractHtml(filePath);

                case ".eml":
                    return ExtractEmail(filePath);

                case ".rtf":
                    return ExtractRtf(filePath);

                case ".doc":
                case ".xls":
                case ".ppt":
                case ".odt":
                    // Старые/редкие форматы через LibreOffice CLI или другие средства
                    return ConvertWithLibreOffice(filePath);

                default:
                    // Фолбэк: читать как текст и фильтровать
                    return ExtractTextFallback(filePath);
            }
        }
        catch (Exception ex)
        {
            // Логирование ошибки и попытка фолбэка
            Console.WriteLine($"Не удалось извлечь текст из {filePath}: {ex.Message}");
            return ExtractTextFallback(filePath);
        }
    }

    private string ExtractOpenXml(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".docx")
        {
            using var doc = WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart?.Document.Body;
            return body?.InnerText ?? "";
        }

        if (ext == ".xlsx")
        {
            using var doc = SpreadsheetDocument.Open(path, false);
            var sheets = doc.WorkbookPart?.Workbook.Sheets;
            var sb = new StringBuilder();

            foreach (var openXmlElement in sheets)
            {
                var sheet = (Sheet)openXmlElement;
                var worksheetPart = (WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id);
                var rows = worksheetPart.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>();
                foreach (var row in rows)
                {
                    foreach (var cell in row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                        sb.Append(cell.InnerText + " ");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        if (ext == ".pptx")
        {
            using var ppt = PresentationDocument.Open(path, false);
            var slides = ppt.PresentationPart?.SlideParts;
            var sb = new StringBuilder();
            foreach (var slide in slides)
                sb.AppendLine(slide.Slide.InnerText);
            return sb.ToString();
        }

        return "";
    }


    private string ExtractPdf(string path)
    {
        using (var pdf = PdfDocument.Open(path))
        {
            var text = new StringBuilder();
            foreach (var page in pdf.GetPages())
                text.AppendLine(page.Text);
            return text.ToString();
        }
    }

    private string ExtractHtml(string path)
    {
        var doc = new HtmlDocument();
        doc.Load(path, Encoding.UTF8);
        return doc.DocumentNode.InnerText;
    }

    private string ExtractEmail(string path)
    {
        var message = MimeMessage.Load(path);
        return message.TextBody ?? message.HtmlBody ?? string.Empty;
    }

    private string ExtractRtf(string path)
    {
        // Для демонстрации: можно использовать RtfBox или RichTextBox
        var rtb = new System.Windows.Forms.RichTextBox();
        rtb.Rtf = File.ReadAllText(path);
        return rtb.Text;
    }

    private string ConvertWithLibreOffice(string path)
    {
        // Запустить soffice CLI, конвертировать в txt, затем прочитать результат.
        // Здесь нужно добавить проверку наличия LibreOffice и путь.
        string outDir = Path.GetTempPath();
        var psi = new System.Diagnostics.ProcessStartInfo("soffice",
            $"--headless --convert-to txt:Text \"{path}\" --outdir \"{outDir}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        var proc = System.Diagnostics.Process.Start(psi);
        proc.WaitForExit(60000); // таймаут 1 мин
        string txtPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + ".txt");
        return File.Exists(txtPath) ? File.ReadAllText(txtPath, Encoding.UTF8) : "";
    }

    private string ExtractTextFallback(string path)
    {
        // Попытка открыть любым кодированием
        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch
        {
            // Фильтрация мусора: взять только печатные символы
            var bytes = File.ReadAllBytes(path);
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                char c = (char)b;
                if (!char.IsControl(c) || c == '\r' || c == '\n')
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}