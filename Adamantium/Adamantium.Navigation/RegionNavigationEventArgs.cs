using System;

namespace Adamantium.Navigation;

/// <summary>Raised by <see cref="IRegion.Navigated"/> after a navigation settles - carries its context and result.</summary>
public sealed class RegionNavigationEventArgs : EventArgs
{
    public RegionNavigationEventArgs(NavigationContext context, NavigationResult result)
    {
        Context = context;
        Result = result;
    }

    public NavigationContext Context { get; }
    public NavigationResult Result { get; }
}
