using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Populates One Pace episode metadata from the project website.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Instantiated by Jellyfin")]
public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
{
    private readonly IRepository _repository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EpisodeProvider> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeProvider"/> class.
    /// </summary>
    /// <param name="repository">The One Pace repository.</param>
    /// <param name="httpClientFactory">The HTTP client factory used to fetch images.</param>
    /// <param name="logger">The log target for this class.</param>
    public EpisodeProvider(
        IRepository repository,
        IHttpClientFactory httpClientFactory,
        ILogger<EpisodeProvider> logger)
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
    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Episode>();

        var episodeMatch = await EpisodeIdentifier.IdentifyAsync(_repository, info, cancellationToken).ConfigureAwait(false);
        if (episodeMatch != null)
        {
            var arc = await _repository.FindArcByIdAsync(episodeMatch.ArcId, cancellationToken).ConfigureAwait(false);
            if (arc != null)
            {
                result.HasMetadata = true;
                result.Provider = Name;

                result.Item = new Episode
                {
                    IndexNumber = episodeMatch.Rank,
                    ParentIndexNumber = arc.Rank,
                    Name = episodeMatch.InvariantTitle,
                    PremiereDate = episodeMatch.ReleaseDate,
                    ProductionYear = episodeMatch.ReleaseDate?.Year
                };

                result.Item.SetOnePaceId(episodeMatch.Id);

                var localization = await _repository
                    .FindBestLocalizationByEpisodeIdAsync(
                        episodeMatch.Id,
                        info.MetadataLanguage ?? "en",
                        cancellationToken)
                    .ConfigureAwait(false);
                if (localization != null)
                {
                    result.Item.Name = localization.Title;
                    result.Item.Overview = localization.Description;
                }
            }
            else
            {
                _log.LogError("Could not find arc {ArcId}", episodeMatch.ArcId);
            }
        }

        _log.LogInformation(
            "Identified Episode {Info} --> {Match}",
            JsonSerializer.Serialize(info),
            JsonSerializer.Serialize(episodeMatch));

        return result;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(
        EpisodeInfo searchInfo,
        CancellationToken cancellationToken)
    {
        var result = new List<RemoteSearchResult>();

        var metadataResult = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);
        if (metadataResult.HasMetadata)
        {
            var episode = metadataResult.Item;

            result.Add(new RemoteSearchResult
            {
                IndexNumber = episode.IndexNumber,
                ParentIndexNumber = episode.ParentIndexNumber,
                Name = episode.Name,
                PremiereDate = episode.PremiereDate,
                ProductionYear = episode.ProductionYear,
                Overview = episode.Overview,
                ProviderIds = episode.ProviderIds,
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
