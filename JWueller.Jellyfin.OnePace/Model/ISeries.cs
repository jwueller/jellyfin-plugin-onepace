namespace JWueller.Jellyfin.OnePace.Model;

/// <summary>
/// Represents the series.
/// </summary>
public interface ISeries
{
    /// <summary>
    /// Gets the invariant title of the series, e.g., "One Pace".
    /// </summary>
    string InvariantTitle { get; }

    /// <summary>
    /// Gets the original title of the series, e.g., "One Piece".
    /// </summary>
    string OriginalTitle { get; }
}
