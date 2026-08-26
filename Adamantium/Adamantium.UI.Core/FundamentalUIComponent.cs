using System.Collections.Generic;
using System.Collections.Specialized;
using Adamantium.Core.Collections;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

public abstract class FundamentalUIComponent : AnimatableUIComponent, IFundamentalUIComponent
{
    private StylesCollection _attachedStyles;
    private Classes _classNames;
    private IFundamentalUIComponent parent;
    private TrackingCollection<IFundamentalUIComponent> logicalChildren;
    
    public static readonly AdamantiumProperty NameProperty = AdamantiumProperty.Register(nameof(Name),
        typeof(String), typeof(FundamentalUIComponent), new PropertyMetadata(String.Empty));
        
    public static readonly AdamantiumProperty StylesProperty =
            AdamantiumProperty.RegisterReadOnly(nameof(Styles), typeof(StylesCollection), typeof(FundamentalUIComponent));
    
    public static readonly AdamantiumProperty BehaviorsProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(Behaviors), typeof(BehaviorCollection), typeof(FundamentalUIComponent));

    public static readonly AdamantiumProperty TriggersProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(Triggers), typeof(TriggerCollection), typeof(FundamentalUIComponent));
    
    public static readonly AdamantiumProperty DataContextProperty = AdamantiumProperty.Register(nameof(DataContext),
        typeof(object), typeof(FundamentalUIComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits, DataContextChangedCallBack));
    
    public static readonly AdamantiumProperty ClassesProperty = AdamantiumProperty.Register(nameof(Classes),
        typeof(Classes), typeof(FundamentalUIComponent),
        new PropertyMetadata(new Classes(), ClassesChangedCallBack));
    
    public static readonly AdamantiumProperty UidProperty = AdamantiumProperty.Register(nameof(Id),
        typeof(String), typeof(FundamentalUIComponent), new PropertyMetadata(String.Empty));
    
    public static readonly AdamantiumProperty AllowDropProperty = AdamantiumProperty.Register(nameof(AllowDrop),
        typeof(Boolean), typeof(FundamentalUIComponent), new PropertyMetadata(true));

    private static void DataContextChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumAdamantiumComponent as FundamentalUIComponent;
        o?.DataContextChanged?.Invoke(o, e);
        o?.UpdateDataContext();
    }
    
    private static void ClassesChangedCallBack(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
    {
        var o = adamantiumAdamantiumComponent as FundamentalUIComponent;
        if (o == null) return;

        if (e.OldValue is Classes oldClasses)
        {
            oldClasses.CollectionChanged -= o.ClassesCollectionChanged;
        }

        if (e.NewValue is Classes newClasses)
        {
            newClasses.CollectionChanged += o.ClassesCollectionChanged;
        }

        o.SyncClassNames();
    }

    // ClassNames, Styles, _attachedStyles, Behaviors and Triggers used to be built in the constructor for EVERY
    // component - 784 of a bare Border's 2824 bytes, spent on collections a template-stamped element never fills.
    //
    // The GETTERS materialise, not just the setters, and that is load-bearing: markup writes
    // <Button.Behaviors><local:X/></Button.Behaviors>, which the generator emits as `element.Behaviors.Add(x)` - a READ,
    // and returning null there would crash the app on ordinary markup. Engine code that only needs to know WHETHER
    // anything is there must use the Has* members or the raw field; every such site is marked.

    public BehaviorCollection Behaviors
    {
        get
        {
            var behaviors = GetValue<BehaviorCollection>(BehaviorsProperty);
            if (behaviors == null)
            {
                SetValue(BehaviorsProperty, behaviors = new BehaviorCollection(this));
            }

            return behaviors;
        }
    }

    private List<ITriggerActivator> _triggerActivators;

    /// <summary>The activators the STYLES applied to this component, as opposed to <see cref="_triggerActivators"/>
    /// which are its own element triggers. A plain field for the same reason that one is: the attach walk asks every
    /// node for it, and for most of them the answer is null.</summary>
    internal List<ITriggerActivator> StyleActivators;

    /// <summary>
    /// Triggers declared directly on this control - the logical, theme-independent layer (vs template/style triggers
    /// which belong to a theme). They act on the control itself (self scope), are applied when it joins a live logical
    /// tree, and are deactivated when it leaves.
    /// </summary>
    public TriggerCollection Triggers
    {
        get
        {
            var triggers = GetValue<TriggerCollection>(TriggersProperty);
            if (triggers == null)
            {
                SetValue(TriggersProperty, triggers = new TriggerCollection());
            }

            return triggers;
        }
    }

    /// <summary>The triggers WITHOUT materialising them - null when this component never declared any. `ApplyTriggers`
    /// runs on every attach for every node, so reading it through <see cref="Triggers"/> would build a collection for
    /// each one just to find it empty, which is the whole cost this laziness exists to avoid.</summary>
    private TriggerCollection TriggersOrNull => GetValue<TriggerCollection>(TriggersProperty);

    /// <summary>What the `DataContext` callback actually does is raise <see cref="DataContextChanged"/> and re-resolve
    /// this element's bindings. An element with neither has nothing to be told, and the walk goes straight to its
    /// children - which is where the bindings usually are. Every other property answers as before.</summary>
    protected override bool NeedsInheritedCallback(AdamantiumProperty property)
        => property != DataContextProperty
           || DataContextChanged != null
           || BindingEngine.HasBindings(this);

    private void UpdateDataContext()
    {
        // Re-resolve this element's own bindings against the new context. Propagation to logical children is handled by
        // value inheritance (resolved on read from the InheritanceParent chain), not an explicit push — which also keeps a
        // child's locally-set DataContext intact and works regardless of build order.
        BindingEngine.RefreshBindings(this);
    }

    private void StylesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                // Route through AttachStyles (not a bare Style.Apply) so a style added to the Styles collection is TRACKED
                // in _attachedStyles. That makes it participate in the theme cycle: on (re)theming, ApplyCurrentTheme
                // detaches it, applies the theme, then re-applies it via ApplyExternalStyles - so a user style always
                // lands AFTER the theme, even if it was added before the control was themed (e.g. an ItemContainerStyle
                // set at container creation). A bare Apply left it stacked BEFORE the theme, so the theme's value won.
                var styles = e.NewItems?.Cast<Style>().ToArray();
                if (styles is { Length: > 0 }) AttachStyles(styles);
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                // OldItems for a Remove (NewItems is null here); detach so the tracked style + its contribution are undone.
                var styles = e.OldItems?.Cast<Style>().ToArray();
                if (styles is { Length: > 0 }) DetachStyles(styles);
                break;
            }
        }
    }

    private void ClassesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        SyncClassNames();
    }

    // The settable Classes property (markup/binding) carries the author's intent; ClassNames is the collection selectors
    // read (Selector.Match). Mirror one into the other so a class set in markup - e.g. <ProgressBar Classes="Ring"/> -
    // actually activates the ".Ring" styles. Without this the two collections drift and class selectors never match.
    private void SyncClassNames()
    {
        // Nothing to mirror INTO and nothing to mirror FROM: don't build the collection just to discover that. The guard
        // used to be `ClassNames == null` (the default-value callback can fire from the base ctor); a materialising
        // getter would have made that dead code AND allocated on every component that never names a class.
        if (_classNames == null && Classes.Count == 0)
        {
            return;
        }

        ClassNames.Clear();
        foreach (var name in Classes)
        {
            ClassNames.Add(name);
        }
    }
    
    public Boolean AllowDrop
    {
        get => GetValue<Boolean>(AllowDropProperty);
        set => SetValue(AllowDropProperty, value);
    }

    public Classes Classes
    {
        get => GetValue<Classes>(ClassesProperty);
        set => SetValue(ClassesProperty, value);
    }

    public object DataContext
    {
        get => GetValue(DataContextProperty);
        set => SetValue(DataContextProperty, value);
    }

    private bool _viewModelApplied;

    /// <summary>
    /// The view-model type declared in markup via <c>x:ViewModel</c>. Generated code-behind overrides this to return
    /// the authored type; the default is null. It is pure metadata - the control does NOT hold a container. When the
    /// element becomes live (attached to the tree / window initialized) the framework resolves an instance from the
    /// app's dependency resolver and assigns it as <see cref="DataContext"/> (see <see cref="ApplyViewModel"/>).
    /// </summary>
    public virtual Type ViewModelType => null;

    /// <summary>What <c>x:KeepAlive</c> declared in markup, generated code-behind overriding it the way it overrides
    /// <see cref="ViewModelType"/>. Pure metadata again: the view states what it wants, and whoever navigates away from
    /// it - not the view itself - decides to park it instead of dropping it.</summary>
    public virtual NavigationCacheMode KeepAlive => NavigationCacheMode.Disabled;

    /// <summary>
    /// Resolves the <see cref="ViewModelType"/> (if any) from the application's dependency resolver and assigns it as
    /// the DataContext - but only the first time, and only if the DataContext wasn't set explicitly (so x:ViewModel is
    /// the default, an explicit DataContext or binding still wins, which keeps a view reusable across view-models).
    /// Reached through the same ambient <see cref="UIAppContext.Current"/> the framework already uses for theming, so
    /// the control never stores the container. No-op when no resolver is available (e.g. the headless designer).
    /// </summary>
    protected void ApplyViewModel()
    {
        if (_viewModelApplied) return;

        var viewModelType = ViewModelType;
        if (viewModelType == null) return;

        if (HasExplicitValue(DataContextProperty))
        {
            _viewModelApplied = true;   // caller already provided a DataContext; don't override it
            return;
        }

        var context = UIAppContext.Current?.UIContext;
        if (context == null) return;    // app context not ready yet; a later attach will retry

        DataContext = context.Resolve(viewModelType);
        _viewModelApplied = true;
    }

    public string Id
    {
        get => GetValue<string>(UidProperty);
        set => SetValue(UidProperty, value);
    }

    public Classes ClassNames => _classNames ??= new Classes();

    /// <summary>Whether this component has any class names, WITHOUT materialising the collection. Style matching and the
    /// theme cache ask this per component, and for most of them the answer is no.</summary>
    public bool HasClassNames => _classNames is { Count: > 0 };

    public StylesCollection Styles
    {
        get
        {
            var styles = GetValue<StylesCollection>(StylesProperty);
            if (styles == null)
            {
                styles = new StylesCollection();
                styles.CollectionChanged += StylesOnCollectionChanged;
                SetValue(StylesProperty, styles);
            }
            return styles;
        }
    }

    /// <summary>The local styles WITHOUT materialising them - null when none were ever added.</summary>
    private StylesCollection StylesOrNull => GetValue<StylesCollection>(StylesProperty);
    
    public bool IsStyleApplied { get; private set; }

    /// <summary>Where this element stands between being built and being released - see <see cref="VisualLifecycle"/>.
    /// Set by whoever performs the transition, which is the only place that knows: "out of the tree" cannot tell a
    /// destroyed part from one merely moving into the new template, from one parked to come back, from a pooled item
    /// container - and judging by that left the toolbar and the tab strip wearing the old theme.</summary>
    public VisualLifecycle Lifecycle { get; private set; } = VisualLifecycle.Live;

    /// <summary>Destroyed for good - the one state in which what holds this element may let go of it. DERIVED from
    /// <see cref="Lifecycle"/> rather than stored: as a stored flag it was only ever set, so a parked keep-alive view
    /// came back still answering "destroyed" and was released under the user.</summary>
    public bool IsDiscarded => Lifecycle == VisualLifecycle.Discarded;

    /// <summary>Deliberately out of the tree and coming back - parked, or waiting in a container pool. Not dead, and
    /// nothing about it may be released.</summary>
    public bool IsAwaitingReturn => Lifecycle is VisualLifecycle.Parked or VisualLifecycle.Recycled;

    /// <summary>A teardown has STARTED on this element. It may still be taking part in the rebuild replacing it, so
    /// nothing may be released yet - that is what separates this from <see cref="VisualLifecycle.Discarded"/>.</summary>
    public void MarkDetaching()
    {
        if (Lifecycle == VisualLifecycle.Live) Lifecycle = VisualLifecycle.Detaching;
    }

    /// <summary>Parked: out of the tree on purpose, returning through the same host (<c>x:KeepAlive</c>).</summary>
    public void MarkParked()
    {
        if (Lifecycle != VisualLifecycle.Discarded) Lifecycle = VisualLifecycle.Parked;
    }

    /// <summary>Pooled by an item container generator, to be re-bound to another item.</summary>
    public void MarkRecycled()
    {
        if (Lifecycle != VisualLifecycle.Discarded) Lifecycle = VisualLifecycle.Recycled;
    }

    /// <summary>It came back. Called wherever an element (re)enters the visual tree, which is the one event that
    /// settles the question for every way of leaving at once - a re-parent, an unpark, a recycled container taking a
    /// new item. Without this the states that mean "coming back" would be indistinguishable from death the moment
    /// anything asked a second time.
    /// <para>Discarded is final and is NOT revived here: an element that really was destroyed and then somehow got
    /// re-attached is a bug to find, not a state to paper over.</para></summary>
    public void Revive()
    {
        if (Lifecycle != VisualLifecycle.Discarded) Lifecycle = VisualLifecycle.Live;
    }

    /// <summary>Which template BUILT this element, by that result's id. TemplatedParent cannot answer it: an
    /// ItemsPanelTemplate stamps the same templated parent on the items panel it makes, so a control's teardown would
    /// take the presenter's live panel for one of its own parts - and did, marking a live panel discarded.</summary>
    public Guid OwningTemplateId { get; internal set; }

    public void MarkDiscarded()
    {
        if (IsDiscarded || IsAwaitingReturn) return;

        Lifecycle = VisualLifecycle.Discarded;
        lock (Discarded) Discarded.Add(new WeakReference<FundamentalUIComponent>(this));

        // The WORK is not done here. Releasing a subtree's subscriptions costs a walk per element, and doing it inside
        // the frame that swaps the content is what made switching to a heavy tab stall for seconds. It is queued and
        // drained in the idle time between frames, where the drain also gets to re-read the state: anything that has
        // come back by then is no longer Discarded and is simply skipped. That is the second half of the fix - not only
        // WHAT is released, but WHEN, and the queue is the only place that can answer the second one honestly.
        DiscardedVisuals.Enqueue(this);
    }

    /// <summary>Run the release for this element, from the queue drain. Re-reads the state first: between being queued
    /// and being reached it may have come back, and the whole point of draining late is to let it.</summary>
    internal void ReleaseFromQueue()
    {
        if (Lifecycle != VisualLifecycle.Discarded) return;
        OnDiscarded();
    }

    /// <summary>This element has been destroyed - let go of anything OUTSIDE it that would otherwise keep it. Overridden
    /// by whoever subscribes to something longer-lived than itself: a control bound to a view model's collection is the
    /// case that made this necessary - the subscription is undone only when the SOURCE is replaced, which never happens
    /// to something discarded, so the collection went on holding the control for the life of the application.</summary>
    protected virtual void OnDiscarded()
    {
        // Close this element's BINDINGS. An expression subscribes to its SOURCE, and a source is normally longer-lived
        // than the target - an ancestor, a view model - so a binding left open on a destroyed element keeps that element
        // alive from the live side of the tree. CloseConnection is called when a binding is REPLACED or when a template
        // result is destroyed, and neither reaches a binding created outside a template (a behaviour's, an authored
        // element's): measured, a live ListBox's PropertyChanged held an AncestorBindingExpression whose target was a
        // discarded Border, and through it a whole subtree.
        var bindings = Data.BindingEngine.GetBindings(this);
        foreach (var binding in bindings) binding.CloseConnection();

        // ...and the BEHAVIOURS, which carry bindings of their own. A binding is keyed by its TARGET, and a behaviour is
        // not a component, so the sweep above cannot see one: a DragSourceBehavior's {Ancestor} binding stayed open, its
        // source (a live ListBox) went on holding the expression, and the expression held the behaviour's element and
        // everything under it. Clearing the collection detaches each one through the path that already exists.
        var behaviours = GetValue<Collections.BehaviorCollection>(BehaviorsProperty);
        if (behaviours == null || behaviours.Count == 0) return;

        foreach (var behaviour in behaviours)
            foreach (var binding in Data.BindingEngine.GetBindings(behaviour))
                binding.CloseConnection();

        behaviours.Clear();
    }

    // TEMP (leak hunt): every part the teardown has destroyed, held WEAKLY. After a forced collection the ones still
    // alive are exactly the retained set - not inferred from a dump, not a path gcroot happened to walk, but the parts
    // that are provably dead and provably still here.
    private static readonly List<WeakReference<FundamentalUIComponent>> Discarded = new();

    /// <summary>TEMP: of the parts that were destroyed, how many survive a collection - and WHO their visual parent is.
    /// A survivor whose parent is NOT itself discarded is held by a live control, and that names the holder outright.</summary>
    public static string SurvivingDiscarded()
    {
        var byParent = new Dictionary<string, int>();
        var alive = 0;

        lock (Discarded)
        {
            for (var i = Discarded.Count - 1; i >= 0; i--)
            {
                if (!Discarded[i].TryGetTarget(out var part))
                {
                    Discarded.RemoveAt(i);
                    continue;
                }

                alive++;
                var parent = (part as IUIComponent)?.VisualParent;
                var key = parent == null
                    // A survivor with no visual parent is the ROOT of a retained subtree - the thing actually being
                    // held. Its own type names whose template leaked, which is what the next fix needs.
                    ? "ROOT: " + part.GetType().Name
                    : parent.GetType().Name +
                      (parent is FundamentalUIComponent { IsDiscarded: true } ? " (also discarded)" : " *** LIVE ***");
                byParent[key] = byParent.TryGetValue(key, out var had) ? had + 1 : 1;
            }
        }

        var rows = new List<string>();
        foreach (var pair in byParent) rows.Add($"{pair.Value,6}  {pair.Key}");
        rows.Sort((a, b) => int.Parse(b.Trim().Split(' ')[0]).CompareTo(int.Parse(a.Trim().Split(' ')[0])));
        return $"surviving discarded parts: {alive}\n  " + string.Join("\n  ", rows.GetRange(0, Math.Min(12, rows.Count)));
    }


    public void AttachStyles(params ReadOnlySpan<Style> styles)
    {
        foreach (var style in styles)
        {
            style.Attach(this);
            // Track so DetachStyles can undo them (re-theming detaches the previously-applied set first - see
            // ApplyCurrentTheme). Guard against duplicates so a re-attach doesn't record the same style twice.
            _attachedStyles ??= new StylesCollection();
            if (!_attachedStyles.Contains(style))
            {
                _attachedStyles.Add(style);
            }
        }
    }

    public void DetachStyles()
    {
        if (_attachedStyles == null)
        {
            return;   // nothing was ever attached
        }

        foreach (var style in _attachedStyles)
        {
            style.Detach(this);
        }
        _attachedStyles.Clear();
    }

    public void DetachStyles(params ReadOnlySpan<Style> styles)
    {
        foreach (var style in styles)
        {
            style.Detach(this);
            _attachedStyles?.Remove(style);
        }
    }

    public IFundamentalUIComponent LogicalParent => parent;
    
    public IAdamantiumComponent TemplatedParent { get; internal set; }

    public void RemoveBinding(string property)
    {
        var adamantiumProperty = GetProperty(property);
    }

    public void RemoveBinding(AdamantiumProperty property)
    {
        throw new NotImplementedException();
    }

    public event AdamantiumPropertyChangedEventHandler DataContextChanged;
    
    public IReadOnlyCollection<IFundamentalUIComponent> LogicalChildren => LogicalChildrenCollection.AsReadOnly();
    
    protected TrackingCollection<IFundamentalUIComponent> LogicalChildrenCollection
    {
        get
        {
            if (logicalChildren == null)
            {
                var list = new TrackingCollection<IFundamentalUIComponent>();
                LogicalChildrenCollection = list;
            }
            return logicalChildren;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (logicalChildren != value && logicalChildren != null)
            {
                logicalChildren.CollectionChanged -= LogicalChildrenCollectionChanged;
            }

            logicalChildren = value;
            logicalChildren.CollectionChanged += LogicalChildrenCollectionChanged;
        }

    }
    
    public String Name
    {
        get => GetValue<String>(NameProperty);
        set => SetValue(NameProperty, value);
    }

    public void AddLogicalChild(IFundamentalUIComponent child)
    {
        LogicalChildrenCollection.Add(child);
    }
    
    public void RemoveLogicalChild(IFundamentalUIComponent child)
    {
        LogicalChildrenCollection.Remove(child);
    }
    
    private void LogicalChildrenCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                SetLogicalParent(e.NewItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Remove:
                ClearLogicalParent(e.OldItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Replace:
                ClearLogicalParent(e.OldItems.Cast<FundamentalUIComponent>());
                SetLogicalParent(e.NewItems.Cast<FundamentalUIComponent>());
                break;

            case NotifyCollectionChangedAction.Reset:
                throw new NotSupportedException("Reset should not be signalled on LogicalChildren collection");
        }
    }

    private void SetLogicalParent(IEnumerable<FundamentalUIComponent> children)
    {
        foreach (var element in children)
        {
            element.SetParent(this);
        }
    }

    private void ClearLogicalParent(IEnumerable<FundamentalUIComponent> children)
    {
        foreach (var element in children)
        {
            if (element.LogicalParent == this)
            {
                element.SetParent(null);
            }
        }
    }

    /// <summary>
    /// Sets the control's logical parent.
    /// </summary>
    /// <param name="logicalParent">The parent.</param>
    private void SetParent(IFundamentalUIComponent logicalParent)
    {
        var old = LogicalParent;

        if (logicalParent != old)
        {
            if (old != null && logicalParent != null)
            {
                // Re-parenting a MOVED element (old AND new both non-null): a re-theme rebuilds a control's template and
                // re-homes the SAME element content from the torn-down ContentPresenter into the new one without detaching
                // it first. Rather than throw, detach from the previous parent so the move succeeds. RemoveLogicalChild
                // fires the detach (via ClearLogicalParent -> SetParent(null), which sets parent=null and raises the
                // detach event) and keeps the old collection consistent; null the local so the detach isn't raised twice.
                old.RemoveLogicalChild(this);
                old = null;
            }

            parent = logicalParent;
            // Wire value inheritance (DataContext and any other Inherits property) to the new logical parent; null on
            // detach. This is what carries a window's DataContext down to its children so their {Binding}s resolve.
            InheritanceParent = logicalParent as AdamantiumComponent;

            //var root = FindStyleRoot(old);

            if (old != null)
            {
                RaiseDetachedFromLogicalTree(new LogicalTreeAttachmentEventArgs(LogicalParent));
            }

            if (parent != null)
            {
                ApplyCurrentTheme();

                RaiseAttachedToLogicalTree(new LogicalTreeAttachmentEventArgs(parent));
            }
        }
    }

    /// <summary>TEMP (leak hunt): how many times a theme has been applied to an element. A swap builds 2.4x as many
    /// templates as the whole application contains, and singletons - the one TitleBar, the one ResizeGripper - are
    /// rebuilt exactly TWICE, so the question is whether the theme is applied to each element twice.</summary>
    public static long ThemeApplications;

    public virtual void ApplyCurrentTheme()
    {
        if (UIAppContext.Current == null)
            return;

        System.Threading.Interlocked.Increment(ref ThemeApplications);

        // Re-theming must undo what the PREVIOUS set left behind (a theme swap re-applies without a preceding detach, and
        // its activators carry live subscriptions) - but ONLY what is genuinely LEAVING. Detaching everything up front
        // dropped each setter's value for the length of the call, and a property that falls back to its default and
        // returns is a property that CHANGED, twice, with every callback firing both times.
        // That is not academic: the applicable set does not change when a control is merely RE-PARENTED (selectors match
        // on type/id/class - never on the ancestor chain), yet SetParent re-themes. Measured in docking - one dock-back
        // put a group's ItemsPanel through theme -> default -> theme, and each write rebuilt the items panel, so the tabs
        // ended up in a panel the layout pass no longer descends into, wearing the positions of their previous life.
        //
        // The leavers still go FIRST, before the incoming set is applied. Applying first and cleaning up afterwards looks
        // tidier and is wrong: a marker setter ({Binding}, {ThemeResource}, {Ancestor}, {Self}) is undone by property
        // alone, with no style key, so the outgoing theme's teardown would tear out the incoming theme's live link.
        var incoming = UIAppContext.Current.ThemeManager?.FindStylesForComponent(this);
        var own = StylesOrNull;   // not Styles: a component with no local styles must not grow one to be re-themed

        // BACKWARDS by index rather than over a ToArray() copy: DetachStyles removes from this very collection, so a
        // forward walk would skip entries - which is what the copy was there to avoid, at the price of an array per
        // component per re-theme. Going backwards, a removal only shifts what has already been visited.
        for (var i = (_attachedStyles?.Count ?? 0) - 1; i >= 0; i--)
        {
            var style = _attachedStyles[i];
            if (incoming != null && Array.IndexOf(incoming, style) >= 0)
            {
                continue;
            }

            if (own != null && own.Contains(style))
            {
                continue;   // an author's own style stays; re-applied below either way
            }

            DetachStyles(style);
        }

        UIAppContext.Current.UIContext.ThemeContext.ApplyCurrentTheme(this);
        UIAppContext.Current.UIContext.ThemeContext.ApplyExternalStyles(this, own == null ? default : own.AsSpan());

        IsStyleApplied = true;
    }
    
    public void InvalidateStyles()
    {
        if (UIAppContext.Current == null)
            return;

        InvalidateStylesCore([]);
    }

    // A node can be reached by BOTH the logical and the visual walk below (content is usually a child in both trees), so
    // recurse through a shared visited-set to keep the combined traversal linear instead of exponential in tree depth.
    private void InvalidateStylesCore(HashSet<IFundamentalUIComponent> visited)
    {
        if (!visited.Add(this)) return;

        IsStyleApplied = false;

        // Register for re-theming on the next layout pass instead of relying on a per-frame full-tree walk to notice
        // the cleared flag. The style queue is drained at the START of the pass, BEFORE measure, because applying a
        // theme can swap a control's Template - changing the very subtree that then gets measured. (Attaching a node to
        // the tree still applies its theme synchronously; this queue path covers a bulk re-theme, e.g. a theme swap.)
        if (this is IUIComponent component)
        {
            LayoutManager.For(component).InvalidateStyle(component);
        }

        foreach (var child in LogicalChildrenCollection)
        {
            (child as FundamentalUIComponent)?.InvalidateStylesCore(visited);
        }

        // Cross the template boundary too: template parts (and the content they host) are attached as VISUAL children
        // via AddTemplateChild, NOT as logical children, so a purely logical walk stops at a templated control and never
        // re-themes its title bar / content presenter / hosted content. On a theme swap that left them with the OLD
        // theme's resolved {ThemeResource}/{ResourceReference} setter values (only live {ObservableResource}s updated,
        // via the global resource-change flush). Walking visual children re-themes the whole rendered subtree.
        if (this is IUIComponent visual)
        {
            foreach (var child in visual.VisualChildren)
            {
                (child as FundamentalUIComponent)?.InvalidateStylesCore(visited);
            }
        }
    }

    // A graft moves a whole SUBTREE into the tree, not just the node whose parent was set: what an element deep inside it
    // can see ABOVE itself changes too, and only the root is told. The visual side already carries its attach down the
    // subtree (UIComponent.AttachedToVisualTree); the logical side has to as well, or anything that resolves against its
    // logical ancestry from inside a subtree built detached - an {Ancestor Logical=True} in a popup's ChildTemplate, say -
    // is established while the subtree is still rootless and is never told to look again.
    private void RaiseAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        OnAttachedToLogicalTree(e);
        foreach (var child in LogicalChildrenCollection)
        {
            (child as FundamentalUIComponent)?.RaiseAttachedToLogicalTree(e);
        }
    }

    private void RaiseDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        OnDetachedFromLogicalTree(e);
        foreach (var child in LogicalChildrenCollection)
        {
            (child as FundamentalUIComponent)?.RaiseDetachedFromLogicalTree(e);
        }
    }

    protected virtual void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        // A nested view (created via 'new' by its parent's generated code) declares its view-model with x:ViewModel;
        // resolve it now that the element is part of a live tree and the app context is reachable.
        ApplyViewModel();
        ApplyTriggers();
        // Raise the public event so consumers can react to LOGICAL attach - the hook a non-visual element (which never
        // enters the visual tree, so AttachedToVisualTreeEvent never fires for it) uses to tree-scope its resources.
        AttachedToLogicalTree?.Invoke(this, e);
    }

    protected virtual void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        DeactivateTriggers();
        DetachedFromLogicalTree?.Invoke(this, e);
    }

    // Logical (control-level) triggers: activated against the control itself (StyleTriggerExecutionContext = self scope),
    // not template parts. Idempotent - skipped if already applied (e.g. a re-attach without an intervening detach).
    private void ApplyTriggers()
    {
        // TriggersOrNull, not Triggers: this runs on every attach for every node, and the property MATERIALISES.
        var triggers = TriggersOrNull;
        if (_triggerActivators != null || triggers == null || triggers.Count == 0)
            return;

        var theme = UIAppContext.Current?.ThemeManager?.CurrentTheme;
        _triggerActivators = new List<ITriggerActivator>();
        foreach (var trigger in triggers)
        {
            _triggerActivators.Add(trigger.Apply(new StyleTriggerExecutionContext(this, theme)));
        }
    }

    /// <summary>The component left the visual tree: stop what its triggers left RUNNING, without forgetting their state.
    /// A page navigated away used to keep its loading pulses ticking for the rest of the session, and every frame paid
    /// for all of them (measured: 25 orphans off one page, render rate a quarter of what it was).</summary>
    protected void SuspendTriggerActions()
    {
        if (_triggerActivators != null)
            foreach (var activator in _triggerActivators)
                activator?.SuspendActions();

        Style.SuspendActivators(this);
    }

    /// <summary>...and it is back: re-run what suspending stopped, where the condition still holds.</summary>
    protected void ResumeTriggerActions()
    {
        if (_triggerActivators != null)
            foreach (var activator in _triggerActivators)
                activator?.ResumeActions();

        Style.ResumeActivators(this);
    }

    private void DeactivateTriggers()
    {
        if (_triggerActivators == null)
            return;

        foreach (var activator in _triggerActivators)
            activator.Deactivate();
        _triggerActivators = null;
    }

    // Re-wire the trigger activators that may reach template parts after this control's template changed - both its own
    // element triggers and any style triggers attached to it. Each undoes what it applied to the OLD parts (and its
    // subscriptions) and re-evaluates against the new template, so a runtime template swap never leaks the old parts or
    // leaves stale trigger values behind. No-op in the common case (no triggers, or nothing currently applied).
    protected void ReevaluateTriggersForTemplateChange()
    {
        if (_triggerActivators != null)
        {
            foreach (var activator in _triggerActivators)
            {
                // Only re-point triggers that reach the (now-swapped) template parts. One that touches only the host's
                // own properties is template-independent; re-pointing it is needless and, for a setter on Template
                // itself, would re-swap the template and recurse.
                if (activator is not { TargetsTemplateParts: true }) continue;
                activator.Deactivate();
                activator.Activate();
            }
        }

        Style.ReevaluateActivators(this);
    }

    /// <summary>
    /// Raised when the control is attached to a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> AttachedToLogicalTree;

    /// <summary>
    /// Raised when the control is detached from a rooted logical tree.
    /// </summary>
    public event EventHandler<LogicalTreeAttachmentEventArgs> DetachedFromLogicalTree;

    public void VerifyAccess()
    {
        UIAppContext.Current.Dispatcher.VerifyAccess();
    }

    public bool CheckAccess() => UIAppContext.Current.Dispatcher.CheckAccess();
}
