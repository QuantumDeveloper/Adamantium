using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    /// <summary>Who a parked view is remembered UNDER. The presenter itself cannot be it: a presenter is a TEMPLATE
    /// PART, and a theme swap rebuilds every template in the application - the replacement then looks the view up under
    /// its own identity, misses, and builds the whole tab again, while the entry filed under the presenter that no
    /// longer exists stays in the park for the life of the process. Measured on the Layout tab: a rebuild of the tab on
    /// every theme swap, ~200 MB a swap retained, and the abandoned subtree's render marks left the cache permanently
    /// dirty - a full rebuild every frame (build 10.9 ms of an 8.9 ms frame) on a scene doing nothing.
    /// <para>The templated parent is the control that OWNS the content - a TabItem, a ContentControl - and a theme swap
    /// replaces its template, not the control. That is the identity a parked view has to be filed under.</para></summary>
    private object ParkOwner => TemplatedParent ?? (object)this;

    // Set when the outgoing visual is one to keep: it still slides out, but is parked instead of destroyed at the end.
    private object _outgoingParkedKey;

    private IUIComponent _outgoingRoot;
    private TemplateResult _outgoingTemplateResult;
    private bool _isContentChanged;
    private bool _transitionPending;
    private bool _transitionRunning;
    private Action _afterTransition;
    private bool _textIsGenerated;                             // is _currentRoot the TextBlock this presenter generated?
    private bool _lastContentRebuilt;                          // did the last measure REBUILD the visual (vs data-only reuse)?
    private Size _lastArrangeSize = new(double.NaN, double.NaN);   // last finalSize we actually walked in ArrangeOverride
    // Deferred content: what is being built away from the loop thread, and which build is still the current one. The
    // token supersedes a build whose content has already been replaced - the user who switches tabs twice must not get
    // the first tab's body handed to them when it finally lands.
    private int _deferToken;
    private bool _deferInFlight;

    // Deferred builds run ONE AT A TIME, process-wide. A view on its way up touches registries written for a single
    // builder - the container's resolution chain, the view locator's cache, the theme's memos - and two overlapping
    // builds turn "already resolving" into a circular dependency that does not exist, which is a tab that never arrives.
    // One queue also keeps a burst of tab switches from starting five builds nobody is waiting for any more.
    private static readonly System.Threading.SemaphoreSlim BuildQueue = new(1, 1);

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

    /// <summary>Build this presenter's content AWAY from the loop thread: the presenter shows its
    /// <see cref="LoadingTemplate"/> at once and adopts the real visual when it is ready. For content whose construction
    /// is measured in hundreds of milliseconds (a tab body is thousands of elements) - not for list items, which are
    /// cheap and would only flash.</summary>
    public static readonly AdamantiumProperty DeferContentProperty = AdamantiumProperty.Register(nameof(DeferContent),
        typeof(Boolean), typeof(ContentPresenter), new PropertyMetadata(false));

    /// <summary>What stands in the content's place while it is being built (a spinner, a skeleton). Optional: without it
    /// the presenter simply shows nothing until the content lands.</summary>
    public static readonly AdamantiumProperty LoadingTemplateProperty = AdamantiumProperty.Register(nameof(LoadingTemplate),
        typeof(DataTemplate), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

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

        SweepPresentersOnDiscard();
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
        if (newContent != null && newContent is not IUIComponent && _currentRoot != null && _currentTemplate != null
            && !_deferInFlight)
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

        // Whatever is still being built off-thread was built for content that is no longer ours: bump the token so it
        // lands into nothing (see AdoptDeferred) instead of replacing the visual that comes next.
        _deferToken++;
        _deferInFlight = false;

        _currentRoot = null;
        _currentTemplateResult = null;
        _currentTemplate = null;
        _builtFor = null;

        if (newContent != null)
            BuildCurrent(newContent);

        // The slide distance is the laid-out size, so defer starting it to the next arrange (size known there). If a
        // transition was expected but produced no new root, drop the kept-alive outgoing content now.
        _transitionPending = animate && (_currentRoot != null || _outgoingRoot != null);
        if (animate && !_transitionPending)
            RemoveOutgoing();

        return true;   // a new visual was built -> the presenter must measure/arrange it
    }

    private void BuildCurrent(object newContent)
    {
        // Coming back to a view that was parked: put it back as it was. Not building it again IS the point - the rebuild
        // is the pause x:KeepAlive exists to avoid.
        if (ParkedVisuals.TryTake(ParkOwner, newContent, this, out var parkedRoot, out var parkedBuilt, out var parkedTemplate, out var parkedHostSize))
        {
            // Came home to a different window or after a theme swap: drop the mark first, so the attach below revalidates
            // every node the ordinary way. Same world - the mark stays and the attach skips what it would recompute.
            if (!ParkedVisuals.IsUnchanged) ParkedSubtree.Revalidate(parkedRoot);

            var reTheme = ParkedVisuals.ThemeChanged;
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

            // Parked THROUGH a theme swap: the walk that re-themes the application runs over the TREE, and this subtree
            // was not in it, so it comes back still wearing the theme it left under. Nothing else puts that right -
            // dropping the parked mark above only restores the ordinary attach work, which does not touch styles.
            // (Until the park survived a swap at all this never showed: the tab was rebuilt from scratch instead.)
            // AFTER the attach, deliberately: invalidating styles registers the subtree for re-theming on the next
            // layout pass, and a node with no tree to belong to has no queue to register WITH - done a few lines
            // earlier, the request was simply lost and the tab came back in the old theme regardless.
            if (reTheme) (_currentRoot as FundamentalUIComponent)?.InvalidateStyles();

            SetContentContext(newContent);
            return;
        }

        _builtFor = newContent;

        // From here on this presenter NAMES content by field, so it has to be swept when that content is destroyed.
        RegisterForDiscardSweep();

        if (newContent is IUIComponent iuiComponent)
        {
            // Host the content element itself (the SAME instance the author built and bound), not a template result,
            // so a {Binding} set on the authored control lives on the element that is actually rendered/clicked.
            _currentRoot = iuiComponent;
        }
        else
        {
            var dataTemplate = ContentTemplate ?? ContentTemplateSelector?.SelectTemplate(newContent, this);

            if (dataTemplate != null && WantsDeferredBuild)
            {
                _currentTemplate = dataTemplate;
                StartDeferredBuild(newContent, dataTemplate);
                return;
            }

            if (dataTemplate != null)
            {
                BuildFromTemplate(newContent, dataTemplate);
                return;
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
                // BOUND, not copied: a copy is only as current as the change notification that refreshes it, and an
                // INHERITED change does not always reach a descendant - the inheritance walk has a cheap path that steps
                // over an element without notifying it. The label then kept the colour the presenter held when the
                // content was built, so a generated label inside a theme scope stayed in the application's colour.
                textBlock.SetBinding(nameof(TextBlock.Foreground),
                    new Core.Data.Binding(nameof(Foreground)) { Source = this });
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

    /// <summary>Builds the visual from its template right here, on the calling thread - the ordinary path, and the one a
    /// failed background build falls back to.</summary>
    private void BuildFromTemplate(object content, DataTemplate template)
    {
        _currentTemplateResult = template.Build(this);
        _currentRoot = _currentTemplateResult?.RootComponent;
        _currentTemplate = template;   // remember it so a recycled rebind to the same template reuses this visual

        if (_currentRoot == null) return;

        AddVisualChild(_currentRoot);
        AddLogicalChild(_currentRoot);
        SetContentContext(content);
        InvalidateMeasure();
    }

    /// <summary>Puts the loading visual in the content's place. It is the current visual like any other, so measure,
    /// arrange and teardown need to know nothing about it.</summary>
    private void ShowLoadingVisual(object content)
    {
        if (!_deferInFlight || _currentRoot != null) return;

        var loading = LoadingTemplate?.Build(this);
        if (loading?.RootComponent == null) return;

        _currentTemplateResult = loading;
        _currentRoot = loading.RootComponent;
        AddVisualChild(_currentRoot);
        AddLogicalChild(_currentRoot);
        SetContentContext(content);
        InvalidateMeasure();
    }

    /// <summary>Lets go of what is on screen now - the loading visual, when the real content is about to take its place.</summary>
    private void DropCurrent()
    {
        if (_currentRoot == null) return;

        Release(_currentRoot, _currentTemplateResult);
        _currentRoot = null;
        _currentTemplateResult = null;
    }

    /// <summary>Deferred only where there ARE next frames for it to arrive in. A one-shot surface - a bitmap bake, a
    /// designer preview, an off-screen test - has exactly one, and a spinner in it is a spinner for good; the surface
    /// itself says so (see IRootVisualComponent.RendersOnce), so nothing has to be switched on around the render.</summary>
    private bool WantsDeferredBuild =>
        DeferContent && !Design.IsDesignMode && (RootVisual as IRootVisualComponent)?.RendersOnce != true;

    /// <summary>Shows the loading visual now and builds the real one on a worker. The presenter goes on living with a
    /// perfectly ordinary child in the meantime - the loading visual IS the current visual - so measure, arrange and the
    /// teardown path need to know nothing about any of this.</summary>
    private void StartDeferredBuild(object content, DataTemplate template)
    {
        var token = ++_deferToken;
        _deferInFlight = true;

        // The loading visual belongs to the tab being ENTERED, so it takes no part in the one that is leaving: while the
        // swap plays, the area the old content vacates stays empty, and the spinner appears after it - and only if the
        // content has still not arrived by then (if it has, it takes the place directly and no spinner is ever seen).
        if (_outgoingRoot == null) ShowLoadingVisual(content);
        else _afterTransition = () => ShowLoadingVisual(content);

        Task.Run(async () =>
        {
            TemplateResult built = null;
            Exception failure = null;

            await BuildQueue.WaitAsync().ConfigureAwait(false);
            try
            {
                // Its turn came - but is it still wanted? A tab the user has already left costs nothing to skip, and
                // skipping it is what lets the tab they ARE waiting for start now instead of after it.
                if (token != System.Threading.Volatile.Read(ref _deferToken)) return;

                // The subtree belongs to nobody until it is handed over: nothing in the live tree can reach it, and it
                // reaches nothing that is not thread-safe - what it might have (the animation heartbeat) is kept out by
                // the elements themselves, which do not animate until they are in a tree.
                built = template.Build(this);
            }
            catch (Exception e)
            {
                failure = e;
            }
            finally
            {
                BuildQueue.Release();
            }

            LoopSignal.Post(() => AdoptDeferred(token, content, template, built, failure));
        });
    }

    /// <summary>Takes the finished subtree in, on the loop thread. A build whose content has already been replaced is
    /// dropped - it was built for a tab the user has left.</summary>
    private void AdoptDeferred(int token, object content, DataTemplate template, TemplateResult built, Exception failure)
    {
        if (failure != null)
        {
            Console.WriteLine(failure);
        }

        if (token != _deferToken)
        {
            // The user left before it was ready. Showing it now would put the tab they LEFT in front of the one they
            // chose, so it is not shown - but it is finished work, and a view that asked to be kept waits by reference
            // instead of being thrown away, exactly as one that was on screen does. Parked with an unknown host size, so
            // the return measures it once (it never was measured - it never was in a tree).
            if (built?.RootComponent is { } finished && ParkedVisuals.ShouldKeep(finished))
            {
                ParkedVisuals.Keep(ParkOwner, content, finished, built, template, new Size(Double.NaN, Double.NaN));
                return;
            }

            built?.Destroy();
            return;
        }

        // The swap this content belongs to is still playing: the tab that was left is on its way out and the loading
        // visual on its way in. Slipping the content in underneath that leaves the OUTGOING tab sliding over content it
        // has nothing to do with - which is what a deferred tab looked like on the way out. It waits for its own swap.
        if (_transitionPending || _transitionRunning)
        {
            _afterTransition = () => AdoptDeferred(token, content, template, built, failure);
            return;
        }

        _deferInFlight = false;

        if (built?.RootComponent == null)
        {
            // Nothing came back. A spinner that never ends is worse than the pause it was meant to hide, so the content
            // is built HERE, the ordinary way - and a view that is genuinely broken throws where it always threw.
            DropCurrent();
            BuildFromTemplate(content, template);
            return;
        }

        // The loading visual has done its job: it goes the ordinary way, because that is all it ever was.
        DropCurrent();

        _currentTemplateResult = built;
        _currentRoot = built.RootComponent;
        AddVisualChild(_currentRoot);
        AddLogicalChild(_currentRoot);
        SetContentContext(content);
        InvalidateMeasure();
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
        ParkedVisuals.Keep(ParkOwner, key, root, built, template, _lastArrangeSize);
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

        // Say that this content is GONE, for the whole subtree, before anything is unpicked. Template teardown already
        // announced its own parts; this is the other half, and the half that was missing - a view authored in markup is
        // CONTENT, not a template part, so nothing marked it and every sweep that asks "was this discarded" answered no
        // for an entire discarded view. Measured: a discarded ListBox left subscribed to a view model's collection,
        // which outlives the application's whole UI. See DiscardedVisuals.
        if (_discardBuf.Count > 0) _discardBuf.Clear();
        CollectForDiscard(visual);
        Core.DiscardedVisuals.Publish(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_discardBuf));
        _discardBuf.Clear();

        RemoveVisualChild(visual);
        RemoveLogicalChild(visual);
        built?.Destroy();
    }

    /// <summary>Destroyed: let the CONTENT go. These fields are the presenter's own handle on what it built, and they
    /// are cleared on the paths that replace content - not on the path where the presenter itself is thrown away, which
    /// is what a theme swap does to every presenter in a rebuilt template. A root held here has no visual parent to give
    /// it away, so nothing else can reach it to release it.</summary>
    protected override void OnDiscarded()
    {
        base.OnDiscarded();
        DropContentHandles();
    }

    private void DropContentHandles()
    {
        _currentRoot = null;
        _outgoingRoot = null;
        _currentTemplateResult = null;
        _outgoingTemplateResult = null;
        _currentTemplate = null;
    }

    // Every presenter that has ever built content, held WEAKLY - the register must not be the thing that keeps one
    // alive. Registered on first build rather than in the constructor: a presenter that never presents anything holds
    // nothing and has no reason to be walked.
    private static readonly List<WeakReference<ContentPresenter>> Presenters = new();
    private bool _registered;

    private void RegisterForDiscardSweep()
    {
        if (_registered) return;
        _registered = true;
        lock (Presenters) Presenters.Add(new WeakReference<ContentPresenter>(this));
    }

    // The other half of the story in OnDiscarded. THERE the presenter is the one destroyed; HERE it survives and its
    // CONTENT is destroyed - a root built from a template that has just been torn down, while the presenter itself sits
    // in a part of the tree that was not. The root has no visual parent by then, so this field is the only thing still
    // naming it, and nothing else can reach it to let go. One handler for every presenter, walking a few hundred weak
    // references once per teardown - not a lookup per discarded element.
    private static void SweepPresentersOnDiscard()
    {
        Core.DiscardedVisuals.Discarded += gone =>
        {
            List<ContentPresenter> live = null;
            lock (Presenters)
            {
                for (var i = Presenters.Count - 1; i >= 0; i--)
                {
                    if (Presenters[i].TryGetTarget(out var presenter)) (live ??= new List<ContentPresenter>()).Add(presenter);
                    else Presenters.RemoveAt(i);
                }
            }

            if (live == null) return;

            foreach (var presenter in live)
            {
                if (IsGone(presenter._currentRoot, gone) || IsGone(presenter._outgoingRoot, gone))
                    presenter.DropContentHandles();
            }
        };
    }

    private static bool IsGone(IUIComponent root, ReadOnlySpan<IFundamentalUIComponent> gone)
    {
        if (root == null) return false;

        foreach (var component in gone)
            if (ReferenceEquals(component, root)) return true;

        return root is Core.FundamentalUIComponent { IsDiscarded: true };
    }

    // Reused: a content swap is a per-frame-ish event on a tab strip, and this runs on the loop thread.
    private readonly List<IFundamentalUIComponent> _discardBuf = new();

    private void CollectForDiscard(IUIComponent node)
    {
        if (node is IFundamentalUIComponent fundamental) _discardBuf.Add(fundamental);
        foreach (var child in node.VisualChildren) CollectForDiscard(child);
    }

    // Starts the slide once the children have been arranged at the full presenter rect (same origin), so the only
    // thing moving them is the animated RenderTransform - layout is untouched. Shared logic lives in ContentTransitions.
    private void StartTransition(Size size)
    {
        // Sliding content can extend past the presenter while it moves; clip it (enforcement depends on renderer clip).
        ClipToBounds = true;
        _transitionRunning = true;
        ContentTransitions.Run(ContentTransition, TransitionDuration, size, _currentRoot, _outgoingRoot, TransitionFinished);
    }

    // The swap is over: the old content goes, and anything that was waiting for the swap to finish happens now.
    private void TransitionFinished()
    {
        _transitionRunning = false;
        RemoveOutgoing();

        var waiting = _afterTransition;
        _afterTransition = null;
        waiting?.Invoke();
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

    public bool DeferContent
    {
        get => GetValue<bool>(DeferContentProperty);
        set => SetValue(DeferContentProperty, value);
    }

    public DataTemplate LoadingTemplate
    {
        get => GetValue<DataTemplate>(LoadingTemplateProperty);
        set => SetValue(LoadingTemplateProperty, value);
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
        if (a is not ContentPresenter presenter) return;

        presenter.ApplyTextStyle();
    }

    private void ApplyTextStyle()
    {
        // The auto-generated TextBlock carries an EXPLICIT Foreground/FontSize (set when it was built), so inheritance
        // wouldn't reach it - re-push on change. TEMPLATED content needs nothing here: it inherits the presenter's
        // Foreground/FontSize naturally (the value is inherited through the ancestor chain), now that no per-TextBlock Style
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

    /// <summary>TEMP: this type's OWN measure costs 214us a call - forty times a Border - and is 45% of the whole measure
    /// phase. Its body is three things; only a split says which. Content construction was the obvious answer and does not
    /// fit the data: a second with 136 attaches still spent 118ms here over 553 calls.</summary>
    public static double UpdateContentMs, CacheHitMs, BaseMeasureMs;
    public static int CacheHits, FullMeasures;

    protected override Size MeasureOverride(Size availableSize)
    {
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        _lastContentRebuilt = UpdateVisualContent(Content);
        var t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        UpdateContentMs += System.Diagnostics.Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds;

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
        {
            CacheHitMs += System.Diagnostics.Stopwatch.GetElapsedTime(t1).TotalMilliseconds;
            CacheHits++;
            return _lastMeasuredInner;
        }

        var t2 = System.Diagnostics.Stopwatch.GetTimestamp();
        CacheHitMs += System.Diagnostics.Stopwatch.GetElapsedTime(t1, t2).TotalMilliseconds;
        var size = base.MeasureOverride(availableSize);
        BaseMeasureMs += System.Diagnostics.Stopwatch.GetElapsedTime(t2).TotalMilliseconds;
        FullMeasures++;
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
