using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sentra.Application.Embedding;
using Sentra.Config;
using Sentra.Domain;
using Sentra.Infrastructure.Crawling;
using Sentra.Infrastructure.Persistence;

namespace Sentra.Application.Indexing
{
    public class Indexer : IIndexer
    {
        private readonly EmbeddingDbContext _dbContext;
        private readonly EmbeddingClient _embeddingClient;
        private readonly IVectorIndex _vectorIndex;
        private readonly ILogger<Indexer>? _logger;
        private readonly RawTextExtractor _extractor = new();

        // Размер чанка в символах (можно вынести в AppConfig)
        private const int ChunkSize = 1000;

        public Indexer(
            EmbeddingDbContext dbContext,
            EmbeddingClient embeddingClient,
            IVectorIndex vectorIndex,
            ILogger<Indexer>? logger = null)
        {
            _dbContext = dbContext;
            _embeddingClient = embeddingClient;
            _vectorIndex = vectorIndex;
            _logger = logger;
        }

        public async Task<List<IndexingResult>> RunAsync(IProgress<double>? progress = null)
        {
            var results = new List<IndexingResult>();
            var allFiles = AppConfig
                .GetTargetFoldersToIndex()
                .SelectMany(FileCrawler.FindFiles)
                .ToList();

            int total = allFiles.Count;
            int processed = 0;

            foreach (var file in allFiles)
            {
                var result = new IndexingResult { FilePath = file };

                try
                {
                    var lastModified = File.GetLastWriteTimeUtc(file);
                    var existingFile = await _dbContext.Files
                        .FirstOrDefaultAsync(f => f.Path == file);

                    // Проверяем, есть ли уже чанки для этого файла
                    bool hasChunks = false;
                    if (existingFile is not null)
                        hasChunks = await _dbContext.Chunks
                            .AnyAsync(c => c.FileRecordId == existingFile.Id);

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
                            Path = file,
                            IndexedAt = DateTime.UtcNow,
                            LastModified = lastModified,
                            Category = FileClassifier.Classify(file)
                        };
                        await _dbContext.Files.AddAsync(existingFile);
                        await _dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        existingFile.IndexedAt = DateTime.UtcNow;
                        existingFile.LastModified = lastModified;
                        existingFile.Category = FileClassifier.Classify(file);
                        _dbContext.Files.Update(existingFile);
                        await _dbContext.SaveChangesAsync();
                    }

                    // Удаляем старые чанки
                    var oldChunks = _dbContext.Chunks
                        .Where(c => c.FileRecordId == existingFile.Id);
                    _dbContext.Chunks.RemoveRange(oldChunks);
                    await _dbContext.SaveChangesAsync();

                    // Разбиваем текст на чанки и собираем буфер новых чанков
                    var bufferedChunks = new List<(FileChunk Entity, float[] Vector)>();
                    for (int i = 0; i < text.Length; i += ChunkSize)
                    {
                        var chunkText = text.Substring(i, Math.Min(ChunkSize, text.Length - i));
                        var enriched = $"File: {Path.GetFileName(file)}\n" +
                                       $"Category: {existingFile.Category}\n\n" +
                                       chunkText;

                        var vec = await _embeddingClient.GetEmbeddingAsync(enriched);
                        if (vec is null || vec.Length == 0)
                            throw new Exception("Пустой эмбеддинг чанка");

                        var chunkEntity = new FileChunk
                        {
                            FileRecordId = existingFile.Id,
                            ChunkIndex = bufferedChunks.Count,
                            Text = chunkText,
                            EmbeddingJson = JsonSerializer.Serialize(vec)
                        };
                        bufferedChunks.Add((chunkEntity, vec));
                        await _dbContext.Chunks.AddAsync(chunkEntity);
                    }

                    // Сохраняем все новые чанки за один раз
                    await _dbContext.SaveChangesAsync();

                    // Наполняем HNSW-индекс корректными ID и векторами
                    foreach (var (entity, vec) in bufferedChunks)
                    {
                        _vectorIndex.AddItem(entity.Id, vec);
                    }

                    result.Success = true;
                    result.VectorLength = bufferedChunks.Count;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    _logger?.LogWarning(ex, "Ошибка индексации файла: {File}", file);
                }

                results.Add(result);
                processed++;
                progress?.Report((double)processed / total);
            }

            Console.WriteLine("🔢 HNSW-индекс построен и готов к поиску");
            return results;
        }
    }
}