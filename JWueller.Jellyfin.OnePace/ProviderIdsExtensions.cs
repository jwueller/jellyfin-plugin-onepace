using MediaBrowser.Model.Entities;

namespace JWueller.Jellyfin.OnePace;

internal static class ProviderIdsExtensions
{
    public static void SetOnePaceId(this IHasProviderIds hasProviderIds, string id)
    {
        hasProviderIds.SetProviderId(Plugin.ProviderName, id);
    }

    public static string? GetOnePaceId(this IHasProviderIds hasProviderIds)
    {
        // Only accept long episode IDs to weed out shorter synthetic IDs that were used before the One Pace API
        // exposed CUIDs.
        var episodeId = hasProviderIds.GetProviderId(Plugin.ProviderName);
        return episodeId != null && episodeId.Length == 25 ? episodeId : null;
    }
}
