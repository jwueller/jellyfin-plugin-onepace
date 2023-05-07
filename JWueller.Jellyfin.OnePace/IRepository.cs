using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JWueller.Jellyfin.OnePace.Model;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Provides One Pace metadata.
/// </summary>
public interface IRepository
{
    /// <summary>
    /// Retrieves the series model.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The series model.</returns>
    Task<ISeries?> FindSeriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the models for all known arcs.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>A read-only collection of arc models.</returns>
    Task<IReadOnlyCollection<IArc>> FindAllArcsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the arc model based on the number.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model or <c>null</c> if not found.</returns>
    Task<IArc?> FindArcByNumberAsync(int arcNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the models for all known episodes.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>A read-only collection of episode models.</returns>
    Task<IReadOnlyCollection<IEpisode>> FindAllEpisodesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the arc and episode model based on the number.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="episodeNumber">Number of the episode within an arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc and episode models or <c>null</c> if not found.</returns>
    Task<IEpisode?> FindEpisodeByNumberAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the available series logo art.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    Task<IReadOnlyCollection<IArt>> FindAllSeriesLogoArtAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the available series cover art.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    Task<IReadOnlyCollection<IArt>> FindAllSeriesCoverArtAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the available arc cover art.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    Task<IReadOnlyCollection<IArt>> FindAllArcCoverArtAsync(int arcNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the available episode cover art.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="episodeNumber">Number of the episode within an arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    Task<IReadOnlyCollection<IArt>> FindAllEpisodeCoverArtAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the series localization data.
    /// </summary>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The series model.</returns>
    Task<ILocalization?> FindBestSeriesLocalizationAsync(string languageCode, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the arc localization data.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model.</returns>
    Task<ILocalization?> FindBestArcLocalizationAsync(int arcNumber, string languageCode, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the episode localization data.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="episodeNumber">Number of the episode within an arc (1-based).</param>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model.</returns>
    Task<ILocalization?> FindBestEpisodeLocalizationAsync(int arcNumber, int episodeNumber, string languageCode, CancellationToken cancellationToken);
}
