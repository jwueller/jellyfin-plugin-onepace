namespace JWueller.Jellyfin.OnePace.Model;

/// <summary>
/// Represents a series arc.
/// </summary>
public interface IArc
{
    /// <summary>
    /// Gets the CUID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the number of the arc within the series.
    /// </summary>
    int Number { get; }

    /// <summary>
    /// Gets the invariant title of the arc, e.g., "Romance Dawn".
    /// </summary>
    string InvariantTitle { get; }

    /// <summary>
    /// Gets the manga chapters associated with the arc. Null if unknown or not applicable.
    /// </summary>
    string? MangaChapters { get; }

    /// <summary>
    /// Gets the release date of the arc. Null if release date is unknown or the arc is unreleased.
    /// </summary>
    System.DateTime? ReleaseDate { get; }
}
