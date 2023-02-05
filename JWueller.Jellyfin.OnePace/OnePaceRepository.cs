using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Caching.Memory;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Provides One Pace metadata from the project website.
/// </summary>
public class OnePaceRepository
{
    private const string FallbackLanguageCode = "en";
    private static readonly string FallbackRawLanguageCode = ToRawLanguageCode(FallbackLanguageCode);

    private IHttpClientFactory _httpClientFactory;
    private IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnePaceRepository"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory used to fetch metadata.</param>
    /// <param name="memoryCache">The cache used to prevent repeated endpoint requests.</param>
    public OnePaceRepository(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
    }

    private async Task<JsonElement?> GetOrFetchJsonAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await _memoryCache.GetOrCreateAsync(url, async cacheEntry =>
            {
                var response = await _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                // TODO: Try to honor the returned caching headers?
                cacheEntry.SlidingExpiration = TimeSpan.FromHours(1);

                var serializedBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                return JsonDocument.Parse(serializedBody).RootElement;
            }).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // This particular language code might not exist, so we fail gracefully in that case.
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                throw;
            }
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

    private static JsonProperty? ChooseBestRawTranslation(JsonElement.ObjectEnumerator rawCandidates, string rawLanguageCode)
    {
        foreach (var rawCandidate in rawCandidates)
        {
            if (LanguageCodesEqual(rawCandidate.Name, rawLanguageCode))
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

    private async Task<JsonElement?> GetOrFetchRawMetadataAsync(string rawLanguageCode, CancellationToken cancellationToken)
    {
        var url = string.Format(CultureInfo.InvariantCulture, "https://onepace.net/_next/data/XpwD5nv9M19xWD65wexYa/{0}.json", rawLanguageCode);
        return await GetOrFetchJsonAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonElement?> GetOrFetchRawContentAsync(CancellationToken cancellationToken)
    {
        // It seems like all arc/episode translations will be present in all language code requests, so we can just
        // always fetch to a known good one.
        var url = "https://onepace.net/_next/data/XpwD5nv9M19xWD65wexYa/en/watch.json";
        return await GetOrFetchJsonAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonElement?> FindRawArcByNumberAsync(int arcNumber, CancellationToken cancellationToken)
    {
        var rawResponse = await GetOrFetchRawContentAsync(cancellationToken).ConfigureAwait(false);
        if (rawResponse != null)
        {
            foreach (var rawArc in rawResponse.Value.GetProperty("pageProps").GetProperty("arcs").EnumerateArray())
            {
                if (rawArc.GetProperty("part").GetInt32() == arcNumber)
                {
                    return rawArc;
                }
            }
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
    public async Task<Model.ISeries?> FindSeriesAsync(CancellationToken cancellationToken)
    {
        var rawResponse = await GetOrFetchRawMetadataAsync(FallbackRawLanguageCode, cancellationToken).ConfigureAwait(false);

        return new RepositorySeries();
    }

    /// <summary>
    /// Retrieves the models for all known arcs.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>A read-only collection of arc models.</returns>
    public async Task<IReadOnlyCollection<Model.IArc>> FindAllArcsAsync(CancellationToken cancellationToken)
    {
        var results = new List<Model.IArc>();

        var rawResponse = await GetOrFetchRawContentAsync(cancellationToken).ConfigureAwait(false);
        if (rawResponse != null)
        {
            foreach (var rawArc in rawResponse.Value.GetProperty("pageProps").GetProperty("arcs").EnumerateArray())
            {
                results.Add(new RepositoryArc(rawArc));
            }
        }

        return results;
    }

    /// <summary>
    /// Retrieves the arc model based on the number.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The arc model or <c>null</c> if not found.</returns>
    public async Task<Model.IArc?> FindArcByNumberAsync(int arcNumber, CancellationToken cancellationToken)
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
    public async Task<IReadOnlyCollection<Model.IEpisode>> FindAllEpisodesAsync(CancellationToken cancellationToken)
    {
        var results = new List<Model.IEpisode>();

        var rawResponse = await GetOrFetchRawContentAsync(cancellationToken).ConfigureAwait(false);
        if (rawResponse != null)
        {
            foreach (var rawArc in rawResponse.Value.GetProperty("pageProps").GetProperty("arcs").EnumerateArray())
            {
                foreach (var rawEpisode in rawArc.GetProperty("episodes").EnumerateArray())
                {
                    results.Add(new RepositoryEpisode(rawArc.GetProperty("part").GetInt32(), rawEpisode));
                }
            }
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
    public async Task<Model.IEpisode?> FindEpisodeByNumberAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken)
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
    public Task<IReadOnlyCollection<Model.IArt>> FindAllSeriesLogoArtAsync()
    {
        var results = new List<Model.IArt>();
        results.Add(new RepositoryArt("image/svg+xml", "https://onepace.net/images/one-pace-logo.svg"));
        return Task.FromResult<IReadOnlyCollection<Model.IArt>>(results);
    }

    /// <summary>
    /// Retrieves the available series cover art.
    /// </summary>
    /// <returns>The art model.</returns>
    public Task<IReadOnlyCollection<Model.IArt>> FindAllSeriesCoverArtAsync()
    {
        return Task.FromResult<IReadOnlyCollection<Model.IArt>>(new List<Model.IArt>());
    }

    /// <summary>
    /// Retrieves the available arc cover art.
    /// </summary>
    /// <param name="arcNumber">Number of the arc (1-based).</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>The art model.</returns>
    public async Task<IReadOnlyCollection<Model.IArt>> FindAllArcCoverArtAsync(int arcNumber, CancellationToken cancellationToken)
    {
        var results = new List<Model.IArt>();

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
    public async Task<IReadOnlyCollection<Model.IArt>> FindAllEpisodeCoverArtAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken)
    {
        var results = new List<Model.IArt>();

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
    public async Task<Model.ILocalization?> FindBestSeriesLocalizationAsync(string languageCode, CancellationToken cancellationToken)
    {
        var rawResponse = await GetOrFetchRawMetadataAsync(ToRawLanguageCode(languageCode), cancellationToken).ConfigureAwait(false);
        if (rawResponse != null)
        {
            var i18nStore = rawResponse.Value.GetProperty("pageProps").GetProperty("_nextI18Next").GetProperty("initialI18nStore");
            var rawTranslationEntry = ChooseBestRawTranslation(i18nStore.EnumerateObject(), ToRawLanguageCode(languageCode));
            if (rawTranslationEntry != null)
            {
                return new RepositoryLocalization(rawTranslationEntry.Value);
            }
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
    public async Task<Model.ILocalization?> FindBestArcLocalizationAsync(int arcNumber, string languageCode, CancellationToken cancellationToken)
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
    public async Task<Model.ILocalization?> FindBestEpisodeLocalizationAsync(int arcNumber, int episodeNumber, string languageCode, CancellationToken cancellationToken)
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

        return DateTime.Parse(releasedDateString, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime();
    }

    private sealed class RepositorySeries : Model.ISeries
    {
        public string InvariantTitle => "One Pace";

        public string OriginalTitle => "One Piece";
    }

    private sealed class RepositoryArc : Model.IArc
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

    private sealed class RepositoryEpisode : Model.IEpisode
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

    private sealed class RepositoryArt : Model.IArt
    {
        public RepositoryArt(string mimeType, string url)
        {
            Url = url;
        }

        public RepositoryArt(string baseUrl, JsonElement rawImage)
        {
            Url = baseUrl + rawImage.GetProperty("src").GetNonNullString();
            Width = rawImage.GetProperty("width").GetNullableInt32();
        }

        public string Url { get; }

        public int? Width { get; }

        public int? Height { get; }
    }

    private sealed class RepositoryLocalization : Model.ILocalization
    {
        public RepositoryLocalization(JsonProperty rawTranslation)
        {
            LanguageCode = ToLanguageCode(rawTranslation.Name);
            Title = "One Pace"; // rawTranslation.Value.GetProperty("home").GetProperty("meta-title").GetNonNullString();
            Description = rawTranslation.Value.GetProperty("home").GetProperty("meta-description").GetString();
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
