using Sentra.Domain;

namespace Sentra.Infrastructure.Crawling;

public static class FileClassifier
{
    // Основная карта расширений → категория
    private static readonly (string[] exts, FileCategory cat)[] Map =
    {
        ([".txt", ".md", ".doc", ".docx", ".pdf", ".csv", ".json", ".xml", ".html", ".htm", ".rtf"], FileCategory.Document),
        ([".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff"], FileCategory.Image),
        ([".exe", ".msi", ".lnk", ".app"], FileCategory.Application),
    };

    public static FileCategory Classify(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        // 1) по карте расширений
        foreach (var (exts, cat) in Map)
            if (exts.Contains(ext))
                return cat;

        // 2) по контенту (читаем первые байты или пытаемся извлечь текст)
        try
        {
            // 2a. Текстовый фолбэк
            string text = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(text) && text.Any(c => !char.IsControl(c)))
                return FileCategory.Document;
        }
        catch
        {
             /* не текст */
        }

        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            if (fs.Read(header) > 0)
            {
                // JPEG
                if (header[0] == 0xFF && header[1] == 0xD8) return FileCategory.Image;
                // PNG
                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return FileCategory.Image;
                // WebP (RIFF....WEBP)
                if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' 
                 && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
                    return FileCategory.Image;
                // PE executable (MZ)
                if (header[0] == 'M' && header[1] == 'Z') return FileCategory.Application;
                // PDF
                if (header[0] == '%' && header[1] == 'P' && header[2] == 'D' && header[3] == 'F') return FileCategory.Document;
            }
        }
        catch { /* не читаем */ }

        // 3) иначе — Other
        return FileCategory.Other;
    }
}
