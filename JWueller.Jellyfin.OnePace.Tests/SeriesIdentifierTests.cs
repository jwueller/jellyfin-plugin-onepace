using JWueller.Jellyfin.OnePace.Model;
using MediaBrowser.Controller.Providers;
using Moq;
using Xunit;

namespace JWueller.Jellyfin.OnePace.Tests;

public class SeriesIdentifierTests
{
    private readonly IRepository _repository;

    private class TestSeries : ISeries
    {
        public string InvariantTitle { get; init; } = null!;

        public string OriginalTitle { get; init; } = null!;
    }

    public SeriesIdentifierTests()
    {
        var series = new TestSeries
        {
            InvariantTitle = "One Pace",
            OriginalTitle = "One Piece",
        };

        var repositoryMock = new Mock<IRepository>(MockBehavior.Strict);
        repositoryMock
            .Setup(repository => repository.FindSeriesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<ISeries?>(series));

        _repository = repositoryMock.Object;
    }

    [Fact]
    public async void ShouldIdentifySeriesByProviderId()
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetOnePaceId(Plugin.DummySeriesId);

        var series = await SeriesIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(series);
        Assert.Equal("One Pace", series.InvariantTitle);
    }

    [Theory]
    [InlineData("/path/to/One Pace")]
    [InlineData("/path/to/One Piece [One Pace]")]
    public async void ShouldIdentifySeriesByPath(string path)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Path = path,
        };

        var series = await SeriesIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(series);
        Assert.Equal("One Pace", series.InvariantTitle);
    }

    [Theory]
    [InlineData("One Pace")]
    [InlineData("One Piece [One Pace]")]
    public async void ShouldIdentifySeriesByName(string name)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            Name = name,
        };

        var series = await SeriesIdentifier.IdentifyAsync(_repository, itemLookupInfo, CancellationToken.None);

        Assert.NotNull(series);
        Assert.Equal("One Pace", series.InvariantTitle);
    }
}
