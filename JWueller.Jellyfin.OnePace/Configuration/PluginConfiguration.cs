using MediaBrowser.Model.Plugins;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        PreferDiscordForArcPosters = false;
    }

    /// <summary>
    /// Gets or sets a value indicating whether discord is used as a cover art source or not.
    /// </summary>
    public bool PreferDiscordForArcPosters { get; set; }
}
