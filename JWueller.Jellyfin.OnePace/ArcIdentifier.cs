using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Providers;

namespace JWueller.Jellyfin.OnePace;

internal static class ArcIdentifier
{
    public static async Task<Model.IArc?> IdentifyAsync(OnePaceRepository repository, SeasonInfo itemLookupInfo, CancellationToken cancellationToken)
    {
        var arcNumber = itemLookupInfo.GetOnePaceArcNumber();
        if (arcNumber != null)
        {
            var arc = await repository.FindArcByNumberAsync(arcNumber.Value, cancellationToken).ConfigureAwait(false);
            if (arc != null)
            {
                return arc;
            }
        }

        if (IdentifierUtil.MatchesOnePaceInvariantTitle(itemLookupInfo.Path))
        {
            var arcs = await repository.FindAllArcsAsync(cancellationToken).ConfigureAwait(false);
            if (arcs != null)
            {
                // All of these folder names should get matched properly:
                // - "[One Pace][1-7] Romance Dawn [1080p]"
                // - "Arc 1 - Romance Dawn"
                // - "Romance Dawn"
                // - "1"
                var directoryName = Path.GetFileName(itemLookupInfo.Path);

                // match against chapter ranges
                foreach (var arc in arcs)
                {
                    if (!string.IsNullOrEmpty(arc.MangaChapters))
                    {
                        if (Regex.IsMatch(directoryName, @"\b" + Regex.Escape(arc.MangaChapters) + @"\b", RegexOptions.IgnoreCase))
                        {
                            return arc;
                        }
                    }
                }

                // match against invariant titles
                foreach (var arc in arcs)
                {
                    if (!string.IsNullOrEmpty(arc.InvariantTitle))
                    {
                        if (Regex.IsMatch(directoryName, @"\b" + Regex.Escape(arc.InvariantTitle) + @"\b", RegexOptions.IgnoreCase))
                        {
                            return arc;
                        }
                    }
                }

                // match against arc numbers
                foreach (var arc in arcs)
                {
                    if (Regex.IsMatch(directoryName, @"\b0*" + Regex.Escape(arc.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)) + @"\b", RegexOptions.IgnoreCase))
                    {
                        return arc;
                    }
                }
            }
        }

        return null;
    }
}
