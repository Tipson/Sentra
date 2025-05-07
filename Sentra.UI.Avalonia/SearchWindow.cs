using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Sentra.UI.Avalonia;

public class SearchWindow : Window
{
    private TextBox _searchBox;

    public SearchWindow()
    {
        Width = 600;
        Height = 140;
        Topmost = true;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));

        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Child = BuildLayout()
        };

        Content = border;

        Deactivated += (_, _) => Hide();
    }

    private Control BuildLayout()
    {
        _searchBox = new TextBox
        {
            FontSize = 20,
            Background = new SolidColorBrush(Color.Parse("#3A3A3A")),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Gray,
            CaretBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Watermark = "Поиск файлов...",
            IsEnabled = true,
            Focusable = true
        };

        var label = new TextBlock
        {
            Text = "🔍 Sentra Search Window",
            Foreground = Brushes.LightGray,
            FontWeight = FontWeight.Bold,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                label,
                _searchBox
            }
        };
    }

    public void FocusInput()
    {
        Activate();
        Focus();

        Dispatcher.UIThread.Post(() =>
        {
            Console.WriteLine($"[FOCUS] Enabled={_searchBox.IsEnabled}, Visible={_searchBox.IsVisible}, Focusable={_searchBox.Focusable}");
            _searchBox.Focus();
        });
    }
}
