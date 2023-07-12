namespace JWueller.Jellyfin.OnePace.Model;

/// <summary>
/// Represents a series episode.
/// </summary>
public interface IEpisode
{
    /// <summary>
    /// Gets the number of the episode within the arc.
    /// </summary>
    int Number { get; }

    /// <summary>
    /// Gets the arc number the episode belongs to.
    /// </summary>
    int ArcNumber { get; }

    /// <summary>
    /// Gets the invariant title of the episode, e.g., "Romance Dawn 01".
    /// </summary>
    string InvariantTitle { get; }

    /// <summary>
    /// Gets the manga chapters associated with the episode. Null if unknown or not applicable.
    /// </summary>
    string? MangaChapters { get; }

    /// <summary>
    /// Gets the release date of the episode. Null if release date is unknown or the episode is unreleased.
    /// </summary>
    System.DateTime? ReleaseDate { get; }
}
