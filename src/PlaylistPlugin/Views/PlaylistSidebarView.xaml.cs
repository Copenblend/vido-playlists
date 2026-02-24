using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PlaylistPlugin.ViewModels;

namespace PlaylistPlugin.Views;

/// <summary>
/// Code-behind for the playlist sidebar view. Handles drag-and-drop
/// from Windows Explorer and double-click-to-play behavior.
/// </summary>
public partial class PlaylistSidebarView : UserControl
{
    public PlaylistSidebarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateEmptyState();

        if (e.OldValue is PlaylistViewModel oldVm)
        {
            oldVm.Items.CollectionChanged -= OnItemsCollectionChanged;
        }

        if (e.NewValue is PlaylistViewModel newVm)
        {
            newVm.Items.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void OnItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        if (DataContext is PlaylistViewModel vm)
        {
            EmptyStateText.Visibility = vm.HasItems ? Visibility.Collapsed : Visibility.Visible;
            PlaylistListBox.Visibility = vm.HasItems ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ── Drag-and-Drop ──

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DragOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;

        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files &&
            DataContext is PlaylistViewModel vm)
        {
            vm.HandleFileDrop(files);
        }

        e.Handled = true;
    }

    // ── Double-click to play ──

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (DataContext is PlaylistViewModel vm &&
            PlaylistListBox.SelectedItem is PlaylistItemViewModel item)
        {
            vm.PlayItemCommand.Execute(item);
            e.Handled = true;
        }
    }
}
