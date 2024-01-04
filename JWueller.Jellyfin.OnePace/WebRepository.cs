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
                    @"{series{invariant_title translations{title description language_code}}arcs{id part invariant_title manga_chapters released_at translations{title description language_code}images{src width}episodes{id part invariant_title manga_chapters released_at crc32 translations{title description language_code}images{src width}}}}",
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

    private async Task<JsonElement?> FindApiArcByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var apiMetadata = await FetchMetadataAsync(cancellationToken).ConfigureAwait(false);
            return apiMetadata?.GetProperty("arcs").EnumerateArray().FirstOrNull(apiArc =>
                apiArc.GetProperty("id").GetNonNullString() == id);
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
            return null;
        }
    }

    private async Task<(string ArcId, JsonElement ApiEpisode)?> FindApiEpisodeByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var apiMetadata = await FetchMetadataAsync(cancellationToken).ConfigureAwait(false);
            if (apiMetadata == null)
            {
                return null;
            }

            foreach (var apiArc in apiMetadata.Value.GetProperty("arcs").EnumerateArray())
            {
                var matchingEpisode = apiArc.GetProperty("episodes").EnumerateArray()
                    .FirstOrNull(apiEpisode => apiEpisode.GetProperty("id").GetNonNullString() == id);

                if (matchingEpisode != null)
                {
                    return (apiArc.GetProperty("id").GetNonNullString(), matchingEpisode.Value);
                }
            }

            return null;
        }
        catch (HttpRequestException)
        {
            // Details should have been logged further down the stack. We just treat this data as unavailable for now
            // and the user can try again manually if they want.
            return null;
        }
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
    public async Task<IArc?> FindArcByIdAsync(string id, CancellationToken cancellationToken)
    {
        var apiArc = await FindApiArcByIdAsync(id, cancellationToken).ConfigureAwait(false);
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
                    select new RepositoryEpisode(apiArc.GetProperty("id").GetNonNullString(), apiEpisode));
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
    public async Task<IEpisode?> FindEpisodeByIdAsync(string id, CancellationToken cancellationToken)
    {
        var result = await FindApiEpisodeByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return result != null
            ? new RepositoryEpisode(result.Value.ArcId, result.Value.ApiEpisode)
            : null;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<IArt>> FindAllLogoArtBySeriesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<IArt>>(new List<IArt>
        {
            new RepositoryArt("https://onepace.net/images/one-pace-logo.svg")
        });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<IArt>> FindAllCoverArtBySeriesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<IArt>>(new List<IArt>());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IArt>> FindAllCoverArtByArcIdAsync(
        string arcId,
        CancellationToken cancellationToken)
    {
        var results = new List<IArt>();

        var apiArc = await FindApiArcByIdAsync(arcId, cancellationToken).ConfigureAwait(false);
        if (apiArc != null)
        {
            results.AddRange(apiArc.Value.GetProperty("images").EnumerateArray().Select(apiImage =>
                new RepositoryArt("https://onepace.net/images/arcs/", apiImage)));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<IArt>> FindAllCoverArtByEpisodeIdAsync(
        string episodeId,
        CancellationToken cancellationToken)
    {
        var results = new List<IArt>();

        var result = await FindApiEpisodeByIdAsync(episodeId, cancellationToken)
            .ConfigureAwait(false);
        if (result != null)
        {
            results.AddRange(result.Value.ApiEpisode.GetProperty("images").EnumerateArray().Select(apiImage =>
                new RepositoryArt("https://onepace.net/images/episodes/", apiImage)));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<ILocalization?> FindBestLocalizationBySeriesAsync(
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
    public async Task<ILocalization?> FindBestLocalizationByArcIdAsync(
        string arcId,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var apiArc = await FindApiArcByIdAsync(arcId, cancellationToken).ConfigureAwait(false);
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
    public async Task<ILocalization?> FindBestLocalizationByEpisodeIdAsync(
        string episodeId,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var result = await FindApiEpisodeByIdAsync(episodeId, cancellationToken)
            .ConfigureAwait(false);
        if (result == null)
        {
            return null;
        }

        var apiTranslation = ChooseBestApiTranslation(
            result.Value.ApiEpisode.GetProperty("translations").EnumerateArray(),
            ToApiLanguageCode(languageCode));

        return apiTranslation != null
            ? new RepositoryLocalization(apiTranslation.Value)
            : null;
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
            Id = apiArc.GetProperty("id").GetNonNullString();
            Number = apiArc.GetProperty("part").GetInt32();
            InvariantTitle = apiArc.GetProperty("invariant_title").GetNonNullString();
            MangaChapters = apiArc.GetProperty("manga_chapters").GetString();
            ReleaseDate = ParseReleaseDate(apiArc.GetProperty("released_at"));
        }

        public string Id { get; }

        public int Number { get; }

        public string InvariantTitle { get; }

        public string? MangaChapters { get; }

        public DateTime? ReleaseDate { get; }
    }

    private sealed class RepositoryEpisode : IEpisode
    {
        public RepositoryEpisode(string arcId, JsonElement apiEpisode)
        {
            Id = apiEpisode.GetProperty("id").GetNonNullString();
            Number = apiEpisode.GetProperty("part").GetInt32();
            ArcId = arcId;
            InvariantTitle = apiEpisode.GetProperty("invariant_title").GetNonNullString();
            MangaChapters = apiEpisode.GetProperty("manga_chapters").GetString();
            ReleaseDate = ParseReleaseDate(apiEpisode.GetProperty("released_at"));

            var crc32String = apiEpisode.GetProperty("crc32").GetString();
            if (crc32String != null)
            {
                Crc32 = uint.Parse(crc32String, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
        }

        public string Id { get; }

        public int Number { get; }

        public string ArcId { get; }

        public string InvariantTitle { get; }

        public string? MangaChapters { get; }

        public DateTime? ReleaseDate { get; }

        public uint? Crc32 { get; }
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
