namespace Sentra.Application.Indexing;

public interface IIndexer
{
    Task<List<IndexingResult>> RunAsync();
}