namespace Sentra.Application.Indexing;

/// <summary>
/// Результат индексации одного файла
/// </summary>
public class IndexingResult
{
    public string FilePath { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? VectorLength { get; set; }
}