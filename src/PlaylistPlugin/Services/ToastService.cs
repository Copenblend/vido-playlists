using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PlaylistPlugin.Services;

/// <summary>
/// Shows VS Code-style toast notifications in the bottom-right corner
/// of the Vido main window, above the status bar.
/// </summary>
public sealed class ToastService
{
    private Border? _currentToast;
    private DispatcherTimer? _hideTimer;

    /// <summary>
    /// Shows an info toast (blue accent background).
    /// Auto-dismisses after 3 seconds with a fade animation.
    /// </summary>
    public void Show(string message, string? boldSuffix = null)
    {
        ShowInternal(message, boldSuffix,
            background: Color.FromRgb(0x00, 0x7A, 0xCC),   // #007ACC
            border: Color.FromRgb(0x00, 0x5A, 0x9E),
            icon: "\uE946"); // info icon
    }

    /// <summary>
    /// Shows an error toast (red background matching Vido's close button).
    /// Auto-dismisses after 3 seconds with a fade animation.
    /// </summary>
    public void ShowError(string message, string? boldSuffix = null)
    {
        ShowInternal(message, boldSuffix,
            background: Color.FromRgb(0xC4, 0x2B, 0x1C),   // #C42B1C
            border: Color.FromRgb(0x9E, 0x22, 0x16),
            icon: "\uEA39"); // error/warning icon
    }

    private void ShowInternal(string message, string? boldSuffix, Color background, Color border, string icon)
    {
        var app = Application.Current;
        if (app is null) return;

        app.Dispatcher.Invoke(() =>
        {
            var mainWindow = app.MainWindow;
            if (mainWindow?.Content is not Border windowBorder) return;
            if (windowBorder.Child is not Grid rootGrid) return;

            // Remove any existing toast
            RemoveCurrentToast(rootGrid);

            // Notification icon
            var iconBlock = new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Message text
            var textBlock = new TextBlock
            {
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 280,
                VerticalAlignment = VerticalAlignment.Center
            };

            textBlock.Inlines.Add(new Run(message));
            if (!string.IsNullOrEmpty(boldSuffix))
            {
                textBlock.Inlines.Add(new Run(boldSuffix) { FontWeight = FontWeights.Bold });
            }

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { iconBlock, textBlock }
            };

            // Notification container
            var toast = new Border
            {
                Background = new SolidColorBrush(background),
                BorderBrush = new SolidColorBrush(border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 12, 8),
                IsHitTestVisible = false,
                Opacity = 0,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.5
                },
                Child = contentPanel
            };

            // Place in row 1 (content area) so it sits above the status bar (row 2)
            Grid.SetRow(toast, 1);

            rootGrid.Children.Add(toast);
            _currentToast = toast;

            // Fade-in animation
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Auto-dismiss timer
            _hideTimer?.Stop();
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer.Stop();
                FadeOutAndRemove(toast, rootGrid);
            };
            _hideTimer.Start();
        });
    }

    private void FadeOutAndRemove(Border toast, Grid rootGrid)
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (_, _) =>
        {
            rootGrid.Children.Remove(toast);
            if (ReferenceEquals(_currentToast, toast))
                _currentToast = null;
        };
        toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void RemoveCurrentToast(Grid rootGrid)
    {
        if (_currentToast is not null)
        {
            _hideTimer?.Stop();
            rootGrid.Children.Remove(_currentToast);
            _currentToast = null;
        }
    }
}
