using System.Collections.Concurrent;
﻿using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core;

public abstract class AdamantiumComponent : IAdamantiumComponent
{
    public UInt128 Uid { get; set; }

    // Every property REGISTERED on this type, filled once in the constructor and never added to again - so it is safe to
    // read from any thread without synchronising, which is what lets GetValue take no lock at all. It also doubles as
    // the monitor WRITERS take (see SetValue): readers never wait on it, so no read can ever be caught in a lock cycle.
    private readonly Dictionary<AdamantiumProperty, ValueContainer> values = new Dictionary<AdamantiumProperty, ValueContainer>();

    // ATTACHED properties are the only ones that turn up later - they belong to another type and this one has no slot
    // for them until somebody sets one. Lazily created, so the components that never see an attached property (nearly
    // all of them) pay nothing for it.
    private ConcurrentDictionary<AdamantiumProperty, ValueContainer> attachedValues;

    private readonly Dictionary<string, StyleValueContainer> styleValues = new();

    // Per-property stack of trigger contributions (token -> value), so several triggers can target one property without
    // clobbering each other - leaving the top one restores the one beneath. The single Trigger slot in `values` holds
    // only the stack's current top. See SetTriggerValue/ClearTriggerValue.
    private readonly Dictionary<string, TriggerValueContainer> triggerValues = new();

    private AdamantiumComponent inheritanceParent;

    protected AdamantiumComponent()
    {
        var list = AdamantiumPropertyMap.GetRegistered(this);

        foreach (var property in list)
        {
            var metadata = property.GetDefaultMetadata(GetType());
            if (property.ValidateValueCallBack?.Invoke(metadata.DefaultValue) == false)
            {
                throw new ArgumentException($"Value {metadata} is incorrect!");
            }

            // The default is NOT coerced here. Coercion answers "given this object's current state, what does that
            // request become", so running it during construction asks a half-built object - and the result used to be
            // written back into metadata.DefaultValue, which belongs to the TYPE: one instance's state would have
            // decided the default for every other instance of it, for the life of the process. A default stands as
            // authored until something is actually set, and from then on the coercion runs on every write.

            if (!values.ContainsKey(property))
            {
                values[property] = new ValueContainer();
            }

            // Seed the slot AND publish it as the effective value. Publishing is a separate step now that the container
            // keeps the request and the coerced result apart - and it matters from the very first read: a property whose
            // effective value is still "unset" hands callbacks that sentinel instead of its default, and code comparing
            // the old value against it sees a type it cannot use (an implicit transition, for one, refuses to animate
            // from it). The default is published as authored; the first write is what runs the coercion.
            values[property].SetEffective(values[property].SetValue(metadata.DefaultValue, ValuePriority.Default));
            if (metadata.DefaultValue != null)
            {
                var e = new AdamantiumPropertyChangedEventArgs(property, AdamantiumProperty.UnsetValue,
                    metadata.DefaultValue);
                metadata.PropertyChangedCallback?.Invoke(this, e);
            }
        }
    }

    // The slots of a property, or null when this component has none. Lock-free: the registered map is immutable after
    // construction, and the attached map is concurrent.
    private ValueContainer Slots(AdamantiumProperty property)
    {
        if (values.TryGetValue(property, out var container)) return container;

        var attached = attachedValues;
        return attached != null && attached.TryGetValue(property, out var slot) ? slot : null;
    }

    // The slots of a property, creating them for an ATTACHED one that has never been set on this component.
    private ValueContainer EnsureSlots(AdamantiumProperty property)
    {
        if (values.TryGetValue(property, out var container)) return container;
        if (!property.IsAttached) return null;

        var map = attachedValues;
        if (map == null)
        {
            Interlocked.CompareExchange(ref attachedValues, new ConcurrentDictionary<AdamantiumProperty, ValueContainer>(), null);
            map = attachedValues;
        }

        return map.GetOrAdd(property, static _ => new ValueContainer());
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
    protected virtual void OnValueSet(AdamantiumProperty property, object oldEffectiveValue, object newValue, ValuePriority priority)
    {
    }

    protected virtual void OnComponentUpdated()
    {
        
    }

    protected void RaiseComponentUpdated()
    {
        OnComponentUpdated();
        ComponentUpdated?.Invoke(this, new ComponentUpdatedEventArgs(this));
    }

    protected void RaisePropertyChanged(AdamantiumProperty property, object oldValue, object newValue)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        var e = new AdamantiumPropertyChangedEventArgs(property, oldValue, newValue);

        try
        {
            OnPropertyChanged(e);
            property.RaiseChanged(this, e);   // global per-property hook: sender = THIS component (identity), not the property

            PropertyChanged?.Invoke(this, e);
            
            RaiseComponentUpdated();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    /// <summary>
    /// Called when a property is changed on the current <see cref="InheritanceParent"/>.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event args.</param>
    /// <remarks>
    /// Checks for changes in an inherited property value.
    /// </remarks>
    private void ParentPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == null)
        {
            throw new ArgumentException("e.Property cannot be null");
        }

        var metadata = e.Property.GetDefaultMetadata(GetType());
        if (metadata is not { Inherits: true } || HasExplicitValue(e.Property)) return;

        // PUSH the inherited value into this element's Inherited slot, so a read resolves it from the local value store
        // (one dictionary hit) instead of recursing up the WHOLE ancestor chain on every GetValue. Setting it fires this
        // element's own PropertyChanged, which cascades the push to its children.
        SetValue(e.Property, e.NewValue, ValuePriority.Inherited);
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
            if (oldParent != null)
                oldParent.PropertyChanged -= ParentPropertyChanged;

            inheritanceParent = value;

            if (inheritanceParent != null)
                inheritanceParent.PropertyChanged += ParentPropertyChanged;

            // The new parent (or null) brings a different set of inherited values. For every inherited property this
            // component hasn't set locally, raise a change from the old inherited value to the new one so the value AND
            // its callbacks (e.g. DataContext -> refresh bindings) apply. This makes inheritance order-independent: an
            // element attached AFTER its parent's value was assigned still picks it up here.
            foreach (var property in AdamantiumPropertyMap.GetRegistered(GetType()))
            {
                var metadata = property.GetDefaultMetadata(GetType());
                if (metadata is not { Inherits: true } || HasExplicitValue(property)) 
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
        var result = GetOrCalculateEffectiveValue(property);

        if (result == AdamantiumProperty.UnsetValue)
        {
            // COLD path only: the property has no value at any source priority. Validate registration HERE (not on every
            // read) - a set property short-circuits above, so measure/arrange reading Margin/alignment/min-max skips the
            // per-read map lookup + HasExplicitValue scan that used to sit on the hottest path in the engine.
            if (!AdamantiumPropertyMap.IsRegistered(this, property))
            {
                ThrowNotRegistered(property);
            }
            result = GetDefaultValue(property);
        }
        // Inherited values are PUSHED into the Inherited slot (see ParentPropertyChanged / InheritanceParent), so the
        // effective-value scan above already resolved them - no per-read recursion up the ancestor chain, and no
        // per-read metadata lookup + HasExplicitValue scan on the hottest path in the engine.

        return result;
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
        if (!triggerValues.TryGetValue(property.Name, out var container))
            triggerValues[property.Name] = container = new TriggerValueContainer();
        container.Set(token, value);
        SetValue(property, container.EffectiveValue, ValuePriority.Trigger);
    }

    // Drops one trigger's contribution; the slot falls back to the next trigger underneath (or UnsetValue -> Style/Local).
    public void ClearTriggerValue(AdamantiumProperty property, object token)
    {
        if (!triggerValues.TryGetValue(property.Name, out var container)) return;
        container.Remove(token);
        SetValue(property, container.EffectiveValue, ValuePriority.Trigger);
    }

    public void SetStyleValue(string propertyName, object value, Style style)
    {
        var property = AdamantiumPropertyMap.ResolveProperty(GetType(), propertyName);
        SetStyleValue(property, value, style);
    }

    public void SetStyleValue(AdamantiumProperty property, object value, Style style)
    {
        SetValue(property, value, ValuePriority.Style);
        AddStyleEntry(property.Name, value, style);
    }

    public void RemoveStyleValue(string propertyName, Style style)
    {
        var previousValue = RemoveStyleEntry(propertyName, style);
        SetValue(propertyName, previousValue, ValuePriority.Style);
    }

    private void AddStyleEntry(string propertyName, object value, Style style)
    {
        if (!styleValues.ContainsKey(propertyName))
        {
            styleValues[propertyName] = new StyleValueContainer();
        }
        styleValues[propertyName].AddValue(style, value);
    }
    
    private object RemoveStyleEntry(string propertyName, Style style)
    {
        if (!styleValues.ContainsKey(propertyName))
        {
            return AdamantiumProperty.UnsetValue;
        }
        
        return styleValues[propertyName].RemoveAndGetEffectiveValue(style);
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

        // The change is reported as the value the property now READS as - the coerced one. A callback that reacted to
        // the raw request would be looking at a value the property never had.
        var args = new AdamantiumPropertyChangedEventArgs(property, slotBefore, effectiveAfterWrite);

        // A write that leaves the EFFECTIVE value where it was is not a change, so it must not run the changed-callback.
        // Running it anyway is how two properties that assign each other close a cycle with no exit: a presenter whose
        // Content is bound to its own DataContext writes back the object it already sits on, the callback re-assigns the
        // same DataContext, that refreshes the bindings, which writes Content again - the app died of a stack overflow.
        // The equal-effective check below guards only invalidation and events, which is why the cycle ran above it.
        if (!Equals(oldEffectiveValue, effectiveAfterWrite))
        {
            metadata.PropertyChangedCallback?.Invoke(this, args);
        }

        // Implicit transitions: let an animatable element turn this base-value change into a smooth animation. Skipped
        // for animation-priority writes (those ARE the transition) so there is no recursion. May start an animation
        // re-entrantly (it writes the Animation slot) - that resolves cleanly via the equal-effective early-return below.
        if (priority != ValuePriority.Animation)
        {
            OnValueSet(property, oldEffectiveValue, value, priority);
        }

        // Re-read under the lock: a changed-callback or a started transition above may have written another slot.
        object newEffectiveValue;
        newEffectiveValue = GetOrCalculateEffectiveValue(property);

        if (Equals(oldEffectiveValue, newEffectiveValue))
        {
            return;
        }
        
        var element = this as IUIComponent;
        if (element is IMeasurableComponent measurable)
        {
            // TODO: think how to improve this code and get rid of type casting
            if (metadata.AffectsMeasure)
            {
                measurable.InvalidateMeasure();
                measurable.InvalidateArrange();
            }
            else if (metadata.AffectsArrange)
            {
                measurable.InvalidateArrange();
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
                    parent.InvalidateMeasure();
                    parent.InvalidateArrange();
                }
                else if (metadata.AffectsParentArrange)
                {
                    parent.InvalidateArrange();
                }
            }
        }

        if (metadata.AffectsRender)
        {
            // Keep this element subscribed to the CURRENT brush of a brush-valued render property, so a later mutation OF
            // the brush itself (an animated gradient stop, a recoloured fill) re-renders it - not only replacing the whole
            // brush. One place covers any AffectsRender brush property on any element. old != new is guaranteed above.
            if (oldEffectiveValue is Media.Brush oldBrush) oldBrush.Changed -= OnAffectsRenderBrushChanged;
            if (newEffectiveValue is Media.Brush newBrush) newBrush.Changed += OnAffectsRenderBrushChanged;
            element?.InvalidateRender(false);
        }
        // Paint-only: same shape, same draw commands, a different colour. Re-bake what is already recorded instead of
        // re-recording the element. Checked as an ELSE of AffectsRender - a property declaring both would mean "the shape
        // changed AND only the colour changed", which is a contradiction; the stronger (geometry) claim wins.
        else if (metadata.AffectsPaint)
        {
            element?.InvalidatePaint();
        }

        if (raiseValueChangedEvent)
        {
            RaisePropertyChanged(property, oldEffectiveValue, newEffectiveValue);
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
    private void OnAffectsRenderBrushChanged(object sender, EventArgs e)
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