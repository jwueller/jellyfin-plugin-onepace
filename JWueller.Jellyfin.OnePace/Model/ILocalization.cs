namespace JWueller.Jellyfin.OnePace.Model;

/// <summary>
/// Represents the localization for either a series, an arc, or an episode.
/// </summary>
public interface ILocalization
{
    /// <summary>
    /// Gets the ISO 639-1 language code for the content.
    /// </summary>
    string LanguageCode { get; }

    /// <summary>
    /// Gets the title of the content in the respective language.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the description of the content in the respective language. Null if no description is provided.
    /// </summary>
    string? Description { get; }
}
