namespace Sentra.Application.Indexing;

/// <summary>
/// Интерфейс для векторного индекса.
/// </summary>
public interface IVectorIndex
{
    /// <summary>
    /// Добавить элемент с заданным id и вектором.
    /// </summary>
    void AddItem(int id, float[] vector);

    /// <summary>
    /// Получить N ближайших id по заданному вектору.
    /// </summary>
    int[] GetNearest(float[] queryVector, int topN);
}