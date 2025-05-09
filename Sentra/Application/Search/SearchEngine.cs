using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sentra.Application.Embedding;
using Sentra.Application.Indexing;
using Sentra.Infrastructure.Persistence;
using Sentra.Domain;

namespace Sentra.Application.Search
{
    public class SearchEngine : ISearchEngine
    {
        private readonly EmbeddingDbContext _db;
        private readonly EmbeddingClient _embeddingClient;
        private readonly IVectorIndex _vectorIndex;

        private const double Epsilon = 1e-8;
        private const float Alpha = 0.7f;  // вес плотной компоненты
        private const float Beta = 0.3f;   // вес sparse-буста

        public SearchEngine(
            EmbeddingDbContext db,
            EmbeddingClient embeddingClient,
            IVectorIndex vectorIndex)
        {
            _db = db;
            _embeddingClient = embeddingClient;
            _vectorIndex = vectorIndex;
        }

        public async Task<List<SearchResult>> SearchAsync(string query, int topN = 5)
        {
            var q = query?.Trim();
            if (string.IsNullOrWhiteSpace(q))
                return new List<SearchResult>();

            // 1) Плотный вектор запроса
            var queryVec = await _embeddingClient.GetEmbeddingAsync(q);
            if (queryVec.Length == 0)
                return new List<SearchResult>();

            // Диагностика: общее число чанков в БД
            var totalChunks = await _db.Chunks.CountAsync();
            Console.WriteLine($"[Debug] Всего чанков в БД: {totalChunks}");

            // 2) Находим ближайшие чанки через HNSW
            int fetchCount = topN * 2;
            var nearestChunkIds = _vectorIndex.GetNearest(queryVec, fetchCount);
            Console.WriteLine($"[Debug] HNSW вернул {nearestChunkIds.Length} чанков: [{string.Join(',', nearestChunkIds)}]");

            // Fallback: если HNSW не вернул ничего — подгружаем все ID чанков из _db
            if (nearestChunkIds.Length == 0)
            {
                Console.WriteLine("[Debug] HNSW пуст, загружаем все ID чанков из БД");
                nearestChunkIds = await _db.Chunks
                    .AsNoTracking()
                    .Select(c => c.Id)
                    .ToArrayAsync();
            }

            // 3) Загружаем из _db только нужные чанки вместе с FileRecord
            var chunks = await _db.Chunks
                .AsNoTracking()
                .Include(c => c.FileRecord)
                .Where(c => nearestChunkIds.Contains(c.Id))
                .ToListAsync();
            Console.WriteLine($"[Debug] Загружено чанков из БД: {chunks.Count}");

            // Если по найденным ID ничего не подгрузилось, делаем fallback на все чанки
            if (chunks.Count == 0)
            {
                Console.WriteLine("[Debug] Не найдено чанков по ID, загружаем все чанки из БД");
                chunks = await _db.Chunks
                    .AsNoTracking()
                    .Include(c => c.FileRecord)
                    .ToListAsync();
                Console.WriteLine($"[Debug] Всего чанков после fallback: {chunks.Count}");
            }

            var scored = new List<(string path, float score, string snippet)>();
            // 4) Считаем комбинированный скор для каждого чанка
            foreach (var c in chunks)
            {
                var vec = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson) ?? Array.Empty<float>();
                if (vec.Length != queryVec.Length)
                    continue;

                // cosine similarity
                float dot = 0, normA = 0, normB = 0;
                for (int i = 0; i < vec.Length; i++)
                {
                    dot   += queryVec[i] * vec[i];
                    normA += queryVec[i] * queryVec[i];
                    normB += vec[i]       * vec[i];
                }
                float denseScore = (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB) + Epsilon));

                // sparse boost
                float sparseBoost = 0;
                if (c.Text.Contains(q, StringComparison.OrdinalIgnoreCase))
                    sparseBoost += 1f;
                var fileName = Path.GetFileName(c.FileRecord.Path);
                if (!string.IsNullOrEmpty(fileName) && fileName.Contains(q, StringComparison.OrdinalIgnoreCase))
                    sparseBoost += 1f;

                float finalScore = Alpha * denseScore + Beta * Math.Min(sparseBoost, 1f);
                scored.Add((c.FileRecord.Path, finalScore, c.Text));
            }

            // 5) Агрегируем по файлу: выбираем лучший скор и сниппет
            var byFile = scored
                .GroupBy(x => x.path)
                .Select(g =>
                {
                    var best = g.MaxBy(x => x.score);
                    return new SearchResult
                    {
                        FilePath = best.path,
                        Score    = best.score,
                        Snippet  = best.snippet.Length > 200
                            ? best.snippet[..200] + "..."
                            : best.snippet
                    };
                });

            // 6) Сортируем и возвращаем Top-N
            return byFile
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }
    }
}
