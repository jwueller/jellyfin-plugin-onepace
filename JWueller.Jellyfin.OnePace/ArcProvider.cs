using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Populates One Pace arc metadata from the project website.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Instantiated by Jellyfin")]
public class ArcProvider : IRemoteMetadataProvider<Season, SeasonInfo>, IHasOrder
{
    private readonly IRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ArcProvider> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArcProvider"/> class.
    /// </summary>
    /// <param name="repository">The One Pace repository.</param>
    /// <param name="httpClientFactory">The HTTP client factory used to fetch images.</param>
    /// <param name="logger">The log target for this class.</param>
    public ArcProvider(IRepository repository, IHttpClientFactory httpClientFactory, ILogger<ArcProvider> logger)
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
    public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Season>();

        var arcMatch = await ArcIdentifier.IdentifyAsync(_repository, info, cancellationToken).ConfigureAwait(false);
        if (arcMatch != null)
        {
            result.HasMetadata = true;
            result.Provider = Name;

            result.Item = new Season
            {
                IndexNumber = arcMatch.Rank,
                Name = arcMatch.InvariantTitle,
                PremiereDate = arcMatch.ReleaseDate,
                ProductionYear = arcMatch.ReleaseDate?.Year,
            };

            result.Item.SetOnePaceId(arcMatch.Id);

            var localization = await _repository
                .FindBestLocalizationByArcIdAsync(arcMatch.Id, info.MetadataLanguage ?? "en", cancellationToken)
                .ConfigureAwait(false);
            if (localization != null)
            {
                result.Item.Name = localization.Title;
                result.Item.Overview = localization.Description;
            }
        }

        _log.LogInformation(
            "Identified Arc {Info} --> {Match}",
            System.Text.Json.JsonSerializer.Serialize(info),
            System.Text.Json.JsonSerializer.Serialize(arcMatch));

        return result;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        SeasonInfo searchInfo,
        CancellationToken cancellationToken)
    {
        var result = new List<RemoteSearchResult>();

        var metadataResult = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);
        if (metadataResult.HasMetadata)
        {
            var season = metadataResult.Item;

            result.Add(new RemoteSearchResult
            {
                IndexNumber = season.IndexNumber,
                Name = season.Name,
                PremiereDate = season.PremiereDate,
                ProductionYear = season.ProductionYear,
                Overview = season.Overview,
                ProviderIds = season.ProviderIds,
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
