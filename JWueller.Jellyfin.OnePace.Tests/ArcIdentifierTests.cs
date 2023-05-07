using JWueller.Jellyfin.OnePace.Model;
using MediaBrowser.Controller.Providers;
using Moq;
using Xunit;

namespace JWueller.Jellyfin.OnePace.Tests;

public class ArcIdentifierTests
{
    private readonly IRepository _repository;

    private class TestArc : IArc
    {
        public int Number { get; init; }

        public string InvariantTitle { get; init; }

        public string MangaChapters { get; init; }

        public DateTime? ReleaseDate { get; init; }
    }

    public ArcIdentifierTests()
    {
        var arcs = new List<IArc>
        {
            new TestArc
            {
                Number = 1,
                InvariantTitle = "Romance Dawn",
                MangaChapters = "1-7",
                ReleaseDate = null,
            },

            new TestArc
            {
                Number = 2,
                InvariantTitle = "Orange Town",
                MangaChapters = "8-21",
                ReleaseDate = null,
            },
        };

        var repositoryMock = new Mock<IRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(repository => repository.FindAllArcsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IReadOnlyCollection<IArc>>(arcs));

        repositoryMock
            .Setup(repository => repository.FindArcByNumberAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int arcNumber, CancellationToken _) =>
            {
                return Task.FromResult(arcs.Find(arc => arc.Number == arcNumber));
            });

        _repository = repositoryMock.Object;
    }

    [Theory]
    [InlineData(1, "Romance Dawn")]
    [InlineData(2, "Orange Town")]
    public async void ShouldIdentifyArcByProviderId(int arcNumber, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetOnePaceArcNumber(arcNumber);

        var arc = await ArcIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(arc);
        Assert.Equal(arcNumber, arc.Number);
        Assert.Equal(expectedInvariantTitle, arc.InvariantTitle);
    }

    [Theory]
    [InlineData("/path/to/One Pace/[One Pace][1-7] Romance Dawn [1080p]", 1, "Romance Dawn")] // release name
    [InlineData("/path/to/One Pace/1-7", 1, "Romance Dawn")] // chapter range
    [InlineData("/path/to/One Pace/Romance Dawn", 1, "Romance Dawn")] // title
    [InlineData("/path/to/One Pace/1", 1, "Romance Dawn")] // number
    [InlineData("/path/to/One Pace/001", 1, "Romance Dawn")] // number (padded)
    [InlineData("/path/to/One Pace/[One Pace][8-21] Orange Town [1080p]", 2, "Orange Town")] // release name
    public async void ShouldIdentifyArcByPath(string path, int expectedArcNumber, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Path = path,
        };

        var arc = await ArcIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(arc);
        Assert.Equal(expectedArcNumber, arc.Number);
        Assert.Equal(expectedInvariantTitle, arc.InvariantTitle);
    }
}
