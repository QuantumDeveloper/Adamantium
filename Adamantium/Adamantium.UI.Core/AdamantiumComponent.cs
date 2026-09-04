using System.Collections.Concurrent;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

public abstract class AdamantiumComponent : IAdamantiumComponent
{
    public UInt128 Uid { get; set; }

    // The properties this component has actually been GIVEN a value for - created on first write, not one per registered
    // property. A control registers ~65 and is given about five, so seeding a container for each cost ~172 bytes per
    // DECLARED property per instance: a bare Border was 12.7 KB before anything was put in it, and a 4K tile grid of
    // 60 000 components carried 533 MB of them. The collector then had to walk that graph, which is where a quarter to a
    // half of every second went (see the server-GC note in the sandbox csproj).
    //
    // CONCURRENT, because it grows: growing a plain Dictionary while another thread reads it is a torn read, and reads
    // happen on worker threads (the parallel arrange). Reads stay lock-free at any concurrency level, so ONE lock rather
    // than one per processor cannot regress them - and the default's 16-core striping cost 1016 bytes per component to
    // guard contention that cannot occur: this map belongs to ONE component.
    //
    // Keyed by the property OBJECT. Re-keying by the globally unique PropertyId to dodge the comparer's virtual calls was
    // tried and MEASURED SLOWER (58ns a read against 46), so it stays.
    //
    // LAZY: a component never given an explicit value reads everything from declared defaults and needs no map at all.
    // Built like attachedValues (one CompareExchange), so two threads racing to write the first property share one map.
    private ConcurrentDictionary<AdamantiumProperty, ValueContainer> values;

    /// <summary>How many properties this component actually has slots for - the number that says whether the map's
    /// capacity is anywhere near right. Diagnostics only.</summary>
    internal int ValueSlotCount => values?.Count ?? 0;

    // ---- Render attachments (see IRenderAttachable) --------------------------------------------------------------
    // A value that draws this element (a Brush) keeps the element in a map of its owners AND subscribed to its Changed
    // event, so a later mutation OF the value repaints whoever paints with it. That link was taken when the property
    // took the value and given up only when the property took a DIFFERENT one - which never happens to an element that
    // is simply DISCARDED. A theme swap discards a whole template's worth of them, and both themes live as long as the
    // application (ThemeManager keeps its map), so every element ever built stayed in a live theme brush's owner map:
    // measured on the stand, +20.1 MB per swap, dead linear over eight swaps, ~6900 elements retained each time.
    //
    // So the link follows the TREE, like every other thing an element holds: given up when it leaves, taken again when
    // it comes back. Symmetric with no special case for parking - a parked subtree leaves and returns through these
    // very calls, and special-casing it would leave the eviction path holding.
    private bool _renderAttachmentsReleased;

    /// <summary>True while this component has given up its render attachments (it is out of the tree). The property
    /// system asks, so a value written WHILE OUT does not re-take a link that leaving already gave up - that would
    /// double the owner's hold count and leaving would only ever undo half of it.</summary>
    public bool RenderAttachmentsReleased => _renderAttachmentsReleased;

    internal void ReleaseRenderAttachments()
    {
        if (_renderAttachmentsReleased) return;
        _renderAttachmentsReleased = true;
        ForEachRenderAttachment(static (value, owner) => value.DetachFrom(owner));
    }

    internal void TakeRenderAttachments()
    {
        if (!_renderAttachmentsReleased) return;
        _renderAttachmentsReleased = false;
        ForEachRenderAttachment(static (value, owner) => value.AttachTo(owner));
    }

    // Driven by the value maps rather than by the type's registered properties: only a property that was actually GIVEN
    // a value ever raised the change that took the link, so a declared DEFAULT brush was never attached and must not
    // start being. Attached properties carry values too, hence both maps.
    private void ForEachRenderAttachment(Action<IRenderAttachable, AdamantiumComponent> act)
    {
        Walk(values);
        Walk(attachedValues);

        void Walk(ConcurrentDictionary<AdamantiumProperty, ValueContainer> map)
        {
            if (map == null) return;
            foreach (var property in map.Keys)
            {
                if (!property.CanAttachToOwner) continue;
                if (GetValue(property) is IRenderAttachable attachable) act(attachable, this);
            }
        }
    }

    // ATTACHED properties are the only ones that turn up later - they belong to another type and this one has no slot
    // for them until somebody sets one. Lazily created, so the components that never see an attached property (nearly
    // all of them) pay nothing for it.
    private ConcurrentDictionary<AdamantiumProperty, ValueContainer> attachedValues;

    // LAZY. Most elements never receive a style or a trigger value, and an empty Dictionary is 80 bytes each - 160 of
    // the 2976 a Border costs to construct. Created by the first writer; every reader treats null as "nothing here",
    // which is the same answer an empty map gave.
    private Dictionary<string, StyleValueContainer> styleValues;

    // Per-property stack of trigger contributions (token -> value), so several triggers can target one property without
    // clobbering each other - leaving the top one restores the one beneath. The single Trigger slot in `values` holds
    // only the stack's current top. See SetTriggerValue/ClearTriggerValue.
    private Dictionary<string, TriggerValueContainer> triggerValues;

    private AdamantiumComponent inheritanceParent;

    // Types whose declared defaults have already been checked. The check is about the TYPE - a registration's default
    // against that registration's validator - and its answer cannot differ between two instances, so it belongs once per
    // type rather than ~65 delegate invocations per component built.
    private static readonly ConcurrentDictionary<Type, bool> ValidatedDefaults = new();

    protected AdamantiumComponent()
    {
        // NOTHING is allocated per property here any more. A container is what holds a VALUE, and a component that has
        // not been given one has nothing to hold: its properties read as their declared defaults, which is precisely what
        // GetValue's cold path already returns when no container exists (see GetValue/GetDefaultValue). The defaults were
        // also PUBLISHED into every container as the effective value - re-stating, sixty-five times per element, what the
        // metadata already says.
        //
        // The default is still never COERCED at construction: coercion answers "given this object's current state, what
        // does that request become", and asking a half-built object was how one instance's state came to decide the
        // default for every other instance of its type. A default stands as authored until something is actually set.
        ValidatedDefaults.GetOrAdd(GetType(), static type =>
        {
            foreach (var property in AdamantiumPropertyMap.GetRegisteredArray(type))
            {
                var metadata = property.GetDefaultMetadata(type);
                if (property.ValidateValueCallBack?.Invoke(metadata.DefaultValue) == false)
                {
                    throw new ArgumentException($"Value {metadata} is incorrect!");
                }
            }

            return true;
        });
    }

    // The slots of a property, or null when this component has never been given a value for it. Null is an ANSWER, not a
    // gap: the caller resolves the declared default (GetValue's cold path), which is what the seeded container used to
    // hold anyway. Lock-free on both maps.
    private ValueContainer Slots(AdamantiumProperty property)
    {
        var own = values;
        if (own != null && own.TryGetValue(property, out var container))
        {
            return container;
        }

        var attached = attachedValues;
        return attached != null && attached.TryGetValue(property, out var slot) ? slot : null;
    }

    // ...and the slots to WRITE into, brought into being on demand. A registered property earns its container the first
    // time it is given a value; an attached one belongs to another type and lives in its own lazy map.
    private ValueContainer EnsureSlots(AdamantiumProperty property)
    {
        var own = values;
        if (own != null && own.TryGetValue(property, out var container))
        {
            return container;
        }

        if (!property.IsAttached)
        {
            // Registered on this type? Then it may be written, and this is where its container starts existing. Anything
            // else is not ours and must stay null - SetValue's ValidateProperty has already refused it, and returning a
            // container for it would silently accept a write nobody could ever read back.
            if (!AdamantiumPropertyMap.IsRegistered(this, property)) return null;

            if (own == null)
            {
                // Capacity 16, not 31: measured on a real laid-out scene, a component ends up with 4-8 slots
                // (ContentPresenter 7, Border 6, Rectangle 6, ItemsControl 8, WrapPanel 8) - 31 buckets were four times
                // the need and cost 480 bytes against 320. Sixteen still leaves headroom over the observed maximum, and
                // exceeding it costs one doubling rather than a wrong answer.
                Interlocked.CompareExchange(ref values,
                    new ConcurrentDictionary<AdamantiumProperty, ValueContainer>(concurrencyLevel: 1, capacity: 16), null);
                own = values;
            }

            return own.GetOrAdd(property, SeedFactory, this);
        }

        var map = attachedValues;
        if (map == null)
        {
            Interlocked.CompareExchange(ref attachedValues, new ConcurrentDictionary<AdamantiumProperty, ValueContainer>(concurrencyLevel: 1, capacity: 4), null);
            map = attachedValues;
        }

        return map.GetOrAdd(property, SeedFactory, this);
    }

    /// <summary>A new container starts life holding the property's DECLARED DEFAULT in its Default slot, published as the
    /// effective value - exactly as the constructor used to seed every one of them. That slot is what everything above
    /// Default falls back TO: clearing the last real value must report the default, not the Unset sentinel, and a
    /// changed-callback must be handed a value it can use rather than a sentinel it cannot.</summary>
    /// <summary>Static, and taking its state as the GetOrAdd ARGUMENT: a method group over an instance method allocates a
    /// fresh delegate on every call - and this one is on the write path of every property in the engine.</summary>
    private static readonly Func<AdamantiumProperty, AdamantiumComponent, ValueContainer> SeedFactory =
        static (property, self) => self.SeedContainer(property);

    private ValueContainer SeedContainer(AdamantiumProperty property)
    {
        var container = new ValueContainer();
        var metadata = property.GetDefaultMetadata(GetType());
        container.SetEffective(container.SetValue(metadata.DefaultValue, ValuePriority.Default));
        return container;
    }

    /// <summary>
    /// Gets the object that inherited <see cref="AdamantiumProperty"/> values are inherited from.
    /// </summary>
    IAdamantiumComponent IAdamantiumComponent.InheritanceParent => InheritanceParent;

    /// <summary>
    /// Fires when value on <see cref="AdamantiumProperty"/> was changed
    /// </summary>
    public event EventHandler<AdamantiumPropertyChangedEventArgs> PropertyChanged;

    /// <summary>
    /// Fires when some <see cref="AdamantiumProperty"/> was updated to 
    /// </summary>
    public event EventHandler<ComponentUpdatedEventArgs> ComponentUpdated;

    /// <summary>
    /// Called when <see cref="AdamantiumProperty"/> changes on the object.
    /// </summary>
    /// <param name="e"></param>
    protected virtual void OnPropertyChanged(AdamantiumPropertyChangedEventArgs e)
    {
    }

    /// <summary>
    /// Called right after a value is set at a NON-animation priority (the "base" value intended by code/binding/style),
    /// even when a higher-priority animation currently masks the effective value. <paramref name="oldEffectiveValue"/>
    /// is the displayed value before the set (a transition's "from"); <paramref name="newValue"/> is the value just set
    /// (the "to"). Used by <see cref="AnimatableUIComponent"/> to drive implicit property transitions.
    /// </summary>
    /// <returns>True if it started a transition - i.e. it wrote another slot, so the effective value must be resolved
    /// again. False means nothing moved and the value written a moment ago still stands.</returns>
    protected virtual bool OnValueSet(AdamantiumProperty property, object oldEffectiveValue, object newValue, ValuePriority priority)
    {
        return false;
    }

    protected virtual void OnComponentUpdated()
    {
        
    }

    protected void RaiseComponentUpdated()
    {
        OnComponentUpdated();
        ComponentUpdated?.Invoke(this, new ComponentUpdatedEventArgs(this));
    }

    // Does this type override OnPropertyChanged? Resolved once per type by reflection and remembered - the alternative is
    // to assume it does and keep building reports for the three types in the engine that actually care.
    private static readonly ConcurrentDictionary<Type, bool> OverrideCache = new();

    private static bool OverridesOnPropertyChanged(Type type) => OverrideCache.GetOrAdd(type, static t =>
        t.GetMethod(nameof(OnPropertyChanged), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.DeclaringType != typeof(AdamantiumComponent));

    protected void RaisePropertyChanged(AdamantiumProperty property, object oldValue, object newValue)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        // Nobody to tell: no instance subscriber, no global hook on the property, nothing inherits it, and this type does
        // not override OnPropertyChanged. Everything below would then build a report and hand it to four things that
        // ignore it. On a 4K tile grid that report was allocated ~150 000 times per drag step for no reader at all.
        if (PropertyChanged == null
            && !property.HasChangedSubscribers
            && !property.CanInherit
            && !OverridesOnPropertyChanged(GetType()))
        {
            return;
        }

        var e = new AdamantiumPropertyChangedEventArgs(property, oldValue, newValue);

        try
        {
            OnPropertyChanged(e);
            property.RaiseChanged(this, e);   // global per-property hook: sender = THIS component (identity), not the property

            PropertyChanged?.Invoke(this, e);

            // The children that inherit FROM this one, told DIRECTLY and only about a property that can inherit. They used
            // to ride the PropertyChanged event above, which meant every write of every property woke every child just to
            // have it look up metadata and return - O(children) per write, on a path that runs tens of thousands of times
            // while a tab is built. Measured on the Brushes tab: 85k writes, 1.4 s in this notification alone.
            if (property.CanInherit) 
                NotifyInheritanceChildren(e);

            RaiseComponentUpdated();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    // The components that inherit values FROM this one. A plain list, walked only for a property that can inherit at all
    // (AdamantiumProperty.CanInherit), which is what keeps an ordinary write from touching the children at all.
    // A SET, not a list: membership is the only question ever asked of it, and it was asked with a linear scan. On a
    // virtualizing panel this collection is every realized container - fifteen thousand of them on a 4K grid of small
    // tiles - so attaching the k-th tile scanned k entries and filling the grid was quadratic: ~113 million reference
    // comparisons, and the same again on the way out through Remove. Nothing here depends on order.
    private HashSet<AdamantiumComponent> inheritanceChildren;

    private void AddInheritanceChild(AdamantiumComponent child)
    {
        inheritanceChildren ??= [];
        inheritanceChildren.Add(child);   // the set IS the "already there?" check
    }

    private void RemoveInheritanceChild(AdamantiumComponent child) => inheritanceChildren?.Remove(child);

    /// <summary>A snapshot of the inheritance children to walk. A snapshot because a child may re-parent from inside its
    /// own notification (a DataContext change rebuilds bindings), which would otherwise mutate the set mid-walk. The
    /// single-child case - overwhelmingly the common one - takes the one element out without allocating anything.</summary>
    private bool TrySnapshotInheritanceChildren(out AdamantiumComponent single, out AdamantiumComponent[] many)
    {
        single = null;
        many = null;
        if (inheritanceChildren is not { Count: > 0 }) return false;

        if (inheritanceChildren.Count == 1)
        {
            foreach (var only in inheritanceChildren) { single = only; break; }
            return true;
        }

        many = new AdamantiumComponent[inheritanceChildren.Count];
        inheritanceChildren.CopyTo(many);
        return true;
    }

    // A child may re-parent from inside its own push (a DataContext change rebuilds bindings), so walk a snapshot: the
    // list can be modified while this runs.
    private void NotifyInheritanceChildren(AdamantiumPropertyChangedEventArgs e)
    {
        if (!TrySnapshotInheritanceChildren(out var single, out var children)) return;

        if (single != null) { single.InheritedValueChanged(e); return; }
        foreach (var child in children)
        {
            child.InheritedValueChanged(e);
        }
    }

    /// <summary>A descendant learns that an ancestor moved an inheriting value. The VALUE itself needs no delivery - the
    /// epoch bump made every cached copy stale and the next read resolves it from the ancestors - so this walk exists
    /// only for what a read cannot do by itself: run a changed-callback (a new DataContext has to re-resolve bindings)
    /// and tell whoever subscribed to THIS element's PropertyChanged (a binding or a trigger watching an inherited
    /// value). An element that needs neither is stepped over, and the walk carries on to its own children.</summary>
    /// <summary>Does an inherited change of this property have to REACH this element, or may the walk step over it and
    /// carry on to its children? Only the callback's own work can answer that, so the element that owns the callback
    /// answers. Stepping over is safe for the VALUE by construction - the epoch bump staled every cached copy and the
    /// next read resolves it from the ancestors; this is only about who has to be told.</summary>
    protected virtual bool NeedsInheritedCallback(AdamantiumProperty property) => true;

    private void InheritedValueChanged(AdamantiumPropertyChangedEventArgs e)
    {
        var metadata = e.Property.GetDefaultMetadata(GetType());
        // An explicit value of its own outranks the inherited one - this element and everything under it keep theirs.
        if (metadata is not { Inherits: true } || HasExplicitValue(e.Property)) return;

        // A callback on the PROPERTY is not the same question as "does this ELEMENT need telling". `DataContext` carries
        // one for every element that ever inherits it, so the cheap step-over below was unreachable for the one property
        // that needs it most: a list rebinding its containers re-resolved the bindings of every element under each of
        // them, and three quarters of those elements have no binding at all. Measured on the Layout tab: 125 534 refreshes
        // over 22 822 elements - 5.5 apiece, of which only 31 259 had anything to re-resolve.
        // ...and an element whose LOOK depends on the value has to be told too, callback or not. Stepping over is safe for
        // the value - the next read resolves it from the ancestors - but the invalidation that a write performs is not a
        // read: skip the write and the element keeps the value it is no longer painting with. That is what froze a tab's
        // label at the resting colour while every probe reported the selected one: the presenter got the trigger's brush
        // and the TextBlock under it - AffectsRender, no callback - was stepped over, so nothing ever asked for the
        // repaint. It only looked intermittent because an element with any PropertyChanged subscriber takes the branch
        // below anyway. DataContext, the property this step-over exists for, carries none of these flags and still skips.
        if ((metadata.PropertyChangedCallback != null && NeedsInheritedCallback(e.Property)) || PropertyChanged != null
            || metadata.AffectsRender || metadata.AffectsPaint || metadata.AffectsMeasure || metadata.AffectsArrange
            || metadata.AffectsParentMeasure || metadata.AffectsParentArrange)
        {
            // The old push, for the few that need telling: it writes, notifies, and cascades to ITS children itself.
            SetValue(e.Property, e.NewValue, ValuePriority.Inherited);
            return;
        }

        if (!TrySnapshotInheritanceChildren(out var single, out var children)) return;

        if (single != null) { single.InheritedValueChanged(e); return; }
        foreach (var child in children)
        {
            child.InheritedValueChanged(e);
        }
    }

    /// <summary>
    /// Gets or sets the parent object that <see cref="AdamantiumProperty"/> values are inherited from.
    /// </summary>
    /// <value>
    /// The inheritance parent.
    /// </value>
    public AdamantiumComponent InheritanceParent
    {
        get => inheritanceParent;
        set
        {
            if (inheritanceParent == value) return;

            var oldParent = inheritanceParent;
            oldParent?.RemoveInheritanceChild(this);

            inheritanceParent = value;

            inheritanceParent?.AddInheritanceChild(this);

            // A different parent brings a different set of inherited values to this whole subtree - the caches below are
            // stale by construction, and one bump says so to all of them.
            AdamantiumProperty.BumpInheritanceEpoch();

            // The new parent (or null) brings a different set of inherited values. For every inherited property this
            // component hasn't set locally, raise a change from the old inherited value to the new one so the value AND
            // its callbacks (e.g. DataContext -> refresh bindings) apply. This makes inheritance order-independent: an
            // element attached AFTER its parent's value was assigned still picks it up here.
            // Only the INHERITING properties, resolved once per type - not all seventy a control registers, each asked for
            // its merged metadata to be told "no". Re-parenting happens once per element realized, so a virtualized grid
            // paid that whole scan per tile.
            var type = GetType();
            foreach (var property in AdamantiumPropertyMap.GetInheriting(type))
            {
                if (HasExplicitValue(property))
                    continue;

                var metadata = property.GetDefaultMetadata(type);

                // A plain inherited VALUE needs nothing here: the epoch bumped above, and the first read of it resolves
                // from the new parent. Only a callback (DataContext -> refresh this element's bindings) or an observer
                // has to be told, and only that is written.
                if (metadata.PropertyChangedCallback == null && PropertyChanged == null)
                    continue;

                var oldValue = oldParent?.GetValue(property) ?? metadata.DefaultValue;
                var newValue = inheritanceParent?.GetValue(property) ?? metadata.DefaultValue;
                if (!Equals(oldValue, newValue))
                {
                    SetValue(property, newValue, ValuePriority.Inherited);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the value of a <see cref="AdamantiumProperty"/>
    /// </summary>
    /// <param name="property"><see cref="AdamantiumProperty"/></param>
    public object this[AdamantiumProperty property]
    {
        get => GetValue(property);
        set => SetValue(property, value);
    }
    

    /// <summary>
    /// Gets the default value for a property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The default value.</returns>
    private object GetDefaultValue(AdamantiumProperty property)
    {
        var value = property.GetDefaultMetadata(GetType());
        if (value.Inherits && inheritanceParent != null)
        {
            return inheritanceParent.GetValue(property);
        }

        return value.DefaultValue;
    }

    public void ClearValue(string propertyName, ValuePriority priority = ValuePriority.Local)
    {
        // ResolveProperty: the name comes from markup, so it may be attached (`Grid.Row`). This is the paired operation
        // for a trigger's setter - resolving less here than the setter did would leave the value it wrote standing when
        // the trigger goes away.
        var property = AdamantiumPropertyMap.ResolveProperty(GetType(), propertyName);
        if (property == null) return;
        
        ClearValue(property, priority);
    }

    /// <summary>
    /// Clears a <see cref="AdamantiumProperty"/>'s local value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="priority"></param>
    public void ClearValue(AdamantiumProperty property, ValuePriority priority = ValuePriority.Local)
    {
        ArgumentNullException.ThrowIfNull(property);

        SetValue(property, AdamantiumProperty.UnsetValue, priority);
    }

    /// <summary>
    /// Gets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The value.</returns>
    public object GetValue(AdamantiumProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        // NO LOCK. A read is a dictionary lookup over a map that never changes after construction plus one volatile
        // field read inside the container - it cannot observe a half-written state, and it cannot wait for anybody.
        // That is what makes a reader unable to take part in a deadlock at all, rather than merely unlikely to.
        // An INHERITING property resolves from the ancestors, and it does it HERE, on the read, rather than by having
        // every ancestor write its value into every descendant. The cached answer lives in the Inherited slot and is
        // good while it carries the current epoch (bumped by any explicit write of an inheriting property, and by any
        // re-parenting); a stale one costs one walk up the chain - measured at ~0.7 us against the ~180 us a pushed
        // write cost.
        if (property.CanInherit) ResolveInherited(property);

        var result = GetOrCalculateEffectiveValue(property);

        if (result == AdamantiumProperty.UnsetValue)
        {
            // No value at any source priority - read the default. The metadata answers BOTH questions in ONE cached
            // lookup: a property declared nowhere in this type's chain is the only case that still has to ask the
            // registry (which runs the static constructors and throws). Asking the registry first cost 64ns on top of
            // the 60ns for the metadata, on EVERY read of an unset property - which, since containers became lazy
            // (nothing is seeded at construction any more), is most reads in the engine rather than a cold path.
            var metadata = property.GetDefaultMetadata(GetType());
            if (metadata == null)
            {
                if (!AdamantiumPropertyMap.IsRegistered(this, property))
                {
                    ThrowNotRegistered(property);
                }
                metadata = property.GetDefaultMetadata(GetType());   // the registry check runs static ctors, which can declare it
            }

            result = metadata.Inherits && inheritanceParent != null
                ? inheritanceParent.GetValue(property)
                : metadata.DefaultValue;
        }
        // Inherited values are resolved by ResolveInherited above and CACHED in the Inherited slot, so the scan sees
        // them like any other value and a second read of the same epoch costs one stamp comparison.

        return result;
    }

    // Fill this element's inherited slot from the nearest ancestor that actually holds a value for the property. Does
    // nothing when the element has a value of its own (an explicit one outranks what it would inherit) or when the cache
    // is already current. This is a CACHE FILL, not a set: nothing is notified, because nothing changed - the property
    // already read as this value, it just had not been written down yet.
    private void ResolveInherited(AdamantiumProperty property)
    {
        // ENSURE, not merely look up: this cache is the whole reason an inherited read is one stamp comparison instead of
        // a walk to the root, and it lives in the container. With containers created only on write, an element that
        // never SETS its DataContext would have had nowhere to remember what it inherits and would have walked the chain
        // on every read - which is exactly the O(depth)-per-read this was written to replace. Only the handful of
        // properties that can inherit reach here, so it is three containers per element, not sixty-five.
        if (EnsureSlots(property) is not { } container) return;
        if (container.InheritedStamp == AdamantiumProperty.InheritanceEpoch) return;
        if (!container.IsDefaultOnly && container.GetValue(ValuePriority.Inherited) == AdamantiumProperty.UnsetValue)
        {
            // An explicit value wins over anything inherited - stamp it so the walk is not attempted again this epoch.
            container.InheritedStamp = AdamantiumProperty.InheritanceEpoch;
            return;
        }

        var metadata = property.GetDefaultMetadata(GetType());
        if (metadata is not { Inherits: true })
        {
            container.InheritedStamp = AdamantiumProperty.InheritanceEpoch;
            return;
        }

        var raw = AdamantiumProperty.UnsetValue;
        for (var ancestor = inheritanceParent; ancestor != null; ancestor = ancestor.inheritanceParent)
        {
            if (!ancestor.HasExplicitValue(property)) continue;
            raw = ancestor.GetValue(property);
            break;
        }

        // A value that DRAWS this element has to keep the element in its owner map, and the link is normally taken by
        // the property system's Changed hook (see AdamantiumProperty.WireOwnerAttachment). This fill raises nothing by
        // design - it is a cache fill, the property already READ as this value - so nothing took the link here, and an
        // element that inherits its Foreground was left out of the brush's owner map entirely. Measured on the stand:
        // of 1028 elements painting with a palette brush, 724 were not owners of it (268 Border, 98 Grid, 80 TextBlock,
        // 76 Path), so a variant recolour had nobody to tell and the text stayed in the old colour until something
        // unrelated re-recorded it - a hover, or switching tabs.
        //
        // Taken HERE rather than on the inheritance walk on purpose: the walk's cheap path deliberately steps over
        // elements without resolving anything (it exists so a list rebinding its DataContext does not re-resolve
        // 125 534 times), and resolving there to take a link would undo exactly that. This costs a reference compare,
        // and only for the handful of properties that can carry an attachable value at all.
        var attachable = property.CanAttachToOwner;
        var before = attachable ? container.Effective : null;

        container.SetInheritedCache(raw,
            raw == AdamantiumProperty.UnsetValue ? container.Effective : Coerce(property, metadata, raw),
            AdamantiumProperty.InheritanceEpoch);

        if (!attachable) return;

        var after = container.Effective;
        if (ReferenceEquals(before, after)) return;

        (before as IRenderAttachable)?.DetachFrom(this);
        // Not while this element is OUT of the tree: leaving gave every link up, and taking one here would leave the
        // value holding an element that the next leave can no longer release. Same rule as the Changed hook.
        if (!_renderAttachmentsReleased) (after as IRenderAttachable)?.AttachTo(this);
    }

    // "Explicit" = a value set from a real source (Animation..Style); the seeded Default and the computed Effective/
    // Inherited slots don't count. Used to decide whether an Inherits property should defer to its parent.
    protected bool HasExplicitValue(AdamantiumProperty property)
    {
        if (Slots(property) is not { } container) return false;

        for (var p = ValuePriority.Animation; p <= ValuePriority.Style; p++)
            if (container.GetValue(p) != AdamantiumProperty.UnsetValue)
                return true;

        return false;
    }

    // The priority slot currently supplying this property's base value (skipping the Animation mask and the Effective
    // cache). A re-coercion (e.g. RangeBase re-clamping Value when Minimum/Maximum change) must rewrite the value IN
    // PLACE at this priority rather than promoting it to Local: Local (1) outranks Binding (2), so a Local re-coerce
    // would permanently mask a {Binding} on that property - which is what pinned a data-bound Slider.Value at its
    // coerced Minimum and stopped the binding from ever applying.
    protected ValuePriority GetBaseValuePriority(AdamantiumProperty property)
    {
        if (Slots(property) is { } container)
        {
            for (var p = ValuePriority.Local; p <= ValuePriority.Default; p++)
                if (container.GetValue(p) != AdamantiumProperty.UnsetValue)
                    return p;
        }

        return ValuePriority.Default;
    }

    // An inherited value changed (parent assigned/attached): fire the metadata callback (e.g. DataContext -> refresh
    // bindings) and re-raise so this element's own descendants inherit in turn.

    /// <summary>
    /// Gets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="property">The property.</param>
    /// <returns>The value.</returns>
    public T GetValue<T>(AdamantiumProperty property)
    {
        return (T)GetValue(property);
    }

    /// <summary>
    /// Check if <see cref="AdamantiumProperty"/> is registered.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>True if property registered, otherwise - false</returns>
    public bool IsRegistered(AdamantiumProperty property)
    {
        return AdamantiumPropertyMap.IsRegistered(this, property);
    }

    /// <summary>
    /// Checks whether a <see cref="AdamantiumProperty"/> is set on this object.
    /// </summary>
    /// <param name="property">Adamantium property.</param>
    /// <returns>True if the property is set, otherwise false.</returns>
    public bool IsSet(AdamantiumProperty property)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        return Slots(property) != null;
    }

    public AdamantiumProperty GetProperty(string propertyName)
    {
        return AdamantiumPropertyMap.ResolveProperty(GetType(), propertyName);
    }

    /// <summary>Bind a property. Declared HERE, on the property system itself, rather than on tree elements: anything
    /// with AdamantiumProperties is a legitimate binding target, and a Transform - a component that carries animatable
    /// properties but stands outside the tree - was refused for no better reason than where the method happened to live.
    /// It reaches a DataContext through its InheritanceParent (see BindingExpressionBase.DataContextSource).</summary>
    public Data.BindingExpressionBase SetBinding(string property, Data.BindingBase bindingBase)
        => SetBinding(GetProperty(property), bindingBase);

    /// <summary>Bindings live in the central BindingEngine registry (queryable/refreshable), not as element-private
    /// state. Returns the base type - a MultiBinding yields a MultiBindingExpression, not a BindingExpression.</summary>
    public Data.BindingExpressionBase SetBinding(AdamantiumProperty property, Data.BindingBase bindingBase)
        => Data.BindingEngine.SetBinding(this, property, bindingBase);

    public void SetTriggerValue(string propertyName, object value, object token)
    {
        var property = AdamantiumPropertyMap.ResolveProperty(GetType(), propertyName);
        SetTriggerValue(property, value, token);
    }

    // A trigger setter pushes its value tagged with a token (the setter itself), so overlapping triggers stack instead
    // of overwriting one slot. The Trigger priority slot just mirrors the stack's current top.
    public void SetTriggerValue(AdamantiumProperty property, object value, object token)
    {
        triggerValues ??= new Dictionary<string, TriggerValueContainer>();
        if (!triggerValues.TryGetValue(property.Name, out var container))
            triggerValues[property.Name] = container = new TriggerValueContainer();
        container.Set(token, value);

        SetValue(property, container.EffectiveValue, ValuePriority.Trigger);
    }

    // Drops one trigger's contribution; the slot falls back to the next trigger underneath (or UnsetValue -> Style/Local).
    public void ClearTriggerValue(AdamantiumProperty property, object token)
    {
        if (triggerValues == null || !triggerValues.TryGetValue(property.Name, out var container)) return;
        container.Remove(token);
        SetValue(property, container.EffectiveValue, ValuePriority.Trigger);
    }

    public void SetStyleValue(string propertyName, object value, Style style)
    {
        var property = AdamantiumPropertyMap.ResolveProperty(GetType(), propertyName);
        SetStyleValue(property, value, style);
    }

    /// <summary>Apply one style's contribution: RECORD FIRST, THEN WRITE WHAT WINS. Every style writes at the same
    /// priority, so writing the incoming value straight through made the last style applied the winner - and handed the
    /// outcome to the order a theme lists its sets in. A less specific rule arriving later is now recorded, ranked
    /// below, and changes nothing.</summary>
    public void SetStyleValue(AdamantiumProperty property, object value, Style style)
    {
        AddStyleEntry(property.Name, value, style);
        WriteStyleValue(property, EffectiveStyleValue(property.Name, value), StyleSlotFor(property, property.Name, style));
    }

    /// <summary>Which slot a style's contribution belongs in. An INHERITABLE property set by a selector that narrows by
    /// nothing but the type is a default FOR THAT TYPE and goes to <see cref="ValuePriority.TypeDefault"/>, below
    /// inheritance; everything else keeps <see cref="ValuePriority.Style"/>. See ValuePriority.TypeDefault for what this
    /// buys, and Style.IsTypeDefault for where the line is drawn.</summary>
    private ValuePriority StyleSlotFor(AdamantiumProperty property, string propertyName, Style applied)
    {
        if (!property.CanInherit) return ValuePriority.Style;

        var winner = styleValues != null && styleValues.TryGetValue(propertyName, out var entry)
            ? entry.EffectiveStyle
            : applied;

        return winner is { IsTypeDefault: true } ? ValuePriority.TypeDefault : ValuePriority.Style;
    }

    // Write the style's contribution to ONE of the two style slots and clear the other. Both have to be touched because
    // the winning entry can change TIER as styles come and go - a class style taken off leaves a bare-type one in force,
    // and a value left behind in the slot it no longer belongs to would go on winning from there. Cleared FIRST, so the
    // stale value never outranks the new one even for an instant.
    private void WriteStyleValue(AdamantiumProperty property, object value, ValuePriority slot)
    {
        var other = slot == ValuePriority.Style ? ValuePriority.TypeDefault : ValuePriority.Style;
        if (Slots(property)?.GetValue(other) is { } held && held != AdamantiumProperty.UnsetValue)
            SetValue(property, AdamantiumProperty.UnsetValue, other);

        SetValue(property, value, slot);
    }

    private object EffectiveStyleValue(string propertyName, object fallback)
        => styleValues != null && styleValues.TryGetValue(propertyName, out var entry)
            ? entry.EffectiveValue
            : fallback;

    public void RemoveStyleValue(string propertyName, Style style)
    {
        var previousValue = RemoveStyleEntry(propertyName, style);
        var property = AdamantiumPropertyMap.ResolveProperty(GetType(), propertyName);
        if (property == null)
        {
            SetValue(propertyName, previousValue, ValuePriority.Style);
            return;
        }

        // The style left standing after this removal decides the slot just as it does on the way in - taking a class
        // style off can leave a bare-type one in force, and that one belongs a tier lower.
        WriteStyleValue(property, previousValue, StyleSlotFor(property, propertyName, style));
    }

    private void AddStyleEntry(string propertyName, object value, Style style)
    {
        styleValues ??= new Dictionary<string, StyleValueContainer>();
        if (!styleValues.TryGetValue(propertyName, out var entry))
        {
            styleValues[propertyName] = entry = new StyleValueContainer();
        }
        entry.AddValue(style, value);
    }
    
    private object RemoveStyleEntry(string propertyName, Style style)
    {
        if (styleValues == null || !styleValues.TryGetValue(propertyName, out var entry))
        {
            return AdamantiumProperty.UnsetValue;
        }

        return entry.RemoveAndGetEffectiveValue(style);
    }

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <param name="value">New value.</param>
    /// <param name="priority"></param>
    public void SetValue(AdamantiumProperty property, object value, ValuePriority priority = ValuePriority.Local)
    {
        ValidateProperty(property);

        // An attached property has no pre-created slot (it isn't in this type's registered set), so ensure its container
        // EXISTS - but do NOT write the value here. RunSetValueSequence reads the OLD effective value first for change
        // detection, then writes; pre-writing it made old == new, so PropertyChanged never fired for attached properties
        // (breaking any binding / trigger / TemplateBinding observing an attached property).
        if (EnsureSlots(property) == null) return;

        // The sequence below runs arbitrary code - a changed-callback, a coercion, layout invalidation, PropertyChanged
        // handlers - and one of those posting to the dispatcher while holding a lock is a deadlock: the pump thread,
        // rebuilding a panel, then waits for the lock its holder can only release once the pump drains the queue
        // (measured on the docking view). Nothing here holds one.
        RunSetValueSequence(property, value, priority, true);
    }

    /// <summary>
    /// Sets a property's value WITHOUT changing its value source (the WPF <c>SetCurrentValue</c> analog). Used by a
    /// control to reflect USER INPUT into a property that may carry a two-way <see cref="ValuePriority.Binding"/> - a
    /// Slider's thumb drag, a CheckBox toggle, a TextBox edit. A plain <see cref="SetValue(AdamantiumProperty, object,
    /// ValuePriority)"/> defaults to <see cref="ValuePriority.Local"/>, which (1) outranks Binding (2) and would
    /// permanently MASK the binding: the effective value freezes at the last user set and the source can never refresh
    /// the control again (the "passive slider's fill/thumb stop tracking" bug). Instead we write into the slot the
    /// value CURRENTLY comes from, capped at Binding: a bound property updates its Binding slot in place (so the next
    /// source change overwrites it cleanly and the two-way write-back still fires); an unbound one lands at Binding
    /// (above Style/Trigger, so user input still wins) without ever creating the masking Local slot.
    /// </summary>
    public void SetCurrentValue(AdamantiumProperty property, object value)
    {
        var basePriority = GetBaseValuePriority(property);
        var priority = basePriority <= ValuePriority.Binding ? basePriority : ValuePriority.Binding;
        SetValue(property, value, priority);
    }

    /// <summary>
    /// Sets a <see cref="AdamantiumProperty"/> value.
    /// </summary>
    /// <param name="property">Name of the AdamantiumProperty reference</param>
    /// <param name="value">The value.</param>
    /// <param name="priority">Priority for value</param>
    public void SetValue(string property, object value, ValuePriority priority = ValuePriority.Local)
    {
        if (string.IsNullOrEmpty(property)) return;

        var adamantiumProperty = AdamantiumPropertyMap.ResolveProperty(GetType(), property);
        if (adamantiumProperty == null)
            return;

        // Honour the caller's priority - this dropped it (always Local), which is why a ControlTemplate literal
        // set via this overload at Template priority could not be overridden by a Trigger (Local outranks Trigger).
        SetValue(adamantiumProperty, value, priority);
    }

    private object GetOrCalculateEffectiveValue(AdamantiumProperty property)
    {
        // One dictionary lookup (was three: ContainsKey + two indexers), and NO write-back. The Effective slot was
        // rewritten on every read but nothing ever reads it - the resolution below scans the SOURCE slots
        // (Animation..Default) and the Effective slot is only a trailing cache - so that write plus its extra lookups were
        // pure overhead on the hottest path in the engine (measure+arrange read Margin/alignment/min-max on every node).
        if (Slots(property) is not { } container) 
            return AdamantiumProperty.UnsetValue;

        // Cached: scans the source slots (Animation..Default) only after a Set dirtied them, not on every read.
        return container.Effective;
    }

    /// <summary>
    /// Re-runs <paramref name="property"/>'s coercion. Use it from a property whose value the coercion DEPENDS ON: the
    /// dependency just moved, so what was legal before may not be now - or, just as important, what had to be clamped
    /// before may be legal again. The request is re-mapped, never rewritten, so a value clamped while its dependency was
    /// still elsewhere comes back the moment there is room for it (WPF's DependencyObject.CoerceValue).
    /// </summary>
    public void CoerceValue(AdamantiumProperty property)
    {
        var slots = Slots(property);
        if (slots == null) return;

        object baseValue;
        lock (slots)
        {
            baseValue = slots.BaseValue;
        }

        if (baseValue == AdamantiumProperty.UnsetValue) return;   // nothing was ever asked for - nothing to re-map

        // Through the normal write path with the SAME request at the SAME priority: the coercion runs again and, if the
        // result differs, the change is announced exactly as any other would be. Re-coercing to the same value ends
        // there (the equal-effective early-return), which is what stops two properties that coerce against each other
        // from bouncing.
        RunSetValueSequence(property, baseValue, GetBaseValuePriority(property), true);
    }

    private object Coerce(AdamantiumProperty property, PropertyMetadata metadata, object baseValue)
    {
        if (metadata.CoerceValueCallback == null) return baseValue;

        var coerced = metadata.CoerceValueCallback.Invoke(this, baseValue);
        if (!Equals(coerced, baseValue) && property.ValidateValueCallBack?.Invoke(coerced) == false)
        {
            throw new ArgumentException($"Value {coerced} is incorrect!");
        }

        return coerced;
    }

    private void RunSetValueSequence(AdamantiumProperty property, object value, ValuePriority priority, bool raiseValueChangedEvent)
    {
        var metadata = property.GetDefaultMetadata(GetType());
        if (property.ValidateValueCallBack?.Invoke(value) == false)
        {
            throw new ArgumentException($"Value {value} is incorrect!");
        }

        // The ONLY part that needs the lock: read the old effective value, write the slot, coerce, publish. Everything
        // after this block is notification, and notification must not hold a lock (see SetValue).
        object oldEffectiveValue;
        object slotBefore;
        object effectiveAfterWrite;
        var slots = EnsureSlots(property);
        if (slots == null) return;

        // The lock is on the CONTAINER, not on the whole value store: two properties of one component never contend,
        // and READERS take no lock at all. It covers only the read-modify-write of these slots - no invalidation and
        // nothing that could go looking for another lock. The coercion inside it only READS (of this or another
        // property, and reads are lock-free), which is what makes it safe to run here.
        lock (slots)
        {
            oldEffectiveValue = slots.Effective;
            slotBefore = slots.GetValue(priority);
            // The slot keeps the REQUEST; coercion maps it to what the property actually reads as. Storing the coerced
            // value instead would throw the request away, and with it any chance of mapping it again later when whatever
            // the coercion depends on has moved (see ValueContainer.BaseValue and CoerceValue).
            var baseValue = slots.SetValue(value, priority);
            effectiveAfterWrite = Coerce(property, metadata, baseValue);
            slots.SetEffective(effectiveAfterWrite);
        }

        // What the property READ as before the write - both ends of the report are that, never a raw slot. A container
        // never written has no effective value (the lazy slots of an attached property); there it is the default,
        // resolved through the same path GetValue uses so an inheriting property reports its parent's value.
        var oldReadValue = oldEffectiveValue == AdamantiumProperty.UnsetValue
            ? GetDefaultValue(property)
            : oldEffectiveValue;
        // A write that leaves the value the property READS as where it was is not a change, so it must not run the
        // changed-callback. Running it anyway is how two properties that assign each other close a cycle with no exit:
        // a presenter whose Content is bound to its own DataContext writes back the object it already sits on, the
        // callback re-assigns the same DataContext, that refreshes the bindings, which writes Content again - the app
        // died of a stack overflow. The equal check below guards only invalidation and events, which is why the cycle
        // ran above it.
        // The args are built INSIDE the branch: they are read by the callback and by nothing else, so a property with no
        // callback (most of them - ActualWidth/ActualHeight among them) was allocating one object per write for nobody.
        var slotsMayHaveMoved = false;
        if (metadata.PropertyChangedCallback != null && !Equals(oldReadValue, effectiveAfterWrite))
        {
            metadata.PropertyChangedCallback.Invoke(this,
                new AdamantiumPropertyChangedEventArgs(property, oldReadValue, effectiveAfterWrite));
            slotsMayHaveMoved = true;
        }

        // Implicit transitions: let an animatable element turn this base-value change into a smooth animation. Skipped
        // for animation-priority writes (those ARE the transition) so there is no recursion. May start an animation
        // re-entrantly (it writes the Animation slot) - that resolves cleanly via the equal-effective early-return below.
        // Skipped for a READ-ONLY property, by what read-only MEANS: a Transitions entry names a property for the element
        // to animate, and only the declaring class writes a read-only one - so there is no transition to find, and an
        // animation on it would be overwritten by its writer on the very next pass. The lookup is not free (it reads
        // Transitions through the property system on every single write), so not doing it is the point.

        if (priority != ValuePriority.Animation && !property.ReadOnly)
        {
            slotsMayHaveMoved |= OnValueSet(property, oldReadValue, value, priority);
        }

        // The effective value is the field the locked write above just set, so re-reading it is only meaningful when
        // something between then and now could have written ANOTHER slot - the changed-callback, or a started transition.
        // Neither ran => what we wrote still stands, and the lookup is skipped rather than repeated.
        var newEffectiveValue = slotsMayHaveMoved ? GetOrCalculateEffectiveValue(property) : effectiveAfterWrite;

        // What every descendant reading this property has cached is now stale: bump the epoch, and their next read
        // re-resolves. O(1) instead of writing the value into each of them.
        if (property.CanInherit && priority < ValuePriority.Inherited) AdamantiumProperty.BumpInheritanceEpoch();

        if (Equals(oldReadValue, newEffectiveValue))
        {
            return;
        }

        var element = this as IUIComponent;
        if (element is IMeasurableComponent measurable)
        {
            if (metadata.AffectsMeasure)
            {
                if (Diagnostics.LayoutTrace.Counting)
                    Diagnostics.LayoutTrace.Count(GetType(), property.Name);

                measurable.InvalidateMeasure();
                measurable.InvalidateArrange();
                // ...and what it DRAWS may depend on the value too (a TextBlock's Text is AffectsMeasure alone, and a
                // changed word of the same width would otherwise keep its old glyphs). Nearly 200 properties declare
                // AffectsMeasure without AffectsRender and have always relied on this - said HERE, on the one element
                // whose value changed, instead of by the layout invalidation, which fires for every node a pass touches.
                element.InvalidateRender(false);
            }
            else if (metadata.AffectsArrange)
            {
                if (Diagnostics.LayoutTrace.Counting)
                    Diagnostics.LayoutTrace.Count(GetType(), property.Name);

                measurable.InvalidateArrange();
                element.InvalidateRender(false);
            }

            // The value belongs to the PARENT's layout, not to this element's own size: a Grid cell index, a figure's
            // segments. Invalidating only this element leaves it exactly where the parent last put it - the parent's
            // measure is measure-valid at an unchanged constraint, so it early-returns and never re-reads the value.
            // Until now these two options were declared in metadata and acted on NOWHERE, so anything relying on them
            // silently did nothing (see the note in PaneHost, which worked around it by hand).
            if (element.VisualParent is IMeasurableComponent parent)
            {
                if (metadata.AffectsParentMeasure)
                {
                    if (Diagnostics.LayoutTrace.Counting) 
                        Diagnostics.LayoutTrace.Count(GetType(), property.Name + " (parent)");
                    
                    parent.InvalidateMeasure();
                    parent.InvalidateArrange();
                }
                else if (metadata.AffectsParentArrange)
                {
                    if (Diagnostics.LayoutTrace.Counting) 
                        Diagnostics.LayoutTrace.Count(GetType(), property.Name + " (parent)");
                    
                    parent.InvalidateArrange();
                }
            }
        }

        if (metadata.AffectsRender)
        {
            element?.InvalidateRender(false);
        }
        // Paint-only: same shape, same draw commands, a different colour. Re-bake what is already recorded instead of
        // re-recording the element. Checked as an ELSE of AffectsRender - a property declaring both would mean "the shape
        // changed AND only the colour changed", which is a contradiction; the stronger (geometry) claim wins.
        else if (metadata.AffectsPaint)
        {
            element?.InvalidatePaint();
        }

        // oldReadValue, NOT the raw slot: a container never written holds UnsetValue, and reporting THAT as OldValue told
        // every listener the property "used to be unset" instead of naming the default it actually read as. The
        // changed-callback above has always been given the resolved value; the notification now says the same thing.
        if (raiseValueChangedEvent)
        {
            RaisePropertyChanged(property, oldReadValue, newEffectiveValue);
        }
    }

    // Re-render this element when a brush it draws with mutates internally (wired in the AffectsRender path above). On a
    // non-element (a brush setting a sub-brush) the cast is null and this is a no-op.
    //
    // NOT for an element that isn't drawn right now. A brush can be SHARED by thousands of elements (a keyed theme brush -
    // the loading-skeleton pulse animates ONE brush that every card paints with), so its Changed fans out to all of them,
    // pooled/Collapsed cards included. Marking those is worse than wasted: a non-visible component that still holds
    // retained units makes the render cache's partial pass FALL BACK to a full tree walk (RenderCache.RecordReRender), so
    // an animated shared brush would re-walk the whole scene every frame. Nothing is lost - Visibility is AffectsRender
    // (and structural), so a card coming back invalidates its render then.
    // A brush MUTATED INTERNALLY (an animation moving its Opacity, a recoloured fill, a gradient stop moving). The element
    // draws exactly what it drew before: same commands, same kinds, same geometry - the payload holds THIS SAME brush object
    // by reference and re-reads its snapshot when the GPU data is baked (see Brush.Snapshot). So this is PAINT, not
    // geometry: re-bake what exists, do not re-record the element.
    //
    // The distinction is the whole point. A brush is routinely SHARED by thousands of elements (a keyed theme brush; the
    // loading skeletons all paint with one pulsing brush), so its Changed fans out to every one of them on every tick.
    // Marking geometry meant each of them re-ran OnRender, rebuilt its draw commands, re-reconciled its units and
    // re-published its frozen layout - measured on the tile fill at ~470 cards per frame, half the fill's throughput, for one
    // number that moved.
    //
    // NOTE the asymmetry with an ordinary SET of a brush property (handled in the AffectsRender path above): assigning a
    // DIFFERENT brush object leaves the recorded command pointing at the OLD one, so that genuinely needs a re-record until
    // payloads reference brushes by identity rather than by object.
    internal void OnRenderValueChanged(object sender, EventArgs e)
    {
        if (this is not IUIComponent { Visibility: Visibility.Visible } element) return;
        element.InvalidatePaint();
    }

    /// <summary>
    /// Throws an exception indicating that the specified property is not registered on this
    /// object.
    /// </summary>
    /// <param name="p">The property</param>
    private void ThrowNotRegistered(AdamantiumProperty p)
    {
        throw new ArgumentException($"Property '{p.Name} not registered on '{this.GetType()}");
    }

    private void ValidateProperty(AdamantiumProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (!AdamantiumPropertyMap.IsRegistered(this, property))
        {
            ThrowNotRegistered(property);
        }
    }
}