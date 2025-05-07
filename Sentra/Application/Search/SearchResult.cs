namespace Sentra.Application.Search;

public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public float Score { get; set; }
    public string Snippet { get; set; } = string.Empty;

}
