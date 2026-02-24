using System.Windows;
using Microsoft.Win32;

namespace PlaylistPlugin.Services;

/// <summary>
/// Concrete dialog service using Win32 dialogs and WPF MessageBox.
/// </summary>
public sealed class DialogService : IDialogService
{
    private const string PlaylistFilter = "Vido Playlist (*.vidpl)|*.vidpl";

    public string? ShowSaveFileDialog(string defaultName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultName,
            Filter = string.IsNullOrEmpty(filter) ? PlaylistFilter : filter,
            DefaultExt = ".vidpl"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowOpenFileDialog(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = string.IsNullOrEmpty(filter) ? PlaylistFilter : filter,
            DefaultExt = ".vidpl"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool? ShowConfirmationDialog(string message, string title)
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }
}
