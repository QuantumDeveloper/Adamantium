using System.ComponentModel;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

public class BindingExpression : BindingExpressionBase
{
   public object DataSource { get; set; }
   public String SourcePropertyName { get; set; }
   public object ResolvedSource { get; set; }
   
   public Binding Binding { get; set; }
   
   public UpdateSourceTrigger UpdateSourceTrigger { get; set; }
   
   public BindingMode Mode { get; set; }

   public BindingExpression(IFundamentalUIComponent target, string targetPropertyName, BindingBase bindingBase)
   {
      Target = target;
      TargetProperty = target.GetProperty(targetPropertyName);
      BindingBase = bindingBase;
      Binding = (Binding)bindingBase;
      UpdateSourceTrigger = Binding.UpdateSourceTrigger;
      Mode = Binding.Mode;
   }

   public BindingExpression(IFundamentalUIComponent target, AdamantiumProperty targetProperty, BindingBase bindingBase)
   {
      Target = target;
      TargetProperty = targetProperty;
      BindingBase = bindingBase;
      Binding = (Binding)bindingBase;
      UpdateSourceTrigger = Binding.UpdateSourceTrigger;
      Mode = Binding.Mode;
   }

   private void Init()
   {
      if (ResolvedSource is INotifyPropertyChanged notify) 
      {
         notify.PropertyChanged += SourcePropertyChanged;
      }

      if (Binding.Mode == BindingMode.TwoWay)
      {
         Target.PropertyChanged += TargetPropertyChanged;
      }
   }

   private void DeInit()
   {
      if (ResolvedSource is INotifyPropertyChanged notify)
      {
         notify.PropertyChanged -= SourcePropertyChanged;
      }

      Target.PropertyChanged -= TargetPropertyChanged;
   }

   public static BindingExpressionBase CreateBindingExpression(IFundamentalUIComponent target, AdamantiumProperty targetProperty,
      BindingBase bindingBase)
   {
      var expression = new BindingExpression(target, targetProperty, bindingBase);
      expression.Init();
      return expression;
   }
   
   public static BindingExpressionBase CreateBindingExpression(IFundamentalUIComponent target, string targetPropertyName,
      BindingBase bindingBase)
   {
      var targetProperty = target.GetProperty(targetPropertyName);
      var expression = new BindingExpression(target, targetProperty, bindingBase);
      expression.Init();
      return expression;
   }

   private void TargetPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
   {
      IsDirty = true;
      if (Mode == BindingMode.TwoWay)
      {
         UpdateSource();
      }

      IsDirty = false;
   }

   private void SourcePropertyChanged(object sender, PropertyChangedEventArgs e)
   {
      if (e.PropertyName != SourcePropertyName) return;
      
      IsDirty = true;
      UpdateTarget();
      IsDirty = false;
   }

   public override void EstablishConnection()
   {
      Init();
   }

   public override void UpdateSource()
   {
      if (!IsDirty) return;
      
      ResolvedSource.GetType()
         .GetProperty(SourcePropertyName)?.SetValue(ResolvedSource, Target.GetValue(TargetProperty));
   }

   public override void UpdateTarget()
   {
      if (!IsDirty) return;
      
      var sourceValue = ResolvedSource.GetType().GetProperty(SourcePropertyName)?.GetValue(ResolvedSource);
      Target.SetValue(TargetProperty, sourceValue, ValuePriority.Binding);
   }

   public override void CloseConnection()
   {
      DeInit();
   }
}