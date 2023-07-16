using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Populates One Pace series cover art from the project website.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Instantiated by Jellyfin")]
public class SeriesImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly IRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SeriesImageProvider> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesImageProvider"/> class.
    /// </summary>
    /// <param name="repository">The One Pace repository.</param>
    /// <param name="httpClientFactory">The HTTP client factory used to fetch images.</param>
    /// <param name="logger">The log target for this class.</param>
    public SeriesImageProvider(
        IRepository repository,
        IHttpClientFactory httpClientFactory,
        ILogger<SeriesImageProvider> logger)
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
    public bool Supports(BaseItem item) => item is Series;

    /// <inheritdoc/>
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new List<ImageType>
    {
        ImageType.Primary
    };

    /// <inheritdoc/>
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var result = new List<RemoteImageInfo>();

        var match = await SeriesIdentifier
            .IdentifyAsync(_repository, ((Series)item).GetLookupInfo(), cancellationToken)
            .ConfigureAwait(false);
        if (match != null)
        {
            var logoArts = await _repository.FindAllSeriesLogoArtAsync(cancellationToken).ConfigureAwait(false);
            result.AddRange(logoArts.Select(logoArt => new RemoteImageInfo
            {
                Type = ImageType.Logo,
                Url = logoArt.Url,
                Width = logoArt.Width,
                ProviderName = Name,
            }));

            var coverArts = await _repository.FindAllSeriesCoverArtAsync(cancellationToken).ConfigureAwait(false);
            result.AddRange(coverArts.Select(coverArt => new RemoteImageInfo
            {
                Type = ImageType.Primary,
                Url = coverArt.Url,
                Width = coverArt.Width,
                ProviderName = Name,
            }));
        }

        _log.LogInformation(
            "Found {Count} series image(s) for {Item} --> {Result}",
            result.Count,
            System.Text.Json.JsonSerializer.Serialize(item),
            System.Text.Json.JsonSerializer.Serialize(result));

        return result;
    }

    /// <inheritdoc/>
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
    }
}
