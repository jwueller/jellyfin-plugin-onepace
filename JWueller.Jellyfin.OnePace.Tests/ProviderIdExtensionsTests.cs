using MediaBrowser.Controller.Providers;
using Xunit;

namespace JWueller.Jellyfin.OnePace.Tests;

public class ProviderIdExtensionsTests
{
    [Fact]
    public void ShouldNotIdentifySeriesWithoutProviderId()
    {
        var itemLookupInfo = new ItemLookupInfo();

        var isOnePace = itemLookupInfo.GetIsOnePaceSeries();

        Assert.False(isOnePace);
    }

    [Theory]
    [InlineData(true, "1")]
    [InlineData(false, null)]
    public void ShouldEncodeSeriesProviderId(bool isOnePace, string expectedProviderId)
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetIsOnePaceSeries(isOnePace);

        Assert.Equal(expectedProviderId, itemLookupInfo.ProviderIds.GetValueOrDefault(Plugin.ProviderName));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData(null, false)]
    public void ShouldDecodeSeriesProviderId(string providerId, bool expectedIsOnePace)
    {
        var itemLookupInfo = new ItemLookupInfo()
        {
            ProviderIds = { [Plugin.ProviderName] = providerId },
        };

        var isOnePace = itemLookupInfo.GetIsOnePaceSeries();

        Assert.Equal(expectedIsOnePace, isOnePace);
    }

    [Theory]
    [InlineData(1, "A1")]
    [InlineData(2, "A2")]
    public void ShouldEncodeArcProviderId(int arcNumber, string expectedProviderId)
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetOnePaceArcNumber(arcNumber);

        Assert.Equal(expectedProviderId, itemLookupInfo.ProviderIds[Plugin.ProviderName]);
    }

    [Theory]
    [InlineData("A1", 1)]
    [InlineData("A2", 2)]
    public void ShouldDecodeArcProviderId(string providerId, int expectedArcNumber)
    {
        var itemLookupInfo = new ItemLookupInfo()
        {
            ProviderIds = { [Plugin.ProviderName] = providerId },
        };

        var arcNumber = itemLookupInfo.GetOnePaceArcNumber();

        Assert.NotNull(arcNumber);
        Assert.Equal(expectedArcNumber, arcNumber);
    }

    [Theory]
    [InlineData(1, 2, "A1E2")]
    [InlineData(3, 40, "A3E40")]
    public void ShouldEncodeEpisodeProviderId(int arcNumber, int episodeNumber, string expectedProviderId)
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetOnePaceEpisodeNumber(arcNumber, episodeNumber);

        Assert.Equal(expectedProviderId, itemLookupInfo.ProviderIds[Plugin.ProviderName]);
    }

    [Theory]
    [InlineData("A1E2", 1, 2)]
    [InlineData("A3E40", 3, 40)]
    public void ShouldDecodeEpisodeProviderId(string providerId, int expectedArcNumber, int expectedEpisodeNumber)
    {
        var itemLookupInfo = new ItemLookupInfo()
        {
            ProviderIds = { [Plugin.ProviderName] = providerId },
        };

        var result = itemLookupInfo.GetOnePaceEpisodeNumber();

        Assert.NotNull(result);

        var (arcNumber, episodeNumber) = result!.Value;
        Assert.Equal(expectedArcNumber, arcNumber);
        Assert.Equal(expectedEpisodeNumber, episodeNumber);
    }
}
