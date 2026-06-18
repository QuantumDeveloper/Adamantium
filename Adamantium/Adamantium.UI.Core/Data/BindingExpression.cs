using System.ComponentModel;
using System.Globalization;
using System.Reflection;
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
/// </summary>
public class BindingExpression : BindingExpressionBase
{
   public object ResolvedSource { get; private set; }
   public string SourcePropertyName { get; private set; }

   private PropertyInfo _sourceProperty;
   private INotifyPropertyChanged _observed;

   public Binding Binding { get; set; }
   public BindingMode Mode { get; set; }

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

   public static BindingExpressionBase CreateBindingExpression(IFundamentalUIComponent target,
      AdamantiumProperty targetProperty, BindingBase bindingBase)
   {
      var expression = new BindingExpression(target, targetProperty, bindingBase);
      expression.EstablishConnection();
      return expression;
   }

   public static BindingExpressionBase CreateBindingExpression(IFundamentalUIComponent target,
      string targetPropertyName, BindingBase bindingBase)
      => CreateBindingExpression(target, target.GetProperty(targetPropertyName), bindingBase);

   public override void EstablishConnection()
   {
      CloseConnection();            // idempotent: a DataContext change re-establishes against the new source
      ResolveSource();
      UpdateTarget();               // initial push
      if (ResolvedSource is INotifyPropertyChanged notify)
      {
         _observed = notify;
         notify.PropertyChanged += OnSourcePropertyChanged;
      }
      if (Mode == BindingMode.TwoWay && Target != null)
         Target.PropertyChanged += OnTargetPropertyChanged;
   }

   public override void CloseConnection()
   {
      if (_observed != null)
      {
         _observed.PropertyChanged -= OnSourcePropertyChanged;
         _observed = null;
      }
      if (Mode == BindingMode.TwoWay && Target != null)
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
      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == SourcePropertyName)
         UpdateTarget();
   }

   private void OnTargetPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
   {
      if (Mode == BindingMode.TwoWay && e.Property == TargetProperty)
         UpdateSource();
   }

   public override void UpdateTarget()
   {
      if (_sourceProperty == null || TargetProperty == null) return;
      var value = _sourceProperty.GetValue(ResolvedSource);
      if (Binding.Converter != null)
         value = Binding.Converter.Convert(value, TargetProperty.PropertyType, Binding.ConverterParameter, CultureInfo.CurrentCulture);
      Target.SetValue(TargetProperty, Coerce(value, TargetProperty.PropertyType), ValuePriority.Binding);
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
   // target, else a best-effort Convert.ChangeType; on failure keep the raw value.
   private static object Coerce(object value, Type targetType)
   {
      if (value == null || targetType == null || targetType.IsInstanceOfType(value)) return value;
      if (targetType == typeof(string)) return value.ToString();
      try { return Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType, CultureInfo.CurrentCulture); }
      catch { return value; }
   }
}
