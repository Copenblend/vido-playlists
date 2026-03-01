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
    private ScrollViewer? _playlistScrollViewer;
    private ContextMenu? _itemContextMenu;
    private ContextMenu? _recentPlaylistsMenu;
    private MenuItem? _moveToTopMenuItem;
    private MenuItem? _moveUpMenuItem;
    private MenuItem? _moveDownMenuItem;
    private MenuItem? _moveToBottomMenuItem;
    private MenuItem? _removeItemMenuItem;

    private static readonly Brush MenuBackgroundBrush = CreateFrozenBrush(0x25, 0x25, 0x26);
    private static readonly Brush MenuForegroundBrush = CreateFrozenBrush(0xCC, 0xCC, 0xCC);
    private static readonly Brush MenuBorderBrush = CreateFrozenBrush(0x3C, 0x3C, 0x3C);
    private static readonly Brush MenuHoverBrush = CreateFrozenBrush(0x2A, 0x2D, 0x2E);
    private static readonly Brush MenuDisabledForegroundBrush = CreateFrozenBrush(0x6E, 0x6E, 0x6E);
    private static readonly Brush InsertionIndicatorBrush = CreateFrozenBrush(0x00, 0x7A, 0xCC);
    private static readonly Style FlatContextMenuStyle = CreateFlatContextMenuStyle();
    private static readonly Style ContextMenuItemStyle = CreateContextMenuItemStyle();

    private static readonly string InternalDragFormat = "PlaylistInternalDrag";

    public PlaylistSidebarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ = GetPlaylistScrollViewer();
        _itemContextMenu ??= CreateItemContextMenu();
        _recentPlaylistsMenu ??= CreateRecentPlaylistsMenu();
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

        _itemContextMenu ??= CreateItemContextMenu();

        UpdateItemContextMenu(vm, item, index, count);

        _itemContextMenu.PlacementTarget = listBoxItem;
        _itemContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        _itemContextMenu.IsOpen = true;
    }

    private void UpdateItemContextMenu(PlaylistViewModel vm, PlaylistItemViewModel item, int index, int count)
    {
        if (_moveToTopMenuItem is null ||
            _moveUpMenuItem is null ||
            _moveDownMenuItem is null ||
            _moveToBottomMenuItem is null ||
            _removeItemMenuItem is null)
        {
            return;
        }

        var canMoveUp = index > 0;
        var canMoveDown = index < count - 1;

        _moveToTopMenuItem.Command = vm.MoveToTopCommand;
        _moveToTopMenuItem.CommandParameter = item;
        _moveToTopMenuItem.IsEnabled = canMoveUp;

        _moveUpMenuItem.Command = vm.MoveUpCommand;
        _moveUpMenuItem.CommandParameter = item;
        _moveUpMenuItem.IsEnabled = canMoveUp;

        _moveDownMenuItem.Command = vm.MoveDownCommand;
        _moveDownMenuItem.CommandParameter = item;
        _moveDownMenuItem.IsEnabled = canMoveDown;

        _moveToBottomMenuItem.Command = vm.MoveToBottomCommand;
        _moveToBottomMenuItem.CommandParameter = item;
        _moveToBottomMenuItem.IsEnabled = canMoveDown;

        _removeItemMenuItem.Command = vm.RemoveItemCommand;
        _removeItemMenuItem.CommandParameter = item;
        _removeItemMenuItem.IsEnabled = true;
    }

    private ContextMenu CreateItemContextMenu()
    {
        _moveToTopMenuItem = CreateContextMenuItem("Move to Top", command: null, parameter: null, isEnabled: false, ContextMenuItemStyle);
        _moveUpMenuItem = CreateContextMenuItem("Move Up", command: null, parameter: null, isEnabled: false, ContextMenuItemStyle);
        _moveDownMenuItem = CreateContextMenuItem("Move Down", command: null, parameter: null, isEnabled: false, ContextMenuItemStyle);
        _moveToBottomMenuItem = CreateContextMenuItem("Move to Bottom", command: null, parameter: null, isEnabled: false, ContextMenuItemStyle);
        _removeItemMenuItem = CreateContextMenuItem("Remove from Playlist", command: null, parameter: null, isEnabled: true, ContextMenuItemStyle);

        var menu = new ContextMenu
        {
            Background = MenuBackgroundBrush,
            Foreground = MenuForegroundBrush,
            BorderBrush = MenuBorderBrush,
            BorderThickness = new Thickness(1),
            Style = FlatContextMenuStyle
        };

        menu.Items.Add(_moveToTopMenuItem);
        menu.Items.Add(_moveUpMenuItem);
        menu.Items.Add(_moveDownMenuItem);
        menu.Items.Add(_moveToBottomMenuItem);
        menu.Items.Add(_removeItemMenuItem);

        return menu;
    }

    private static MenuItem CreateContextMenuItem(string header, ICommand? command, object? parameter, bool isEnabled, Style style)
    {
        return new MenuItem
        {
            Header = header,
            Command = command,
            CommandParameter = parameter,
            IsEnabled = isEnabled,
            Foreground = MenuForegroundBrush,
            Style = style
        };
    }

    private static Style CreateContextMenuItemStyle()
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(MenuItem.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(MenuItem.ForegroundProperty, MenuForegroundBrush));

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

        var disabledTrigger = new Trigger { Property = MenuItem.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(MenuItem.ForegroundProperty, MenuDisabledForegroundBrush));
        style.Triggers.Add(disabledTrigger);

        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, MenuHoverBrush));
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
            Background = InsertionIndicatorBrush,
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
        var scrollViewer = GetPlaylistScrollViewer();
        if (scrollViewer is null) return;

        var pos = e.GetPosition(PlaylistListBox);
        const double scrollZone = 30;
        const double scrollStep = 10;

        if (pos.Y < scrollZone)
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollStep);
        else if (pos.Y > PlaylistListBox.ActualHeight - scrollZone)
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollStep);
    }

    private ScrollViewer? GetPlaylistScrollViewer()
    {
        _playlistScrollViewer ??= FindVisualChild<ScrollViewer>(PlaylistListBox);
        return _playlistScrollViewer;
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

        _recentPlaylistsMenu ??= CreateRecentPlaylistsMenu();
        _recentPlaylistsMenu.Items.Clear();

        foreach (var path in vm.RecentPlaylists)
        {
            var menuItem = new MenuItem
            {
                Header = System.IO.Path.GetFileNameWithoutExtension(path),
                ToolTip = path,
                Command = vm.OpenRecentPlaylistCommand,
                CommandParameter = path,
                Style = ContextMenuItemStyle
            };
            _recentPlaylistsMenu.Items.Add(menuItem);
        }

        _recentPlaylistsMenu.PlacementTarget = (Button)sender;
        _recentPlaylistsMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        _recentPlaylistsMenu.IsOpen = true;
    }

    private ContextMenu CreateRecentPlaylistsMenu()
    {
        return new ContextMenu
        {
            Background = MenuBackgroundBrush,
            Foreground = MenuForegroundBrush,
            BorderBrush = MenuBorderBrush,
            BorderThickness = new Thickness(1),
            Style = FlatContextMenuStyle,
            ItemContainerStyle = ContextMenuItemStyle
        };
    }

    private static Style CreateFlatContextMenuStyle()
    {
        var style = new Style(typeof(ContextMenu));

        var template = new ControlTemplate(typeof(ContextMenu));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, MenuBackgroundBrush);
        border.SetValue(Border.BorderBrushProperty, MenuBorderBrush);
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

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
