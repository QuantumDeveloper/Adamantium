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
    
    Transform LayoutTransform { get; set; }
    
    Transform RenderTransform { get; set; }

    Matrix4x4F WorldTransform { get; }

    IReadOnlyCollection<IUIComponent> GetVisualDescendants();
        
    IReadOnlyCollection<IUIComponent> VisualChildren { get; }

    void InvalidateRender(bool invalidateChildren);

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