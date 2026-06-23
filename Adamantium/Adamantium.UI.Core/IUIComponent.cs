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
    IRootVisualComponent RootVisual { get; }
    Int32 ZIndex { get; set; }
    bool IsAttachedToVisualTree { get; }

    bool IsRootComponent { get; }

    Transform LayoutTransform { get; set; }
    
    Transform RenderTransform { get; set; }

    Matrix4x4F WorldTransform { get; }

    IReadOnlyCollection<IUIComponent> GetVisualDescendants();
        
    IReadOnlyCollection<IUIComponent> VisualChildren { get; }

    void InvalidateRender(bool invalidateChildren);

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