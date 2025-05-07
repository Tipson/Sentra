// FileRecord.cs — сущность файла (слой Domain)

namespace Sentra.Domain;

public class FileRecord
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string VectorJson { get; set; } = string.Empty;
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; }

    public float[] GetVector() =>
        System.Text.Json.JsonSerializer.Deserialize<float[]>(VectorJson) ?? Array.Empty<float>();

    public void SetVector(float[] vector)
    {
        VectorJson = System.Text.Json.JsonSerializer.Serialize(vector);
    }
}