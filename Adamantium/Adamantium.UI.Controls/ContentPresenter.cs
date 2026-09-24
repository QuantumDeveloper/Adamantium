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
    private IUIComponent _currentRoot;
    private TemplateResult _currentTemplateResult;
    private DataTemplate _currentTemplate;   // so a recycled container can reuse the visual
    // The key the current visual waits under in ParkedVisuals while it is away.
    private object _builtFor;

    // The templated parent, not the presenter: a theme swap rebuilds every template part, and a view filed under the old
    // presenter was never found again and stayed parked for good.
    private object ParkOwner => TemplatedParent ?? (object)this;

    private object _outgoingParkedKey;

    private IUIComponent _outgoingRoot;
    private TemplateResult _outgoingTemplateResult;
    private bool _isContentChanged;
    private bool _transitionPending;
    private bool _transitionRunning;
    private Action _afterTransition;
    private bool _textIsGenerated;
    private bool _lastContentRebuilt;
    private Size _lastArrangeSize = new(double.NaN, double.NaN);
    // Supersedes a background build whose content has already been replaced.
    private int _deferToken;
    private bool _deferInFlight;

    // One background build at a time, process-wide: overlapping builds see a circular dependency that does not exist.
    private static readonly System.Threading.SemaphoreSlim BuildQueue = new(1, 1);

    private Size _lastMeasuredInner;   // without this element's margin/transform
    private Size _lastContentDesired;

    public static readonly AdamantiumProperty ContentProperty = AdamantiumProperty.Register(nameof(Content),
        typeof(object), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnContentPropertyChanged));

    // AffectsMeasure: the visual is rebuilt inside MeasureOverride, so a template swap must invalidate it.
    public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
        typeof(DataTemplate), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnContentTemplateChanged));

    public static readonly AdamantiumProperty ContentTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ContentTemplateSelector),
        typeof(DataTemplateSelector), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnContentTemplateSelectorChanged));

    /// <summary>Builds the content off the loop thread, showing <see cref="LoadingTemplate"/> meanwhile. For content that
    /// takes hundreds of milliseconds to build, not for list items.</summary>
    public static readonly AdamantiumProperty DeferContentProperty = AdamantiumProperty.Register(nameof(DeferContent),
        typeof(Boolean), typeof(ContentPresenter), new PropertyMetadata(false));

    /// <summary>What stands in the content's place while it is being built. Optional.</summary>
    public static readonly AdamantiumProperty LoadingTemplateProperty = AdamantiumProperty.Register(nameof(LoadingTemplate),
        typeof(DataTemplate), typeof(ContentPresenter), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty ContentTransitionProperty = AdamantiumProperty.Register(nameof(ContentTransition),
        typeof(ContentTransition), typeof(ContentPresenter), new PropertyMetadata(ContentTransition.None));

    public static readonly AdamantiumProperty TransitionDurationProperty = AdamantiumProperty.Register(nameof(TransitionDuration),
        typeof(Double), typeof(ContentPresenter), new PropertyMetadata(0.25));

    /// <summary>How the label generated for string content is cut when it does not fit. Default None: the label reports
    /// the width it wants.</summary>
    public static readonly AdamantiumProperty TextTrimmingProperty = AdamantiumProperty.Register(nameof(TextTrimming),
        typeof(TextTrimming), typeof(ContentPresenter),
        new PropertyMetadata(TextTrimming.None, PropertyMetadataOptions.AffectsMeasure, OnTextStyleChanged));

    public TextTrimming TextTrimming
    {
        get => GetValue<TextTrimming>(TextTrimmingProperty);
        set => SetValue(TextTrimmingProperty, value);
    }

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

        // Data content becomes the DataContext of the visual built for it (SetContentContext), never of the presenter:
        // the presenter's own bindings resolve against its DataContext, and a local value there would mask inheritance.
    }

    private void SetContentContext(object content)
    {
        if (_currentTemplateResult != null && _currentRoot is FundamentalUIComponent root) root.DataContext = content;
    }

    // True when a new visual was built and must be measured; false on reuse or no change.
    private bool UpdateVisualContent(object newContent)
    {

        if (!_isContentChanged)
            return false;

        _isContentChanged = false;

        // Same template as the current visual (a recycled container): keep the visual and its GPU buffers - rebuilding
        // per scroll frame ran the device out of memory.
        if (newContent != null && newContent is not IUIComponent && _currentRoot != null && _currentTemplate != null
            && !_deferInFlight)
        {
            var reuseTemplate = ContentTemplate ?? ContentTemplateSelector?.SelectTemplate(newContent, this);
            if (ReferenceEquals(reuseTemplate, _currentTemplate))
            {
                SetContentContext(newContent);
                return false;
            }
        }

        // The same for plain string content in the generated TextBlock.
        if (newContent != null && newContent is not IUIComponent && _currentTemplate == null
            && _currentRoot is TextBlock reusableText
            && ContentTemplate == null && ContentTemplateSelector == null)
        {
            reusableText.Text = newContent.ToString();
            return false;
        }

        if (_outgoingRoot != null)
            RemoveOutgoing();

        // Never in the designer: there a swap is only a reload, and the slide left the captured frame blank.
        var animate = ContentTransition != ContentTransition.None && newContent != null
                      && !Design.IsDesignMode;

        // Decided here, while the content it was built for is still known.
        var parkedKey = _currentRoot != null && ParkedVisuals.ShouldKeep(_currentRoot) && CanComeBack(_builtFor)
            ? _builtFor
            : null;

        if (animate)
        {
            _outgoingRoot = _currentRoot;
            _outgoingTemplateResult = _currentTemplateResult;
            _outgoingParkedKey = parkedKey;
        }
        else if (_currentRoot != null)
        {
            if (parkedKey != null) ParkCurrent(parkedKey, _currentRoot, _currentTemplateResult, _currentTemplate);
            else Release(_currentRoot, _currentTemplateResult);
        }

        // A background build still running was for content that is no longer ours.
        _deferToken++;
        _deferInFlight = false;

        _currentRoot = null;
        _currentTemplateResult = null;
        _currentTemplate = null;
        _builtFor = null;

        if (newContent != null)
            BuildCurrent(newContent);

        // The slide distance is the laid-out size, so it starts at the next arrange.
        _transitionPending = animate && (_currentRoot != null || _outgoingRoot != null);
        if (_transitionPending) ContentTransitions.Prepare(_currentRoot, _outgoingRoot);
        if (animate && !_transitionPending)
            RemoveOutgoing();

        return true;
    }

    private void BuildCurrent(object newContent)
    {
        if (ParkedVisuals.TryTake(ParkOwner, newContent, this, out var parkedRoot, out var parkedBuilt, out var parkedTemplate, out var parkedHostSize))
        {
            // Another window or theme: the attach below must revalidate every node the ordinary way.
            if (!ParkedVisuals.IsUnchanged) ParkedSubtree.Revalidate(parkedRoot);

            var reTheme = ParkedVisuals.ThemeChanged;
            _currentRoot = parkedRoot;
            _currentTemplateResult = parkedBuilt;
            _currentTemplate = parkedTemplate;
            _builtFor = newContent;

            AddVisualChild(_currentRoot);
            AddLogicalChild(_currentRoot);
            // Re-measured only in a host of another size: the layout it kept is still right otherwise.
            ParkedSubtree.Unpark(_currentRoot, remeasure: parkedHostSize != _lastArrangeSize);

            // The re-theming walk missed it while parked. After the attach: a node out of the tree has no queue to
            // register the restyle with.
            if (reTheme) (_currentRoot as FundamentalUIComponent)?.InvalidateStyles();

            SetContentContext(newContent);
            return;
        }

        _builtFor = newContent;

        RegisterForDiscardSweep();

        if (newContent is IUIComponent iuiComponent)
        {
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
                var textBlock = new TextBlock
                {
                    Text = newContent.ToString(),
                    TextTrimming = TextTrimming,
                    HorizontalTextAlignment = ToTextAlignment(HorizontalAlignment),
                    VerticalTextAlignment = ToTextAlignment(VerticalAlignment),
                    // Centred, not stretched: a stretched block does not hand its height to the text layout, so the
                    // label sat against the top of its slot.
                    HorizontalAlignment = HorizontalAlignment,
                    VerticalAlignment = VerticalAlignment == VerticalAlignment.Stretch
                        ? VerticalAlignment.Center
                        : VerticalAlignment
                };
                // Bound, not copied: an inherited change can step over the label without notifying it.
                textBlock.SetBinding(nameof(TextBlock.Foreground),
                    new Core.Data.Binding(nameof(Foreground)) { Source = this });

                textBlock.SetBinding(nameof(TextBlock.FontSize),
                    new Core.Data.Binding(nameof(FontSize)) { Source = this });
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

    private void BuildFromTemplate(object content, DataTemplate template)
    {
        _currentTemplateResult = template.Build(this);
        _currentRoot = _currentTemplateResult?.RootComponent;
        _currentTemplate = template;

        if (_currentRoot == null) return;

        AddVisualChild(_currentRoot);
        AddLogicalChild(_currentRoot);
        SetContentContext(content);
        InvalidateMeasure();
    }

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

    private void DropCurrent()
    {
        if (_currentRoot == null) return;

        Release(_currentRoot, _currentTemplateResult);
        _currentRoot = null;
        _currentTemplateResult = null;
    }

    // A surface that renders once has no next frame for the content to arrive in.
    private bool WantsDeferredBuild =>
        DeferContent && !Design.IsDesignMode && (RootVisual as IRootVisualComponent)?.RendersOnce != true;

    private void StartDeferredBuild(object content, DataTemplate template)
    {
        var token = ++_deferToken;
        _deferInFlight = true;

        // The spinner belongs to the tab being entered: it appears after the swap, if the content has not arrived by then.
        if (_outgoingRoot == null) ShowLoadingVisual(content);
        else _afterTransition = () => ShowLoadingVisual(content);

        Task.Run(async () =>
        {
            TemplateResult built = null;
            Exception failure = null;

            await BuildQueue.WaitAsync().ConfigureAwait(false);
            try
            {
                // A tab already left is skipped, so the one being waited for starts now.
                if (token != System.Threading.Volatile.Read(ref _deferToken)) return;

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

    private void AdoptDeferred(int token, object content, DataTemplate template, TemplateResult built, Exception failure)
    {
        if (failure != null)
        {
            Console.WriteLine(failure);
        }

        if (token != _deferToken)
        {
            // Left before it was ready: a view that asked to be kept is parked like one that was on screen.
            if (built?.RootComponent is { } finished && ParkedVisuals.ShouldKeep(finished) && CanComeBack(content))
            {
                ParkedVisuals.Keep(ParkOwner, content, finished, built, template, new Size(Double.NaN, Double.NaN));
                return;
            }

            built?.Destroy();
            return;
        }

        // Waits for its own swap, or the outgoing tab slides over it.
        if (_transitionPending || _transitionRunning)
        {
            _afterTransition = () => AdoptDeferred(token, content, template, built, failure);
            return;
        }

        _deferInFlight = false;

        if (built?.RootComponent == null)
        {
            // Nothing came back: build it here, so a broken view throws where it always threw.
            DropCurrent();
            BuildFromTemplate(content, template);
            return;
        }

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

    private bool IsVisualChild(IUIComponent visual)
    {
        foreach (var child in VisualChildren)
        {
            if (ReferenceEquals(child, visual)) return true;
        }
        return false;
    }

    private void ParkCurrent(object key, IUIComponent root, TemplateResult built, DataTemplate template)
    {
        ParkedVisuals.Keep(ParkOwner, key, root, built, template, _lastArrangeSize);
        RemoveVisualChild(root);
        RemoveLogicalChild(root);
    }

    private bool IsOwnersOwnItem(IUIComponent visual) =>
        visual != null && TemplatedParent is ItemsControl owner && owner.Items.Contains(visual);

    private bool CanComeBack(object content) =>
        TemplatedParent is not ItemsControl owner || owner.Items.Contains(content);

    private void Release(IUIComponent visual, TemplateResult built)
    {
        // Our own children, not the visual's parent pointer: adoption does not empty this collection, and an element
        // another presenter adopted must keep its new home.
        if (built == null && !IsVisualChild(visual)) return;

        // Content authored in markup is not a template part, so nothing else announces its discard. Not an item of the
        // owner, though: the owner hands it back, and a discard is one way.
        if (!IsOwnersOwnItem(visual))
        {
            if (_discardBuf.Count > 0) _discardBuf.Clear();
            CollectForDiscard(visual);
            Core.DiscardedVisuals.Publish(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_discardBuf));
            _discardBuf.Clear();
        }

        RemoveVisualChild(visual);
        RemoveLogicalChild(visual);
        built?.Destroy();
    }

    /// <summary>Destroyed: lets the content go, since a root held only by these fields cannot be reached otherwise.</summary>
    protected override void OnDiscarded()
    {
        base.OnDiscarded();
        DropContentHandles();
    }

    private void DropContentHandles()
    {
        // Removed, not just forgotten: a root discarded while this presenter lives is still our child and would stay drawn.
        LetGo(_currentRoot);
        LetGo(_outgoingRoot);

        _currentRoot = null;
        _outgoingRoot = null;
        _currentTemplateResult = null;
        _outgoingTemplateResult = null;
        _currentTemplate = null;
    }

    private void LetGo(IUIComponent root)
    {
        if (root == null || !IsVisualChild(root)) return;
        RemoveVisualChild(root);
        RemoveLogicalChild(root);
    }

    // Weak, so the register does not keep a presenter alive.
    private static readonly List<WeakReference<ContentPresenter>> Presenters = new();
    private bool _registered;

    private void RegisterForDiscardSweep()
    {
        if (_registered) return;
        _registered = true;
        lock (Presenters) Presenters.Add(new WeakReference<ContentPresenter>(this));
    }

    // The presenter survives and its content is destroyed: the field is then the only thing still naming the root.
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

    private readonly List<IFundamentalUIComponent> _discardBuf = new();

    private void CollectForDiscard(IUIComponent node)
    {
        if (node is IFundamentalUIComponent fundamental) _discardBuf.Add(fundamental);
        foreach (var child in node.VisualChildren) CollectForDiscard(child);
    }

    private void StartTransition(Size size)
    {
        ClipToBounds = true;
        _transitionRunning = true;
        ContentTransitions.Run(ContentTransition, TransitionDuration, size, _currentRoot, _outgoingRoot, TransitionFinished);
    }

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
        // Only the generated label: an explicit write into an authored one would outrank its inheritance for good.
        if (!_textIsGenerated || _currentRoot is not TextBlock textBlock) return;
        textBlock.FontSize = FontSize;
        textBlock.TextTrimming = TextTrimming;
        if (Foreground != null) textBlock.Foreground = Foreground;
    }

    /// <summary>TEMP: where this type's own measure cost goes.</summary>
    public static double UpdateContentMs, CacheHitMs, BaseMeasureMs;
    public static int CacheHits, FullMeasures;

    protected override Size MeasureOverride(Size availableSize)
    {
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        _lastContentRebuilt = UpdateVisualContent(Content);
        var t1 = System.Diagnostics.Stopwatch.GetTimestamp();
        UpdateContentMs += System.Diagnostics.Stopwatch.GetElapsedTime(t0, t1).TotalMilliseconds;

        var sizeBefore = DesiredSize;
        // Reused data-only content keeps its size. The inner size is returned, not DesiredSize, which would add the
        // margin again on every skip; and only while the child is still the size the cache was made from.
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

        // A rebuild inside a directly requested measure leaves the parent with a size of content that no longer exists.
        if (_lastContentRebuilt)
        {
            (VisualParent as IMeasurableComponent)?.InvalidateMeasure();
        }

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // A pending transition is not "nothing changed": a kept view comes back arrange-valid at the same size, and
        // skipping swallowed its entrance.
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
