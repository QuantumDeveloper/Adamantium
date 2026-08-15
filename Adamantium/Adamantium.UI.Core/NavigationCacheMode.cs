namespace Adamantium.UI.Core;

/// <summary>What <c>x:KeepAlive</c> asks of whoever navigates away from a view. An enum and not a flag, because "keep
/// it" and "keep it NO MATTER WHAT" are different promises and a cache that cannot be bounded is a leak - stating it as
/// a bool now would mean breaking the markup to add the third answer later. Named after WinUI's NavigationCacheMode,
/// which answers the same question.</summary>
public enum NavigationCacheMode
{
    /// <summary>Rebuild the view on every visit. The default - what every view does today.</summary>
    Disabled,

    /// <summary>Keep the view alive between visits, but let the cache evict it when it grows past its limit.</summary>
    Enabled,

    /// <summary>Keep the view alive and never evict it. For a page whose rebuild is the pause worth avoiding.</summary>
    Required
}
