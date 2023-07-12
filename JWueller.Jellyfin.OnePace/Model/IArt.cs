namespace JWueller.Jellyfin.OnePace.Model;

/// <summary>
/// Represents artwork for either a series, an arc, or an episode.
/// </summary>
public interface IArt
{
    /// <summary>
    /// Gets the URL of the artwork.
    /// </summary>
    string Url { get; }

    /// <summary>
    /// Gets the width of the artwork in pixels. Null if unknown.
    /// </summary>
    int? Width { get; }

    /// <summary>
    /// Gets the height of the artwork in pixels. Null if unknown.
    /// </summary>
    int? Height { get; }
}
