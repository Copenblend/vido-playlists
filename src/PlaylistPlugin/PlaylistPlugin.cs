using Vido.Core.Plugin;

namespace PlaylistPlugin;

/// <summary>
/// Playlists plugin entry point. Implements the Vido plugin lifecycle.
/// </summary>
public class PlaylistPlugin : IVidoPlugin
{
    private IPluginContext? _context;

    public void Activate(IPluginContext context)
    {
        _context = context;
        _context.Logger.Info("Playlists plugin activated", "PlaylistPlugin");
    }

    public void Deactivate()
    {
        _context?.Logger.Info("Playlists plugin deactivated", "PlaylistPlugin");
        _context = null;
    }
}
