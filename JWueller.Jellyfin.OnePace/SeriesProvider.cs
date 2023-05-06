using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Populates One Pace series metadata from the project website.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Instantiated by Jellyfin")]
public class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
{
    private readonly OnePaceRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SeriesProvider> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesProvider"/> class.
    /// </summary>
    /// <param name="repository">The One Pace repository.</param>
    /// <param name="httpClientFactory">The HTTP client factory used to fetch images.</param>
    /// <param name="logger">The log target for this class.</param>
    public SeriesProvider(OnePaceRepository repository, IHttpClientFactory httpClientFactory, ILogger<SeriesProvider> logger)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
        _log = logger;
    }

    /// <inheritdoc/>
    public int Order => -1000;

    /// <inheritdoc/>
    public string Name => Plugin.ProviderName;

    /// <inheritdoc/>
    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Series>();

        var match = await SeriesIdentifier.IdentifyAsync(_repository, info, cancellationToken).ConfigureAwait(false);
        if (match != null)
        {
            result.HasMetadata = true;
            result.Provider = Name;

            result.Item = new Series
            {
                Name = match.InvariantTitle,
                OriginalTitle = match.OriginalTitle,
            };

            result.Item.SetIsOnePaceSeries(true);
            result.Item.SetProviderId("AniDB", "69"); // https://anidb.net/anime/69
            result.Item.SetProviderId("AniList", "21"); // https://anilist.co/anime/21/ONE-PIECE/

            var localization = await _repository.FindBestSeriesLocalizationAsync(info.MetadataLanguage ?? "en", cancellationToken).ConfigureAwait(false);
            if (localization != null)
            {
                result.Item.Name = localization.Title;
                result.Item.Overview = localization.Description;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        var result = new List<RemoteSearchResult>();

        var metadataResult = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);
        if (metadataResult.HasMetadata)
        {
            var series = metadataResult.Item;

            result.Add(new RemoteSearchResult
            {
                Name = series.Name,
                Overview = series.Overview,
                ProviderIds = series.ProviderIds,
                SearchProviderName = Name,
            });
        }

        return result;
    }

    /// <inheritdoc/>
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
    }
}
