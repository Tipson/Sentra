namespace Sentra.Application.Indexing;

/// <summary>
/// Вспомогательный класс для хранения id и вектора.
/// </summary>
public class EmbeddingItem
{
    public int Id { get; }
    public float[] Vector { get; }

    public EmbeddingItem(int id, float[] vector)
    {
        Id = id;
        Vector = vector;
    }
}