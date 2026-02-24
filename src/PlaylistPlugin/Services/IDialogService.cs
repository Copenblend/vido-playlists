namespace PlaylistPlugin.Services;

/// <summary>
/// Abstraction over file dialogs and message boxes for testability.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a save file dialog. Returns the selected path or null if cancelled.
    /// </summary>
    string? ShowSaveFileDialog(string defaultName, string filter);

    /// <summary>
    /// Shows an open file dialog. Returns the selected path or null if cancelled.
    /// </summary>
    string? ShowOpenFileDialog(string filter);

    /// <summary>
    /// Shows a confirmation dialog with Yes/No/Cancel buttons.
    /// Returns true for Yes, false for No, null for Cancel.
    /// </summary>
    bool? ShowConfirmationDialog(string message, string title);
}
