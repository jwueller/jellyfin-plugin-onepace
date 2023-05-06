using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JWueller.Jellyfin.OnePace.Model;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Provides One Pace metadata from the project website.
/// </summary>
public class OnePaceRepository
{
    private const string FallbackLanguageCode = "en";
    private static readonly string FallbackRawLanguageCode = ToRawLanguageCode(FallbackLanguageCode);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<OnePaceRepository> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnePaceRepository"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory used to fetch metadata.</param>
    /// <param name="memoryCache">The cache used to prevent repeated endpoint requests.</param>
    /// <param name="logger">The log target for this class.</param>
    public OnePaceRepository(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, ILogger<OnePaceRepository> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _log = logger;
    }

    private async Task<string> GetOrFetchAsync(string url, CancellationToken cancellationToken)
    {
        return await _memoryCache.GetOrCreateAsync(url, async cacheEntry =>
        {
            _log.LogTrace("Fetching: {0}", url);

            var response = await _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // TODO: Try to honor the returned caching headers?
            cacheEntry.SlidingExpiration = TimeSpan.FromHours(1);

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task<JsonElement?> GetOrFetchJsonAsync(string url, CancellationToken cancellationToken)
    {
        var json = await GetOrFetchAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(json).RootElement;
    }

    private async Task<JsonElement?> GetOrFetchRawMetadataAsync(string rawLanguageCode, CancellationToken cancellationToken)
    {
        var url = string.Format(CultureInfo.InvariantCulture, "https://onepace.net/static/locales/{0}/home.json", rawLanguageCode);

        try
        {
            return await GetOrFetchJsonAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // This means that the language code is not supported upstream. We can't do anything about it, but the
                // consumer should be able to handle it and fall back to something reasonable. This is not strictly an
                // error.
                return null;
            }
            else
            {
                _log.LogWarning(ex, "Failed to fetch One Pace metadata for language code: {0}", rawLanguageCode);
                throw;
            }
        }
    }

    private async Task<JsonElement?> GetOrFetchRawContentAsync(CancellationToken cancellationToken)
    {
        const string Query = @"
            query {
                databaseGetAllArcs {
                    part,
                    title,
                    manga_chapters,
                    released_date,

                    translations {
                        title,
                        description,
                        language {
                            code,
                        },
                    },

                    images {
                        src,
                        width,
                    },

                    episodes {
                        part,
                        title,
                        manga_chapters,
                        released_date,

                        translations {
                            title,
                            description,
                            language {
                                code,
                            },
                        },

                        images {
                            src,
                            width,
                        },
                    }
                }
            }
        ";

        var url = string.Format(CultureInfo.InvariantCulture, "https://onepace.net/api/graphql?query={0}", Uri.EscapeDataString(Query));

        try
        {
            return await GetOrFetchJsonAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _log.LogError(ex, "Could not find the One Pace content data, please report a bug at https://github.com/jwueller/jellyfin-plugin-onepace if this happened on the latest version");
            }
            else
            {
                _log.LogWarning(ex, "Failed to fetch One Pace content data");
            }

            throw;
        }
    }

    // One Pace doesn't quite seem to follow the same locales that Jellyfin does, so we do some best-effort mapping here.
    private static string ToRawLanguageCode(string languageCode)
    {
        if (languageCode.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh_cn";
        }

        return languageCode.Replace("-", "_", StringComparison.InvariantCultureIgnoreCase);
    }

    private static string ToLanguageCode(string rawLanguageCode)
    {
        if (rawLanguageCode.Equals("zh_cn", StringComparison.OrdinalIgnoreCase))
        {
            return "zh";
        }

        return rawLanguageCode.Replace("_", "-", StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool LanguageCodesEqual(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement? ChooseBestRawTranslation(JsonElement.ArrayEnumerator rawCandidates, string rawLanguageCode)
    {
        foreach (var rawCandidate in rawCandidates)
        {
            if (LanguageCodesEqual(rawCandidate.GetProperty("language").GetProperty("code").GetNonNullString(), rawLanguageCode))
            {
                return rawCandidate;
            }
        }

        // Fall back to a known good translation that should always be present.
        if (!LanguageCodesEqual(rawLanguageCode, FallbackRawLanguageCode))
        {
            return ChooseBestRawTranslation(rawCandidates, FallbackRawLanguageCode);
        }

        return null;
    }

    private async Task<JsonElement?> FindRawArcByNumberAsync(int arcNumber, CancellationToken cancellationToken)
    {
        try
        {
            var rawResponse = await GetOrFetchRawContentAsync(cancellationToken).ConfigureAwait(false);
            if (rawResponse != null)
            {
                foreach (var rawArc in rawResponse.Value.GetProperty("data").GetProperty("databaseGetAllArcs").EnumerateArray())
                {
                    if (rawArc.GetProperty("part").GetInt32() == arcNumber)
                    {
                        return rawArc;
                    }
                }
            }
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
        }

        return null;
    }

    private async Task<JsonElement?> FindRawEpisodeByNumberAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken)
    {
        var rawArc = await FindRawArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (rawArc != null)
        {
            foreach (var rawEpisode in rawArc.Value.GetProperty("episodes").EnumerateArray())
            {
                if (rawEpisode.GetProperty("part").GetInt32() == episodeNumber)
                {
                    return rawEpisode;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves the series model.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The series model.</returns>
    public async Task<ISeries?> FindSeriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rawResponse = await GetOrFetchRawMetadataAsync(FallbackRawLanguageCode, cancellationToken).ConfigureAwait(false);
            if (rawResponse != null)
            {
                return new RepositorySeries();
            }
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
        }

        return null;
    }

    /// <summary>
    /// Retrieves the models for all known arcs.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>A read-only collection of arc models.</returns>
    public async Task<IReadOnlyCollection<IArc>> FindAllArcsAsync(CancellationToken cancellationToken)
    {
        var results = new List<IArc>();

        try
        {
            var rawResponse = await GetOrFetchRawContentAsync(cancellationToken).ConfigureAwait(false);
            if (rawResponse != null)
            {
                foreach (var rawArc in rawResponse.Value.GetProperty("data").GetProperty("databaseGetAllArcs").EnumerateArray())
                {
                    results.Add(new RepositoryArc(rawArc));
                }
            }
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
        }

        return results;
    }

    /// <summary>
    /// Retrieves the arc model based on the number.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model or <c>null</c> if not found.</returns>
    public async Task<IArc?> FindArcByNumberAsync(int arcNumber, CancellationToken cancellationToken)
    {
        var rawArc = await FindRawArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (rawArc != null)
        {
            return new RepositoryArc(rawArc.Value);
        }

        return null;
    }

    /// <summary>
    /// Retrieves the models for all known episodes.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>A read-only collection of episode models.</returns>
    public async Task<IReadOnlyCollection<IEpisode>> FindAllEpisodesAsync(CancellationToken cancellationToken)
    {
        var results = new List<IEpisode>();

        try
        {
            var rawResponse = await GetOrFetchRawContentAsync(cancellationToken).ConfigureAwait(false);
            if (rawResponse != null)
            {
                foreach (var rawArc in rawResponse.Value.GetProperty("data").GetProperty("databaseGetAllArcs").EnumerateArray())
                {
                    foreach (var rawEpisode in rawArc.GetProperty("episodes").EnumerateArray())
                    {
                        results.Add(new RepositoryEpisode(rawArc.GetProperty("part").GetInt32(), rawEpisode));
                    }
                }
            }
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
        }

        return results;
    }

    /// <summary>
    /// Retrieves the arc and episode model based on the number.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="episodeNumber">Number of the episode within an arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc and episode models or <c>null</c> if not found.</returns>
    public async Task<IEpisode?> FindEpisodeByNumberAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken)
    {
        var rawEpisode = await FindRawEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken).ConfigureAwait(false);
        if (rawEpisode != null)
        {
            return new RepositoryEpisode(arcNumber, rawEpisode.Value);
        }

        return null;
    }

    /// <summary>
    /// Retrieves the available series logo art.
    /// </summary>
    /// <returns>The art model.</returns>
    public Task<IReadOnlyCollection<IArt>> FindAllSeriesLogoArtAsync()
    {
        var results = new List<IArt>();
        results.Add(new RepositoryArt("https://onepace.net/images/one-pace-logo.svg"));
        return Task.FromResult<IReadOnlyCollection<IArt>>(results);
    }

    /// <summary>
    /// Retrieves the available series cover art.
    /// </summary>
    /// <returns>The art model.</returns>
    public Task<IReadOnlyCollection<IArt>> FindAllSeriesCoverArtAsync()
    {
        return Task.FromResult<IReadOnlyCollection<IArt>>(new List<IArt>());
    }

    /// <summary>
    /// Retrieves the available arc cover art.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    public async Task<IReadOnlyCollection<IArt>> FindAllArcCoverArtAsync(int arcNumber, CancellationToken cancellationToken)
    {
        var results = new List<IArt>();

        var rawArc = await FindRawArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (rawArc != null)
        {
            foreach (var rawImage in rawArc.Value.GetProperty("images").EnumerateArray())
            {
                results.Add(new RepositoryArt("https://onepace.net/images/arcs/", rawImage));
            }
        }

        return results;
    }

    /// <summary>
    /// Retrieves the available episode cover art.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="episodeNumber">Number of the episode within an arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    public async Task<IReadOnlyCollection<IArt>> FindAllEpisodeCoverArtAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken)
    {
        var results = new List<IArt>();

        var rawEpisode = await FindRawEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken).ConfigureAwait(false);
        if (rawEpisode != null)
        {
            foreach (var rawImage in rawEpisode.Value.GetProperty("images").EnumerateArray())
            {
                results.Add(new RepositoryArt("https://onepace.net/images/episodes/", rawImage));
            }
        }

        return results;
    }

    /// <summary>
    /// Retrieves the series localization data.
    /// </summary>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The series model.</returns>
    public async Task<ILocalization?> FindBestSeriesLocalizationAsync(string languageCode, CancellationToken cancellationToken)
    {
        try
        {
            var rawResponse = await GetOrFetchRawMetadataAsync(ToRawLanguageCode(languageCode), cancellationToken).ConfigureAwait(false);
            if (rawResponse != null)
            {
                return new RepositoryLocalization(languageCode, rawResponse.Value);
            }
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
        }

        return null;
    }

    /// <summary>
    /// Retrieves the arc localization data.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model.</returns>
    public async Task<ILocalization?> FindBestArcLocalizationAsync(int arcNumber, string languageCode, CancellationToken cancellationToken)
    {
        var rawArc = await FindRawArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (rawArc != null)
        {
            var rawTranslation = ChooseBestRawTranslation(rawArc.Value.GetProperty("translations").EnumerateArray(), ToRawLanguageCode(languageCode));
            if (rawTranslation != null)
            {
                return new RepositoryLocalization(rawTranslation.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves the episode localization data.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="episodeNumber">Number of the episode within an arc (1-based).</param>
    /// <param name="languageCode">Preferred language code.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model.</returns>
    public async Task<ILocalization?> FindBestEpisodeLocalizationAsync(int arcNumber, int episodeNumber, string languageCode, CancellationToken cancellationToken)
    {
        var rawEpisode = await FindRawEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken).ConfigureAwait(false);
        if (rawEpisode != null)
        {
            var rawTranslation = ChooseBestRawTranslation(rawEpisode.Value.GetProperty("translations").EnumerateArray(), ToRawLanguageCode(languageCode));
            if (rawTranslation != null)
            {
                return new RepositoryLocalization(rawTranslation.Value);
            }
        }

        return null;
    }

    private static DateTime? ParseReleaseDate(JsonElement jsonElement)
    {
        var releasedDateString = jsonElement.GetString();
        if (releasedDateString == null || releasedDateString.Equals("unreleased", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return DateTime.Parse(releasedDateString, CultureInfo.InvariantCulture).ToUniversalTime();
    }

    private sealed class RepositorySeries : ISeries
    {
        public string InvariantTitle => "One Pace";

        public string OriginalTitle => "One Piece";
    }

    private sealed class RepositoryArc : IArc
    {
        public RepositoryArc(JsonElement rawArc)
        {
            Number = rawArc.GetProperty("part").GetInt32();
            InvariantTitle = rawArc.GetProperty("title").GetNonNullString();
            MangaChapters = rawArc.GetProperty("manga_chapters").GetNonNullString();
            ReleaseDate = ParseReleaseDate(rawArc.GetProperty("released_date"));
        }

        public int Number { get; }

        public string InvariantTitle { get; }

        public string MangaChapters { get; }

        public DateTime? ReleaseDate { get; }
    }

    private sealed class RepositoryEpisode : IEpisode
    {
        public RepositoryEpisode(int arcNumber, JsonElement rawEpisode)
        {
            Number = rawEpisode.GetProperty("part").GetInt32();
            ArcNumber = arcNumber;
            InvariantTitle = rawEpisode.GetProperty("title").GetNonNullString();
            MangaChapters = rawEpisode.GetProperty("manga_chapters").GetNonNullString();
            ReleaseDate = ParseReleaseDate(rawEpisode.GetProperty("released_date"));
        }

        public int Number { get; }

        public int ArcNumber { get; }

        public string InvariantTitle { get; }

        public string MangaChapters { get; }

        public DateTime? ReleaseDate { get; }
    }

    private sealed class RepositoryArt : IArt
    {
        public RepositoryArt(string url)
        {
            Url = url;
        }

        public RepositoryArt(string baseUrl, JsonElement rawImage)
        {
            Url = baseUrl + rawImage.GetProperty("src").GetNonNullString();
            Width = rawImage.GetProperty("width").CoerceNullableInt32();
        }

        public string Url { get; }

        public int? Width { get; }

        public int? Height { get; }
    }

    private sealed class RepositoryLocalization : ILocalization
    {
        public RepositoryLocalization(string languageCode, JsonElement rawTranslation)
        {
            LanguageCode = languageCode;

            var rawTitle = rawTranslation.GetProperty("meta-title").GetString();
            Title = rawTitle != null ? rawTitle.Split("|")[0].Trim() : "One Pace";

            Description = rawTranslation.GetProperty("meta-description").GetString();
        }

        public RepositoryLocalization(JsonElement rawTranslation)
        {
            LanguageCode = ToLanguageCode(rawTranslation.GetProperty("language").GetProperty("code").GetNonNullString());
            Title = rawTranslation.GetProperty("title").GetNonNullString();
            Description = rawTranslation.GetProperty("description").GetString();
        }

        public string LanguageCode { get; }

        public string Title { get; }

        public string? Description { get; }
    }
}
