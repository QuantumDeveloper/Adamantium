using System.Collections.Specialized;
using Adamantium.Core.Collections;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Base;

public class UIComponent : FundamentalUIComponent, IUIComponent
{
    private Size renderSize;
    
    protected bool sizeChanged;
    protected Size previousRenderSize;

    #region Adamantium properties
    
    public static readonly AdamantiumProperty RenderTransformProperty =
        AdamantiumProperty.Register(nameof(RenderTransform), typeof(Transform), typeof(UIComponent));
    
    public static readonly AdamantiumProperty LayoutTransformProperty =
        AdamantiumProperty.Register(nameof(LayoutTransform), typeof(Transform), typeof(UIComponent));

    public static readonly AdamantiumProperty VisibilityProperty = AdamantiumProperty.Register(nameof(Visibility),
        typeof(Visibility), typeof(UIComponent),
        new PropertyMetadata(Visibility.Visible,
            PropertyMetadataOptions.BindsTwoWayByDefault | 
            PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsRender));
      
    public static readonly AdamantiumProperty IsHitTestVisibleProperty =
        AdamantiumProperty.Register(nameof(IsHitTestVisible),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ClipToBoundsProperty = AdamantiumProperty.Register(nameof(ClipToBounds),
        typeof(Boolean), typeof(UIComponent),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty IsEnabledProperty = AdamantiumProperty.Register(nameof(IsEnabled),
        typeof(Boolean), typeof(UIComponent),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
        typeof(Double), typeof(UIComponent),
        new PropertyMetadata(1.0, PropertyMetadataOptions.BindsTwoWayByDefault));

    #endregion

    #region Events
    
    public event EventHandler<VisualParentChangedEventArgs> VisualParentChanged;
    
    #endregion

    #region Properties

    //public Vector2 Location { get; internal set; }

    public Visibility Visibility
    {
        get => GetValue<Visibility>(VisibilityProperty);
        set => SetValue(VisibilityProperty, value);
    }

    public Guid RenderId { get; }

    public Boolean ClipToBounds
    {
        get => GetValue<Boolean>(ClipToBoundsProperty);
        set => SetValue(ClipToBoundsProperty, value);
    }

    public Double Opacity
    {
        get => GetValue<Double>(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    public bool IsEnabled
    {
        get => GetValue<Boolean>(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    public Boolean IsHitTestVisible
    {
        get => GetValue<Boolean>(IsHitTestVisibleProperty);
        set => SetValue(IsHitTestVisibleProperty, value);
    }

    #endregion

    public UIComponent()
    {
        RenderId = Guid.NewGuid();
        VisualChildrenCollection = new TrackingCollection<IUIComponent>();
        VisualChildrenCollection.CollectionChanged += VisualChildrenCollectionChanged;
        // A visual root (e.g. a window) has no parent, so SetVisualParent never attaches it. Seed RootVisual to
        // itself here so the root reports IsAttachedToVisualTree = true.
        if (this is IRootVisualComponent root) RootVisual = root;
    }

    public bool IsGeometryValid { get; protected set; }

    public Size RenderSize
    {
        get => Visibility == Visibility.Collapsed ? Size.Zero : renderSize;
        set
        {
            if (renderSize != value)
            {
                previousRenderSize = renderSize;
                sizeChanged = true;
            }
            renderSize = value;
        }
    }

    public void InvalidateRender(bool invalidateChildren)
    {
        IsGeometryValid = false;
        if (!invalidateChildren) return;
        
        foreach (var uiComponent in VisualChildrenCollection)
        {
            uiComponent.InvalidateRender(true);
        }
    }

    /// <summary>
    /// Tests whether a control's size can be changed by a layout pass.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <returns>True if the control's size can change; otherwise false.</returns>
    private static bool IsResizable(MeasurableUIComponent control)
    {
        return Double.IsNaN(control.Width) || Double.IsNaN(control.Height);
    }

    public void Render(IDrawingContext context)
    {
        if (IsGeometryValid) return;
        
        OnRender(context);
        IsGeometryValid = true;
        OnRenderCompleted();
    }

    public event EventHandler<VisualTreeAttachmentEventArgs> AttachedToVisualTreeEvent;
    public event EventHandler<VisualTreeAttachmentEventArgs> DetachedFromVisualTreeEvent;

    /// <summary>
    /// Tests whether any of a <see cref="Rect"/>'s properties include negative values, a NaN or Infinity.
    /// </summary>
    /// <param name="rect">The rect.</param>
    /// <returns>True if the rect is invalid; otherwise false.</returns>
    protected static bool IsInvalidRect(Rect rect)
    {
        return rect.Width < 0 || rect.Height < 0 ||
               Double.IsInfinity(rect.X) || Double.IsInfinity(rect.Y) ||
               Double.IsInfinity(rect.Width) || Double.IsInfinity(rect.Height) ||
               Double.IsNaN(rect.X) || Double.IsNaN(rect.Y) ||
               Double.IsNaN(rect.Width) || Double.IsNaN(rect.Height);
    }

    /// <summary>
    /// Tests whether any of a <see cref="Size"/>'s properties include negative values, a NaN or Infinity.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>True if the size is invalid; otherwise false.</returns>
    protected static bool IsInvalidSize(Size size)
    {
        return size.Width < 0 || size.Height < 0 ||
               Double.IsInfinity(size.Width) || Double.IsInfinity(size.Height) ||
               Double.IsNaN(size.Width) || Double.IsNaN(size.Height);
    }

    /// <summary>
    /// Ensures neither component of a <see cref="Size"/> is negative.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>The non-negative size.</returns>
    protected static Size NonNegative(Size size)
    {
        return new Size(Math.Max(size.Width, 0), Math.Max(size.Height, 0));
    }

    private void VisualChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (UIComponent visual in e.NewItems)
                {
                    visual.SetVisualParent(this);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (UIComponent visual in e.OldItems)
                {
                    visual.SetVisualParent(null);
                }
                break;
        }
    }

    public Rect Bounds { get; set; }

    public Rect ClipRectangle { get; internal set; }

    /// <summary>Narrow-phase hit test (see <see cref="IUIComponent.HitTestCore"/>). Default: anywhere inside the
    /// element's box - the hit-test walk already broad-phase-checked the bounds. Shapes override this with their real
    /// geometry so a click off the shape (but inside its bounding box) doesn't select it.</summary>
    public virtual bool HitTestCore(Vector2 localPoint) => true;

    public Vector2 ClipPosition { get; set; }

    public IUIComponent VisualParent { get; private set; }
    
    public IRootVisualComponent RootVisual { get; private set; }

    /// <summary>
    /// <c>true</c> when this component is its own visual root (e.g. a window). Allocation-free alternative to an
    /// <c>is IRootVisualComponent</c> check.
    /// </summary>
    public bool IsRootComponent => ReferenceEquals(RootVisual, this);

    public Int32 ZIndex { get; set; }

    public Transform RenderTransform
    {
        get => GetValue<Transform>(RenderTransformProperty);
        set => SetValue(RenderTransformProperty, value);
    }
    
    public Transform LayoutTransform
    {
        get => GetValue<Transform>(LayoutTransformProperty);
        set => SetValue(LayoutTransformProperty, value);
    }
    
    public Matrix4x4F WorldTransform
    {
        get
        {
            // Local transform: the render transform (applied in the element's local space, may be animating) followed
            // by the layout offset that positions the element inside its parent.
            var localTransform = Matrix4x4F.Translation((float)Bounds.Location.X, (float)Bounds.Location.Y, 0);

            var renderTransform = RenderTransform;
            if (renderTransform != null)
            {
                localTransform = (Matrix4x4F)renderTransform.Matrix * localTransform;
            }

            // Compose up the visual tree. Computed live each call (not cached): RenderTransform animates per-frame, and
            // an animated ancestor must carry its whole subtree - a dirty-flag cache would freeze descendants mid-flight.
            return VisualParent != null ? localTransform * VisualParent.WorldTransform : localTransform;
        }
    }


    public IReadOnlyCollection<IUIComponent> GetVisualDescendants()
    {
        return VisualChildren;
    }

    public IReadOnlyCollection<IUIComponent> VisualChildren => VisualChildrenCollection.AsReadOnly();

    protected TrackingCollection<IUIComponent> VisualChildrenCollection { get; private set; }

    protected void AddVisualChild(IUIComponent child)
    {
        VisualChildrenCollection.Add(child);
    }
    
    protected void RemoveVisualChild(IUIComponent child)
    {
        VisualChildrenCollection.Remove(child);
    }

    protected void RemoveVisualChildren()
    {
        VisualChildrenCollection.Clear();
    }

    /// <summary>
    /// <c>true</c> while this component is connected to a visual root. Backed by <see cref="RootVisual"/> (set on
    /// attach, cleared on detach); a root component is seeded as its own <see cref="RootVisual"/>.
    /// </summary>
    public bool IsAttachedToVisualTree => RootVisual != null;

    protected void SetVisualParent(IUIComponent parent)
    {
        if (VisualParent == parent)
        {
            return;
        }

        var old = VisualParent;
        VisualParent = parent;

        if (IsAttachedToVisualTree)
        {
            var e = new VisualTreeAttachmentEventArgs(RootVisual, this);
            DetachedFromVisualTree(e);
        }

        if (VisualParent is IRootVisualComponent || VisualParent?.IsAttachedToVisualTree == true)
        {
            var root =  this.GetVisualAncestors().OfType<IRootVisualComponent>().FirstOrDefault();
            var e = new VisualTreeAttachmentEventArgs(root, this);
            AttachedToVisualTree(e);
        }

        OnVisualParentChanged(old, parent);
    }

    private void AttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        RootVisual = e.Root;
        
        OnAttachedToVisualTree(e);
        AttachedToVisualTreeEvent?.Invoke(this, e);

        // TODO: check if we need to call AttachedToVisualTree in chain
        if (VisualChildren.Count > 0)
        {
            foreach (var uiComponent in VisualChildren)
            {
                var visual = (UIComponent)uiComponent;
                visual.AttachedToVisualTree(e);
            }
        }
    }

    private void DetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Clear the root link so IsAttachedToVisualTree (=> RootVisual != null) flips to false for this subtree.
        RootVisual = null;

        OnDetachedFromVisualTree(e);
        DetachedFromVisualTreeEvent?.Invoke(this, e);

        if (VisualChildren.Count > 0)
        {
            foreach (UIComponent visual in VisualChildren)
            {
                visual.DetachedFromVisualTree(e);
            }
        }
    }

    protected virtual void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
         
    }

    protected virtual void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
         
    }

    protected void OnVisualParentChanged(IUIComponent oldParent, IUIComponent newParent)
    {
        VisualParentChanged?.Invoke(this, new VisualParentChangedEventArgs(oldParent, newParent));
    }

    protected virtual void OnRender(IDrawingContext context)
    {
    }

    protected virtual void OnRenderCompleted()
    {
    }
}