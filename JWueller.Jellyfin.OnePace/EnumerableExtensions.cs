using System;
using System.Collections.Generic;

namespace JWueller.Jellyfin.OnePace;

internal static class EnumerableExtensions
{
    public static T? FirstOrNull<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        where T : struct
    {
        foreach (var item in source)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return null;
    }

    public static T? FirstOrNull<T>(this IEnumerable<T> source)
        where T : struct
    {
        foreach (var item in source)
        {
            return item;
        }

        return null;
    }
}
