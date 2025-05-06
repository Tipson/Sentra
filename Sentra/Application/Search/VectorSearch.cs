
using Microsoft.EntityFrameworkCore;
using Sentra.Application.Embedding;
using Sentra.Infrastructure.Persistence;

namespace Sentra.Application.Search;

public class VectorSearch : ISearchEngine
{
    private readonly EmbeddingDbContext _db;
    private readonly EmbeddingClient _embeddingClient;

    public VectorSearch(EmbeddingDbContext db, EmbeddingClient embeddingClient)
    {
        _db = db;
        _embeddingClient = embeddingClient;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int topN = 5)
    {
        var queryVector = await _embeddingClient.GetEmbeddingAsync(query);
        if (queryVector.Length == 0)
            return new List<SearchResult>();

        var allFiles = await _db.Files.ToListAsync();
        var results = new List<SearchResult>();

        foreach (var file in allFiles)
        {
            var fileVector = file.GetVector();
            if (fileVector.Length != queryVector.Length) continue;

            float score = CosineSimilarity(queryVector, fileVector);
            results.Add(new SearchResult
            {
                FilePath = file.Path,
                Score = score
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .ToList();
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0;
        float normA = 0;
        float normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-8)); // +epsilon чтобы не делить на 0
    }
}
