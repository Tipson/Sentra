using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sentra.Application.Embedding;
using Sentra.Domain;
using Sentra.Infrastructure.Persistence;

namespace Sentra.Application.Search
{
    public class SearchEngine : ISearchEngine
    {
        private readonly EmbeddingDbContext _db;
        private readonly EmbeddingClient   _embeddingClient;
        private const double Epsilon = 1e-8;
        private const float  Alpha   = 0.7f;  // вес плотной компоненты
        private const float  Beta    = 0.3f;  // вес sparse-буста

        public SearchEngine(EmbeddingDbContext db, EmbeddingClient embeddingClient)
        {
            _db              = db;
            _embeddingClient = embeddingClient;
        }

        public async Task<List<SearchResult>> SearchAsync(string query, int topN = 5)
        {
            var q = query.Trim();
            if (string.IsNullOrWhiteSpace(q))
                return new List<SearchResult>();

            // 1) Плотный вектор запроса
            var queryVec = await _embeddingClient.GetEmbeddingAsync(q);
            if (queryVec.Length == 0)
                return new List<SearchResult>();

            // 2) Загружаем все чанки вместе с их FileRecord
            var chunks = await _db.Chunks
                .Include(c => c.FileRecord)
                .ToListAsync();

            var scored = new List<(string path, float score, string snippet)>();

            // 3) Для каждого чанка считаем комбинированный скор
            foreach (var c in chunks)
            {
                // десериализуем вектор чанка
                var vec = JsonSerializer
                    .Deserialize<float[]>(c.EmbeddingJson)
                    ?? Array.Empty<float>();
                if (vec.Length != queryVec.Length)
                    continue;

                // 3a) cosine similarity
                float dot = 0, normA = 0, normB = 0;
                for (int i = 0; i < vec.Length; i++)
                {
                    dot   += queryVec[i] * vec[i];
                    normA += queryVec[i] * queryVec[i];
                    normB += vec[i]       * vec[i];
                }
                float denseScore = (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB) + Epsilon));

                // 3b) sparse-boost: точное вхождение в текст чанка или имя файла
                float sparseBoost = 0;
                if (c.Text.Contains(q, StringComparison.OrdinalIgnoreCase))
                    sparseBoost += 1f;

                var fileName = Path.GetFileName(c.FileRecord.Path);
                if (!string.IsNullOrEmpty(fileName) &&
                    fileName.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    sparseBoost += 1f;
                }

                // итоговый скор
                float finalScore = Alpha * denseScore + Beta * Math.Min(sparseBoost, 1f);

                scored.Add((c.FileRecord.Path, finalScore, c.Text));
            }

            // 4) Агрегируем по файлу: берём максимальный скор и соответствующий сниппет
            var byFile = scored
                .GroupBy(x => x.path)
                .Select(g =>
                {
                    var best = g.OrderByDescending(x => x.score).First();
                    return new SearchResult
                    {
                        FilePath = best.path,
                        Score    = best.score,
                        Snippet  = best.snippet.Length > 200
                            ? best.snippet[..200] + "..."
                            : best.snippet
                    };
                });

            // 5) Сортируем и возвращаем Top-N
            return byFile
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }
    }
}
