using System.ComponentModel;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// Live connection for <c>{Ancestor ...}</c>: resolves the nearest matching ancestor of the target (visual tree by
/// default, logical when <see cref="Ancestor.Logical"/>), reads its source property and pushes it to the target. It
/// RE-RESOLVES whenever the target (re)attaches to the tree, so a template rebuild / re-parent / virtualization recycle
/// refreshes the binding instead of leaving it pointed at a stale (or never-found) ancestor - the WPF failure mode.
/// </summary>
public class AncestorBindingExpression : BindingExpressionBase
{
    private readonly Ancestor _def;

    /// <summary>Priority the resolved value is written at. Binding for a normal property; a Setter passes Style/Trigger so
    /// the ancestor value slots into the right band of the value stack.</summary>
    internal ValuePriority Priority { get; set; } = ValuePriority.Binding;

    private IFundamentalUIComponent _source;
    private AdamantiumProperty _sourceProperty;   // the ancestor's AdamantiumProperty for the FIRST path segment
    private string[] _segments = [];
    private INotifyPropertyChanged _leafOwner;    // for a dotted path: the object owning the leaf, observed for changes
    private bool _hooked;
    private bool _targetHooked;

    public AncestorBindingExpression(IAdamantiumComponent target, AdamantiumProperty targetProperty, Ancestor def)
    {
        Target = target;
        TargetProperty = targetProperty;
        _def = def;
    }

    public override void EstablishConnection()
    {
        CloseConnection();
        HookTree();
        ResolveAndReport();   // resolves now if the target is already rooted; otherwise an attach event re-runs this
    }

    public override void CloseConnection()
    {
        DetachSource();
        UnhookTree();
    }

    // The element the subscriptions are ON. The target itself may be a Transform or another non-tree component, which
    // has no attach/detach of its own and rides its owner's - and the SAME instance must be used to unsubscribe, so it
    // is kept rather than re-walked (the owner can change between hook and unhook).
    private IFundamentalUIComponent _anchor;

    // Subscribe to the target's own attach/detach so the ancestor is (re)resolved every time the target enters the tree.
    private void HookTree()
    {
        if (_hooked) return;

        _anchor = DataContextSource;
        if (_anchor == null) return;

        if (_def.Logical)
        {
            _anchor.AttachedToLogicalTree += OnLogicalAttached;
            _anchor.DetachedFromLogicalTree += OnLogicalDetached;
        }
        else if (_anchor is IUIComponent visual)
        {
            visual.AttachedToVisualTreeEvent += OnVisualAttached;
            visual.DetachedFromVisualTreeEvent += OnVisualDetached;
        }
        else
        {
            // Non-visual target (a Behavior): it has no visual node, so it re-resolves against its HOST's visual tree.
            // AddLogicalChild (attach to the host) raises the logical-tree event - use that to (re)resolve.
            _anchor.AttachedToLogicalTree += OnLogicalAttached;
            _anchor.DetachedFromLogicalTree += OnLogicalDetached;
        }
        _hooked = true;
    }

    private void UnhookTree()
    {
        if (!_hooked || _anchor == null) return;
        if (_def.Logical)
        {
            _anchor.AttachedToLogicalTree -= OnLogicalAttached;
            _anchor.DetachedFromLogicalTree -= OnLogicalDetached;
        }
        else if (_anchor is IUIComponent visual)
        {
            visual.AttachedToVisualTreeEvent -= OnVisualAttached;
            visual.DetachedFromVisualTreeEvent -= OnVisualDetached;
        }
        else
        {
            _anchor.AttachedToLogicalTree -= OnLogicalAttached;
            _anchor.DetachedFromLogicalTree -= OnLogicalDetached;
        }
        _hooked = false;
        _anchor = null;
    }

    private void OnLogicalAttached(object sender, LogicalTreeAttachmentEventArgs e) => ResolveAndReport();
    private void OnVisualAttached(object sender, VisualTreeAttachmentEventArgs e) => ResolveAndReport();
    private void OnLogicalDetached(object sender, LogicalTreeAttachmentEventArgs e) => OnDetached();
    private void OnVisualDetached(object sender, VisualTreeAttachmentEventArgs e) => OnDetached();

    // Resolve, then classify the outcome. A found source is already Active (or PathError for a missing property - set in
    // Resolve). A MISS is a real "no such ancestor" (PathError - the failure WPF swallowed silently) when the target IS
    // in the tree, or merely NotAttached when it isn't rooted yet (the transient case; a later attach re-runs this).
    private void ResolveAndReport()
    {
        Resolve();
        if (_source != null) return;
        ApplyFallback();   // no source resolved -> push FallbackValue if one was set (else leave the target at its default)
        if (IsTargetAttached())
        {
            Status = BindingStatus.PathError;
            BindingTrace.Log($"{{Ancestor}} on {Target?.GetType().Name}.{TargetProperty?.Name}: no {_def.AncestorType?.Name} ancestor found in the {(_def.Logical ? "logical" : "visual")} tree.");
        }
        else
        {
            Status = BindingStatus.NotAttached;
        }
    }

    // No source: run the pipeline with an Unset raw value so a FallbackValue / TargetNullValue (if any) still reaches the
    // target. With neither set this produces Unset and leaves the target at its default.
    private void ApplyFallback()
    {
        if (TargetProperty == null) return;
        var value = RelativeBindingPipeline.Produce(RelativeBindingPipeline.Unset, _def.Converter, _def.ConverterParameter,
            TargetProperty.PropertyType, _def.FallbackValue, _def.TargetNullValue);
        if (!ReferenceEquals(value, RelativeBindingPipeline.Unset)) Target.SetValue(TargetProperty, value, Priority);
    }

    // Asked of the nearest ELEMENT: a Transform (or any other non-tree target) has no place in the tree of its own and
    // is attached exactly when its owner is.
    private bool IsTargetAttached()
    {
        var element = DataContextSource;
        return _def.Logical ? element?.GetLogicalParentOrBridge() != null
             : element is IUIComponent visual ? visual.VisualParent != null
             : element?.LogicalParent != null;   // non-visual (a Behavior): attached once it has a host
    }

    private void OnDetached()
    {
        DetachSource();
        Status = BindingStatus.Detached;
    }

    // Re-walk for the ancestor and rebind. Detaching the old source first means a re-parent (detach then attach) lands
    // cleanly on the NEW ancestor rather than stacking a second subscription on the old one.
    private void Resolve()
    {
        DetachSource();
        _source = FindAncestor();
        if (_source == null) return;   // not rooted yet, or no match - a later attach re-resolves (and warns then)

        _segments = _def.Path?.Split('.') ?? [];
        _sourceProperty = _segments.Length > 0 ? _source.GetProperty(_segments[0]) : null;

        // The first hop must exist as either an AdamantiumProperty (observable) or a plain CLR property (for a dotted
        // path off e.g. DataContext). Neither -> a genuine path error.
        var firstHopExists = _sourceProperty != null
            || (_segments.Length > 0 && _source.GetType().GetProperty(_segments[0]) != null);
        if (!firstHopExists)
        {
            Status = BindingStatus.PathError;
            BindingTrace.Log($"{{Ancestor}} on {Target?.GetType().Name}.{TargetProperty?.Name}: {_def.AncestorType?.Name} has no '{_def.Path}' property.");
            return;
        }

        if (_sourceProperty != null) _source.PropertyChanged += OnSourcePropertyChanged;
        HookLeafOwner();

        // TwoWay only for a single, directly-observable property hop - writing back through a reflected dotted chain has
        // no well-defined meaning.
        if (_def.Mode == BindingMode.TwoWay && _segments.Length == 1 && _sourceProperty != null && Target != null)
        {
            Target.PropertyChanged += OnTargetPropertyChanged;
            _targetHooked = true;
        }
        UpdateTarget();
        Status = BindingStatus.Active;
    }

    // For a dotted path, observe the object that owns the leaf property so a change to the leaf (e.g. a VM's Title)
    // propagates. Single-segment paths need no leaf hook (the source property itself is observed above).
    private void HookLeafOwner()
    {
        if (_segments.Length <= 1) return;
        _leafOwner = RelativeBindingPipeline.LeafOwner(_source, _sourceProperty, _segments) as INotifyPropertyChanged;
        if (_leafOwner != null) _leafOwner.PropertyChanged += OnLeafOwnerChanged;
    }

    private void UnhookLeafOwner()
    {
        if (_leafOwner != null) _leafOwner.PropertyChanged -= OnLeafOwnerChanged;
        _leafOwner = null;
    }

    private void OnLeafOwnerChanged(object sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _segments[^1]) ScheduleUpdate();
    }

    private IFundamentalUIComponent FindAncestor()
    {
        // The walk starts from the nearest ELEMENT: the target may be a Transform, which is not in the tree at all and
        // whose ancestors are its owner's.
        var anchor = DataContextSource;
        if (_def.AncestorType == null || anchor == null) return null;
        var skip = _def.Skip;

        if (_def.Logical)
        {
            // Logical walk BRIDGES template boundaries via TemplatedParent (GetLogicalParentOrBridge) - otherwise it
            // dead-ends at each container's template part and never reaches the ItemsControl. See docs/TREE_MODEL_DESIGN.md.
            for (var cur = anchor.GetLogicalParentOrBridge(); cur != null; cur = cur.GetLogicalParentOrBridge())
            {
                if (_def.Stop != null && _def.Stop.IsInstanceOfType(cur)) return null;
                if (Matches(cur) && skip-- <= 0) return cur;
            }
        }
        else
        {
            // Default (visual) walk. For a visual target start at its VisualParent; for a NON-visual target (a Behavior)
            // start at its host - the element it's attached to (its logical parent) - and walk that host's VISUAL tree.
            // The visual tree crosses template boundaries, so this reaches an ItemsControl / Window ancestor that a
            // logical walk (which stops at each container's template parts) never could.
            var start = anchor is IUIComponent visual ? visual.VisualParent : anchor.LogicalParent as IUIComponent;
            for (var cur = start; cur != null; cur = cur.VisualParent)
            {
                if (_def.Stop != null && _def.Stop.IsInstanceOfType(cur)) return null;
                if (Matches(cur) && skip-- <= 0) return cur;
            }
        }
        return null;
    }

    // is-a match (a base type / interface matches subtypes, like the style selectors), plus an optional Name filter.
    private bool Matches(IFundamentalUIComponent candidate)
    {
        if (!_def.AncestorType.IsInstanceOfType(candidate)) return false;
        return string.IsNullOrEmpty(_def.Name) || candidate.Name == _def.Name;
    }

    private void OnSourcePropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != _sourceProperty) return;
        if (_segments.Length > 1) { UnhookLeafOwner(); HookLeafOwner(); }   // first hop changed -> the leaf owner moved
        ScheduleUpdate();   // coalesced per-frame flush, like {Binding}
    }

    private void OnTargetPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property == TargetProperty) UpdateSource();
    }

    public override void UpdateTarget()
    {
        if (_source == null || TargetProperty == null) return;
        var raw = RelativeBindingPipeline.Walk(_source, _sourceProperty, _segments);
        var value = RelativeBindingPipeline.Produce(raw, _def.Converter, _def.ConverterParameter,
            TargetProperty.PropertyType, _def.FallbackValue, _def.TargetNullValue);
        if (ReferenceEquals(value, RelativeBindingPipeline.Unset)) return;   // nothing usable - leave the target default
        Target.SetValue(TargetProperty, value, Priority);
    }

    public override void UpdateSource()
    {
        if (_source == null || _sourceProperty == null || _def.Mode != BindingMode.TwoWay || _segments.Length != 1) return;
        // Write-back is authoritative (the user edited the target), so push at Local priority - a Binding-priority write
        // would be masked by any Local/Style value the ancestor property already holds.
        var value = RelativeBindingPipeline.ConvertBack(Target.GetValue(TargetProperty), _def.Converter, _def.ConverterParameter, _sourceProperty.PropertyType);
        if (RelativeBindingPipeline.TryCoerce(value, _sourceProperty.PropertyType, out var coerced))
            _source.SetValue(_sourceProperty, coerced, ValuePriority.Local);
    }

    private void DetachSource()
    {
        BindingUpdateQueue.Remove(this);   // a re-resolve must not be applied to the old source by a later flush
        if (_source != null) _source.PropertyChanged -= OnSourcePropertyChanged;
        if (_targetHooked && Target != null) Target.PropertyChanged -= OnTargetPropertyChanged;
        UnhookLeafOwner();
        _source = null;
        _sourceProperty = null;
        _segments = [];
        _targetHooked = false;
    }
}
