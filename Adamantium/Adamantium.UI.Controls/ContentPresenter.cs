using System.Collections.Generic;
using Adamantium.Graphics.Fonts;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

public class ContentPresenter : InputUIComponent
{
    // A content presenter is a passive host for another element - never a keyboard-focus target. Comes for free from the
    // Focusable=false default (see InputUIComponent); covers ScrollContentPresenter and any templated content host.

    // The currently hosted content (the incoming one while a transition runs) and, when it came from a DataTemplate,
    // the result to destroy on replacement. While a transition plays we also keep the previous content alive as the
    // "outgoing" pair so it can slide out before being removed.
    private IUIComponent _currentRoot;
    private TemplateResult _currentTemplateResult;
    private DataTemplate _currentTemplate;   // the template that built _currentRoot, so a recycled container can reuse it
    // What the current visual was built for: the KEY it waits under in ParkedVisuals while it is away.
    private object _builtFor;

    // Set when the outgoing visual is one to keep: it still slides out, but is parked instead of destroyed at the end.
    private object _outgoingParkedKey;

    private IUIComponent _outgoingRoot;
    private TemplateResult _outgoingTemplateResult;
    private bool _isContentChanged;
    private bool _transitionPending;
    private bool _textIsGenerated;                             // is _currentRoot the TextBlock this presenter generated?
    private bool _lastContentRebuilt;                          // did the last measure REBUILD the visual (vs data-only reuse)?
    private Size _lastArrangeSize = new(double.NaN, double.NaN);   // last finalSize we actually walked in ArrangeOverride
    private Size _lastMeasuredInner;   // what MeasureOverride last returned - WITHOUT this element's margin/transform
    private Size _lastContentDesired;  // ...and the content size that produced it - the cache is only good while it holds

    public static readonly AdamantiumProperty ContentProperty = AdamantiumProperty.Register(nameof(Content),
        typeof(object), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnContentPropertyChanged));

    // AffectsMeasure like Content, and for the same reason: the visual is (re)built inside MeasureOverride, so a template
    // swap that leaves measure valid is never picked up - the property holds the new template while the screen keeps the
    // visual built from the old one (a tab label whose template turns on its side stayed lying flat forever).
    public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
        typeof(DataTemplate), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnContentTemplateChanged));

    public static readonly AdamantiumProperty ContentTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ContentTemplateSelector),
        typeof(DataTemplateSelector), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnContentTemplateSelectorChanged));

    public static readonly AdamantiumProperty ContentTransitionProperty = AdamantiumProperty.Register(nameof(ContentTransition),
        typeof(ContentTransition), typeof(ContentPresenter), new PropertyMetadata(ContentTransition.None));

    public static readonly AdamantiumProperty TransitionDurationProperty = AdamantiumProperty.Register(nameof(TransitionDuration),
        typeof(Double), typeof(ContentPresenter), new PropertyMetadata(0.25));

    // The generated text follows the presenter's Foreground/FontSize (template-bound from the templated control, so its
    // theme states - accent/pressed/disabled - drive the text); the callback re-pushes them onto the already-built TextBlock.
    static ContentPresenter()
    {
        ForegroundProperty.OverrideMetadata(typeof(ContentPresenter),
            new PropertyMetadata(null, PropertyMetadataOptions.Inherits | PropertyMetadataOptions.AffectsRender, OnTextStyleChanged));
        FontSizeProperty.OverrideMetadata(typeof(ContentPresenter),
            new PropertyMetadata(14.0, PropertyMetadataOptions.Inherits | PropertyMetadataOptions.AffectsMeasure, OnTextStyleChanged));
    }

    private static void OnContentPropertyChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ContentPresenter presenter)
        {
            presenter.OnContentChangedInternal(e.OldValue, e.NewValue);
        }
    }

    private void OnContentChangedInternal(object oldContent, object newContent)
    {
        _isContentChanged = true;

        // WPF ContentPresenter semantics: when the content is a DATA object (not a UI element) rendered through a
        // ContentTemplate/ContentTemplateSelector, the presenter's DataContext IS that object, so the template's
        // {Binding}s resolve against it. This is what lets a data-templated body/header bind to its own item view-model
        // even when the presenter sits in an outer template (e.g. a TabControl's PART_SelectedContentHost, whose ambient
        // DataContext is the TabControl's, not the selected tab's). A UI-element (or null) content is left to inherit the
        // ambient DataContext - the element brought its own bindings. Only SET (never clear), so an externally assigned
        // DataContext (ItemsControl.PrepareContainer sets it before Content) is matched, never clobbered.
        //
        // The context goes on the visual the template BUILDS (see SetContentContext), never on the presenter itself. The
        // presenter's own properties may be bound - <ContentPresenter Content="{Binding Header}"/> inside an item template
        // is the ordinary case - and those bindings resolve against the presenter's DataContext. Stamping the CONTENT
        // there makes the value a binding just produced decide what that binding reads next: it resolved Header against
        // the item, then re-resolved it against the header, and settled on whatever it found. Worse, the stamp is a LOCAL
        // value, so it masks inheritance for good: recycling the container onto another item no longer reaches the
        // presenter, its Content binding never re-resolves, and the row keeps the name it first showed - a virtualized
        // list then repeats its first screenful over and over.
    }

    /// <summary>Puts the content under the visual built for it, which is what a ContentTemplate's <c>{Binding}</c>s resolve
    /// against. Re-applied on every content change, including the recycling fast-path below - that IS how a reused visual
    /// follows its new item.</summary>
    private void SetContentContext(object content)
    {
        if (_currentTemplateResult != null && _currentRoot is FundamentalUIComponent root) root.DataContext = content;
    }

    // Returns TRUE if it tore down and built a NEW visual (so the presenter must re-measure/arrange the new subtree),
    // FALSE if the existing visual was reused or nothing changed (a data-only rebind: the reused subtree keeps its size,
    // so the presenter can skip re-walking it - the reused child invalidates ITSELF if its own size actually changed).
    private bool UpdateVisualContent(object newContent)
    {

        if (!_isContentChanged)
            return false;

        _isContentChanged = false;

        // Recycling fast-path: the new content uses the SAME DataTemplate that built the current visual (a virtualized
        // list rebinding a recycled container to another item). Keep the visual AND its render units/GPU buffers - the
        // data updates by itself via the container's DataContext (the item template's {Binding}s re-resolve). Rebuilding
        // here would dispose and recreate every buffer each scroll frame (the OutOfDeviceMemory under fast scroll).
        if (newContent != null && newContent is not IUIComponent && _currentRoot != null && _currentTemplate != null)
        {
            var reuseTemplate = ContentTemplate ?? ContentTemplateSelector?.SelectTemplate(newContent, this);
            if (ReferenceEquals(reuseTemplate, _currentTemplate))
            {
                SetContentContext(newContent);   // point the kept visual at the new item; its {Binding}s re-resolve
                return false;                    // reuse: no teardown, no rebuild
            }
        }

        // Recycling fast-path 2: plain (no-template) content already hosted in the auto-generated TextBlock - just update
        // its text instead of tearing it down and building a new one. Without this, a virtualized list of STRING items
        // (no ItemTemplate) recreates a TextBlock - and its render units / GPU buffers - on every recycle/rebind during
        // scroll, which exhausts device memory (the OutOfDeviceMemory the template fast-path above already guards against,
        // but only for DataTemplate content).
        if (newContent != null && newContent is not IUIComponent && _currentTemplate == null
            && _currentRoot is TextBlock reusableText
            && ContentTemplate == null && ContentTemplateSelector == null)
        {
            reusableText.Text = newContent.ToString();   // Text is AffectsMeasure -> the TextBlock re-measures itself if it grew
            return false;
        }

        // A new swap supersedes one still mid-flight: finish the previous transition instantly before starting over.
        if (_outgoingRoot != null)
            RemoveOutgoing();

        // Animate when a transition is selected and there is new content - including the FIRST content. NEVER in the
        // designer (live or one-shot): a content transition is triggered by content replacement, but in the previewer
        // that only happens on initial load / a live-reconcile re-apply, not a real user-driven swap - so playing it
        // just slides the content off-screen and the captured frame(s) come back blank (the white-screen the live
        // designer showed). The designer shows the settled content instead, the way the WPF designer does.
        var animate = ContentTransition != ContentTransition.None && newContent != null
                      && !Design.IsDesignMode;

        // A view that asked to be kept is PARKED instead of destroyed - it goes on waiting by reference, holding what was
        // built for it (see ParkedSubtree). Which one it is has to be decided here, while the content it was built for is
        // still known.
        var parkedKey = _currentRoot != null && ParkedVisuals.ShouldKeep(_currentRoot) ? _builtFor : null;

        if (animate)
        {
            // Keep the current content (if any) as outgoing; it slides out and is removed when its animation completes.
            _outgoingRoot = _currentRoot;
            _outgoingTemplateResult = _currentTemplateResult;
            _outgoingParkedKey = parkedKey;
        }
        else if (_currentRoot != null)
        {
            if (parkedKey != null) ParkCurrent(parkedKey, _currentRoot, _currentTemplateResult, _currentTemplate);
            else Release(_currentRoot, _currentTemplateResult);
        }

        _currentRoot = null;
        _currentTemplateResult = null;
        _currentTemplate = null;
        _builtFor = null;

        if (newContent != null)
            BuildCurrent(newContent);

        // The slide distance is the laid-out size, so defer starting it to the next arrange (size known there). If a
        // transition was expected but produced no new root, drop the kept-alive outgoing content now.
        _transitionPending = animate && _currentRoot != null;
        if (animate && !_transitionPending)
            RemoveOutgoing();

        return true;   // a new visual was built -> the presenter must measure/arrange it
    }

    private void BuildCurrent(object newContent)
    {
        // Coming back to a view that was parked: put it back as it was. Not building it again IS the point - the rebuild
        // is the pause x:KeepAlive exists to avoid.
        if (ParkedVisuals.TryTake(newContent, this, out var parkedRoot, out var parkedBuilt, out var parkedTemplate, out var parkedHostSize))
        {
            // Came home to a different window or after a theme swap: drop the mark first, so the attach below revalidates
            // every node the ordinary way. Same world - the mark stays and the attach skips what it would recompute.
            if (!ParkedVisuals.IsUnchanged) ParkedSubtree.Revalidate(parkedRoot);
            _currentRoot = parkedRoot;
            _currentTemplateResult = parkedBuilt;
            _currentTemplate = parkedTemplate;
            _builtFor = newContent;

            AddVisualChild(_currentRoot);
            AddLogicalChild(_currentRoot);
            // Measure it again only if it comes back into a DIFFERENT sized host than it left. Same size means the layout
            // it kept is still the right one, and re-measuring a page of a thousand rows to arrive at what it already has
            // is the whole remaining cost of a return.
            ParkedSubtree.Unpark(_currentRoot, remeasure: parkedHostSize != _lastArrangeSize);
            SetContentContext(newContent);
            return;
        }

        _builtFor = newContent;

        if (newContent is IUIComponent iuiComponent)
        {
            // Host the content element itself (the SAME instance the author built and bound), not a template result,
            // so a {Binding} set on the authored control lives on the element that is actually rendered/clicked.
            _currentRoot = iuiComponent;
        }
        else
        {
            var dataTemplate = ContentTemplate ?? ContentTemplateSelector?.SelectTemplate(newContent, this);
            if (dataTemplate != null)
            {
                _currentTemplateResult = dataTemplate.Build(this);
                _currentRoot = _currentTemplateResult?.RootComponent;
                _currentTemplate = dataTemplate;   // remember it so a recycled rebind to the same template reuses this visual
            }
            else
            {
                // The auto-generated content text aligns to the PRESENTER's own alignment - which a control template binds
                // from the control's Horizontal/VerticalContentAlignment (e.g. <ContentPresenter VerticalAlignment=
                // "{TemplateBinding VerticalContentAlignment}"/>). So content lands where the template asked, instead of
                // the TextBlock's default (VerticalTextAlignment.Bottom, which looked low). Authored TextBlocks are unaffected.
                var textBlock = new TextBlock
                {
                    Text = newContent.ToString(),
                    FontSize = FontSize,
                    HorizontalTextAlignment = ToTextAlignment(HorizontalAlignment),
                    VerticalTextAlignment = ToTextAlignment(VerticalAlignment)
                };
                if (Foreground != null) textBlock.Foreground = Foreground;
                _currentRoot = textBlock;
            }
        }

        if (_currentRoot != null)
        {
            AddVisualChild(_currentRoot);
            AddLogicalChild(_currentRoot);
            SetContentContext(newContent);
        }

    }

    private void RemoveOutgoing()
    {
        if (_outgoingRoot == null)
            return;

        if (_outgoingParkedKey != null) ParkCurrent(_outgoingParkedKey, _outgoingRoot, _outgoingTemplateResult, template: null);
        else Release(_outgoingRoot, _outgoingTemplateResult);

        _outgoingRoot = null;
        _outgoingTemplateResult = null;
        _outgoingParkedKey = null;
    }

    /// <summary>Puts a visual aside instead of destroying it. The store parks it (so the renderer keeps what it built);
    /// taking it out of the tree is this presenter's job, because only it knows what removal means here.</summary>
    private void ParkCurrent(object key, IUIComponent root, TemplateResult built, DataTemplate template)
    {
        ParkedVisuals.Keep(key, root, built, template, _lastArrangeSize);
        RemoveVisualChild(root);
        RemoveLogicalChild(root);
    }

    /// <summary>
    /// Lets go of a visual this presenter was showing - but ONLY while it is still ours.
    /// <para>Content can be an ELEMENT, and an element has exactly one parent. When the same element is handed to another
    /// presenter, that presenter adopts it and this one is told about the change afterwards - so tearing it down here
    /// unconditionally reaches into its NEW home and rips it out. Measured on docking: merging two floating windows moved
    /// a tab's body to the surviving window's presenter, and the emptied one - notified a moment later - detached it
    /// again, leaving the tab blank with its content parented nowhere.</para>
    /// <para>A visual built from a TEMPLATE is ours by construction and is always destroyed with it.</para>
    /// </summary>
    private void Release(IUIComponent visual, TemplateResult built)
    {
        if (built == null && !ReferenceEquals(visual.VisualParent, this)) return;

        RemoveVisualChild(visual);
        RemoveLogicalChild(visual);
        built?.Destroy();
    }

    // Starts the slide once the children have been arranged at the full presenter rect (same origin), so the only
    // thing moving them is the animated RenderTransform - layout is untouched. Shared logic lives in ContentTransitions.
    private void StartTransition(Size size)
    {
        // Sliding content can extend past the presenter while it moves; clip it (enforcement depends on renderer clip).
        ClipToBounds = true;
        ContentTransitions.Run(ContentTransition, TransitionDuration, size, _currentRoot, _outgoingRoot, RemoveOutgoing);
    }

    private static void OnContentTemplateChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ContentPresenter presenter)
        {
            presenter.OnContentChangedInternal(e.OldValue, e.NewValue);
        }
    }

    private static void OnContentTemplateSelectorChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ContentPresenter presenter)
        {
            presenter.OnContentChangedInternal(e.OldValue, e.NewValue);
        }
    }

    [Content]
    public object Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public DataTemplate ContentTemplate
    {
        get => GetValue<DataTemplate>(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public DataTemplateSelector ContentTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ContentTemplateSelectorProperty);
        set => SetValue(ContentTemplateSelectorProperty, value);
    }

    public ContentTransition ContentTransition
    {
        get => GetValue<ContentTransition>(ContentTransitionProperty);
        set => SetValue(ContentTransitionProperty, value);
    }

    public Double TransitionDuration
    {
        get => GetValue<Double>(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    private static void OnTextStyleChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is ContentPresenter presenter) presenter.ApplyTextStyle();
    }

    private void ApplyTextStyle()
    {
        // The auto-generated TextBlock carries an EXPLICIT Foreground/FontSize (set when it was built), so inheritance
        // wouldn't reach it - re-push on change. TEMPLATED content needs nothing here: it inherits the presenter's
        // Foreground/FontSize naturally (the change cascades via ParentPropertyChanged), now that no per-TextBlock Style
        // sits at a higher priority masking it - see TextBlockStyleSet.
        //
        // ONLY the generated one. An AUTHORED TextBlock handed over as content is not ours to write into: an explicit
        // write outranks inheritance permanently, so whatever colour this presenter happened to hold at that instant
        // becomes the element's own for good. Measured on docking: merging two floating windows had the EMPTIED
        // presenter - by then holding the Transparent default - stamp it onto the tab's body, and the body stayed
        // invisible in its new home, where the live presenter's white could no longer reach it.
        if (!_textIsGenerated || _currentRoot is not TextBlock textBlock) return;
        textBlock.FontSize = FontSize;
        if (Foreground != null) textBlock.Foreground = Foreground;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _lastContentRebuilt = UpdateVisualContent(Content);

        var sizeBefore = DesiredSize;
        // Data-only reuse (a virtualized list rebinding a recycled container): the visual is kept and only its bound data
        // changed, so the subtree's SIZE is unchanged - skip re-walking it. If the reused child's OWN size actually changed
        // (a string's text, an AffectsMeasure binding), it invalidated ITSELF, so IsMeasureValid is false and we fall
        // through to measure it. A genuine content REBUILD (_lastContentRebuilt) always measures the new subtree.
        // What is returned is the INNER size: MeasureCore adds this element's margin (and its LayoutTransform) on top.
        // DesiredSize is the OUTER result of that, so returning IT compounds both on every skip - the margin grew by its
        // own width per re-measure, which a ribbon group caption (Margin="0 4 0 0") turned into a caption climbing 4px up
        // its group each time the tab was re-opened.
        // ...and only while the content is STILL the size that produced the cached answer. "Valid" does not mean
        // "unchanged": the layout pass re-measures a dirty child before it reaches the parent, so by our turn the child
        // can be valid AND a different size, and answering from the cache reports a size that no longer exists.
        // Measured: a quick-access bar gaining a button grew to 122 while its presenter kept saying 94, so the caption's
        // Auto column never widened and the window title never stepped aside - until a resize re-measured everything.
        if (!_lastContentRebuilt && _currentRoot is IMeasurableComponent { IsMeasureValid: true } measured
            && PreviousMeasureConstraint == availableSize
            && LayoutTransform == null
            && measured.DesiredSize == _lastContentDesired)
            return _lastMeasuredInner;

        var size = base.MeasureOverride(availableSize);
        _lastMeasuredInner = size;
        _lastContentDesired = _currentRoot is IMeasurableComponent content ? content.DesiredSize : default;

        // A REBUILT content is very often a different size than the one the PARENT measured us at - and the rebuild
        // happens inside our own measure, which may have been requested directly (a tab strip measures a header with an
        // infinite axis to find its natural size) rather than through the parent. The parent then keeps a size derived
        // from content that no longer exists, and nothing ever asks it again.
        // Measured: a folded tab's turned label reported 17x54 here while its tab still carried the 78x29 it had when the
        // label was lying flat - so three tabs in a column were laid out 29 apart and drew on top of each other.
        if (_lastContentRebuilt)
        {
            (VisualParent as IMeasurableComponent)?.InvalidateMeasure();
        }

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Mirror the measure skip: a data-only reuse at the same arranged size leaves the subtree positioned as-is, so
        // don't re-walk it. A rebuilt/resized child was re-measured (arrange-invalid) and is handled by base below.
        // A PENDING TRANSITION is not "nothing changed", though: a kept view comes back arrange-VALID at the same size
        // (it kept the arrange it had when it was parked), so this skip swallowed its entrance - and it stayed wherever
        // the slide-out had left it, off screen. That is what made a kept view come back to an empty page.
        if (!_transitionPending && !_lastContentRebuilt
            && _currentRoot is IMeasurableComponent { IsArrangeValid: true } && finalSize == _lastArrangeSize)
            return finalSize;

        _lastArrangeSize = finalSize;
        var size = base.ArrangeOverride(finalSize);

        if (_transitionPending)
        {
            _transitionPending = false;
            StartTransition(finalSize);
        }

        return size;
    }

    // Map the presenter's own content alignment onto the auto-generated text's alignment, so string content honours the
    // template's Horizontal/VerticalContentAlignment. Stretch has no text equivalent -> left / centre (sensible defaults).
    private static HorizontalTextAlignment ToTextAlignment(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => HorizontalTextAlignment.Center,
        HorizontalAlignment.Right => HorizontalTextAlignment.Right,
        _ => HorizontalTextAlignment.Left,
    };

    private static VerticalTextAlignment ToTextAlignment(VerticalAlignment alignment) => alignment switch
    {
        VerticalAlignment.Top => VerticalTextAlignment.Top,
        VerticalAlignment.Bottom => VerticalTextAlignment.Bottom,
        _ => VerticalTextAlignment.Center,
    };
}
