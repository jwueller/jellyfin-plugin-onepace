using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JWueller.Jellyfin.OnePace.Model;
using MediaBrowser.Controller.Providers;

namespace JWueller.Jellyfin.OnePace;

internal static class EpisodeIdentifier
{
    public static async Task<IEpisode?> IdentifyAsync(
        IRepository repository,
        ItemLookupInfo itemLookupInfo,
        CancellationToken cancellationToken)
    {
        var episodeId = itemLookupInfo.GetOnePaceId();
        if (episodeId != null)
        {
            var episodeInfo = await repository
                .FindEpisodeByIdAsync(episodeId, cancellationToken)
                .ConfigureAwait(false);
            if (episodeInfo != null)
            {
                return episodeInfo;
            }
        }

        if (IdentifierUtil.MatchesOnePaceInvariantTitle(itemLookupInfo.Path))
        {
            var episodes = await repository.FindAllEpisodesAsync(cancellationToken).ConfigureAwait(false);

            // All of these file names should get matched properly:
            // - "[One Pace][3-5] Romance Dawn 03 [1080p][D767799C]"
            // - "Romance Dawn 03"
            // - "3-5"
            var fileName = Path.GetFileNameWithoutExtension(itemLookupInfo.Path);

            // match against CRC-32
            foreach (var episode in episodes)
            {
                if (episode.Crc32 != null)
                {
                    var pattern = $@"\b{episode.Crc32.Value:X8}\b";
                    if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                    {
                        return episode;
                    }
                }
            }

            // match against chapter ranges
            foreach (var episode in episodes.OrderByDescending(episode => episode.MangaChapters?.Length ?? 0))
            {
                if (!string.IsNullOrEmpty(episode.MangaChapters))
                {
                    var pattern = @"\b" + Regex.Escape(episode.MangaChapters) + @"\b";
                    if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                    {
                        return episode;
                    }
                }
            }

            // match against invariant titles
            foreach (var episode in episodes.OrderByDescending(episode => episode.InvariantTitle.Length))
            {
                if (!string.IsNullOrEmpty(episode.InvariantTitle))
                {
                    var pattern = @"\b" + Regex.Escape(episode.InvariantTitle) + @"\b";
                    if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                    {
                        return episode;
                    }
                }
            }
        }

        return null;
    }
}
