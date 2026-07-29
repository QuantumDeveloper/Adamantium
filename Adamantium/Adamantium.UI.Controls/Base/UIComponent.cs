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

    // Same wiring for a LayoutTransform, PLUS mark it as one: a value change on it must re-run the owner's layout (it
    // reshapes the footprint), not just the render - see Transform.UpdateTransform.
    private static void OnLayoutTransformChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not UIComponent owner) return;
        if (e.OldValue is Transform old && ReferenceEquals(old.Owner, owner)) { old.Owner = null; old.IsLayoutTransform = false; }
        if (e.NewValue is Transform t) { t.Owner = owner; t.IsLayoutTransform = true; }
    }

    public static readonly AdamantiumProperty LayoutTransformProperty =
        AdamantiumProperty.Register(nameof(LayoutTransform), typeof(Transform), typeof(UIComponent),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, OnLayoutTransformChanged));

    // Paint/hit-test order among siblings: higher ZIndex is drawn later (on top) and hit first. AffectsRender because a
    // change re-orders how the parent composites its children (the render walk re-sorts siblings by ZIndex, mirroring the
    // hit-test's ZSort). Default 0 keeps natural document order.
    public static readonly AdamantiumProperty ZIndexProperty = AdamantiumProperty.Register(nameof(ZIndex),
        typeof(Int32), typeof(UIComponent), new PropertyMetadata(0, PropertyMetadataOptions.AffectsRender, OnZIndexChanged));

    private static void OnZIndexChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        // Resolved value, not e.NewValue (a trigger exit writes UnsetValue). See OnVisibilityChanged.
        if (d is UIComponent component) component._zIndex = component.GetValue<int>(ZIndexProperty);
    }

    public static readonly AdamantiumProperty VisibilityProperty = AdamantiumProperty.Register(nameof(Visibility),
        typeof(Visibility), typeof(UIComponent),
        new PropertyMetadata(Visibility.Visible,
            PropertyMetadataOptions.BindsTwoWayByDefault |
            PropertyMetadataOptions.AffectsMeasure |
            PropertyMetadataOptions.AffectsRender,
            OnVisibilityChanged));

    // A show/hide changes the DRAWN set (a unit enters/leaves the paint order), so it is a STRUCTURAL change for the render
    // cache, which NAMES the component so the change can be spliced instead of re-derived by walking the whole tree.
    //
    // UnsetValue means the property was never assigned - but the component's EFFECTIVE visibility was still the default
    // (Visible), so a control going straight from that default to Collapsed really did leave the drawn set. Treating that as
    // "there is no old value, skip it" is why a pooled container - collapsed for the FIRST time in its life, which is exactly
    // what recycling does to it - named nobody: the render cache never learned it was gone and kept drawing its tile at the
    // old slot (the gaps and overlapping tiles while the size slider is dragged). The old full-walk rebuild hid this, because
    // it re-derived the drawn set from the tree every frame and never needed the mark to be right.
    //
    // What the guard is actually for is the constructor's SEED (UnsetValue -> Visible, fired for every control ever created),
    // and that is a no-op change once the old value is read as the default it really was.
    private static void OnVisibilityChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not UIComponent component) return;

        // Resolved value, not e.NewValue: a trigger exit clears its slot with UnsetValue, which is not a Visibility.
        var previous = component._visibility;
        var effective = component.GetValue<Visibility>(VisibilityProperty);
        component._visibility = effective;

        if (previous != effective)
            VisualTreeNotifications.RaiseVisibilityChanged(component);
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
        new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint | PropertyMetadataOptions.BindsTwoWayByDefault, OnOpacityChanged));

    // Opacity composites DOWN the visual tree (a descendant's effective opacity includes every ancestor's), so a change
    // re-bakes this element AND its whole subtree. AffectsPaint (not AffectsRender): the shapes, draw commands and units are
    // unchanged - only the alpha the slots bake with moved - so it is an O(dirty-slots) RE-BAKE, not a re-record. The
    // effective value is composed from the frozen snapshot chain at bake time (RenderCache.EffectiveOpacity), which is what
    // lets a paint-only change re-bake without re-recording.
    private static void OnOpacityChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not UIComponent component) return;
        // Field mirror, read in the hot bake/snapshot-capture path instead of GetValue (a lock + a box - see
        // hot-paths-must-not-use-property-system). Resolved value, NOT e.NewValue: a trigger-exit writes UnsetValue.
        component._opacity = component.GetValue<Double>(OpacityProperty);
        component.InvalidatePaintTree();
    }

    public static readonly AdamantiumProperty SelfOpacityProperty = AdamantiumProperty.Register(nameof(SelfOpacity),
        typeof(Double), typeof(UIComponent),
        new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint | PropertyMetadataOptions.BindsTwoWayByDefault, OnSelfOpacityChanged));

    // SelfOpacity fades ONLY this element's own draws, NOT its subtree (unlike Opacity). So a change re-bakes just this
    // element (AffectsPaint already marked it) - descendants' baked alpha never included it. Only the field mirror to keep.
    private static void OnSelfOpacityChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is UIComponent component)
            component._selfOpacity = component.GetValue<Double>(SelfOpacityProperty);
    }

    private double _opacity = 1.0;       // hot-path mirror of Opacity (see OnOpacityChanged)
    private double _selfOpacity = 1.0;   // hot-path mirror of SelfOpacity

    // Paint-invalidate this element and its whole subtree: Opacity composites down, so every descendant's baked alpha is
    // stale. Mirrors InvalidateRender(true) but on the PAINT path - no IsGeometryValid touch, so nothing re-records.
    private void InvalidatePaintTree()
    {
        InvalidatePaint();
        if (VisualChildrenCollection == null) return;   // callback can fire during the base ctor, before the collection
        foreach (var child in VisualChildrenCollection)
        {
            if (child is UIComponent uc) uc.InvalidatePaintTree();
            else child.InvalidatePaint();
        }
    }

    // Font family is INHERITED (like DataContext): set it on any element (a window, a panel) and every descendant's text
    // picks it up unless it sets its own. Default null = "inherit"; at the root a null resolves to DefaultFontFamily.
    // No layout flags here (they don't fire on inherited propagation anyway - see AdamantiumComponent.RaiseInheritedChange);
    // TextBlock OverrideMetadata's it with a callback that re-measures, which fires on both a direct set AND the cascade.
    public static readonly AdamantiumProperty FontFamilyProperty = AdamantiumProperty.Register(nameof(FontFamily),
        typeof(FontFamily), typeof(UIComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits | PropertyMetadataOptions.AffectsMeasure));

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

    // FontSize is INHERITED (like FontFamily / Foreground): declared ONCE here so the SAME property flows across
    // Control / TextBlock / ContentPresenter etc. (inheritance is by property IDENTITY - separate per-control registrations
    // never cross-inherit, which is why an unstyled templated header used to fall back to its OWN default instead of the
    // control's size). AffectsMeasure fires on BOTH a direct set AND the inherited cascade (RunSetValueSequence runs the
    // flags for every priority, including ValuePriority.Inherited), so text re-measures when an ancestor's FontSize flows
    // in. Default 14; leaf types OverrideMetadata only to change the default (and re-specify Inherits | AffectsMeasure).
    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(double), typeof(UIComponent),
        new PropertyMetadata(14.0, PropertyMetadataOptions.Inherits | PropertyMetadataOptions.AffectsMeasure));

    /// <summary>The inherited font size (DIPs) for text in this element and its descendants, unless one sets its own.</summary>
    public double FontSize
    {
        get => GetValue<double>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    // Cursor is INHERITED (like Foreground): the cursor is applied from the element the pointer ENTERS (see
    // InputUIComponent.OnMouseEnter), so a cursor set on a container must flow down to its parts - a grip/splitter whose hit
    // target is a template child would otherwise show the child's default arrow. Declared HERE (not on InputUIComponent) so
    // the property exists on EVERY node the inheritance walks - including non-input ones like Popup - otherwise propagating
    // it into their subtree throws "not registered" on them.
    public static readonly AdamantiumProperty CursorProperty = AdamantiumProperty.Register(nameof(Cursor),
        typeof(Cursor), typeof(UIComponent), new PropertyMetadata(Cursor.Default, PropertyMetadataOptions.Inherits));

    public Cursor Cursor
    {
        get => GetValue<Cursor>(CursorProperty);
        set => SetValue(CursorProperty, value);
    }

    #endregion

    #region Events
    
    public event EventHandler<VisualParentChangedEventArgs> VisualParentChanged;
    
    #endregion

    #region Properties

    //public Vector2 Location { get; internal set; }

    private Visibility _visibility = Visibility.Visible;

    /// <summary>Whether this component is drawn, and whether it takes part in layout.</summary>
    /// <remarks>
    /// The GETTER reads a plain field, not the property store. This is the single hottest property in the engine - deriving
    /// the paint order, hit-testing, and every subtree walk ask it about EVERY component they touch, several times each - and
    /// one read through the property system takes a lock, resolves the value's priority stack, and BOXES the enum. On a 4800
    /// tile grid that was tens of milliseconds per frame of pure overhead, entirely independent of what had actually changed.
    /// The field is kept current by the property's own changed callback, so bindings, styles and triggers all still drive it
    /// through SetValue exactly as before.
    /// </remarks>
    public Visibility Visibility
    {
        get => _visibility;
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
        get => _opacity;   // field mirror (hot path: composed per drawn component); the setter still goes through the store
        set => SetValue(OpacityProperty, value);
    }

    /// <summary>Opacity applied to THIS element's own rendering only - it is NOT composited onto descendants the way
    /// <see cref="Opacity"/> is. Fades a control's own chrome (its background/border/fill) while its content stays put.
    /// Multiplies with <see cref="Opacity"/> for this element's draws. 1.0 = opaque.</summary>
    public Double SelfOpacity
    {
        get => _selfOpacity;
        set => SetValue(SelfOpacityProperty, value);
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
            if (!value) VisualTreeNotifications.RaiseContentInvalidated(this);   // what it draws is stale -> it re-renders
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
                VisualTreeNotifications.RaiseContentInvalidated(this);   // a resize changes what it draws
            }
            renderSize = value;
        }
    }

    /// <summary>Only the paint changed - do NOT touch IsGeometryValid. That flag is what makes the recorder re-run OnRender
    /// and rebuild this element's draw commands, and none of that is needed for a new colour: the commands are the same, the
    /// units are the same, and the GPU data they bake from the brush is all that is stale.</summary>
    public void InvalidatePaint() => VisualTreeNotifications.RaisePaintInvalidated(this);

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

    // Emit this element's draw commands into a context WITHOUT touching IsGeometryValid (so no RenderDirty mark, no loop
    // wake) and WITHOUT the clean-frame gate. For an OFF-SCREEN snapshot of a LIVE (already-valid) element through a
    // parallel render cache: the ordinary Render() would no-op on a valid element, and forcing it via InvalidateRender
    // would mark the global RenderDirty and wake the window loop into a concurrent render (a hang). This just replays
    // OnRender read-only - the element's own render state is untouched, so the window never notices.
    public void RenderReadOnly(IDrawingContext context) => OnRender(context);

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

    // EVERY change to the visual children lands here - and every one of them changes the DRAWN set, so every one of them must
    // NAME the components that entered or left it. That is what lets the render cache splice the paint order (O(changed))
    // instead of re-deriving it by walking the whole tree (O(scene), which on a 4K fill meant re-recording ~20 000 components
    // on every frame of the fill).
    //
    // The marks live HERE, at the collection, and not at the callers, because there is no single caller: AddVisualChild does
    // it, but so do Decorator.Child (a Border putting its content in - the case that kept the fill in full-walk mode), Panel's
    // Children reconciliation, Slider's tooltip, TabStripScroller's child swap. Marking at each site is a rule that gets
    // forgotten - and a forgotten one is silent: the old full-walk rebuild re-derived the drawn set from the tree anyway, so
    // an unnamed change cost nothing and left no trace. Now it costs a whole-tree re-record, so the collection names them
    // itself and no caller can forget.
    // The visual tree's OWN plumbing, and the only place that knows a child entered or left it. Attaching/detaching the child
    // and telling the renderer that the drawn set changed are two halves of the same fact, so they live together, here - once.
    // No control ever writes either of them: a control adds a child, and everything that follows is the tree's business.
    private void VisualChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        Detach(e.OldItems);
        Attach(e.NewItems);
    }

    private void Attach(System.Collections.IList visuals)
    {
        if (visuals == null) return;
        foreach (UIComponent visual in visuals)
        {
            visual.SetVisualParent(this);
            VisualTreeNotifications.RaiseAttached(visual);
        }
    }

    /// <summary>Give up a child another parent has taken. The base drops it from the visual children; a container that
    /// lays out from a collection of its OWN - a panel's Children, a decorator's Child - overrides to drop it there too,
    /// because that is what its measure and arrange actually walk.
    /// <para>Deliberately NOT routed through <see cref="IContainer"/>, tempting as that is: that interface is about
    /// AUTHORED markup children (it is what the designer edits incrementally), not about what a control lays out. Asking
    /// it here reached an ItemsControl's Items and a ContentControl's Content - the model, not the visual tree - so a
    /// control merely moving to another parent would have deleted the item or the content it was showing.</para></summary>
    protected internal virtual void DisownVisualChild(IUIComponent child)
    {
        VisualChildrenCollection.Remove(child);
    }

    private static void Detach(System.Collections.IList visuals)
    {
        if (visuals == null) return;
        foreach (UIComponent visual in visuals)
        {
            visual.SetVisualParent(null);
            VisualTreeNotifications.RaiseDetached(visual);
        }
    }

    private Rect _bounds;

    // Position + size of this element in its parent's space. A MOVE (position change, same size) does NOT touch
    // IsGeometryValid, but it DOES change where the element draws (its world transform), so it must bump the render
    // revision too - otherwise the clean-frame fast path would skip a frame in which a tile just moved.
    /// <summary>See <see cref="IUIComponent.IsRenderMotionNode"/>. Settable by the element that drives subtree-as-a-unit
    /// movement (a virtualizing items host under transform-only scroll).</summary>
    public bool IsRenderMotionNode { get; set; }

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
            if (IsRenderMotionNode) VisualTreeNotifications.RaiseSubtreeMoved(this);
            else VisualTreeNotifications.RaiseMoved(this);   // a move: same content, new place
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

    /// <summary>See <see cref="IUIComponent.RenderParent"/>. Virtual so an Adorner - which lives outside the visual tree -
    /// can name the element whose space it draws in.</summary>
    public virtual IUIComponent RenderParent => VisualParent;

    public IRootVisualComponent RootVisual { get; private set; }

    /// <summary>
    /// <c>true</c> when this component is its own visual root (e.g. a window). Allocation-free alternative to an
    /// <c>is IRootVisualComponent</c> check.
    /// </summary>
    public bool IsRootComponent => ReferenceEquals(RootVisual, this);

    private int _zIndex;

    /// <summary>Paint/hit-test order among siblings (higher = drawn later, hit first).</summary>
    /// <remarks>
    /// The GETTER reads a plain field, not the property store. Deriving the paint order asks EVERY child of EVERY component
    /// for its ZIndex, on every walk - and one read through the property system takes a lock, resolves the value's priority
    /// stack, and BOXES the int. At ~4800 children that was ~16 ms per pass, an overhead that did not depend on how much had
    /// actually changed. The field is kept current by the property's own changed callback, so bindings, styles and triggers
    /// all still work through SetValue exactly as before.
    /// </remarks>
    public Int32 ZIndex
    {
        get => _zIndex;
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
    
    /// <summary>The point the <see cref="RenderTransform"/> is applied AROUND, as a fraction of this element's own size:
    /// (0,0) = top-left (the default), (0.5,0.5) = centre, (1,1) = bottom-right. Rotation and scale both honour it.</summary>
    /// <remarks>
    /// This belongs to the ELEMENT, not to the Transform, because it is the only one of the two that knows the size. A
    /// Transform can only name an ABSOLUTE centre (RotationCenterX/Y, in pixels), which a TEMPLATE cannot know: it is
    /// written once and then used at whatever size the control is given, so a hardcoded centre is right at exactly one
    /// size and visibly wrong at every other (a spinner's arc orbiting a point off its own centre). Stating the origin as a
    /// FRACTION makes it size-independent. Same seam as WPF's RenderTransformOrigin - which is likewise on the element,
    /// alongside the transform's own absolute centre.
    ///
    /// It also fixes SCALE, which had no centre at all (the transform scales about zero): a scale animation grew its
    /// element out of its top-left corner instead of its middle.
    /// </remarks>
    public static readonly AdamantiumProperty RenderTransformOriginProperty = AdamantiumProperty.Register(
        nameof(RenderTransformOrigin), typeof(Vector2), typeof(UIComponent),
        new PropertyMetadata(default(Vector2), RenderTransformOriginChanged));

    public Vector2 RenderTransformOrigin
    {
        get => GetValue<Vector2>(RenderTransformOriginProperty);
        set => SetValue(RenderTransformOriginProperty, value);
    }

    // Moving the origin MOVES the element (its composed transform changes) without changing a thing it draws - the same
    // fact a Transform reports when one of its own values changes, so it is announced the same way.
    private static void RenderTransformOriginChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not UIComponent component) return;
        if (component.IsRenderMotionNode) VisualTreeNotifications.RaiseSubtreeMoved(component);
        else VisualTreeNotifications.RaiseMoved(component);
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
                var matrix = (Matrix4x4F)renderTransform.Matrix;

                // Apply the transform AROUND the render-transform origin: move that point to the local origin, transform,
                // move it back. Resolved from the element's CURRENT size, so the same template stays centred at any size.
                var origin = RenderTransformOrigin;
                if (origin.X != 0 || origin.Y != 0)
                {
                    var ox = (float)(origin.X * RenderSize.Width);
                    var oy = (float)(origin.Y * RenderSize.Height);
                    matrix = Matrix4x4F.Translation(-ox, -oy, 0) * matrix * Matrix4x4F.Translation(ox, oy, 0);
                }

                localTransform = matrix * localTransform;
            }

            // LayoutTransform is applied INNERMOST - to the content in its own (inner) space, before RenderTransform and
            // the layout offset - so the rendered content scales/rotates to fill the footprint that layout already
            // reserved for its bounding box (ArrangeCore set RenderSize to the inner size, Bounds to the outer one).
            var layoutTransform = LayoutTransform;
            if (layoutTransform != null)
                localTransform = (Matrix4x4F)layoutTransform.Matrix * localTransform;

            return localTransform;
        }
    }

    public virtual Matrix4x4F WorldTransform
    {
        // Compose up the RENDER parent chain (= the visual tree for everything but an adorner, which draws in its adorned
        // element's space). Computed live each call (not cached): RenderTransform animates per-frame, and an animated
        // ancestor must carry its whole subtree - a persistent dirty-flag cache would freeze descendants mid-flight. Hot
        // callers that read it repeatedly within ONE frame (the render pass) memoize it frame-scoped instead (RenderCache),
        // composing over the SAME RenderParent chain - so the frozen path cannot disagree with this one.
        get => RenderParent != null ? LocalTransform * RenderParent.WorldTransform : LocalTransform;
    }


    public IReadOnlyCollection<IUIComponent> GetVisualDescendants()
    {
        return VisualChildren;
    }

    public IReadOnlyCollection<IUIComponent> VisualChildren => VisualChildrenCollection.AsReadOnly();

    protected TrackingCollection<IUIComponent> VisualChildrenCollection { get; private set; }

    // Add/Remove need no mark of their own: the collection names every component that enters or leaves it
    // (VisualChildrenCollectionChanged), so no caller - here or anywhere else - can forget to.
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

        // Moving to a new parent while another still holds it: take it off the old one, exactly as SetParent does for
        // the logical tree. A component belongs to one parent, but nothing here used to reach the previous parent's
        // child collection - so it went on listing the component, and went on measuring and arranging it. Two parents
        // placing one control means its position is decided by whichever the layout pass reaches last, which is an
        // accident of tree order. Measured through docking: a tab moved into another group kept the bounds of its old
        // strip and so sat on top of a neighbour.
        if (old != null && parent != null) (old as UIComponent)?.DisownVisualChild(this);

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