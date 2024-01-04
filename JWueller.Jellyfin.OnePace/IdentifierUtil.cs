using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace JWueller.Jellyfin.OnePace;

internal static class IdentifierUtil
{
    public static readonly Regex OnePaceInvariantTitleRegex = BuildTextRegex("One Pace");

    [SuppressMessage("ReSharper", "StringLiteralTypo", Justification = "Regex")]
    public static Regex BuildTextRegex(string needle)
    {
        var pattern = @"\b" + string.Join(@"\s+", needle.Split().Select(Regex.Escape)) + @"\b";

        // This is such a common typo that even the One Pace team made it: The arc is called "Whisky Peak",
        // but even some of the distributed files are called "Whiskey Peak" instead. We accept both.
        pattern = pattern.Replace("Whisky", "Whiske?y", StringComparison.InvariantCultureIgnoreCase);

        return new Regex(pattern, RegexOptions.IgnoreCase);
    }
}
