namespace Adamantium.Game.Sandbox;

/// <summary>The names of the app's regions, said once. A region is addressed from two sides - the view declares it with
/// <c>nav:RegionManager.RegionName</c> and a view-model navigates into it by the same name - and a literal repeated on
/// both sides is a rename waiting to break one of them silently: the markup would go on declaring a region nobody
/// navigates to, and the navigation would create an empty one nobody shows.
/// <para>Markup reads these with <c>{x:Static local:RegionNames.BrushStand}</c>, so both sides quote the same constant
/// rather than the same spelling.</para></summary>
public static class RegionNames
{
    /// <summary>The brushes tab's stand host: one live stand at a time, navigated in by view key.</summary>
    public const string BrushStand = "BrushStand";
}
