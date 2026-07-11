using System.Collections.Specialized;
using Adamantium.Core.Collections;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Base;

public class UIComponent : FundamentalUIComponent, IUIComponent
{
    private Size renderSize;
    
    protected bool sizeChanged;
    protected Size previousRenderSize;

    #region Adamantium properties
    
    public static readonly AdamantiumProperty RenderTransformProperty =
        // AffectsRender so ASSIGNING a new RenderTransform re-renders + bumps the render revision (a non-animated
        // transform change must not be skipped by the clean-frame fast path). An ANIMATED transform mutates the same
        // Transform object's inner values without re-assigning this property, so it is covered separately by the render
        // cache's "no active animation" guard instead.
        AdamantiumProperty.Register(nameof(RenderTransform), typeof(Transform), typeof(UIComponent),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnRenderTransformChanged));

    // Tell the transform WHO it moves: a transform tick on a MOTION-NODE owner then marks only that node (one table-slot
    // matrix rewrite + replay - the O(1) tilt/flip path) instead of the global re-bake-everything transform flag.
    private static void OnRenderTransformChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not UIComponent owner) return;
        if (e.OldValue is Transform old && ReferenceEquals(old.Owner, owner)) old.Owner = null;
        if (e.NewValue is Transform t) t.Owner = owner;
    }
    
    public static readonly AdamantiumProperty LayoutTransformProperty =
        AdamantiumProperty.Register(nameof(LayoutTransform), typeof(Transform), typeof(UIComponent));

    // Paint/hit-test order among siblings: higher ZIndex is drawn later (on top) and hit first. AffectsRender because a
    // change re-orders how the parent composites its children (the render walk re-sorts siblings by ZIndex, mirroring the
    // hit-test's ZSort). Default 0 keeps natural document order.
    public static readonly AdamantiumProperty ZIndexProperty = AdamantiumProperty.Register(nameof(ZIndex),
        typeof(Int32), typeof(UIComponent), new PropertyMetadata(0, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty VisibilityProperty = AdamantiumProperty.Register(nameof(Visibility),
        typeof(Visibility), typeof(UIComponent),
        new PropertyMetadata(Visibility.Visible,
            PropertyMetadataOptions.BindsTwoWayByDefault |
            PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsRender,
            OnVisibilityChanged));

    // A show/hide changes the DRAWN set (a unit enters/leaves the paint-order list), so it is a STRUCTURAL change for
    // the render cache - a partial in-place re-render can't add/remove a unit. Force a full walk. Skip the constructor's
    // default-value SEED (UnsetValue -> Visible, fired for every control ever created) and any no-op set.
    private static void OnVisibilityChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.OldValue != AdamantiumProperty.UnsetValue && !Equals(e.OldValue, e.NewValue))
            RenderDirty.MarkStructural();
    }
      
    public static readonly AdamantiumProperty IsHitTestVisibleProperty =
        AdamantiumProperty.Register(nameof(IsHitTestVisible),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(true));

    // Opt-in, like WPF: a component clips its descendants to its bounds (a Vulkan scissor, honoured by the renderer)
    // only when this is set. Default false so content that intentionally overflows its bounds - drop shadows, the
    // analytic-AA fill fringe, glyph effect padding, render-transformed children - is never clipped unless asked. A
    // ScrollViewer's content host, and a control mid content-transition, set it to true.
    public static readonly AdamantiumProperty ClipToBoundsProperty = AdamantiumProperty.Register(nameof(ClipToBounds),
        typeof(Boolean), typeof(UIComponent),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty IsEnabledProperty = AdamantiumProperty.Register(nameof(IsEnabled),
        typeof(Boolean), typeof(UIComponent),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
        typeof(Double), typeof(UIComponent),
        new PropertyMetadata(1.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnOpacityChanged));

    // Opacity composites DOWN the visual tree (a descendant's effective opacity includes every ancestor's - see
    // DrawingContext.GetEffectiveOpacity), so a change must re-render this element AND its whole subtree: the children's
    // render units bake the effective opacity at record time and would otherwise keep the stale value (the plain
    // AffectsRender flag only invalidates self, which is why fading a container left its grandchildren - e.g. a
    // scrollbar's thumb - frozen at their old opacity).
    private static void OnOpacityChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        (d as UIComponent)?.InvalidateRender(true);
    }

    // Font family is INHERITED (like DataContext): set it on any element (a window, a panel) and every descendant's text
    // picks it up unless it sets its own. Default null = "inherit"; at the root a null resolves to DefaultFontFamily.
    // No layout flags here (they don't fire on inherited propagation anyway - see AdamantiumComponent.RaiseInheritedChange);
    // TextBlock OverrideMetadata's it with a callback that re-measures, which fires on both a direct set AND the cascade.
    public static readonly AdamantiumProperty FontFamilyProperty = AdamantiumProperty.Register(nameof(FontFamily),
        typeof(FontFamily), typeof(UIComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits));

    // Foreground is INHERITED (like FontFamily / DataContext): set it on an ancestor and descendant text picks it up
    // unless it sets its own (an explicit local/style value stops the cascade; a mere default does not, so overriding the
    // default per type is safe). Declared once here so the SAME property flows across Control / TextBlock / ContentPresenter
    // (inheritance is by property IDENTITY - three separate registrations would never cross-inherit). Leaf types
    // OverrideMetadata to keep their own default brush + render/measure callbacks while preserving Inherits.
    public static readonly AdamantiumProperty ForegroundProperty = AdamantiumProperty.Register(nameof(Foreground),
        typeof(Brush), typeof(UIComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits));

    public Brush Foreground
    {
        get => GetValue<Brush>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

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

    /// <summary>The inherited font family for text in this element and its descendants. Unset (null) means "inherit from
    /// the parent"; at the root a null resolves to <see cref="DefaultFontFamily"/>.</summary>
    public FontFamily FontFamily
    {
        get => GetValue<FontFamily>(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    #endregion

    private static FontFamily _defaultFontFamilyOverride;

    /// <summary>The fallback font used when no <see cref="FontFamily"/> is set anywhere up the tree. By default it is the
    /// CURRENT THEME's <see cref="Theme.FontFamily"/> - the theme owns the look; assign this to override globally
    /// regardless of theme. The per-platform default itself lives in the theme (<see cref="Theme.SystemDefaultFontFamily"/>).</summary>
    public static FontFamily DefaultFontFamily
    {
        get => _defaultFontFamilyOverride
            ?? UIAppContext.Current?.ThemeManager?.CurrentTheme?.FontFamily
            ?? Theme.SystemDefaultFontFamily;   // no theme yet (e.g. headless tests)
        set => _defaultFontFamilyOverride = value;
    }

    public UIComponent()
    {
        RenderId = Guid.NewGuid();
        VisualChildrenCollection = new TrackingCollection<IUIComponent>();
        VisualChildrenCollection.CollectionChanged += VisualChildrenCollectionChanged;
        // A visual root (e.g. a window) has no parent, so SetVisualParent never attaches it. Seed RootVisual to
        // itself here so the root reports IsAttachedToVisualTree = true.
        if (this is IRootVisualComponent root) RootVisual = root;
    }

    private bool _isGeometryValid;

    public bool IsGeometryValid
    {
        get => _isGeometryValid;
        // Going INVALID = this element's rendered geometry is now stale (InvalidateRender / measure / arrange-resize /
        // opacity / visibility all route here). That is the single choke point the clean-frame fast path keys off, so
        // bump the global render revision. Going valid again (after Render re-records) is not a scene change - no bump.
        protected set
        {
            _isGeometryValid = value;
            if (!value) RenderDirty.MarkGeometry(this);   // stale geometry -> this component re-renders (partial rebuild)
        }
    }

    public Size RenderSize
    {
        get => Visibility == Visibility.Collapsed ? Size.Zero : renderSize;
        set
        {
            if (renderSize != value)
            {
                previousRenderSize = renderSize;
                sizeChanged = true;
                RenderDirty.MarkGeometry(this);   // a resize changes the recorded geometry -> re-render
            }
            renderSize = value;
        }
    }

    public void InvalidateRender(bool invalidateChildren)
    {
        IsGeometryValid = false;
        // The child collection is null until the UIComponent ctor runs; a property-changed callback (e.g. Opacity's)
        // can fire earlier, while the base ctor applies defaults - guard so an early invalidate is a harmless no-op.
        if (!invalidateChildren || VisualChildrenCollection == null) return;

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

    private Rect _bounds;

    // Position + size of this element in its parent's space. A MOVE (position change, same size) does NOT touch
    // IsGeometryValid, but it DOES change where the element draws (its world transform), so it must bump the render
    // revision too - otherwise the clean-frame fast path would skip a frame in which a tile just moved.
    /// <summary>See <see cref="IUIComponent.IsRenderMotionNode"/>. Settable by the element that drives subtree-as-a-unit
    /// movement (a virtualizing items host under transform-only scroll).</summary>
    public bool IsRenderMotionNode { get; protected internal set; }

    public Rect Bounds
    {
        get => _bounds;
        set
        {
            if (_bounds == value) return;
            _bounds = value;
            // A MOTION NODE moving is the granular case: its subtree's instances reference its transform-table slot, so
            // the render rewrites ONE matrix and replays - no global transform invalidation, no O(N) re-bake (the
            // transform-only scroll). Everything else keeps the conservative global mark.
            if (IsRenderMotionNode) RenderDirty.MarkNodeTransform(this);
            else RenderDirty.MarkTransform();   // a move: same recorded geometry, only the world transform changes -> re-bake
        }
    }

    public Rect ClipRectangle { get; internal set; }

    /// <summary>Narrow-phase hit test (see <see cref="IUIComponent.HitTestCore"/>). Default: anywhere inside the
    /// element's box - the hit-test walk already broad-phase-checked the bounds. Shapes override this with their real
    /// geometry so a click off the shape (but inside its bounding box) doesn't select it.</summary>
    public virtual bool HitTestCore(Vector2 localPoint) => true;

    /// <summary>Scroll every enclosing <see cref="ScrollViewer"/> the minimum needed to make this element visible (WPF's
    /// <c>BringIntoView</c>). Walks the visual tree from here outward, so nested viewers all scroll (innermost first).
    /// NOTE: this brings the CURRENTLY REALIZED element into view; under UI virtualization an item that has not been
    /// realized yet has no visual to scroll to - that requires the panel to first materialize it (a separate concern).</summary>
    public void BringIntoView()
    {
        for (IUIComponent node = VisualParent; node != null; node = node.VisualParent)
            (node as ScrollViewer)?.BringDescendantIntoView(this);
    }

    public Vector2 ClipPosition { get; set; }

    public IUIComponent VisualParent { get; private set; }
    
    public IRootVisualComponent RootVisual { get; private set; }

    /// <summary>
    /// <c>true</c> when this component is its own visual root (e.g. a window). Allocation-free alternative to an
    /// <c>is IRootVisualComponent</c> check.
    /// </summary>
    public bool IsRootComponent => ReferenceEquals(RootVisual, this);

    public Int32 ZIndex
    {
        get => GetValue<Int32>(ZIndexProperty);
        set => SetValue(ZIndexProperty, value);
    }

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
    
    /// <summary>This element's transform in its PARENT's coordinate space: the render transform (local space, may be
    /// animating) followed by the layout offset that positions it inside its parent. The parent-relative part of
    /// <see cref="WorldTransform"/>, exposed so a frame-scoped consumer (the render pass) can compose world transforms
    /// top-down without re-walking to the root per node.</summary>
    public Matrix4x4F LocalTransform
    {
        get
        {
            var localTransform = Matrix4x4F.Translation((float)Bounds.Location.X, (float)Bounds.Location.Y, 0);
            var renderTransform = RenderTransform;
            if (renderTransform != null)
            {
                localTransform = (Matrix4x4F)renderTransform.Matrix * localTransform;
            }
            return localTransform;
        }
    }

    // Virtual so an Adorner can return its adorned element's transform (it draws in that element's coordinate space).
    public virtual Matrix4x4F WorldTransform
    {
        // Compose up the visual tree. Computed live each call (not cached): RenderTransform animates per-frame, and an
        // animated ancestor must carry its whole subtree - a persistent dirty-flag cache would freeze descendants
        // mid-flight. Hot callers that read it repeatedly within ONE frame (the render pass) memoize it frame-scoped
        // instead (RenderCache), where the transforms are already stable (layout + animation applied before render).
        get => VisualParent != null ? LocalTransform * VisualParent.WorldTransform : LocalTransform;
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
        RenderDirty.MarkStructural();   // new content -> paint-order list must be rebuilt
    }
    
    protected void RemoveVisualChild(IUIComponent child)
    {
        VisualChildrenCollection.Remove(child);
        RenderDirty.MarkStructural();   // removed content -> paint-order list must be rebuilt
    }

    protected void RemoveVisualChildren()
    {
        VisualChildrenCollection.Clear();
        RenderDirty.MarkStructural();
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

        // While detached, a control's cached render units are freed (RenderCache.ReconcileDetachedControls). On re-attach
        // it is still geometry-valid, so its Render() would record nothing and it would draw blank (e.g. a TabItem body
        // shown, hidden by switching tabs, then shown again). Invalidate so the next render pass rebuilds its units; the
        // recursion below carries this to the whole re-attached subtree.
        InvalidateRender(false);

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