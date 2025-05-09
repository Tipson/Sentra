using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using Sentra.Application.Embedding;
using Sentra.Application.Indexing;
using Sentra.UI.Avalonia.Views;
using Sentra.Infrastructure.Persistence;
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

            // Создаём общие объекты
            var db           = new EmbeddingDbContext();
            var embed        = new EmbeddingClient();
            var vectorIndex  = new HnswVectorIndex();

            // 0) Восстанавливаем векторный индекс из БД (при необходимости)
            Task.Run(async () =>
            {
                var existing = await db.Chunks
                    .AsNoTracking()
                    .Select(c => new { c.Id, c.EmbeddingJson })
                    .ToListAsync();
                foreach (var c in existing)
                {
                    var vec = System.Text.Json.JsonSerializer.Deserialize<float[]>(c.EmbeddingJson);
                    if (vec?.Length > 0)
                        vectorIndex.AddItem(c.Id, vec);
                }
                Console.WriteLine($"🔄 Восстановлено {existing.Count} векторов из БД");

                // 1) Запускаем индексацию (добавит только новые чанки)
                var indexer = new Indexer(db, embed, vectorIndex);
                Console.WriteLine("🚀 Начинаем индексацию...");
                await indexer.RunAsync(new Progress<double>(p =>
                {
                    IndexingProgress = p;
                    Console.WriteLine($"📊 Индексация: {(p * 100):0.0}%");
                }));

                IndexingProgress = 1;
                Console.WriteLine("✅ Индексация завершена");
            });

            // 2) Создаём окно поиска с теми же зависимостями
            _searchWindow = new SearchWindow(db, embed, vectorIndex);

            // 3) Настраиваем глобальный хоткей
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
                    _searchWindow.ShowCentered();
                }
            });
        }
    }
}