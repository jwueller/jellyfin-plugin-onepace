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
        public string Id { get; init; } = null!;

        public int Number { get; init; }

        public string InvariantTitle { get; init; } = null!;

        public string? MangaChapters { get; init; }

        public DateTime? ReleaseDate { get; init; }
    }

    public ArcIdentifierTests()
    {
        var arcs = new List<IArc>
        {
            new TestArc
            {
                Id = "clkso3n3l000008l751pk86u4",
                Number = 1,
                InvariantTitle = "Romance Dawn",
                MangaChapters = "1-7",
                ReleaseDate = null,
            },

            new TestArc
            {
                Id = "clkso3uwi000108l724rj9vc0",
                Number = 2,
                InvariantTitle = "Orange Town",
                MangaChapters = "8-21",
                ReleaseDate = null,
            },

            new TestArc
            {
                Id = "clkso3zi6000208l7bhq7dtn6",
                Number = 3,
                InvariantTitle = "Syrup Village",
                MangaChapters = null,
                ReleaseDate = null,
            },
        };

        var repositoryMock = new Mock<IRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(repository => repository.FindAllArcsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<IReadOnlyCollection<IArc>>(arcs));

        repositoryMock
            .Setup(repository => repository.FindArcByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) =>
            {
                return Task.FromResult(arcs.Find(arc => arc.Id == id));
            });

        _repository = repositoryMock.Object;
    }

    [Theory]
    [InlineData("clkso3n3l000008l751pk86u4", "Romance Dawn")]
    [InlineData("clkso3uwi000108l724rj9vc0", "Orange Town")]
    [InlineData("clkso3zi6000208l7bhq7dtn6", "Syrup Village")]
    public async void ShouldIdentifyArcByProviderId(string id, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetOnePaceId(id);

        var arc = await ArcIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(arc);
        Assert.Equal(expectedInvariantTitle, arc.InvariantTitle);
    }

    [Theory]
    [InlineData("/path/to/One Pace/[One Pace][1-7] Romance Dawn [1080p]", "Romance Dawn")] // release name
    [InlineData("/path/to/One Pace/1-7", "Romance Dawn")] // chapter range
    [InlineData("/path/to/One Pace/Romance Dawn", "Romance Dawn")] // title
    [InlineData("/path/to/One Pace/1", "Romance Dawn")] // number
    [InlineData("/path/to/One Pace/001", "Romance Dawn")] // number (padded)
    [InlineData("/path/to/One Pace/[One Pace][8-21] Orange Town [1080p]", "Orange Town")] // release name
    [InlineData("/path/to/One Pace/[One Pace][23-41] Syrup Village [480p]", "Syrup Village")] // release name
    public async void ShouldIdentifyArcByPath(string path, string expectedInvariantTitle)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Path = path,
        };

        var arc = await ArcIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(arc);
        Assert.Equal(expectedInvariantTitle, arc.InvariantTitle);
    }
}
