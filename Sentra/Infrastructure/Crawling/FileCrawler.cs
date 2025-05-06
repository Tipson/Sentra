// FileCrawler.cs — модуль поиска файлов по указанной директории

namespace Sentra.Infrastructure.Crawling;

public static partial class FileCrawler
{
    // Список допустимых расширений для индексации
    private static readonly string[] AllowedExtensions = {
        ".txt", ".md", ".docx", ".pdf", ".xlsx"
    };

    /// <summary>
    /// Поиск всех подходящих файлов в указанной папке (рекурсивно)
    /// </summary>
    /// <param name="rootPath">Путь к папке</param>
    /// <returns>Список путей к найденным файлам</returns>
    public static List<string> FindFiles(string rootPath)
    {
        var found = new List<string>();

        try
        {
            if (!Directory.Exists(rootPath))
                return found;

            var allFiles = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories);
            found = allFiles
                .Where(file => AllowedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при обходе файлов: " + ex.Message);
        }

        return found;
    }
}