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
        public int Number { get; init; }

        public int ArcNumber { get; init; }

        public string InvariantTitle { get; init; } = null!;

        public string MangaChapters { get; init; } = null!;

        public DateTime? ReleaseDate { get; init; }
    }

    public EpisodeIdentifierTests()
    {
        var episodes = new List<IEpisode>
        {
            new TestEpisode
            {
                Number = 1,
                ArcNumber = 1,
                InvariantTitle = "Romance Dawn 01",
                MangaChapters = "1",
                ReleaseDate = null,
            },

            new TestEpisode
            {
                Number = 2,
                ArcNumber = 1,
                InvariantTitle = "Romance Dawn 02",
                MangaChapters = "2",
                ReleaseDate = null,
            },

            new TestEpisode
            {
                Number = 1,
                ArcNumber = 2,
                InvariantTitle = "Orange Town 01",
                MangaChapters = "8-11",
                ReleaseDate = null,
            },
        };

        var repositoryMock = new Mock<IRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(repository => repository.FindAllEpisodesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IReadOnlyCollection<IEpisode>>(episodes));

        repositoryMock
            .Setup(repository => repository.FindEpisodeByNumberAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int arcNumber, int episodeNumber, CancellationToken _) =>
            {
                return Task.FromResult(episodes.Find(episode => episode.ArcNumber == arcNumber && episode.Number == episodeNumber));
            });

        _repository = repositoryMock.Object;
    }

    [Theory]
    [InlineData(1, 1, "Romance Dawn 01")]
    [InlineData(1, 2, "Romance Dawn 02")]
    [InlineData(2, 1, "Orange Town 01")]
    public async void ShouldIdentifyArcByProviderId(int arcNumber, int episodeNumber, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetOnePaceEpisodeNumber(arcNumber, episodeNumber);

        var episode = await EpisodeIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(episode);
        Assert.Equal(arcNumber, episode.ArcNumber);
        Assert.Equal(episodeNumber, episode.Number);
        Assert.Equal(expectedInvariantTitle, episode.InvariantTitle);
    }

    [Theory]
    [InlineData("/path/to/One Pace/[One Pace][1-7] Romance Dawn [1080p]/[One Pace][1] Romance Dawn 01 [1080p][D767799C].mkv", 1, 1, "Romance Dawn 01")] // nested release name
    [InlineData("/path/to/One Pace/[One Pace][1] Romance Dawn 01 [1080p][D767799C].mkv", 1, 1, "Romance Dawn 01")] // release name
    [InlineData("/path/to/One Pace/1.mkv", 1, 1, "Romance Dawn 01")] // chapter range
    [InlineData("/path/to/One Pace/Romance Dawn 01.mkv", 1, 1, "Romance Dawn 01")] // title
    [InlineData("/path/to/One Pace/[One Pace][2] Romance Dawn 02 [1080p][04A43CEF].mkv", 1, 2, "Romance Dawn 02")] // release name
    [InlineData("/path/to/One Pace/[One Pace][8-11] Orange Town 01 [480p][A2F5F372].mkv", 2, 1, "Orange Town 01")] // release name
    [InlineData("/path/to/One Pace/8-11.mkv", 2, 1, "Orange Town 01")] // chapter range
    [InlineData("/path/to/One Pace/Orange Town 01.mkv", 2, 1, "Orange Town 01")] // title
    public async void ShouldIdentifyEpisodeByPath(string path, int expectedArcNumber, int expectedEpisodeNumber, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Path = path,
        };

        var episode = await EpisodeIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(episode);
        Assert.Equal(expectedArcNumber, episode.ArcNumber);
        Assert.Equal(expectedEpisodeNumber, episode.Number);
        Assert.Equal(expectedInvariantTitle, episode.InvariantTitle);
    }
}
