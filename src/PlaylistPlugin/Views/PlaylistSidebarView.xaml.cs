using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PlaylistPlugin.ViewModels;

namespace PlaylistPlugin.Views;

/// <summary>
/// Code-behind for the playlist sidebar view. Handles drag-and-drop
/// from Windows Explorer, double-click-to-play, and recent playlists dropdown.
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
            BorderThickness = new Thickness(1)
        };

        // Style to remove the icon/check column and left-align text
        var menuItemStyle = new Style(typeof(MenuItem));
        menuItemStyle.Setters.Add(new Setter(MenuItem.BackgroundProperty, Brushes.Transparent));
        menuItemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))));

        // Custom template: simple border with left-aligned content
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var itemBorder = new FrameworkElementFactory(typeof(Border));
        itemBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(MenuItem.BackgroundProperty));
        itemBorder.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        itemBorder.AppendChild(contentPresenter);
        itemTemplate.VisualTree = itemBorder;
        menuItemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, itemTemplate));

        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x2E))));
        menuItemStyle.Triggers.Add(hoverTrigger);

        menu.ItemContainerStyle = menuItemStyle;

        // Hide the icon column by overriding the menu template
        menu.Style = CreateFlatContextMenuStyle();

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
