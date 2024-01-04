using System;

namespace JWueller.Jellyfin.OnePace.Model;

/// <summary>
/// Represents a series episode.
/// </summary>
public interface IEpisode
{
    /// <summary>
    /// Gets the CUID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the GUID of the arc that the episode belongs to.
    /// </summary>
    string ArcId { get; }

    /// <summary>
    /// Gets the rank (i.e. order) of the episode within the arc.
    /// </summary>
    int Rank { get; }

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
    DateTime? ReleaseDate { get; }

    /// <summary>
    /// Gets the CRC-32 checksum of the episode file. Null if unknown or the episode is unreleased.
    /// </summary>
    uint? Crc32 { get; }
}
