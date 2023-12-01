using System;
using System.Collections.Generic;
using Adamantium.UI.Media;
using Transform = Adamantium.UI.Media.Transform;

namespace Adamantium.UI;

public interface IUIComponent : IFundamentalUIComponent
{
    event EventHandler<VisualParentChangedEventArgs> VisualParentChanged;
        
    Boolean ClipToBounds { get; set; }
    Double Opacity { get; set; }
    bool IsEnabled { get; set; }
    Boolean AllowDrop { get; set; }
    Boolean IsHitTestVisible { get; set; }
    bool IsGeometryValid { get; }
    Size RenderSize { get; set; }
    Vector2 Location { get; set; }
    Visibility Visibility { get; set; }
    Rect Bounds { get; set; }
    Rect ClipRectangle { get; }
    Vector2 ClipPosition { get; set; }
    IUIComponent VisualParent { get; }
    Int32 ZIndex { get; set; }
    bool IsAttachedToVisualTree { get; }
    
    Transform LayoutTransform { get; set; }
    
    Transform RenderTransform { get; set; }

    IReadOnlyCollection<IUIComponent> GetVisualDescendants();
        
    IReadOnlyCollection<IUIComponent> VisualChildren { get; }

    void InvalidateRender(bool invalidateChildren);

    void Render(DrawingContext context);
}