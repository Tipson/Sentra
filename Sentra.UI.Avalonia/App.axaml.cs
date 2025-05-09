using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Sentra.Application.Embedding;
using Sentra.Application.Indexing;
using Sentra.Infrastructure.Persistence;
using Sentra.UI.Avalonia.Views;
using SharpHook;
using SharpHook.Native;

namespace Sentra.UI.Avalonia;

public partial class App : global::Avalonia.Application
{
    public static double IndexingProgress { get; set; } = 0;

    private TaskPoolGlobalHook? _hook;
    private SearchWindow? _searchWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Console.WriteLine("== Sentra запущена ==");

            _searchWindow = new SearchWindow();

            // ✅ Стартуем индексацию асинхронно
            _ = Task.Run(async () =>
            {
                var db = new EmbeddingDbContext();
                var embed = new EmbeddingClient();
                var indexer = new Indexer(db, embed);

                Console.WriteLine("🚀 Начинаем индексацию...");

                await indexer.RunAsync(new Progress<double>(p =>
                {
                    App.IndexingProgress = p;
                    Console.WriteLine($"📊 Индексация: {(p * 100):0.0}%");
                }));

                App.IndexingProgress = 1; // ⬅️ чтобы скрыть индикатор после завершения
                Console.WriteLine("✅ Индексация завершена");
            });

            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnGlobalHotkey;
            _hook.RunAsync();

            desktop.Exit += (_, _) =>
            {
                _hook?.Dispose();
                Console.WriteLine("🛑 Hook остановлен");
            };

            Console.WriteLine("🔗 Глобальный хоткей инициализирован");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnGlobalHotkey(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == KeyCode.VcSpace &&
            (e.RawEvent.Mask & ModifierMask.Ctrl) != 0)
        {
            Console.WriteLine("🎯 Ctrl + Space сработал");

            Dispatcher.UIThread.Post(() =>
            {
                if (_searchWindow is { IsVisible: false })
                {
                    _searchWindow.Show();
                }
            });
        }
    }
}