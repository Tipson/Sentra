using Sentra.Application.Indexing;
using Sentra.Application.Embedding;
using Sentra.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sentra.Tests;

public class IndexerTests
{
    [Fact]
    public async Task Indexer_Should_Index_At_Least_One_File()
    {
        // Arrange
        var db = new EmbeddingDbContext();
        var embeddingClient = new EmbeddingClient(); // работает с AppConfig
        var vectorIndex = new HnswVectorIndex();
        var indexer = new Indexer(db, embeddingClient, vectorIndex, NullLogger<Indexer>.Instance);

        // Act
        var results = await indexer.RunAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Success);
    }
}