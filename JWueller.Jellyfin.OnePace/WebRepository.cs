using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
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
public class WebRepository : IRepository
{
    private const string FallbackLanguageCode = "en";
    private static readonly string FallbackRawLanguageCode = ToRawLanguageCode(FallbackLanguageCode);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<WebRepository> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebRepository"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory used to fetch metadata.</param>
    /// <param name="memoryCache">The cache used to prevent repeated endpoint requests.</param>
    /// <param name="logger">The log target for this class.</param>
    public WebRepository(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, ILogger<WebRepository> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _log = logger;
    }

    private async Task<string> FetchStringResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _memoryCache.GetOrCreateAsync(request, async cacheEntry =>
        {
            _log.LogTrace("Fetching: {Request}", request.ToString());

            var client = _httpClientFactory.CreateClient(NamedClient.Default);
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Honor some common caching headers, if present.
            cacheEntry.SlidingExpiration = response.Headers.CacheControl?.MaxAge;
            cacheEntry.AbsoluteExpiration = response.Content.Headers.Expires;

            // Fall back to a default expiration if no explicit one was set.
            if (!cacheEntry.SlidingExpiration.HasValue && !cacheEntry.AbsoluteExpiration.HasValue)
            {
                cacheEntry.SlidingExpiration = TimeSpan.FromHours(1);
            }

            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task<JsonElement?> FetchJsonResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var json = await FetchStringResponseAsync(request, cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(json).RootElement;
    }

    private async Task<JsonElement?> FetchMetadataAsync(string apiLanguageCode, CancellationToken cancellationToken)
    {
        var url = string.Format(CultureInfo.InvariantCulture, "https://onepace.net/static/locales/{0}/home.json", apiLanguageCode);

        try
        {
            return await FetchJsonResponseAsync(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken).ConfigureAwait(false);
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
                _log.LogWarning(ex, "Failed to fetch One Pace metadata for language code: {LanguageCode}", apiLanguageCode);
                throw;
            }
        }
    }

    private async Task<JsonElement?> FetchContentAsync(CancellationToken cancellationToken)
    {
        const string Url = "https://onepace.net/api/graphql";
        const string Query = "{\"query\": \"{ databaseGetAllArcs { part, title, manga_chapters, released_date, translations { title, description, language { code } }, images { src, width }, episodes { part, title, manga_chapters, released_date, translations { title, description, language { code } }, images { src, width } } } }\"}";

        try
        {
            var message = new HttpRequestMessage(HttpMethod.Post, Url);
            message.Content = new StringContent(Query, Encoding.UTF8, "application/json");
            return await FetchJsonResponseAsync(message, cancellationToken).ConfigureAwait(false);
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<IArc?> FindArcByNumberAsync(int arcNumber, CancellationToken cancellationToken)
    {
        var rawArc = await FindRawArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (rawArc != null)
        {
            return new RepositoryArc(rawArc.Value);
        }

        return null;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<IEpisode?> FindEpisodeByNumberAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken)
    {
        var rawEpisode = await FindRawEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken).ConfigureAwait(false);
        if (rawEpisode != null)
        {
            return new RepositoryEpisode(arcNumber, rawEpisode.Value);
        }

        return null;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<IArt>> FindAllSeriesLogoArtAsync(CancellationToken cancellationToken)
    {
        var results = new List<IArt>();
        results.Add(new RepositoryArt("https://onepace.net/images/one-pace-logo.svg"));
        return Task.FromResult<IReadOnlyCollection<IArt>>(results);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<IArt>> FindAllSeriesCoverArtAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<IArt>>(new List<IArt>());
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<ILocalization?> FindBestSeriesLocalizationAsync(string languageCode, CancellationToken cancellationToken)
    {
        try
        {
            var rawResponse = await GetOrFetchRawMetadataAsync(ToRawLanguageCode(languageCode), cancellationToken).ConfigureAwait(false);
            if (rawResponse != null)
            {
                return new RepositoryLocalization(languageCode, rawResponse.Value);
            }

            // Try to fall back to a known good language code if the requested one is not available. This mirrors the
            // behavior of the other localization resolvers.
            if (languageCode != FallbackLanguageCode)
            {
                var fallbackLocalization = await FindBestSeriesLocalizationAsync(FallbackLanguageCode, cancellationToken).ConfigureAwait(false);
                if (fallbackLocalization != null)
                {
                    return fallbackLocalization;
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
