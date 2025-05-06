using System.DirectoryServices;

namespace Sentra.Application.Search;

public interface ISearchEngine
{
    Task<List<SearchResult>> SearchAsync(string query, int topN = 5);
}