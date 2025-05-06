using Sentra.Application.Embedding;
using Sentra.Application.Indexing;
using Sentra.Infrastructure.Persistence;
using Sentra.UI;

namespace Sentra;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static async Task Main()
    {
        var db = new EmbeddingDbContext();
        var embedding = new EmbeddingClient();
        var indexer = new Indexer(db, embedding);

        var results = await indexer.RunAsync();
        Console.WriteLine($"Индексировано файлов: {results.Count(r => r.Success)}");

        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new MainForm());
    }
}