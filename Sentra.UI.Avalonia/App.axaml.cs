using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SharpHook;
using SharpHook.Native;

namespace Sentra.UI.Avalonia;

public partial class App : global::Avalonia.Application
{
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
        // Проверяем Ctrl + Space
        if (e.Data.KeyCode == KeyCode.VcSpace &&
            (e.RawEvent.Mask & ModifierMask.Ctrl) != 0)
        {
            Console.WriteLine("🎯 Ctrl + Space сработал");

            Dispatcher.UIThread.Post(() =>
            {
                if (_searchWindow is { IsVisible: false })
                {
                    _searchWindow.Show();
                    _searchWindow.FocusInput();
                }
            });
        }
    }
}