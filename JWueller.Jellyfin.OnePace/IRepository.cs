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
    /// Retrieves the arc model based on the ID.
    /// </summary>
    /// <param name="id">ID of the arc.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model or <c>null</c> if not found.</returns>
    Task<IArc?> FindArcByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the models for all known episodes.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>A read-only collection of episode models.</returns>
    Task<IReadOnlyCollection<IEpisode>> FindAllEpisodesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the arc and episode model based on the ID.
    /// </summary>
    /// <param name="id">ID of the episode.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc and episode models or <c>null</c> if not found.</returns>
    Task<IEpisode?> FindEpisodeByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the available series logo art.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    Task<IReadOnlyCollection<IArt>> FindAllLogoArtBySeriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the available series cover art.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    Task<IReadOnlyCollection<IArt>> FindAllCoverArtBySeriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the available arc cover art.
    /// </summary>
    /// <param name="arcId">ID of the arc.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    Task<IReadOnlyCollection<IArt>> FindAllCoverArtByArcIdAsync(string arcId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the available episode cover art.
    /// </summary>
    /// <param name="episodeId">ID of the episode.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    Task<IReadOnlyCollection<IArt>> FindAllCoverArtByEpisodeIdAsync(string episodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the series localization data.
    /// </summary>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The series model.</returns>
    Task<ILocalization?> FindBestLocalizationBySeriesAsync(string languageCode, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the arc localization data.
    /// </summary>
    /// <param name="arcId">ID of the arc.</param>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model.</returns>
    Task<ILocalization?> FindBestLocalizationByArcIdAsync(string arcId, string languageCode, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the episode localization data.
    /// </summary>
    /// <param name="episodeId">ID of the episode.</param>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model.</returns>
    Task<ILocalization?> FindBestLocalizationByEpisodeIdAsync(string episodeId, string languageCode, CancellationToken cancellationToken);
}
