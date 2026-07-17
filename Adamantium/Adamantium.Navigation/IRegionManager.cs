using System.Collections.Generic;

namespace Adamantium.Navigation;

/// <summary>Owns the app's regions. A view model creates an anonymous VM-owned region (<see cref="CreateRegion"/>) or
/// resolves a named one (<c>this[name]</c>/<see cref="GetOrCreateRegion"/>) that a control declares via
/// <c>RegionManager.RegionName</c>.</summary>
public interface IRegionManager
{
    IRegion this[string regionName] { get; }
    IRegion GetOrCreateRegion(string regionName);

    /// <summary>Create a region; a null name gets an auto-generated one (a VM owns it and binds it to a control).</summary>
    IRegion CreateRegion(string name = null);

    bool TryGetRegion(string regionName, out IRegion region);
    void RegisterRegion(IRegion region);
    IReadOnlyCollection<IRegion> Regions { get; }
}
