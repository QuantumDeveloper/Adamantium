using System.ComponentModel;
using System.Globalization;
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
   private INotifyPropertyChanged _observed;

   public Binding Binding { get; set; }
   public BindingMode Mode { get; set; }

   private bool IsProducer => TargetProperty == null;

   public BindingExpression(IFundamentalUIComponent target, AdamantiumProperty targetProperty, BindingBase bindingBase)
   {
      Target = target;
      TargetProperty = targetProperty;
      BindingBase = bindingBase;
      Binding = (Binding)bindingBase;
      Mode = Binding.Mode;
   }

   public BindingExpression(IFundamentalUIComponent target, string targetPropertyName, BindingBase bindingBase)
      : this(target, target.GetProperty(targetPropertyName), bindingBase)
   {
   }

   // Factory + dispatch: a MultiBinding becomes a MultiBindingExpression, anything else a plain BindingExpression.
   // This is the single place that turns a BindingBase into a live, connected expression.
   public static BindingExpressionBase CreateBindingExpression(IFundamentalUIComponent target,
      AdamantiumProperty targetProperty, BindingBase bindingBase)
   {
      BindingExpressionBase expression = bindingBase is MultiBinding
         ? new MultiBindingExpression(target, targetProperty, bindingBase)
         : new BindingExpression(target, targetProperty, bindingBase);
      expression.EstablishConnection();
      return expression;
   }

   public static BindingExpressionBase CreateBindingExpression(IFundamentalUIComponent target,
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
      // tile every scroll frame is O(N^2) and was the ~118 KB-per-binding rebind allocation storm (gen2 GC freeze). Just
      // re-read the value against the new DataContext.
      if (ReferenceEquals(newObserved, previousObserved) && previousObserved != null)
      {
         Refresh();
         return;
      }

      // Source object genuinely changed: tear down the old subscription and establish the new one.
      CloseConnection();
      Refresh();                    // initial push (or produce)
      if (newObserved != null)
      {
         _observed = newObserved;
         newObserved.PropertyChanged += OnSourcePropertyChanged;
      }
      if (Mode == BindingMode.TwoWay && !IsProducer && Target != null)
         Target.PropertyChanged += OnTargetPropertyChanged;
   }

   public override void CloseConnection()
   {
      BindingUpdateQueue.Remove(this);   // F2: a closed binding must not be applied by a later flush
      if (_observed != null)
      {
         _observed.PropertyChanged -= OnSourcePropertyChanged;
         _observed = null;
      }
      if (Mode == BindingMode.TwoWay && !IsProducer && Target != null)
         Target.PropertyChanged -= OnTargetPropertyChanged;
   }

   // Source = explicit Binding.Source, else the target's DataContext. Walk all but the last path segment to reach
   // the object that owns the bound property; the leaf segment is the property we read/observe.
   private void ResolveSource()
   {
      ResolvedSource = null;
      _sourceProperty = null;
      SourcePropertyName = null;

      var root = Binding.Source ?? Target?.DataContext;
      var path = Binding.Path?.Path;
      if (root == null || string.IsNullOrEmpty(path)) return;

      var segments = path.Split('.');
      object current = root;
      for (var i = 0; i < segments.Length - 1 && current != null; i++)
         current = current.GetType().GetProperty(segments[i])?.GetValue(current);

      if (current == null)
         return;

      ResolvedSource = current;
      SourcePropertyName = segments[^1];
      _sourceProperty = current.GetType().GetProperty(SourcePropertyName);
   }

   private void OnSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
   {
      // F2: a runtime source change is batched + coalesced (applied once per frame), not pushed synchronously.
      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == SourcePropertyName)
         ScheduleUpdate();
   }

   // F2: the coalesced apply reads the current source value (producer mode publishes ProducedValue, top-level pushes
   // to the target) - same path as a source change, just deferred to the per-frame flush.
   internal override void ApplyPending() => Refresh();

   private void OnTargetPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
   {
      if (Mode == BindingMode.TwoWay && e.Property == TargetProperty)
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
      if (_sourceProperty == null) return BindingBase.FallbackValue;
      var value = _sourceProperty.GetValue(ResolvedSource);
      if (Binding.Converter != null)
         value = Binding.Converter.Convert(value, targetType, Binding.ConverterParameter, CultureInfo.CurrentCulture);
      if (value == null)
         value = BindingBase.TargetNullValue ?? BindingBase.FallbackValue;
      return value;
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
      var value = Target.GetValue(TargetProperty);
      if (Binding.Converter != null)
         value = Binding.Converter.ConvertBack(value, _sourceProperty.PropertyType, Binding.ConverterParameter, CultureInfo.CurrentCulture);
      _sourceProperty.SetValue(ResolvedSource, Coerce(value, _sourceProperty.PropertyType));
   }

   // Minimal target-type coercion (no full converter pipeline): pass-through when assignable, ToString for a string
   // target, else a best-effort Convert.ChangeType; on failure keep the raw value (lenient - used writing back to source).
   private static object Coerce(object value, Type targetType)
      => TryCoerce(value, targetType, out var result) ? result : value;

   // Strict coercion: pass-through when assignable, ToString for a string target, else Convert.ChangeType. Returns
   // false (with result=null) when the value can't be made to fit - the caller skips the assignment rather than push an
   // incompatible value (which would throw, e.g. a string FallbackValue onto an ICommand/Brush property).
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
      catch { result = null; return false; }
   }
}
