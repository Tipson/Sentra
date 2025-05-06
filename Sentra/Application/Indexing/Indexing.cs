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
    private readonly UniversalTextExtractor _extractor = new();
    private readonly EmbeddingClient _embeddingClient;
    private readonly EmbeddingDbContext _dbContext;
    private readonly ILogger<Indexer>? _logger;

    public Indexer(EmbeddingDbContext dbContext, EmbeddingClient embeddingClient, ILogger<Indexer>? logger = null)
    {
        _embeddingClient = embeddingClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<IndexingResult>> RunAsync()
    {
        var results = new List<IndexingResult>();

        var targetFolders = AppConfig.GetTargetFoldersToIndex();
        foreach (var folder in targetFolders)
        {
            var files = FileCrawler.FindFiles(folder);

            foreach (var file in files)
            {
                var result = new IndexingResult { FilePath = file };
                try
                {
                    // Проверка, не проиндексирован ли уже
                    if (await _dbContext.Files.AnyAsync(f => f.Path == file)) continue;

                    var text = _extractor.ExtractText(file);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var vector = await _embeddingClient.GetEmbeddingAsync(text);
                    if (vector.Length == 0) throw new Exception("Вектор пуст");

                    var record = new FileRecord
                    {
                        Path = file,
                        Content = text,
                        VectorJson = System.Text.Json.JsonSerializer.Serialize(vector),
                        IndexedAt = DateTime.UtcNow
                    };

                    await _dbContext.Files.AddAsync(record);
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
            }
        }

        return results;
    }
}
