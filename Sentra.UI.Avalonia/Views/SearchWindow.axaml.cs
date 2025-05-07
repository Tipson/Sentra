using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Sentra.Application.Embedding;
using Sentra.Application.Search;
using Sentra.Infrastructure.Persistence;
using Sentra.Domain;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sentra.UI.Avalonia.Views;

public partial class SearchWindow : Window
{
    private readonly ISearchEngine _searchEngine;
    private readonly EmbeddingDbContext _dbContext;

    public SearchWindow()
    {
        InitializeComponent();

        _dbContext = new EmbeddingDbContext();
        var embedding = new EmbeddingClient();
        _searchEngine = new VectorSearch(_dbContext, embedding);

        Deactivated += (_, _) => Hide();

        SearchBox.KeyDown += OnKeyDown;
        
        DispatcherTimer.Run(TimeSpan.FromMilliseconds(300), () =>
        {
            if (App.IndexingProgress > 0 && App.IndexingProgress < 1)
            {
                IndexingStatus.IsVisible = true;
                IndexingStatus.Text = $"🟡 Индексация: {(App.IndexingProgress * 100):0}%";
            }
            else
            {
                IndexingStatus.IsVisible = false;
            }

            return true;
        });
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var query = SearchBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

          var results = await _searchEngine.SearchAsync(query);

            _dbContext.SearchHistory.Add(new SearchHistory
            {
                Query = query,
                FilePath = "",
                WasOpened = false,
                Timestamp = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();

            if (results.Count == 0)
            {
                ResultsBox.ItemsSource = new[] { "Ничего не найдено." };
            }
            else
            {
                ResultsBox.ItemsSource = results.Select(r =>
                   $"📄 {r.FilePath}\n{r.Snippet[..Math.Min(200, r.Snippet.Length)]}...");
              ResultsBox.PointerReleased += OnResultClick;
            }
        }
        else if (e.Key == Key.Escape)
       {
            Hide();
            SearchBox.Text = string.Empty;
      }
    }

private void OnResultClick(object? sender, PointerReleasedEventArgs e)
{
    if (ResultsBox.SelectedItem is string selectedText &&
        selectedText.StartsWith("📄 "))
    {
        var path = selectedText.Split('\n').FirstOrDefault()?.Replace("📄 ", "").Trim();
        if (File.Exists(path))
        {
            try
            {
                // открыть файл
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });

                // логгировать как открытие
                _dbContext.SearchHistory.Add(new SearchHistory
                {
                    Query = SearchBox.Text ?? "",
                    FilePath = path,
                    WasOpened = true,
                    Timestamp = DateTime.UtcNow
                });
                _dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось открыть файл: " + ex.Message);
            }

            Hide();
            SearchBox.Text = string.Empty;
        }
    }
}


    public void ShowCentered()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Show();
        Activate();
        Dispatcher.UIThread.Post(() => SearchBox.Focus());
    }
}
