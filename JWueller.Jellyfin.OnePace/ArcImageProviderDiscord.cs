using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Populates One Pace arc cover art from the project website.
/// </summary>
public class ArcImageProviderDiscord : IDynamicImageProvider, IHasOrder
{
    private readonly OnePaceRepository _repository;
    private readonly ILogger<ArcImageProviderDiscord> _log;
    private readonly string _discordImageUrl;
    private readonly string _discordImageCacheDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArcImageProviderDiscord"/> class.
    /// </summary>
    /// <param name="repository">The One Pace repository.</param>
    /// <param name="logger">The log target for this class.</param>
    public ArcImageProviderDiscord(OnePaceRepository repository, ILogger<ArcImageProviderDiscord> logger)
    {
        _repository = repository;
        _log = logger;
        _discordImageUrl = "https://cdn.discordapp.com/attachments/514544186670841857/1069843820050661406/OnePaceArcPosters.zip";
        _discordImageCacheDir = Path.Combine(Plugin.Instance!.ApplicationPaths.CachePath, "OnePace");
    }

    /// <summary>
    /// Gets the order of results based on the plugin configuration. A lower order means the result is preferred more.
    /// When we want to prefer discord results, we want this to be 0, and 1 otherwise.
    /// </summary>
    public int Order => Convert.ToInt32(!Plugin.Instance!.Configuration.PreferDiscordForArcPosters); // 0 or 1

    /// <inheritdoc/>
    public string Name => Plugin.ProviderName;

    /// <inheritdoc/>
    public bool Supports(BaseItem item) => item is Season;

    /// <inheritdoc/>
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new List<ImageType> { ImageType.Primary };

    /// <inheritdoc/>
    public async Task<DynamicImageResponse> GetImage(BaseItem item, ImageType type, CancellationToken cancellationToken)
    {
        var result = new DynamicImageResponse();
        var cached = await CacheDiscordArcImages().ConfigureAwait(false);
        if (!cached)
        {
            return result; // unable to cache, return empty result.
        }

        var match = await ArcIdentifier.IdentifyAsync(_repository, ((Season)item).GetLookupInfo(), cancellationToken).ConfigureAwait(false);
        if (match != null)
        {
            result = UpdateGetImageResult(result, match.Number);
        }

        return result;
    }

    private async Task<bool> CacheDiscordArcImages()
    {
        if (Directory.Exists(_discordImageCacheDir) && Directory.GetFiles(_discordImageCacheDir, "*.png").Length == 35) // Check if the 35 images in the discord cover art zip are already cached
        {
            return true;
        }

        using var response = await new HttpClient().GetAsync(_discordImageUrl).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _log.LogError("Could not download arc posters from Discord");
            return false;
        }

        using var streamToReadFrom = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var zip = new ZipArchive(streamToReadFrom);
        zip.ExtractToDirectory(_discordImageCacheDir, true); // Overwrites existing files while extracting. Useful if the cache was incompletely cleared out etc.
        return true;
    }

    private DynamicImageResponse UpdateGetImageResult(DynamicImageResponse result, int arcNumber)
    {
        string pattern = @"^0*" + arcNumber + @"\D"; // Use regex to find the image file starting with the given arc number.
        Regex regex = new Regex(pattern);
        foreach (var file in Directory.GetFiles(_discordImageCacheDir))
        {
            if (regex.IsMatch(Path.GetFileName(file)))
            {
                result.Format = MediaBrowser.Model.Drawing.ImageFormat.Png;
                result.HasImage = true;
                result.Path = file;
                result.Protocol = MediaBrowser.Model.MediaInfo.MediaProtocol.File;
                break;
            }
        }

        return result;
    }
}
