using JWueller.Jellyfin.OnePace.Model;
using MediaBrowser.Controller.Providers;
using Moq;
using Xunit;

namespace JWueller.Jellyfin.OnePace.Tests;

public class EpisodeIdentifierTests
{
    private readonly IRepository _repository;

    private class TestEpisode : IEpisode
    {
        public string Id { get; init; } = null!;

        public int Rank { get; init; }

        public string ArcId { get; init; } = null!;

        public string InvariantTitle { get; init; } = null!;

        public string? MangaChapters { get; init; }

        public DateTime? ReleaseDate { get; init; }

        public uint? Crc32 { get; init; }
    }

    public EpisodeIdentifierTests()
    {
        var episodes = new List<IEpisode>
        {
            new TestEpisode
            {
                Id = "clkso9n2a000008jkdjxn6acj",
                Rank = 1,
                ArcId = "clksod80d000408jkbahl6yqa",
                InvariantTitle = "Romance Dawn 01",
                MangaChapters = "1",
                ReleaseDate = null,
                Crc32 = 0xD767799C
            },

            new TestEpisode
            {
                Id = "clkso9t8u000108jk5lbu2409",
                Rank = 2,
                ArcId = "clksodbar000508jk9wkz0y2n",
                InvariantTitle = "Romance Dawn 02",
                MangaChapters = "2",
                ReleaseDate = null,
                Crc32 = 0x04A43CEF
            },

            new TestEpisode
            {
                Id = "clkso9z6n000208jk069u63ih",
                Rank = 1,
                ArcId = "clksode6a000608jkfm0m77m3",
                InvariantTitle = "Orange Town 01",
                MangaChapters = "8-11",
                ReleaseDate = null,
                Crc32 = 0xC7CA5080
            },

            new TestEpisode
            {
                Id = "clksoa57k000308jkb3cu73n8",
                Rank = 2,
                ArcId = "clksodhex000708jk7bak1tml",
                InvariantTitle = "Orange Town 02",
                MangaChapters = null,
                ReleaseDate = null,
                Crc32 = null
            },

            new TestEpisode
            {
                Id = "clqgsm6a403yjnv5cw3owwctw",
                Rank = 1,
                ArcId = "clqgslsp8006pnv5c08glvvjm",
                InvariantTitle = "Whisky Peak 01",
                MangaChapters = null,
                ReleaseDate = null,
                Crc32 = null
            },

            new TestEpisode
            {
                Id = "clqgsm60g03vvnv5cvf2afpq8",
                Rank = 1,
                ArcId = "clqgslt5n00bwnv5cj0e4wb0i",
                InvariantTitle = "Enies Lobby 01",
                MangaChapters = null,
                ReleaseDate = null,
                Crc32 = null
            },

            new TestEpisode
            {
                Id = "clqgslvmk011bnv5cvl0khvob",
                Rank = 1,
                ArcId = "clqgslt7v00cjnv5cg8eumgzc",
                InvariantTitle = "Post-Enies Lobby 01",
                MangaChapters = null,
                ReleaseDate = null,
                Crc32 = null
            }
        };

        var repositoryMock = new Mock<IRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(repository => repository.FindAllEpisodesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IReadOnlyCollection<IEpisode>>(episodes));

        repositoryMock
            .Setup(repository => repository.FindEpisodeByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string episodeId, CancellationToken _) =>
            {
                return Task.FromResult(episodes.Find(episode => episode.Id == episodeId));
            });

        _repository = repositoryMock.Object;
    }

    [Theory]
    [InlineData("clkso9n2a000008jkdjxn6acj", "Romance Dawn 01")]
    [InlineData("clkso9t8u000108jk5lbu2409", "Romance Dawn 02")]
    [InlineData("clkso9z6n000208jk069u63ih", "Orange Town 01")]
    [InlineData("clksoa57k000308jkb3cu73n8", "Orange Town 02")]
    public async Task ShouldIdentifyEpisodeByProviderId(string episodeId, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetOnePaceId(episodeId);

        var episode = await EpisodeIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(episode);
        Assert.Equal(episodeId, episode.Id);
        Assert.Equal(expectedInvariantTitle, episode.InvariantTitle);
    }

    [Theory]
    [InlineData("/path/to/One Pace/[One Pace][1-7] Romance Dawn [1080p]/[One Pace][1] Romance Dawn 01 [1080p][D767799C].mkv", "Romance Dawn 01")] // nested release name
    [InlineData("/path/to/One Pace/[One Pace][1] Romance Dawn 01 [1080p][D767799C].mkv", "Romance Dawn 01")] // release name
    [InlineData("/path/to/One Pace/[One Pace][2] Romance Dawn 02 [1080p][04A43CEF].mkv", "Romance Dawn 02")] // release name
    [InlineData("/path/to/One Pace/[One Pace][8-11] Orange Town 01 [480p][A2F5F372].mkv", "Orange Town 01")] // release name
    [InlineData("/path/to/One Pace/[One Pace][11-16] Orange Town 02 [480p][3D7957D8].mkv", "Orange Town 02")] // release name
    [InlineData("/path/to/One Pace/1.mkv", "Romance Dawn 01")] // chapter range only
    [InlineData("/path/to/One Pace/8-11.mkv", "Orange Town 01")] // chapter range only
    [InlineData("/path/to/One Pace/Romance Dawn 01.mkv", "Romance Dawn 01")] // invariant title only
    [InlineData("/path/to/One Pace/Orange Town 01.mkv", "Orange Town 01")] // invariant title only
    [InlineData("/path/to/One Pace/D767799C.mkv", "Romance Dawn 01")] // uppercase CRC-32 only
    [InlineData("/path/to/One Pace/c7ca5080.mkv", "Orange Town 01")] // lowercase CRC-32 only
    public async Task ShouldIdentifyEpisodeByPath(string path, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Path = path
        };

        var episode = await EpisodeIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(episode);
        Assert.Equal(expectedInvariantTitle, episode.InvariantTitle);
    }

    /// <summary>
    /// Regression test for titles that are substrings of other titles.
    /// </summary>
    /// <see href="https://github.com/jwueller/jellyfin-plugin-onepace/issues/34"/>
    [Theory]
    [InlineData("/path/to/One Pace/Enies Lobby 01.mkv", "Enies Lobby 01")]
    [InlineData("/path/to/One Pace/Post-Enies Lobby 01.mkv", "Post-Enies Lobby 01")]
    public async Task ShouldPreferLongerTitles(string path, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Path = path
        };

        var episode = await EpisodeIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(episode);
        Assert.Equal(expectedInvariantTitle, episode.InvariantTitle);
    }

    /// <summary>
    /// It's 'Whisky Peak', not 'Whiskey Peak', but its such a common typo that we need to handle it.
    /// </summary>
    [Fact]
    public async Task ShouldIdentifyWhiskyPeakDespiteTypo()
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Path = "/path/to/One Pace/Whiskey Peak 01.mkv"
        };

        var episode = await EpisodeIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(episode);
        Assert.Equal("Whisky Peak 01", episode.InvariantTitle);
    }

    /// <summary>
    /// Jellyfin 10.9.x apparently decided to not have paths for all media anymore.
    /// </summary>
    [Fact]
    public async Task ShouldNotCrashWithMissingPath()
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Path = null
        };

        var episode = await EpisodeIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.Null(episode);
    }
}
