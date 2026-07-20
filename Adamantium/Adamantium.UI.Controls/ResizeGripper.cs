using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A resize affordance for a fully custom-chromed window: a small grip in the bottom-right corner whose bounds are
/// published as the window's <see cref="IWindow.ResizeGripRect"/>, so the platform hit-test treats a point in it as the
/// bottom-right sizing corner (HTBOTTOMRIGHT) and the OS runs its native resize from there. Used with
/// <see cref="WindowResizeMode.CanResizeWithGrip"/> - a window with NO edge resize borders, where the grip is the only
/// way to resize. The visual (diagonal dots) is a template; this class only publishes the region.
/// </summary>
public class ResizeGripper : Control
{
    /// <summary>Whether the grip publishes its rect to the OS window as the native sizing corner (HTBOTTOMRIGHT). Default
    /// true (the window-chrome use). Set false to reuse ONLY the visual + hit box - e.g. an OverlayWindow, which is not an
    /// OS window and resizes itself with a managed drag; publishing there would make the OS resize the whole window.</summary>
    public static readonly AdamantiumProperty PublishToWindowProperty = AdamantiumProperty.Register(nameof(PublishToWindow),
        typeof(bool), typeof(ResizeGripper), new PropertyMetadata(true));

    public bool PublishToWindow
    {
        get => GetValue<bool>(PublishToWindowProperty);
        set => SetValue(PublishToWindowProperty, value);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        var window = PublishToWindow ? OwnerWindow : null;
        if (window != null)
        {
            // The grip lives in the window's bottom-right corner, so its client-DIP rect follows directly from the client
            // size. (Deriving it from this control's own world transform / RenderSize is unreliable here: neither is final
            // during the control's OWN ArrangeOverride - both are set by Arrange() only AFTER this returns, so they read as
            // an origin-anchored 0-size box and the corner never hit-tests as a sizing grip.)
            window.ResizeGripRect = new Rect(window.ClientWidth - finalSize.Width, window.ClientHeight - finalSize.Height,
                finalSize.Width, finalSize.Height);
        }
        return size;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var window = PublishToWindow ? OwnerWindow : null;
        if (window != null) window.ResizeGripRect = default;   // grip left the tree -> stop hit-testing its (stale) rect
        base.OnDetachedFromVisualTree(e);
    }

    // The window this grip is hosted in (walk up the visual tree, fall back to RootVisual). Null outside a WindowBase.
    private WindowBase OwnerWindow
    {
        get
        {
            for (IUIComponent node = this; node != null; node = node.VisualParent)
                if (node is WindowBase window) return window;
            return RootVisual as WindowBase;
        }
    }
}
