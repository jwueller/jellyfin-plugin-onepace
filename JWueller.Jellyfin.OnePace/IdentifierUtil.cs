using System.Text.RegularExpressions;

namespace JWueller.Jellyfin.OnePace;

internal static class IdentifierUtil
{
    public static bool MatchesOnePaceInvariantTitle(string? candidate)
    {
        return candidate != null && Regex.IsMatch(candidate, @"\bOne\s*Pace\b", RegexOptions.IgnoreCase);
    }
}
