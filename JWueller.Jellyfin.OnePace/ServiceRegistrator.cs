using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace JWueller.Jellyfin.OnePace;

/// <inheritdoc/>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Instantiated by Jellyfin")]
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IRepository, WebRepository>();
    }
}
