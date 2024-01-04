using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Providers;

namespace JWueller.Jellyfin.OnePace;

internal static class ArcIdentifier
{
    public static async Task<Model.IArc?> IdentifyAsync(
        IRepository repository,
        ItemLookupInfo itemLookupInfo,
        CancellationToken cancellationToken)
    {
        var arcId = itemLookupInfo.GetOnePaceId();
        if (arcId != null)
        {
            var arc = await repository.FindArcByIdAsync(arcId, cancellationToken).ConfigureAwait(false);
            if (arc != null)
            {
                return arc;
            }
        }

        if (IdentifierUtil.MatchesOnePaceInvariantTitle(itemLookupInfo.Path))
        {
            var arcs = await repository.FindAllArcsAsync(cancellationToken).ConfigureAwait(false);

            // All of these folder names should get matched properly:
            // - "[One Pace][1-7] Romance Dawn [1080p]"
            // - "Arc 1 - Romance Dawn"
            // - "Romance Dawn"
            // - "1"
            var directoryName = Path.GetFileName(itemLookupInfo.Path);

            // match against chapter ranges
            foreach (var arc in arcs.OrderByDescending(arc => arc.MangaChapters?.Length ?? 0))
            {
                if (!string.IsNullOrEmpty(arc.MangaChapters))
                {
                    var pattern = @"\b" + Regex.Escape(arc.MangaChapters) + @"\b";
                    if (Regex.IsMatch(directoryName, pattern, RegexOptions.IgnoreCase))
                    {
                        return arc;
                    }
                }
            }

            // match against invariant titles
            foreach (var arc in arcs.OrderByDescending(arc => arc.InvariantTitle.Length))
            {
                if (!string.IsNullOrEmpty(arc.InvariantTitle))
                {
                    var pattern = @"\b" + Regex.Escape(arc.InvariantTitle) + @"\b";
                    if (Regex.IsMatch(directoryName, pattern, RegexOptions.IgnoreCase))
                    {
                        return arc;
                    }
                }
            }

            // match against arc ranks
            foreach (var arc in arcs)
            {
                var pattern =
                    @"\b0*" +
                    Regex.Escape(arc.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
                    @"\b";

                if (Regex.IsMatch(directoryName, pattern, RegexOptions.IgnoreCase))
                {
                    return arc;
                }
            }
        }

        return null;
    }
}
