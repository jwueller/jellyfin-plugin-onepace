using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// Provides an external ID for the series.
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Instantiated by Jellyfin")]
public class SeriesExternalId : IExternalId
{
    /// <inheritdoc/>
    public string ProviderName => Plugin.ProviderName;

    /// <inheritdoc/>
    public string Key => Plugin.ProviderName;

    /// <inheritdoc/>
    public ExternalIdMediaType? Type => null;

    /// <inheritdoc/>
    public string UrlFormatString => "https://onepace.net/";

    /// <inheritdoc/>
    public bool Supports(IHasProviderIds item) => item is Series;
}
