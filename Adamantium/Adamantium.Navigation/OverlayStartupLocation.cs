namespace Adamantium.Navigation;

/// <summary>Where an <c>OverlayWindow</c> first appears.</summary>
public enum OverlayStartupLocation
{
    /// <summary>Centred on the parent window, cascaded so multiple don't stack exactly (the default).</summary>
    CenterOwner,

    /// <summary>At the explicit <see cref="IOverlayAware.Left"/>/<see cref="IOverlayAware.Top"/>, in window coordinates.</summary>
    Manual
}
