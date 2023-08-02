using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace JWueller.Jellyfin.OnePace;

/// <summary>
/// The main plugin.
/// </summary>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global", Justification = "Instantiated by Jellyfin")]
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    internal const string ProviderName = "One Pace";

    /// <summary>
    /// The series doesn't expose an ID right now, so we just pretend this is it.
    /// </summary>
    internal const string DummySeriesId = "clkspj4vn000008k33lnnb4hj";

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    /// <inheritdoc />
    public override string Name => ProviderName;

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("1c0bf35e-3df4-47cc-8a4e-e3865de60d2f");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new List<PluginPageInfo>();
    }
}
