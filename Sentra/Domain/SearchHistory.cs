namespace Sentra.Domain;

public class SearchHistory
{
    public int Id { get; set; }
    public string Query { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool WasOpened { get; set; } = false;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
