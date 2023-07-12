using System;
using System.Globalization;
using System.Text.RegularExpressions;
using MediaBrowser.Model.Entities;

namespace JWueller.Jellyfin.OnePace;

internal static class ProviderIdsExtensions
{
    public static void SetIsOnePaceSeries(this IHasProviderIds hasProviderIds, bool isOnePace)
    {
        var providerId = isOnePace ? "1" : null;
        hasProviderIds.SetProviderId(Plugin.ProviderName, providerId);
    }

    public static bool GetIsOnePaceSeries(this IHasProviderIds hasProviderIds)
    {
        var providerId = hasProviderIds.GetProviderId(Plugin.ProviderName);
        return providerId == "1";
    }

    public static void SetOnePaceArcNumber(this IHasProviderIds hasProviderIds, int arcNumber)
    {
        var providerId = string.Format(CultureInfo.InvariantCulture, "A{0}", arcNumber);
        hasProviderIds.SetProviderId(Plugin.ProviderName, providerId);
    }

    public static int? GetOnePaceArcNumber(this IHasProviderIds hasProviderIds)
    {
        var providerId = hasProviderIds.GetProviderId(Plugin.ProviderName);
        if (providerId != null)
        {
            var match = Regex.Match(providerId, @"^A(\d+)$");
            if (match.Success)
            {
                try
                {
                    return int.Parse(match.Groups[1].ToString(), CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    // continue
                }
            }
        }

        return null;
    }

    public static void SetOnePaceEpisodeNumber(this IHasProviderIds hasProviderIds, int arcNumber, int episodeNumber)
    {
        var providerId = string.Format(CultureInfo.InvariantCulture, "A{0}E{1}", arcNumber, episodeNumber);
        hasProviderIds.SetProviderId(Plugin.ProviderName, providerId);
    }

    public static (int ArcNumber, int EpisodeNumber)? GetOnePaceEpisodeNumber(this IHasProviderIds hasProviderIds)
    {
        var providerId = hasProviderIds.GetProviderId(Plugin.ProviderName);
        if (providerId != null)
        {
            var match = Regex.Match(providerId, @"^A(\d+)E(\d+)$");
            if (match.Success)
            {
                try
                {
                    int arcNumber = int.Parse(match.Groups[1].ToString(), CultureInfo.InvariantCulture);
                    int episodeNumber = int.Parse(match.Groups[2].ToString(), CultureInfo.InvariantCulture);
                    return (arcNumber, episodeNumber);
                }
                catch (FormatException)
                {
                    // continue
                }
            }
        }

        return null;
    }
}
