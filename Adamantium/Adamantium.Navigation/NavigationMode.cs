namespace Adamantium.Navigation;

/// <summary>How the current navigation was reached - lets an <see cref="INavigationAware"/> target tell a fresh
/// navigation from a back/forward journal move or a same-target refresh.</summary>
public enum NavigationMode
{
    New,
    Back,
    Forward,
    Refresh
}
