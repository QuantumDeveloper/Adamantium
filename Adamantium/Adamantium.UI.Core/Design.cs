namespace Adamantium.UI.Core;

/// <summary>
/// Design-time flag, mirroring WPF's <c>DesignerProperties.GetIsInDesignMode</c> and Avalonia's
/// <c>Design.IsDesignMode</c>. The AUML designer host sets this before loading markup so design-unsafe code -
/// e.g. a behavior that spins up a game - can opt out and stay dormant in the previewer.
/// </summary>
public static class Design
{
    /// <summary>True while markup is being loaded for the designer/previewer rather than the running app.</summary>
    public static bool IsDesignMode { get; set; }

    /// <summary>
    /// True while the designer is rendering a LIVE preview (a continuously-ticked frame stream) rather than a single
    /// static shot. Animations play in design mode only when this is set, so a one-shot render still captures the
    /// settled state while the live previewer animates.
    /// </summary>
    public static bool IsLivePreview { get; set; }
}
