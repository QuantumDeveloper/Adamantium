using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.RoutedEvents;
using Transform = Adamantium.UI.Core.Media.Transform;

namespace Adamantium.UI.Core;

public interface IUIComponent : IFundamentalUIComponent
{
    event EventHandler<VisualParentChangedEventArgs> VisualParentChanged;
    
    Guid RenderId { get; }
    Boolean ClipToBounds { get; set; }
    Double Opacity { get; set; }
    bool IsEnabled { get; set; }
    Boolean AllowDrop { get; set; }
    Boolean IsHitTestVisible { get; set; }
    bool IsGeometryValid { get; }
    Size RenderSize { get; set; }
    //Vector2 Location { get; }
    Visibility Visibility { get; set; }
    Rect Bounds { get; set; }
    Rect ClipRectangle { get; }
    Vector2 ClipPosition { get; set; }
    IUIComponent VisualParent { get; }

    /// <summary>The component in whose coordinate space this one is DRAWN - normally the visual parent. An adorner is not
    /// in the visual tree at all (its VisualParent is null) yet draws in its adorned element's space, so it reports that
    /// element here. Both the live <see cref="WorldTransform"/> and the renderer's frozen composition go through this, so
    /// there is one answer to "whose space am I in", not two that can drift apart.</summary>
    IUIComponent RenderParent { get; }

    IRootVisualComponent RootVisual { get; }
    Int32 ZIndex { get; set; }
    bool IsAttachedToVisualTree { get; }

    /// <summary>A render MOTION NODE: this element's subtree translates as a unit (a transform-only-scrolled panel).
    /// The render cache bakes its descendants' batched instances in THIS node's space and gives them its transform-table
    /// slot, so moving the node costs one matrix write instead of re-baking the subtree (the O(1)-scroll path). Set by
    /// the element that drives such movement (a virtualizing items host) - or by the COMPOSITOR, which promotes any element
    /// whose transform it takes over: one matrix write is precisely what the render thread can do on its own, and a
    /// world-baked element could not be moved without a re-record, which is the loop thread's job.</summary>
    bool IsRenderMotionNode { get; set; }

    bool IsRootComponent { get; }

    Transform LayoutTransform { get; set; }
    
    Transform RenderTransform { get; set; }

    /// <summary>The point <see cref="RenderTransform"/> turns/scales about, as a FRACTION of the element's own size (0.5,0.5
    /// = its centre). Relative, so one template stays centred at any size. Read by the compositor when it composes the
    /// element's matrix itself.</summary>
    Vector2 RenderTransformOrigin { get; set; }

    Matrix4x4F WorldTransform { get; }

    /// <summary>This element's transform in its parent's space (the parent-relative part of <see cref="WorldTransform"/>),
    /// so a frame-scoped consumer can compose world transforms top-down without re-walking to the root per node.</summary>
    Matrix4x4F LocalTransform { get; }

    IReadOnlyCollection<IUIComponent> GetVisualDescendants();
        
    IReadOnlyCollection<IUIComponent> VisualChildren { get; }

    void InvalidateRender(bool invalidateChildren);

    /// <summary>Only this element's PAINT changed - same shape, same draw commands, a new colour/brush/opacity. It is NOT
    /// re-rendered: the renderer re-bakes the GPU data of the units it already holds (see
    /// <see cref="PropertyMetadataOptions.AffectsPaint"/>).</summary>
    void InvalidatePaint();

    /// <summary>
    /// Narrow-phase hit test: is <paramref name="localPoint"/> (in this element's local coordinates) actually on the
    /// element's geometry? The hit-test walk uses <see cref="ClipRectangle"/> as the cheap broad-phase (and to descend
    /// into children); this is the tight test for whether the element ITSELF is hit. The default is the bounding box
    /// (always true within it); shapes override it for their real geometry (a Line by distance to its segment, an
    /// Ellipse by the ellipse equation, a Path by point-in-geometry) so clicks off the shape don't select it.
    /// </summary>
    bool HitTestCore(Vector2 localPoint);

    void Render(IDrawingContext context);
    
    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    public event EventHandler<VisualTreeAttachmentEventArgs> AttachedToVisualTreeEvent;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    public event EventHandler<VisualTreeAttachmentEventArgs> DetachedFromVisualTreeEvent;
}