using MediaBrowser.Controller.Providers;
using Xunit;

namespace JWueller.Jellyfin.OnePace.Tests;

public class ProviderIdsExtensionsTests
{
    [Theory]
    [InlineData("A1E2")] // legacy format
    [InlineData("clksp2ju3000008kwfdu141iy")]
    public void ShouldStoreOnePaceId(string providerId)
    {
        var itemLookupInfo = new ItemLookupInfo();
        itemLookupInfo.SetOnePaceId(providerId);

        Assert.Equal(providerId, itemLookupInfo.ProviderIds.GetValueOrDefault(Plugin.ProviderName));
    }

    [Theory]
    [InlineData("clksp2ju3000008kwfdu141iy")]
    public void ShouldExtractOnePaceId(string providerId)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            ProviderIds =
            {
                [Plugin.ProviderName] = providerId
            }
        };

        Assert.Equal(providerId, itemLookupInfo.GetOnePaceId());
    }

    [Theory]
    [InlineData("A1")]
    [InlineData("A2E40")]
    public void ShouldDiscardLegacyOnePaceId(string providerId)
    {
        var itemLookupInfo = new ItemLookupInfo
        {
            ProviderIds =
            {
                [Plugin.ProviderName] = providerId
            }
        };

        Assert.Null(itemLookupInfo.GetOnePaceId());
    }
}
