using System;
using System.ComponentModel;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Data;

public class BindingExpression : BindingExpressionBase
{
   public object DataSource { get; set; }
   public Binding ParentBinding { get; set; }
   public String SourcePropertyName { get; set; }
   public object ResolvedSource { get; set; }

   public void Init()
   {
      if (ResolvedSource is INotifyPropertyChanged)
      {
         var notify = ResolvedSource as INotifyPropertyChanged;
         notify.PropertyChanged += SourcePropertyChanged;
      }

      Target.PropertyChanged += TargetPropertyChanged;
   }

   private void TargetPropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
   {
      IsDirty = true;
      UpdateSource();
      IsDirty = false;
   }

   private void SourcePropertyChanged(object sender, PropertyChangedEventArgs e)
   {
      if (e.PropertyName == SourcePropertyName)
      {
         IsDirty = true;
         UpdateTarget();
         IsDirty = false;
      }
   }

   public override void UpdateSource()
   {
      if (IsDirty)
      {
         ResolvedSource.GetType()
            .GetProperty(SourcePropertyName)
            .SetValue(ResolvedSource, Target.GetValue(TargetProperty));

      }
   }

   public override void UpdateTarget()
   {
      if (IsDirty)
      {
         var sourceValue = ResolvedSource.GetType().GetProperty(SourcePropertyName).GetValue(ResolvedSource);
         Target.SetCurrentValue(TargetProperty, sourceValue);
      }
   }
}