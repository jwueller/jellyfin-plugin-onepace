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
    private static readonly string FallbackApiLanguageCode = ToApiLanguageCode(FallbackLanguageCode);

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
    private static string ToApiLanguageCode(string languageCode)
    {
        if (languageCode.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "zh_cn";
        }

        return languageCode.Replace("-", "_", StringComparison.InvariantCultureIgnoreCase);
    }

    private static string ToLanguageCode(string apiLanguageCode)
    {
        if (apiLanguageCode.Equals("zh_cn", StringComparison.OrdinalIgnoreCase))
        {
            return "zh";
        }

        return apiLanguageCode.Replace("_", "-", StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool LanguageCodesEqual(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement? ChooseBestApiTranslation(JsonElement.ArrayEnumerator apiCandidates, string apiLanguageCode)
    {
        while (true)
        {
            foreach (var apiCandidate in apiCandidates)
            {
                if (LanguageCodesEqual(apiCandidate.GetProperty("language").GetProperty("code").GetNonNullString(), apiLanguageCode))
                {
                    return apiCandidate;
                }
            }

            // Fall back to a known good translation that should always be present and try again.
            if (!LanguageCodesEqual(apiLanguageCode, FallbackApiLanguageCode))
            {
                apiLanguageCode = FallbackApiLanguageCode;
                continue;
            }

            return null;
        }
    }

    private async Task<JsonElement?> FindApiArcByNumberAsync(int arcNumber, CancellationToken cancellationToken)
    {
        try
        {
            var apiResponse = await FetchContentAsync(cancellationToken).ConfigureAwait(false);
            if (apiResponse != null)
            {
                foreach (var apiArc in apiResponse.Value.GetProperty("data").GetProperty("databaseGetAllArcs").EnumerateArray())
                {
                    if (apiArc.GetProperty("part").GetInt32() == arcNumber)
                    {
                        return apiArc;
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

    private async Task<JsonElement?> FindApiEpisodeByNumberAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken)
    {
        var apiArc = await FindApiArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (apiArc != null)
        {
            foreach (var apiEpisode in apiArc.Value.GetProperty("episodes").EnumerateArray())
            {
                if (apiEpisode.GetProperty("part").GetInt32() == episodeNumber)
                {
                    return apiEpisode;
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
            var apiMetadata = await FetchMetadataAsync(FallbackApiLanguageCode, cancellationToken).ConfigureAwait(false);
            if (apiMetadata != null)
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
            var apiContent = await FetchContentAsync(cancellationToken).ConfigureAwait(false);
            if (apiContent != null)
            {
                foreach (var apiArc in apiContent.Value.GetProperty("data").GetProperty("databaseGetAllArcs").EnumerateArray())
                {
                    results.Add(new RepositoryArc(apiArc));
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
        var apiArc = await FindApiArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (apiArc != null)
        {
            return new RepositoryArc(apiArc.Value);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IEpisode>> FindAllEpisodesAsync(CancellationToken cancellationToken)
    {
        var results = new List<IEpisode>();

        try
        {
            var apiResponse = await FetchContentAsync(cancellationToken).ConfigureAwait(false);
            if (apiResponse != null)
            {
                foreach (var apiArc in apiResponse.Value.GetProperty("data").GetProperty("databaseGetAllArcs").EnumerateArray())
                {
                    foreach (var apiEpisode in apiArc.GetProperty("episodes").EnumerateArray())
                    {
                        results.Add(new RepositoryEpisode(apiArc.GetProperty("part").GetInt32(), apiEpisode));
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
        var apiEpisode = await FindApiEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken).ConfigureAwait(false);
        if (apiEpisode != null)
        {
            return new RepositoryEpisode(arcNumber, apiEpisode.Value);
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

        var apiArc = await FindApiArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (apiArc != null)
        {
            foreach (var apiImage in apiArc.Value.GetProperty("images").EnumerateArray())
            {
                results.Add(new RepositoryArt("https://onepace.net/images/arcs/", apiImage));
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IArt>> FindAllEpisodeCoverArtAsync(int arcNumber, int episodeNumber, CancellationToken cancellationToken)
    {
        var results = new List<IArt>();

        var apiEpisode = await FindApiEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken).ConfigureAwait(false);
        if (apiEpisode != null)
        {
            foreach (var apiImage in apiEpisode.Value.GetProperty("images").EnumerateArray())
            {
                results.Add(new RepositoryArt("https://onepace.net/images/episodes/", apiImage));
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<ILocalization?> FindBestSeriesLocalizationAsync(string languageCode, CancellationToken cancellationToken)
    {
        try
        {
            var apiMetadata = await FetchMetadataAsync(ToApiLanguageCode(languageCode), cancellationToken).ConfigureAwait(false);
            if (apiMetadata != null)
            {
                return new RepositoryLocalization(languageCode, apiMetadata.Value);
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
        var apiArc = await FindApiArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (apiArc != null)
        {
            var apiTranslation = ChooseBestApiTranslation(apiArc.Value.GetProperty("translations").EnumerateArray(), ToApiLanguageCode(languageCode));
            if (apiTranslation != null)
            {
                return new RepositoryLocalization(apiTranslation.Value);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<ILocalization?> FindBestEpisodeLocalizationAsync(int arcNumber, int episodeNumber, string languageCode, CancellationToken cancellationToken)
    {
        var apiEpisode = await FindApiEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken).ConfigureAwait(false);
        if (apiEpisode != null)
        {
            var apiTranslation = ChooseBestApiTranslation(apiEpisode.Value.GetProperty("translations").EnumerateArray(), ToApiLanguageCode(languageCode));
            if (apiTranslation != null)
            {
                return new RepositoryLocalization(apiTranslation.Value);
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
        public RepositoryArc(JsonElement apiArc)
        {
            Number = apiArc.GetProperty("part").GetInt32();
            InvariantTitle = apiArc.GetProperty("title").GetNonNullString();
            MangaChapters = apiArc.GetProperty("manga_chapters").GetNonNullString();
            ReleaseDate = ParseReleaseDate(apiArc.GetProperty("released_date"));
        }

        public int Number { get; }

        public string InvariantTitle { get; }

        public string MangaChapters { get; }

        public DateTime? ReleaseDate { get; }
    }

    private sealed class RepositoryEpisode : IEpisode
    {
        public RepositoryEpisode(int arcNumber, JsonElement apiEpisode)
        {
            Number = apiEpisode.GetProperty("part").GetInt32();
            ArcNumber = arcNumber;
            InvariantTitle = apiEpisode.GetProperty("title").GetNonNullString();
            MangaChapters = apiEpisode.GetProperty("manga_chapters").GetNonNullString();
            ReleaseDate = ParseReleaseDate(apiEpisode.GetProperty("released_date"));
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

        public RepositoryArt(string baseUrl, JsonElement apiImage)
        {
            Url = baseUrl + apiImage.GetProperty("src").GetNonNullString();
            Width = apiImage.GetProperty("width").CoerceNullableInt32();
        }

        public string Url { get; }

        public int? Width { get; }

        public int? Height { get; }
    }

    private sealed class RepositoryLocalization : ILocalization
    {
        public RepositoryLocalization(string languageCode, JsonElement apiTranslation)
        {
            LanguageCode = languageCode;

            var apiTitle = apiTranslation.GetProperty("meta-title").GetString();
            Title = apiTitle != null ? apiTitle.Split("|")[0].Trim() : "One Pace";

            Description = apiTranslation.GetProperty("meta-description").GetString();
        }

        public RepositoryLocalization(JsonElement apiTranslation)
        {
            LanguageCode = ToLanguageCode(apiTranslation.GetProperty("language").GetProperty("code").GetNonNullString());
            Title = apiTranslation.GetProperty("title").GetNonNullString();
            Description = apiTranslation.GetProperty("description").GetString();
        }

        public string LanguageCode { get; }

        public string Title { get; }

        public string? Description { get; }
    }
}
