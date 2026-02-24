using PlaylistPlugin.Services;
using PlaylistPlugin.ViewModels;
using PlaylistPlugin.Views;
using Vido.Core.Plugin;

namespace PlaylistPlugin;

/// <summary>
/// Playlists plugin entry point. Implements the Vido plugin lifecycle.
/// Creates the playlist ViewModel and registers the sidebar panel and context menu.
/// </summary>
public class PlaylistPlugin : IVidoPlugin
{
    private IPluginContext? _context;
    private PlaylistViewModel? _viewModel;

    public void Activate(IPluginContext context)
    {
        _context = context;

        var fileService = new PlaylistFileService();
        _viewModel = new PlaylistViewModel(fileService, context.VideoEngine, context.Events);

        // Register the playlist sidebar panel with Vido
        context.RegisterSidebarPanel("playlist-sidebar",
            () => new PlaylistSidebarView { DataContext = _viewModel });

        // Register "Add to Playlist" context menu item in the file explorer
        context.RegisterContextMenuHandler("add-to-playlist", node =>
        {
            _viewModel.AddFromFileNode(node.FullPath, node.IsDirectory);
        });

        _context.Logger.Info("Playlists plugin activated", "PlaylistPlugin");
    }

    public void Deactivate()
    {
        _viewModel?.Dispose();
        _viewModel = null;

        _context?.Logger.Info("Playlists plugin deactivated", "PlaylistPlugin");
        _context = null;
    }
}
