using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Providers;

namespace JWueller.Jellyfin.OnePace;

internal static class SeriesIdentifier
{
    public static async Task<Model.ISeries?> IdentifyAsync(IRepository repository, ItemLookupInfo itemLookupInfo, CancellationToken cancellationToken)
    {
        if (itemLookupInfo.GetIsOnePaceSeries()
            || IdentifierUtil.MatchesOnePaceInvariantTitle(itemLookupInfo.Name)
            || IdentifierUtil.MatchesOnePaceInvariantTitle(itemLookupInfo.OriginalTitle)
            || IdentifierUtil.MatchesOnePaceInvariantTitle(itemLookupInfo.Path))
        {
            return await repository.FindSeriesAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
