using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Populates One Pace episode cover art from the project website.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Instantiated by Jellyfin")]
public class EpisodeImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly IRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeImageProvider"/> class.
    /// </summary>
    /// <param name="repository">The One Pace repository.</param>
    /// <param name="httpClientFactory">The HTTP client factory used to fetch images.</param>
    public EpisodeImageProvider(IRepository repository, IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public int Order => -1000;

    /// <inheritdoc/>
    public string Name => Plugin.ProviderName;

    /// <inheritdoc/>
    public bool Supports(BaseItem item) => item is Episode;

    /// <inheritdoc/>
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new List<ImageType> { ImageType.Primary };

    /// <inheritdoc/>
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var result = new List<RemoteImageInfo>();

        var match = await EpisodeIdentifier.IdentifyAsync(_repository, ((Episode)item).GetLookupInfo(), cancellationToken).ConfigureAwait(false);
        if (match != null)
        {
            foreach (var coverArt in await _repository.FindAllEpisodeCoverArtAsync(match.ArcNumber, match.Number, cancellationToken).ConfigureAwait(false))
            {
                result.Add(new RemoteImageInfo
                {
                    Type = ImageType.Primary,
                    Url = coverArt.Url,
                    Width = coverArt.Width,
                    ProviderName = Name,
                });
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
    }
}
