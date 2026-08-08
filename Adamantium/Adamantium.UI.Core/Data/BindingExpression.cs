using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// A live <c>{Binding}</c> connection between a target <see cref="AdamantiumProperty"/> and a source object reached
/// through the binding's <see cref="Binding.Path"/>. The source is the binding's explicit <see cref="Binding.Source"/>,
/// otherwise the target's (inherited) <c>DataContext</c>. On connect — and whenever the source raises
/// <see cref="INotifyPropertyChanged"/> — the source value is pushed to the target (one-way); two-way also writes the
/// target back. Dotted paths (<c>A.B.C</c>) are walked by reflection and the leaf object is observed.
/// <see cref="EstablishConnection"/> is idempotent, so it is re-run when the target's DataContext changes (the tree is
/// usually built before its DataContext is assigned).
/// <para>When created with a null <see cref="BindingExpressionBase.TargetProperty"/> the expression runs in
/// <em>producer</em> mode: instead of writing to a target it exposes the converted value via
/// <see cref="BindingExpressionBase.ProducedValue"/> and raises <see cref="BindingExpressionBase.ValueChanged"/> — this
/// is how a child of a <see cref="MultiBinding"/> feeds the parent converter.</para>
/// </summary>
public class BindingExpression : BindingExpressionBase
{
   public object ResolvedSource { get; private set; }
   public string SourcePropertyName { get; private set; }

   private PropertyInfo _sourceProperty;
   private Func<object, object> _sourceGetter;   // compiled reader for _sourceProperty (the hot ComputeValue path)
   private bool _bindToSource;   // empty path ({Binding}, {Binding ElementName=x}) -> the value IS the resolved source object
   private INotifyPropertyChanged _observed;
   private AdamantiumComponent _observedComponent;   // element source ({ElementName}) - observed via AdamantiumProperty changes, not INPC
   private string[] _segments;   // cached Binding.Path split on '.', computed once (path is fixed per expression)

   // Property-accessor cache: `GetType().GetProperty(name)` is a slow metadata search and `PropertyInfo.GetValue` a slow
   // reflection invoke, and a virtualized list re-runs both for EVERY binding of EVERY recycled row on scroll (a fling can
   // rebind hundreds of rows x ~15 bindings/frame). Cache the PropertyInfo + a COMPILED getter delegate per (type, name)
   // so a rebind is a dictionary hit + a near-native call instead. Shared across all bindings, populated once per shape.
   private static readonly ConcurrentDictionary<(Type, string), (PropertyInfo Prop, Func<object, object> Getter)> _accessors = new();

   private static (PropertyInfo Prop, Func<object, object> Getter) GetAccessor(Type type, string name)
      => _accessors.GetOrAdd((type, name), static key =>
      {
         var prop = key.Item1.GetProperty(key.Item2);
         if (prop is not { CanRead: true }) return (prop, null);
         var o = Expression.Parameter(typeof(object), "o");
         var body = Expression.Convert(Expression.Property(Expression.Convert(o, key.Item1), prop), typeof(object));
         return (prop, Expression.Lambda<Func<object, object>>(body, o).Compile());
      });

   public Binding Binding { get; set; }
   public BindingMode Mode { get; set; }

   private bool IsProducer => TargetProperty == null;

   public BindingExpression(IAdamantiumComponent target, AdamantiumProperty targetProperty, BindingBase bindingBase)
   {
      Target = target;
      TargetProperty = targetProperty;
      BindingBase = bindingBase;
      Binding = (Binding)bindingBase;
      Mode = Binding.Mode;
   }

   public BindingExpression(IAdamantiumComponent target, string targetPropertyName, BindingBase bindingBase)
      : this(target, target.GetProperty(targetPropertyName), bindingBase)
   {
   }

   // Factory + dispatch: a MultiBinding becomes a MultiBindingExpression, anything else a plain BindingExpression.
   // This is the single place that turns a BindingBase into a live, connected expression.
   public static BindingExpressionBase CreateBindingExpression(IAdamantiumComponent target,
      AdamantiumProperty targetProperty, BindingBase bindingBase)
   {
      BindingExpressionBase expression = bindingBase is MultiBinding
         ? new MultiBindingExpression(target, targetProperty, bindingBase)
         : new BindingExpression(target, targetProperty, bindingBase);
      expression.EstablishConnection();
      return expression;
   }

   public static BindingExpressionBase CreateBindingExpression(IAdamantiumComponent target,
      string targetPropertyName, BindingBase bindingBase)
      => CreateBindingExpression(target, target.GetProperty(targetPropertyName), bindingBase);

   public override void EstablishConnection()
   {
      // Re-resolve against the (possibly new) DataContext BEFORE touching subscriptions.
      var previousObserved = _observed;
      ResolveSource();
      var newObserved = ResolvedSource as INotifyPropertyChanged;

      // Fast path: the resolved SOURCE OBJECT is unchanged. This is the norm for a virtualized-list rebind whose bound
      // path points at a shared sub-view-model every item exposes (e.g. item.Stroke): the DataContext changed, but
      // item.Stroke is the SAME object for every item. The existing PropertyChanged subscription is therefore still
      // correct - so do NOT unsubscribe + re-subscribe. On a source with many subscribers (one per such binding per
      // realized tile), each -=/+= rebuilds the whole multicast invocation list (O(subscribers)); doing that for every
      // tile every scroll frame is O(N^2) and was the ~118 KB-per-binding rebind allocation storm (gen2 GC freeze).
      if (ReferenceEquals(newObserved, previousObserved) && previousObserved != null)
      {
         // The source object AND our subscription are unchanged, so the value cannot have moved since it was last
         // pushed (a real change arrives via OnSourcePropertyChanged -> batched apply). The recycled target already
         // holds exactly this value - the item it previously showed bound to this SAME shared source. So skip the
         // re-push entirely: for a shared sub-VM (the 12 Stroke.* bindings per tile) this was re-computing + re-writing
         // an identical value on every rebind of every tile every scroll frame - the dominant per-rebind cost
         // (updTarget ~33 ms/frame, incl. the brush-string re-parse). Producer mode must still republish for its parent.
         if (IsProducer) Refresh();
         return;
      }

      // Source object genuinely changed: tear down the old subscription and establish the new one.
      CloseConnection();

      // OneWayToSource: the flow is TARGET -> SOURCE only. Never observe or push the source; write its initial value from
      // the target, then update it whenever the target changes (a target with a read-only/private-set property that can't
      // be bound TwoWay - e.g. reading a control's SelectedItem out into a view-model).
      if (Mode == BindingMode.OneWayToSource && !IsProducer)
      {
         UpdateSource();
         if (Target != null) Target.PropertyChanged += OnTargetPropertyChanged;
         return;
      }

      Refresh();                    // initial push (or produce)
      if (newObserved != null)
      {
         _observed = newObserved;
         SharedSourceRegistry.Subscribe(newObserved, this);   // one shared subscription per source, O(1) add (see SharedSourceRegistry)
      }
      // Element source ({ElementName}): a UI element does not implement INotifyPropertyChanged - its properties change
      // through the AdamantiumProperty system. Observe THAT so source->target updates flow (a wheel-zoom moving a slider
      // bound to ZoomBox.ScaleX, a value readout, ...). Not shared (element bindings aren't the virtualized-list hot path).
      if (newObserved == null && ResolvedSource is AdamantiumComponent component)
      {
         _observedComponent = component;
         component.PropertyChanged += OnSourceComponentChanged;
      }
      if (Mode == BindingMode.TwoWay && !IsProducer && Target != null)
         Target.PropertyChanged += OnTargetPropertyChanged;
   }

   public override void CloseConnection()
   {
      BindingUpdateQueue.Remove(this);   // F2: a closed binding must not be applied by a later flush
      if (_observed != null)
      {
         SharedSourceRegistry.Unsubscribe(_observed, this);   // O(1) remove - the reason a shrunk window can release cheaply
         _observed = null;
      }
      if (_observedComponent != null)
      {
         _observedComponent.PropertyChanged -= OnSourceComponentChanged;
         _observedComponent = null;
      }
      if ((Mode == BindingMode.TwoWay || Mode == BindingMode.OneWayToSource) && !IsProducer && Target != null)
         Target.PropertyChanged -= OnTargetPropertyChanged;
   }

   // Source = explicit Binding.Source, else the target's DataContext. Walk all but the last path segment to reach
   // the object that owns the bound property; the leaf segment is the property we read/observe.
   private void ResolveSource()
   {
      ResolvedSource = null;
      _sourceProperty = null;
      _sourceGetter = null;
      SourcePropertyName = null;
      _bindToSource = false;

      var root = Binding.Source ?? ResolveElementName() ?? DataContextSource?.DataContext;
      var path = Binding.Path?.Path;
      if (root == null) return;

      // Empty path with a source -> bind to the SOURCE OBJECT ITSELF ({Binding}, {Binding ElementName=x}, {Binding Source=y}),
      // the standard WPF behaviour. There is no leaf property to read/observe - the value simply IS the resolved source.
      if (string.IsNullOrEmpty(path))
      {
         ResolvedSource = root;
         _bindToSource = true;
         return;
      }

      // Split ONCE per expression, not per resolve: the path is fixed for the binding's life, but ResolveSource runs on
      // every DataContext change (every rebind of every recycled tile) - a fresh string[] alloc per call was steady GC
      // churn on the scroll hot path.
      var segments = _segments ??= path.Split('.');
      object current = root;
      for (var i = 0; i < segments.Length - 1 && current != null; i++)
         current = GetAccessor(current.GetType(), segments[i]).Getter?.Invoke(current);

      if (current == null)
         return;

      ResolvedSource = current;
      SourcePropertyName = segments[^1];
      (_sourceProperty, _sourceGetter) = GetAccessor(current.GetType(), SourcePropertyName);
   }

   // {Binding Path, ElementName=X}: the source is the element named X in the target's tree (not the DataContext). Resolved
   // by walking to the tree root and searching for a matching Name - re-runs with ResolveSource on every DataContext change
   // (attach), so a forward-referenced element resolves once the whole tree exists. Null until then.
   private object ResolveElementName()
   {
      if (string.IsNullOrEmpty(Binding.ElementName) || Target is not IUIComponent target) return null;

      // The TEMPLATE first, when this binding was written inside one. A template is its own namescope - its names belong
      // to the control that applied it, not to the window - so they are not on the visual tree under Name and the walk
      // below cannot see them. Without this an ElementName inside a ControlTemplate silently resolved to nothing, which
      // is a binding that never reports a problem and simply never has a value.
      if (target.TemplatedParent is ITemplateHost host && host.GetTemplateChild(Binding.ElementName) is IUIComponent inTemplate)
         return inTemplate;

      var root = target;
      while (root.VisualParent is { } parent) root = parent;
      return FindByName(root, Binding.ElementName);
   }

   private static IUIComponent FindByName(IUIComponent node, string name)
   {
      if (node is IFundamentalUIComponent named && named.Name == name) return node;
      foreach (var child in node.VisualChildren)
         if (FindByName(child, name) is { } found) return found;
      return null;
   }

   // True WHILE UpdateSource writes the source (a TwoWay write-back from the target). The write raises the source's
   // PropertyChanged synchronously; without this guard OnSourcePropertyChanged would schedule a source->target push that
   // echoes our OWN write back onto the target one frame later - fighting an active drag so a TwoWay-bound slider never
   // converges on the endpoint (stuck ~0.1 short of Minimum). Other bindings to the same source still update; only THIS
   // expression skips echoing its own write.
   private bool _writingSource;

   // Called by SharedSourceRegistry (the source's single fan-out handler), not subscribed directly.
   internal void OnSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
   {
      if (_writingSource) return;   // our own TwoWay write-back - don't echo it back to the target
      // F2: a runtime source change is batched + coalesced (applied once per frame), not pushed synchronously.
      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == SourcePropertyName)
         ScheduleUpdate();
   }

   // Element source ({ElementName}) property changed via the AdamantiumProperty system - push to the target if it's the
   // property we bind.
   private void OnSourceComponentChanged(object sender, AdamantiumPropertyChangedEventArgs e)
   {
      if (_writingSource) return;   // our own TwoWay write-back - don't echo it back to the target
      if (e.Property?.Name == SourcePropertyName)
         ScheduleUpdate();
   }

   // F2: the coalesced apply reads the current source value (producer mode publishes ProducedValue, top-level pushes
   // to the target) - same path as a source change, just deferred to the per-frame flush.
   internal override void ApplyPending() => Refresh();

   private void OnTargetPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
   {
      if ((Mode == BindingMode.TwoWay || Mode == BindingMode.OneWayToSource) && e.Property == TargetProperty)
         UpdateSource();
   }

   // Top-level: push to the target property. Producer: publish ProducedValue for a parent MultiBinding.
   private void Refresh()
   {
      if (IsProducer)
      {
         ProducedValue = ComputeValue(typeof(object));
         RaiseValueChanged();
      }
      else
      {
         UpdateTarget();
      }
   }

   // Reads the source value through the (optional) converter. targetType drives the converter's requested type.
   // FallbackValue is used when the binding can't resolve a source/path (WPF semantics); TargetNullValue when the
   // resolved value is null (falling back to FallbackValue if no TargetNullValue is set).
   private object ComputeValue(Type targetType)
   {
      // Empty-path binding: the value is the resolved source object itself (optionally run through the converter).
      if (_bindToSource)
      {
         var self = Binding.Converter != null ? ConvertCached(ResolvedSource, targetType) : ResolvedSource;
         return self ?? BindingBase.TargetNullValue ?? BindingBase.FallbackValue;
      }
      if (_sourceProperty == null) return BindingBase.FallbackValue;
      var value = _sourceGetter != null ? _sourceGetter(ResolvedSource) : _sourceProperty.GetValue(ResolvedSource);
      if (Binding.Converter != null)
         value = ConvertCached(value, targetType);
      if (value == null)
         value = BindingBase.TargetNullValue ?? BindingBase.FallbackValue;
      return value;
   }

   // Per-SOURCE-OBJECT converted-value cache. A value-converter (e.g. a colour string -> Brush) is otherwise re-run for
   // every realized tile every scroll frame - the dominant per-rebind cost. Keyed by the SOURCE OBJECT (the item), so
   // each item gets ONE converted instance: reused across rebinds and across whichever recycled container currently shows
   // the item, yet DISTINCT per item (mutating one item's brush never bleeds into another item that happens to share a
   // colour string). Weak keys -> an item's cached conversions die with the item, no manual eviction. The stored RAW
   // input guards staleness: if the source property changed (item.Color mutated -> re-convert), the raw differs and we
   // rebuild. A boxed value-type source can't key a weak table (identity-less), so it falls back to a direct convert.
   private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object,
      Dictionary<(IValueConverter, Type, string), (object Raw, object Converted)>> _convertCache = new();

   private object ConvertCached(object raw, Type targetType)
   {
      var source = ResolvedSource;
      if (source == null || source.GetType().IsValueType)
         return Binding.Converter.Convert(raw, targetType, Binding.ConverterParameter, CultureInfo.CurrentCulture);

      var cache = _convertCache.GetOrCreateValue(source);
      var key = (Binding.Converter, targetType, SourcePropertyName);
      if (cache.TryGetValue(key, out var entry) && Equals(entry.Raw, raw))
         return entry.Converted;

      var converted = Binding.Converter.Convert(raw, targetType, Binding.ConverterParameter, CultureInfo.CurrentCulture);
      cache[key] = (raw, converted);
      return converted;
   }

   public override void UpdateTarget()
   {
      if (TargetProperty == null) return;
      var value = ComputeValue(TargetProperty.PropertyType);
      // No source value and no fallback: leave the target at its default (don't clobber with null).
      if (value == null) return;
      // Can't make the value fit the target type (e.g. a FallbackValue="50" on an ICommand property)? Leave the target
      // at its default instead of pushing an incompatible value, which would throw in SetValue and abort the whole load.
      if (!TryCoerce(value, TargetProperty.PropertyType, out var coerced)) return;
      Target.SetValue(TargetProperty, coerced, ValuePriority.Binding);
      RuntimeStats.BindingUpdatesApplied++;   // diagnostics: a binding wrote its target (initial/establish, DataContext re-resolve, or a batched source change)
   }

   public override void UpdateSource()
   {
      if (_sourceProperty is not { CanWrite: true } || TargetProperty == null) return;
      var targetValue = Target.GetValue(TargetProperty);
      var value = targetValue;
      if (Binding.Converter != null)
         value = Binding.Converter.ConvertBack(value, _sourceProperty.PropertyType, Binding.ConverterParameter,
            CultureInfo.CurrentCulture);

      // "No value" cannot be written into a source that has no way to hold it: a NumericUpDown that was cleared has a
      // null Value, and a view-model exposing a plain double would take it as a reflection error mid-keystroke. Leave
      // the source at what it last agreed to instead - and leave our own copy of it alone too, since nothing moved.
      if (value == null && _sourceProperty.PropertyType.IsValueType &&
          Nullable.GetUnderlyingType(_sourceProperty.PropertyType) == null)
      {
         return;
      }

      // Guard the ECHO (see _writingSource): our synchronous source write must not schedule a source->target push back.
      _writingSource = true;
      try
      {
         _sourceProperty.SetValue(ResolvedSource, Coerce(value, _sourceProperty.PropertyType));
      }
      finally
      {
         _writingSource = false;
      }

      // The Binding slot is this expression's copy of what the SOURCE holds, and the write above just moved the source.
      // The echo guard suppresses the push that would normally refresh that copy, so without this the target keeps a
      // request the source no longer has anywhere: a value that had to be CLAMPED was published to the source as the
      // clamped one, yet the target still remembered the number from before the clamp - and a later re-coercion (the
      // ceiling moving back up) resurrected it and overwrote the source with it. A range slider whose end had been
      // squeezed by a shrinking Maximum therefore rode the edge all the way back up instead of staying where the
      // view-model said it was. Re-entrancy is bounded by the write below leaving the effective value alone (it IS the
      // effective value), which raises nothing.
      if (Mode != BindingMode.TwoWay || _syncingSlot) 
         return;
      
      _syncingSlot = true;
      try
      {
         Target.SetValue(TargetProperty, targetValue, ValuePriority.Binding);
      }
      finally
      {
         _syncingSlot = false;
      }
   }

   private bool _syncingSlot;   // see UpdateSource: the slot refresh must not drive another write-back

   // Lenient coercion: keeps the raw value when it can't be made to fit (used writing back to source).
   private static object Coerce(object value, Type targetType)
      => TryCoerce(value, targetType, out var result) ? result : value;

   // Strict: false (result=null) when the value can't be made to fit, so the caller skips the assignment instead of
   // pushing something that would throw.
   private static bool TryCoerce(object value, Type targetType, out object result)
   {
      result = value;
      if (value == null || targetType == null || targetType.IsInstanceOfType(value)) return true;
      if (targetType == typeof(string)) { result = value.ToString(); return true; }
      try
      {
         result = Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType, CultureInfo.CurrentCulture);
         return true;
      }
      catch
      {
         // A string Convert.ChangeType cannot place (Geometry, Brush, Thickness...) still converts through the engine's
         // TypeParser - the SAME conversion the markup compiler runs, so an attribute and a {Binding} land alike.
         if (value is string)
         {
            var parsed = TypeCastFactory.CastFromString(value, targetType);
            if (parsed != AdamantiumProperty.UnsetValue)
            {
               result = parsed;
               return true;
            }
         }

         result = null;
         return false;
      }
   }
}
