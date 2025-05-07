using Sentra.Config;
using Sentra.Domain;
using Sentra.Infrastructure.Crawling;
using Sentra.Infrastructure.Persistence;
using Sentra.Application.Embedding;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Sentra.Application.Indexing;

public class Indexer : IIndexer
{
    private readonly RawTextExtractor _extractor = new();
    private readonly EmbeddingClient _embeddingClient;
    private readonly EmbeddingDbContext _dbContext;
    private readonly ILogger<Indexer>? _logger;

    public Indexer(EmbeddingDbContext dbContext, EmbeddingClient embeddingClient, ILogger<Indexer>? logger = null)
    {
        _embeddingClient = embeddingClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<IndexingResult>> RunAsync(IProgress<double>? progress = null)
    {
        var results = new List<IndexingResult>();
        var targetFolders = AppConfig.GetTargetFoldersToIndex();

        var allFiles = new List<string>();
        foreach (var folder in targetFolders)
            allFiles.AddRange(FileCrawler.FindFiles(folder));

        int total = allFiles.Count;
        int processed = 0;

        foreach (var file in allFiles)
        {
            var result = new IndexingResult { FilePath = file };
            try
            {
                var lastModified = File.GetLastWriteTimeUtc(file);
                var existing = await _dbContext.Files.FirstOrDefaultAsync(f => f.Path == file);

                if (existing is not null && existing.LastModified == lastModified)
                {
                    // Не изменялся — пропускаем
                    processed++;
                    progress?.Report((double)processed / total);
                    continue;
                }

                var text = _extractor.TryExtract(file);
                if (string.IsNullOrWhiteSpace(text))
                {
                    processed++;
                    progress?.Report((double)processed / total);
                    continue;
                }

                var vector = await _embeddingClient.GetEmbeddingAsync(text);
                if (vector.Length == 0)
                    throw new Exception("Вектор пуст");

                if (existing is not null)
                {
                    existing.Content = text;
                    existing.SetVector(vector);
                    existing.IndexedAt = DateTime.UtcNow;
                    existing.LastModified = lastModified;
                    _dbContext.Files.Update(existing);
                }
                else
                {
                    var record = new FileRecord
                    {
                        Path = file,
                        Content = text,
                        VectorJson = System.Text.Json.JsonSerializer.Serialize(vector),
                        IndexedAt = DateTime.UtcNow,
                        LastModified = lastModified
                    };
                    await _dbContext.Files.AddAsync(record);
                }

                await _dbContext.SaveChangesAsync();

                result.Success = true;
                result.VectorLength = vector.Length;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger?.LogWarning(ex, $"Ошибка при индексации файла: {file}");
            }

            results.Add(result);
            processed++;
            progress?.Report((double)processed / total);
        }

        return results;
    }
}
