using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    private async Task<JsonElement> QueryGraphQlAsync(string query, CancellationToken cancellationToken)
    {
        return await _memoryCache.GetOrCreateAsync(query, async cacheEntry =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://onepace.net/api/graphql");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query
                }),
                Encoding.UTF8,
                "application/json");

            var client = _httpClientFactory.CreateClient(NamedClient.Default);
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Honor some common caching headers, if present.
            var noCache = response.Headers.CacheControl?.NoCache;
            var maxAge = response.Headers.CacheControl?.MaxAge;
            if ((noCache != null && noCache.Value) || maxAge <= TimeSpan.Zero)
            {
                // Caching is actively forbidden!
                cacheEntry.AbsoluteExpiration = DateTimeOffset.MinValue;
            }
            else
            {
                cacheEntry.SlidingExpiration = maxAge;
                cacheEntry.AbsoluteExpiration = response.Content.Headers.Expires;

                // Fall back to a reasonable default expiration if no explicit one was set.
                if (!cacheEntry.SlidingExpiration.HasValue && !cacheEntry.AbsoluteExpiration.HasValue)
                {
                    cacheEntry.SlidingExpiration = TimeSpan.FromHours(1);
                }
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var document = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return document.RootElement.GetProperty("data");
        }).ConfigureAwait(false);
    }

    private async Task<JsonElement?> FetchMetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            // language=graphql
            return await QueryGraphQlAsync(
                    @"{series{invariant_title translations{title description language_code}}arcs{part invariant_title manga_chapters released_at translations{title description language_code}images{src width}episodes{part invariant_title manga_chapters released_at translations{title description language_code}images{src width}}}}",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _log.LogError(ex, "Could not find the One Pace metadata, please report a bug at" +
                                  " https://github.com/jwueller/jellyfin-plugin-onepace if this happened on the" +
                                  " latest version");
            }
            else
            {
                _log.LogWarning(ex, "Failed to fetch One Pace metadata");
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

    private static JsonElement? ChooseBestApiTranslation(
        JsonElement.ArrayEnumerator apiCandidates,
        string apiLanguageCode)
    {
        foreach (var apiCandidate in apiCandidates)
        {
            if (LanguageCodesEqual(apiCandidate.GetProperty("language_code").GetNonNullString(), apiLanguageCode))
            {
                return apiCandidate;
            }
        }

        // Fall back to the next best thing.
        return !LanguageCodesEqual(apiLanguageCode, FallbackApiLanguageCode)
            ? ChooseBestApiTranslation(apiCandidates, FallbackApiLanguageCode)
            : null;
    }

    private async Task<JsonElement?> FindApiArcByNumberAsync(int arcNumber, CancellationToken cancellationToken)
    {
        try
        {
            var apiMetadata = await FetchMetadataAsync(cancellationToken).ConfigureAwait(false);
            return apiMetadata?.GetProperty("arcs").EnumerateArray().FirstOrDefault(apiArc =>
                apiArc.GetProperty("part").GetInt32() == arcNumber);
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
            return null;
        }
    }

    private async Task<JsonElement?> FindApiEpisodeByNumberAsync(
        int arcNumber,
        int episodeNumber,
        CancellationToken cancellationToken)
    {
        var apiArc = await FindApiArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        return apiArc?.GetProperty("episodes").EnumerateArray().FirstOrDefault(apiEpisode =>
            apiEpisode.GetProperty("part").GetInt32() == episodeNumber);
    }

    /// <inheritdoc/>
    public async Task<ISeries?> FindSeriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var apiMetadata = await FetchMetadataAsync(cancellationToken).ConfigureAwait(false);
            return apiMetadata != null
                ? new RepositorySeries(apiMetadata.Value.GetProperty("series"))
                : null;
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IArc>> FindAllArcsAsync(CancellationToken cancellationToken)
    {
        var results = new List<IArc>();

        try
        {
            var apiMetadata = await FetchMetadataAsync(cancellationToken).ConfigureAwait(false);
            if (apiMetadata != null)
            {
                results.AddRange(apiMetadata.Value.GetProperty("arcs").EnumerateArray().Select(apiArc =>
                    new RepositoryArc(apiArc)));
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
        return apiArc != null
            ? new RepositoryArc(apiArc.Value)
            : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IEpisode>> FindAllEpisodesAsync(CancellationToken cancellationToken)
    {
        var results = new List<IEpisode>();

        try
        {
            var apiMetadata = await FetchMetadataAsync(cancellationToken).ConfigureAwait(false);
            if (apiMetadata != null)
            {
                results.AddRange(
                    from apiArc in apiMetadata.Value.GetProperty("arcs").EnumerateArray()
                    from apiEpisode in apiArc.GetProperty("episodes").EnumerateArray()
                    select new RepositoryEpisode(apiArc.GetProperty("part").GetInt32(), apiEpisode));
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
    public async Task<IEpisode?> FindEpisodeByNumberAsync(
        int arcNumber,
        int episodeNumber,
        CancellationToken cancellationToken)
    {
        var apiEpisode = await FindApiEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken)
            .ConfigureAwait(false);
        return apiEpisode != null
            ? new RepositoryEpisode(arcNumber, apiEpisode.Value)
            : null;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<IArt>> FindAllSeriesLogoArtAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<IArt>>(new List<IArt>
        {
            new RepositoryArt("https://onepace.net/images/one-pace-logo.svg")
        });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<IArt>> FindAllSeriesCoverArtAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<IArt>>(new List<IArt>());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IArt>> FindAllArcCoverArtAsync(
        int arcNumber,
        CancellationToken cancellationToken)
    {
        var results = new List<IArt>();

        var apiArc = await FindApiArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (apiArc != null)
        {
            results.AddRange(apiArc.Value.GetProperty("images").EnumerateArray().Select(apiImage =>
                new RepositoryArt("https://onepace.net/images/arcs/", apiImage)));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IArt>> FindAllEpisodeCoverArtAsync(
        int arcNumber,
        int episodeNumber,
        CancellationToken cancellationToken)
    {
        var results = new List<IArt>();

        var apiEpisode = await FindApiEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken)
            .ConfigureAwait(false);
        if (apiEpisode != null)
        {
            results.AddRange(apiEpisode.Value.GetProperty("images").EnumerateArray().Select(apiImage =>
                new RepositoryArt("https://onepace.net/images/episodes/", apiImage)));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<ILocalization?> FindBestSeriesLocalizationAsync(
        string languageCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var apiMetadata = await FetchMetadataAsync(cancellationToken).ConfigureAwait(false);
            var apiSeries = apiMetadata?.GetProperty("series");

            var bestApiTranslation = apiSeries != null
                ? ChooseBestApiTranslation(
                    apiSeries.Value.GetProperty("translations").EnumerateArray(),
                    ToApiLanguageCode(languageCode))
                : null;

            return bestApiTranslation != null
                ? new RepositoryLocalization(bestApiTranslation.Value)
                : null;
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<ILocalization?> FindBestArcLocalizationAsync(
        int arcNumber,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var apiArc = await FindApiArcByNumberAsync(arcNumber, cancellationToken).ConfigureAwait(false);
        if (apiArc != null)
        {
            var bestApiTranslation = ChooseBestApiTranslation(
                apiArc.Value.GetProperty("translations").EnumerateArray(),
                ToApiLanguageCode(languageCode));

            if (bestApiTranslation != null)
            {
                return new RepositoryLocalization(bestApiTranslation.Value);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<ILocalization?> FindBestEpisodeLocalizationAsync(
        int arcNumber,
        int episodeNumber,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var apiEpisode = await FindApiEpisodeByNumberAsync(arcNumber, episodeNumber, cancellationToken)
            .ConfigureAwait(false);
        if (apiEpisode != null)
        {
            var apiTranslation = ChooseBestApiTranslation(
                apiEpisode.Value.GetProperty("translations").EnumerateArray(),
                ToApiLanguageCode(languageCode));

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
        public RepositorySeries(JsonElement apiSeries)
        {
            InvariantTitle = apiSeries.GetProperty("invariant_title").GetNonNullString();
        }

        public string InvariantTitle { get; }

        // TODO: This should become part of the ILocalization interface in the future when the API starts exposing it.
        public string OriginalTitle => "One Piece";
    }

    private sealed class RepositoryArc : IArc
    {
        public RepositoryArc(JsonElement apiArc)
        {
            Number = apiArc.GetProperty("part").GetInt32();
            InvariantTitle = apiArc.GetProperty("invariant_title").GetNonNullString();
            MangaChapters = apiArc.GetProperty("manga_chapters").GetString();
            ReleaseDate = ParseReleaseDate(apiArc.GetProperty("released_at"));
        }

        public int Number { get; }

        public string InvariantTitle { get; }

        public string? MangaChapters { get; }

        public DateTime? ReleaseDate { get; }
    }

    private sealed class RepositoryEpisode : IEpisode
    {
        public RepositoryEpisode(int arcNumber, JsonElement apiEpisode)
        {
            Number = apiEpisode.GetProperty("part").GetInt32();
            ArcNumber = arcNumber;
            InvariantTitle = apiEpisode.GetProperty("invariant_title").GetNonNullString();
            MangaChapters = apiEpisode.GetProperty("manga_chapters").GetString();
            ReleaseDate = ParseReleaseDate(apiEpisode.GetProperty("released_at"));
        }

        public int Number { get; }

        public int ArcNumber { get; }

        public string InvariantTitle { get; }

        public string? MangaChapters { get; }

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
        public RepositoryLocalization(JsonElement apiTranslation)
        {
            LanguageCode = ToLanguageCode(apiTranslation.GetProperty("language_code").GetNonNullString());
            Title = apiTranslation.GetProperty("title").GetNonNullString();
            Description = apiTranslation.GetProperty("description").GetString();
        }

        public string LanguageCode { get; }

        public string Title { get; }

        public string? Description { get; }
    }
}
