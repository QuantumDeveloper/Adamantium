using System;

namespace Adamantium.UI.Core;

/// <summary>
/// What an element says about ITSELF: I joined the tree, I left it, I became invisible, my content is stale, I resized, I
/// moved, my colour changed, a theme swap has begun. Facts about the visual tree - and not one word about how, or whether,
/// any of it is drawn.
///
/// This is the seam between the two layers. A control announces a fact and knows nothing more: no dirty registry, no marks,
/// no render cache. The RENDERER listens here and decides what each fact costs it - a splice, a re-record, a re-bake, or
/// nothing at all (see RenderDirtyBridge). That decision is the renderer's alone, and it can change completely without a
/// single line moving in the control layer.
/// </summary>
public static class VisualTreeNotifications
{
    // Whoever cares must be listening BEFORE the first fact is announced, or that fact is simply lost. So the listeners are
    // wired the moment this type is first touched - which is, by definition, the moment before the first announcement.
    static VisualTreeNotifications() => RenderDirtyBridge.Ensure();

    /// <summary>An element entered the visual tree.</summary>
    public static event Action<IUIComponent> Attached;

    /// <summary>An element left the visual tree.</summary>
    public static event Action<IUIComponent> Detached;

    /// <summary>An element's Visibility changed - it draws now, or it no longer does.</summary>
    public static event Action<IUIComponent> VisibilityChanged;

    /// <summary>An element's CLIP changed - it began, or stopped, cutting its whole subtree to its bounds. Its own
    /// content is untouched; what changed is the shape of everything drawn beneath it.</summary>
    public static event Action<IUIComponent> ClipChanged;

    /// <summary>An element's CONTENT is stale: what it draws is not what it drew (a new size, a new shape, new text).</summary>
    public static event Action<IUIComponent> ContentInvalidated;

    /// <summary>An element's PAINT is stale: it draws exactly the same thing, in a different colour.</summary>
    public static event Action<IUIComponent> PaintInvalidated;

    /// <summary>An element MOVED: same content, new place.</summary>
    public static event Action<IUIComponent> Moved;

    /// <summary>An element that moves its whole subtree as one (a scrolling items host) moved.</summary>
    public static event Action<IUIComponent> SubtreeMoved;

    /// <summary>A state swap has begun (a theme, a DPI change) and will settle over several passes.</summary>
    public static event Action StateSwapStarted;

    public static void RaiseAttached(IUIComponent component) { if (component != null) Attached?.Invoke(component); }
    public static void RaiseDetached(IUIComponent component) { if (component != null) Detached?.Invoke(component); }
    public static void RaiseVisibilityChanged(IUIComponent component) { if (component != null) VisibilityChanged?.Invoke(component); }
    public static void RaiseClipChanged(IUIComponent component) { if (component != null) ClipChanged?.Invoke(component); }
    public static void RaiseContentInvalidated(IUIComponent component) { if (component != null) ContentInvalidated?.Invoke(component); }
    public static void RaisePaintInvalidated(IUIComponent component) { if (component != null) PaintInvalidated?.Invoke(component); }
    public static void RaiseMoved(IUIComponent component) { if (component != null) Moved?.Invoke(component); }
    public static void RaiseSubtreeMoved(IUIComponent component) { if (component != null) SubtreeMoved?.Invoke(component); }
    public static void RaiseStateSwapStarted() => StateSwapStarted?.Invoke();
}
