using Adamantium.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Wires a region to a host control: resolves the adapter for the control's type from the app-registered
/// <see cref="RegionAdapterMappings"/> and attaches. Used by the <c>Region.Source</c> and <c>RegionManager.RegionName</c>
/// attached properties.</summary>
internal static class RegionHost
{
    public static void Attach(IRegion region, IUIComponent host)
    {
        if (region == null || host == null) return;
        var mappings = UIAppContext.Current?.UIContext?.Resolve<RegionAdapterMappings>();
        mappings?.GetAdapter(host.GetType())?.Attach(region, host);
    }
}
