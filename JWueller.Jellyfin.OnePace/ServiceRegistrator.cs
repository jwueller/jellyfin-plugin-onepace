using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace JWueller.Jellyfin.OnePace;

/// <inheritdoc/>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<OnePaceRepository>();
    }
}
