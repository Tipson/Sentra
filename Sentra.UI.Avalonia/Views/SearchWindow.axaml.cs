using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using Sentra.Application.Search;
using Sentra.Application.Embedding;
using Sentra.Infrastructure.Persistence;
using Sentra.Domain;

namespace Sentra.UI.Avalonia.Views;

public partial class SearchWindow : Window
{
    private readonly ISearchEngine _searchEngine;
    private readonly EmbeddingDbContext _dbContext;
    private int? _currentSearchId;

    public SearchWindow()
    {
        InitializeComponent();

        // Инициализируем контекст и движок поиска
        _dbContext     = new EmbeddingDbContext();
        var embedding  = new EmbeddingClient();
        _searchEngine  = new SearchEngine(_dbContext, embedding);

        // Скрываем окно при потере фокуса
        Deactivated += (_, _) => Hide();
        
        InitializeIndexingStatusTimer();
    }

    // 1) Поиск по Enter: логируем запрос и показываем результаты
    private async void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var query = SearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;

        // Логируем сам факт поиска
        var entry = new SearchHistory
        {
            Query     = query,
            FilePath  = "",
            WasOpened = false,
            Timestamp = DateTime.UtcNow
        };
        _dbContext.SearchHistory.Add(entry);
        await _dbContext.SaveChangesAsync();
        _currentSearchId = entry.Id;

        // Выполняем поиск и отображаем объекты SearchResult
        var results = await _searchEngine.SearchAsync(query);
        ResultsBox.ItemsSource = results;
    }

    // 2) Клик по результату: открываем файл и обновляем запись истории
    private async void OnResultClick(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not StackPanel panel ||
            panel.DataContext is not SearchResult result ||
            _currentSearchId == null)
            return;

        var path = result.FilePath;
        // Открываем файл в ассоциированном приложении
        if (File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else
            Console.WriteLine($"Файл не найден: {path}");

        // Обновляем запись истории: теперь указана папка и WasOpened = true
        try
        {
            var history = await _dbContext.SearchHistory.FindAsync(_currentSearchId.Value);
            if (history != null)
            {
                history.FilePath  = path;
                history.WasOpened = true;
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обновления истории: {ex.Message}");
        }

        Hide();
        e.Handled = true;
    }
    
    private void InitializeIndexingStatusTimer()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        timer.Tick += (_, _) =>
        {
            var p = App.IndexingProgress; // от 0.0 до 1.0
            if (p > 0 && p < 1)
            {
                IndexingStatus.IsVisible = true;
                IndexingStatus.Text      = $"🟡 Индексация: {(p * 100):0}%";
            }
            else
            {
                IndexingStatus.IsVisible = false;
            }
        };
        timer.Start();
    }

    // Вспомогательный метод для показа окна по хоткею
    public void ShowCentered()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Show();
        Activate();
        Dispatcher.UIThread.Post(() => SearchBox.Focus());
    }
}