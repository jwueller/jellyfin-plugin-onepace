using System.Threading;
using System.Threading.Tasks;
using JWueller.Jellyfin.OnePace.Model;
using MediaBrowser.Controller.Providers;

namespace JWueller.Jellyfin.OnePace;

internal static class SeriesIdentifier
{
    public static async Task<ISeries?> IdentifyAsync(
        IRepository repository,
        ItemLookupInfo itemLookupInfo,
        CancellationToken cancellationToken)
    {
        if (itemLookupInfo.GetOnePaceId() == Plugin.DummySeriesId
            || (itemLookupInfo.Name != null && IdentifierUtil.OnePaceInvariantTitleRegex.IsMatch(itemLookupInfo.Name))
            || (itemLookupInfo.Path != null && IdentifierUtil.OnePaceInvariantTitleRegex.IsMatch(itemLookupInfo.Path)))
        {
            return await repository.FindSeriesAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
