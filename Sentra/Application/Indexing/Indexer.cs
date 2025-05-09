using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sentra.Application.Embedding;
using Sentra.Config;
using Sentra.Domain;
using Sentra.Infrastructure.Crawling;
using Sentra.Infrastructure.Persistence;

namespace Sentra.Application.Indexing
{
    public class Indexer(EmbeddingDbContext dbContext,
                        EmbeddingClient embeddingClient,
                        ILogger<Indexer>? logger = null)
        : IIndexer
    {
        private readonly RawTextExtractor _extractor = new();

        // Размер чанка в символах (можно вынести в AppConfig)
        private const int ChunkSize = 1000;

        public async Task<List<IndexingResult>> RunAsync(IProgress<double>? progress = null)
        {
            var results  = new List<IndexingResult>();
            var allFiles = AppConfig
                .GetTargetFoldersToIndex()
                .SelectMany(FileCrawler.FindFiles)
                .ToList();

            int total     = allFiles.Count;
            int processed = 0;

            foreach (var file in allFiles)
            {
                var result = new IndexingResult { FilePath = file };

                try
                {
                    var lastModified = File.GetLastWriteTimeUtc(file);
                    var existingFile = await dbContext.Files
                        .FirstOrDefaultAsync(f => f.Path == file);

                    // Проверяем, есть ли уже чанки для этого файла
                    bool hasChunks = false;
                    if (existingFile is not null)
                    {
                        hasChunks = await dbContext.Chunks
                            .AnyAsync(c => c.FileRecordId == existingFile.Id);
                    }

                    // Если файл не изменился и чанки уже есть — пропускаем
                    if (existingFile is not null
                        && existingFile.LastModified == lastModified
                        && hasChunks)
                    {
                        processed++;
                        progress?.Report((double)processed / total);
                        continue;
                    }

                    // Извлекаем полный текст
                    var text = _extractor.TryExtract(file);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        processed++;
                        progress?.Report((double)processed / total);
                        continue;
                    }

                    // Создаём или обновляем FileRecord
                    if (existingFile is null)
                    {
                        existingFile = new FileRecord
                        {
                            Path         = file,
                            IndexedAt    = DateTime.UtcNow,
                            LastModified = lastModified,
                            Category     = FileClassifier.Classify(file)
                        };
                        await dbContext.Files.AddAsync(existingFile);
                        await dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        existingFile.IndexedAt    = DateTime.UtcNow;
                        existingFile.LastModified = lastModified;
                        existingFile.Category     = FileClassifier.Classify(file);
                        dbContext.Files.Update(existingFile);
                        await dbContext.SaveChangesAsync();
                    }

                    // Удаляем старые чанки
                    var oldChunks = dbContext.Chunks
                        .Where(c => c.FileRecordId == existingFile.Id);
                    dbContext.Chunks.RemoveRange(oldChunks);
                    await dbContext.SaveChangesAsync();

                    // Разбиваем текст на чанки и индексируем каждый
                    var chunks = new List<string>();
                    for (int i = 0; i < text.Length; i += ChunkSize)
                        chunks.Add(text.Substring(i, Math.Min(ChunkSize, text.Length - i)));

                    int idx = 0;
                    foreach (var chunk in chunks)
                    {
                        var enriched = $"File: {Path.GetFileName(file)}\n" +
                                       $"Category: {existingFile.Category}\n\n" +
                                       chunk;

                        var vec = await embeddingClient.GetEmbeddingAsync(enriched);
                        if (vec.Length == 0)
                            throw new Exception("Пустой эмбеддинг чанка");

                        var chunkEntity = new FileChunk
                        {
                            FileRecordId  = existingFile.Id,
                            ChunkIndex    = idx++,
                            Text          = chunk,
                            EmbeddingJson = JsonSerializer.Serialize(vec)
                        };
                        await dbContext.Chunks.AddAsync(chunkEntity);
                    }

                    await dbContext.SaveChangesAsync();

                    result.Success      = true;
                    result.VectorLength = chunks.Count;
                }
                catch (Exception ex)
                {
                    result.Success      = false;
                    result.ErrorMessage = ex.Message;
                    logger?.LogWarning(ex, "Ошибка индексации файла: {File}", file);
                }

                results.Add(result);
                processed++;
                progress?.Report((double)processed / total);
            }

            return results;
        }
    }
}
