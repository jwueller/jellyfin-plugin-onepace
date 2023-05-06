using System;
using System.Globalization;
using System.Text.Json;

namespace JWueller.Jellyfin.OnePace;

internal static class JsonElementExtensions
{
    public static string GetNonNullString(this JsonElement jsonElement)
    {
        var maybeNullString = jsonElement.GetString();
        if (maybeNullString == null)
        {
            throw new FormatException("Expected a non-null string");
        }

        return maybeNullString;
    }

    public static int? GetNullableInt32(this JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != JsonValueKind.Undefined && jsonElement.ValueKind != JsonValueKind.Null)
        {
            return jsonElement.GetInt32();
        }

        return null;
    }

    public static int? CoerceNullableInt32(this JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != JsonValueKind.Undefined && jsonElement.ValueKind != JsonValueKind.Null)
        {
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                return int.Parse(jsonElement.GetNonNullString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            else if (jsonElement.ValueKind == JsonValueKind.Number)
            {
                return jsonElement.GetInt32();
            }
            else
            {
                throw new FormatException("Expected a string or number");
            }
        }

        return null;
    }
}
