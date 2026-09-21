using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.Navigation;

/// <summary>Default region registry. Regions it creates get the DI resolver (to resolve target view models) and the
/// navigation service (for the lifecycle context).</summary>
public sealed class RegionManager : IRegionManager
{
    private readonly IDependencyResolver _resolver;
    private readonly Dictionary<string, IRegion> _regions = new();
    private int _anonymous;

    public RegionManager(IDependencyResolver resolver)
    {
        _resolver = resolver;
    }

    // Set by NavigationService once it exists; back-filled onto regions created before that (see setter).
    private INavigationService _navigationService;
    internal INavigationService NavigationService
    {
        get => _navigationService;
        set
        {
            _navigationService = value;
            foreach (var region in _regions.Values)
                if (region is Region concrete) concrete.NavigationService = value;
        }
    }

    public IRegion this[string regionName] => GetOrCreateRegion(regionName);
    public IReadOnlyCollection<IRegion> Regions => _regions.Values;

    public IRegion GetOrCreateRegion(string regionName)
    {
        if (regionName == null) return CreateRegion(null);
        if (!_regions.TryGetValue(regionName, out var region))
        {
            region = new Region(regionName, _resolver, _navigationService);
            _regions[regionName] = region;
        }
        return region;
    }

    public IRegion CreateRegion(string name = null)
    {
        name ??= "__region_" + (++_anonymous);
        var region = new Region(name, _resolver, _navigationService);
        _regions[name] = region;
        return region;
    }

    public bool TryGetRegion(string regionName, out IRegion region) => _regions.TryGetValue(regionName, out region);

    public void RegisterRegion(IRegion region)
    {
        if (region != null) _regions[region.Name] = region;
    }
}
