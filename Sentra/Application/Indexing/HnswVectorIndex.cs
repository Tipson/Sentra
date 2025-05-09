using HNSWIndex;

namespace Sentra.Application.Indexing;

/// <summary>
/// Реализация IVectorIndex на базе HNSWIndex (HNSWIndex&lt;EmbeddingItem, float&gt;).
/// </summary>
public class HnswVectorIndex : IVectorIndex
{
    private readonly HNSWIndex<EmbeddingItem, float> _index;

    public HnswVectorIndex()
    {
        // Создаем HNSWIndex с метрикой косинусного расстояния между векторами.
        _index = new HNSWIndex<EmbeddingItem, float>(
            (a, b) => ComputeCosineDistance(a.Vector, b.Vector)
        );
    }

    public void AddItem(int id, float[] vector)
    {
        _index.Add(new EmbeddingItem(id, vector));
    }

    public void Build()
    {
    }

    public int[] GetNearest(float[] queryVector, int topN)
    {
        var queryItem = new EmbeddingItem(-1, queryVector);
        var neighbors = _index.KnnQuery(queryItem, topN);
        return neighbors.Select(item => item.Id).ToArray();
    }

    private static float ComputeCosineDistance(float[] a, float[] b)
    {
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return 1f - (dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB)));
    }
}