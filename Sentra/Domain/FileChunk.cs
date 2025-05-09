namespace Sentra.Domain;

/// <summary>
/// Один «чанк» (кусок) большого текстового файла.
/// </summary>
public class FileChunk
{
    public int    Id           { get; set; }
    public int FileRecordId { get; set; }
    public FileRecord FileRecord  { get; set; } = null!;
    public int    ChunkIndex   { get; set; }       // порядковый номер чанка
    public string Text         { get; set; } = ""; // исходный текст чанка
    /// <summary>
    /// Сериализованный JSON-массив чисел (эмбеддинг чанка).
    /// </summary>
    public string EmbeddingJson { get; set; } = "";

    // /// <summary>
    // /// Отдельный эмбеддинг для метаданных (имя файла, категория и т.п.).
    // /// </summary>
    // public string? MetadataEmbeddingJson { get; set; }
}