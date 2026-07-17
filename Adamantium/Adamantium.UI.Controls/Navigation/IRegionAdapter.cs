using Adamantium.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Binds a control-agnostic <see cref="IRegion"/> to a concrete host control. This is the seam that makes a
/// region work on ContentControl, Selector/TabControl, or a future DockingControl - register a new adapter for a new
/// host type via <see cref="RegionAdapterMappings"/>.</summary>
public interface IRegionAdapter
{
    void Attach(IRegion region, IUIComponent host);
}
