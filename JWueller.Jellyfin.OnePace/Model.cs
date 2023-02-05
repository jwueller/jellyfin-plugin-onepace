using System;

namespace JWueller.Jellyfin.OnePace.Model;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public interface ISeries
{
    string InvariantTitle { get; } // e.g. "One Pace"

    string OriginalTitle { get; } // e.g. "One Piece"
}

public interface IArc
{
    int Number { get; }

    string InvariantTitle { get; } // e.g. "Romance Dawn"

    DateTime? ReleaseDate { get; }
}

public interface IEpisode
{
    int Number { get; }

    int ArcNumber { get; }

    string InvariantTitle { get; } // e.g. "Romance Dawn 01"

    string MangaChapters { get; }

    DateTime? ReleaseDate { get; }
}

public interface IArt
{
    string Url { get; }

    int? Width { get; }

    int? Height { get; }
}

public interface ILocalization
{
    string LanguageCode { get; }

    string Title { get; }

    string? Description { get; }
}
