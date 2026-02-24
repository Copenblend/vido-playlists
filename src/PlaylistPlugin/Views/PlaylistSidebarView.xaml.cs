using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PlaylistPlugin.ViewModels;

namespace PlaylistPlugin.Views;

/// <summary>
/// Code-behind for the playlist sidebar view. Handles drag-and-drop
/// from Windows Explorer, double-click-to-play, internal drag-reorder,
/// item context menu, and recent playlists dropdown.
/// </summary>
public partial class PlaylistSidebarView : UserControl
{
    // ── Internal drag-reorder state ──
    private Point _dragStartPoint;
    private PlaylistItemViewModel? _draggedItem;
    private static readonly string InternalDragFormat = "PlaylistInternalDrag";

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

    // ── External file drag-and-drop ──

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        // Ignore internal reorder drags at the root level
        if (e.Data.GetDataPresent(InternalDragFormat))
            return;

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
        if (e.Data.GetDataPresent(InternalDragFormat))
            return;

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
        if (e.Data.GetDataPresent(InternalDragFormat))
            return;

        DragOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(InternalDragFormat))
            return;

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

    // ── Item context menu ──

    private void OnItemPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Prevent Vido's explorer context menu from firing
        e.Handled = true;

        if (sender is not ListBoxItem listBoxItem ||
            listBoxItem.DataContext is not PlaylistItemViewModel item ||
            DataContext is not PlaylistViewModel vm)
            return;

        // Select the right-clicked item
        PlaylistListBox.SelectedItem = item;

        var index = vm.Items.IndexOf(item);
        var count = vm.Items.Count;

        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)),
            BorderThickness = new Thickness(1),
            Style = CreateFlatContextMenuStyle()
        };

        var itemStyle = CreateContextMenuItemStyle();

        menu.Items.Add(CreateContextMenuItem("Remove from Playlist", vm.RemoveItemCommand, item, true, itemStyle));
        menu.Items.Add(new Separator
        {
            Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)),
            Margin = new Thickness(4, 2, 4, 2)
        });
        menu.Items.Add(CreateContextMenuItem("Move to Top", vm.MoveToTopCommand, item, index > 0, itemStyle));
        menu.Items.Add(CreateContextMenuItem("Move Up", vm.MoveUpCommand, item, index > 0, itemStyle));
        menu.Items.Add(CreateContextMenuItem("Move Down", vm.MoveDownCommand, item, index < count - 1, itemStyle));
        menu.Items.Add(CreateContextMenuItem("Move to Bottom", vm.MoveToBottomCommand, item, index < count - 1, itemStyle));

        menu.PlacementTarget = listBoxItem;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static MenuItem CreateContextMenuItem(string header, ICommand command, object parameter, bool isEnabled, Style style)
    {
        return new MenuItem
        {
            Header = new TextBlock
            {
                Text = header,
                Foreground = new SolidColorBrush(isEnabled
                    ? Color.FromRgb(0xCC, 0xCC, 0xCC)
                    : Color.FromRgb(0x6E, 0x6E, 0x6E))
            },
            Command = command,
            CommandParameter = parameter,
            IsEnabled = isEnabled,
            Style = style
        };
    }

    private static Style CreateContextMenuItemStyle()
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(MenuItem.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(MenuItem.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))));

        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var itemBorder = new FrameworkElementFactory(typeof(Border));
        itemBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(MenuItem.BackgroundProperty));
        itemBorder.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        itemBorder.AppendChild(contentPresenter);
        itemTemplate.VisualTree = itemBorder;
        style.Setters.Add(new Setter(MenuItem.TemplateProperty, itemTemplate));

        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x2E))));
        style.Triggers.Add(hoverTrigger);

        return style;
    }

    // ── Internal drag-and-drop reorder ──

    private void OnItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnItemPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var diff = e.GetPosition(null) - _dragStartPoint;

        // Only start drag if mouse has moved far enough (avoid accidental drags)
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is not ListBoxItem listBoxItem ||
            listBoxItem.DataContext is not PlaylistItemViewModel item)
            return;

        _draggedItem = item;

        var data = new DataObject();
        data.SetData(InternalDragFormat, item);

        DragDrop.DoDragDrop(listBoxItem, data, DragDropEffects.Move);

        _draggedItem = null;
        ClearInsertionIndicator();
    }

    private void OnItemDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(InternalDragFormat))
            return;

        e.Effects = DragDropEffects.Move;
        UpdateInsertionIndicator(sender as ListBoxItem, e);
        e.Handled = true;
    }

    private void OnItemDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(InternalDragFormat))
            return;

        e.Effects = DragDropEffects.Move;
        UpdateInsertionIndicator(sender as ListBoxItem, e);

        // Auto-scroll when near edges
        AutoScrollListBox(e);

        e.Handled = true;
    }

    private void OnItemDragLeave(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(InternalDragFormat))
            return;

        ClearInsertionIndicator();
        e.Handled = true;
    }

    private void OnItemDrop(object sender, DragEventArgs e)
    {
        ClearInsertionIndicator();

        if (!e.Data.GetDataPresent(InternalDragFormat))
            return;

        if (sender is not ListBoxItem targetItem ||
            targetItem.DataContext is not PlaylistItemViewModel targetVm ||
            DataContext is not PlaylistViewModel vm ||
            _draggedItem is null)
            return;

        var fromIndex = vm.Items.IndexOf(_draggedItem);
        var toIndex = vm.Items.IndexOf(targetVm);

        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            e.Handled = true;
            return;
        }

        // Determine if dropping above or below the target
        var pos = e.GetPosition(targetItem);
        var dropBelow = pos.Y > targetItem.ActualHeight / 2;

        if (dropBelow && toIndex < vm.Items.Count - 1 && toIndex >= fromIndex)
            toIndex++;
        else if (!dropBelow && toIndex > 0 && toIndex <= fromIndex)
            toIndex--;

        if (fromIndex != toIndex)
            vm.MoveItem(fromIndex, toIndex);

        e.Handled = true;
    }

    // ── Insertion indicator ──

    private Border? _insertionIndicator;

    private void UpdateInsertionIndicator(ListBoxItem? targetItem, DragEventArgs e)
    {
        if (targetItem is null) return;

        var pos = e.GetPosition(targetItem);
        var showBelow = pos.Y > targetItem.ActualHeight / 2;

        // Find or create the indicator
        EnsureInsertionIndicator();
        if (_insertionIndicator is null) return;

        _insertionIndicator.Visibility = Visibility.Visible;

        // Position the indicator relative to the target item
        var transform = targetItem.TranslatePoint(new Point(0, 0), PlaylistListBox);
        var topY = showBelow ? transform.Y + targetItem.ActualHeight : transform.Y;

        _insertionIndicator.Margin = new Thickness(4, topY - 1, 4, 0);
    }

    private void EnsureInsertionIndicator()
    {
        if (_insertionIndicator is not null) return;

        _insertionIndicator = new Border
        {
            Height = 2,
            Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)), // Accent blue
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        // Add to the content area grid (row 2 — same row as the ListBox)
        var contentGrid = PlaylistListBox.Parent as Grid;
        if (contentGrid is not null)
        {
            Grid.SetRow(_insertionIndicator, 0);
            contentGrid.Children.Add(_insertionIndicator);
        }
    }

    private void ClearInsertionIndicator()
    {
        if (_insertionIndicator is not null)
            _insertionIndicator.Visibility = Visibility.Collapsed;
    }

    private void AutoScrollListBox(DragEventArgs e)
    {
        var scrollViewer = FindVisualChild<ScrollViewer>(PlaylistListBox);
        if (scrollViewer is null) return;

        var pos = e.GetPosition(PlaylistListBox);
        const double scrollZone = 30;
        const double scrollStep = 10;

        if (pos.Y < scrollZone)
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollStep);
        else if (pos.Y > PlaylistListBox.ActualHeight - scrollZone)
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollStep);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;
            var found = FindVisualChild<T>(child);
            if (found is not null)
                return found;
        }
        return null;
    }

    // ── Recent Playlists Dropdown ──

    private void OnRecentButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlaylistViewModel vm || vm.RecentPlaylists.Count == 0)
            return;

        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)),
            BorderThickness = new Thickness(1),
            Style = CreateFlatContextMenuStyle(),
            ItemContainerStyle = CreateContextMenuItemStyle()
        };

        foreach (var path in vm.RecentPlaylists)
        {
            var nameText = new TextBlock
            {
                Text = System.IO.Path.GetFileNameWithoutExtension(path),
                MaxWidth = 180,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
            };
            var menuItem = new MenuItem
            {
                Header = nameText,
                ToolTip = path,
                Command = vm.OpenRecentPlaylistCommand,
                CommandParameter = path
            };
            menu.Items.Add(menuItem);
        }

        menu.PlacementTarget = (Button)sender;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static Style CreateFlatContextMenuStyle()
    {
        var style = new Style(typeof(ContextMenu));

        var template = new ControlTemplate(typeof(ContextMenu));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)));
        border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        border.SetValue(Border.PaddingProperty, new Thickness(2));

        var presenter = new FrameworkElementFactory(typeof(StackPanel));
        presenter.SetValue(StackPanel.IsItemsHostProperty, true);
        presenter.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
        border.AppendChild(presenter);

        template.VisualTree = border;
        style.Setters.Add(new Setter(ContextMenu.TemplateProperty, template));
        style.Setters.Add(new Setter(ContextMenu.HasDropShadowProperty, true));

        return style;
    }
}
