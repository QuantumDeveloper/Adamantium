using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.UI.Exceptions;

namespace Adamantium.UI;

public class Style
{
   private Dictionary<AdamantiumProperty, ISetter> settersDict;
   
   public Style(Type targetType, Style basedOn = null)
   {
      settersDict = new Dictionary<AdamantiumProperty, ISetter>();
      Setters = new SetterCollection();
      Triggers = new TriggerCollection();
      TargetType = targetType;
      BasedOn = basedOn;
   }

   public Style BasedOn { get; set; }

   public Type TargetType { get; set; }

   public SetterCollection Setters { get; }

   public TriggerCollection Triggers { get; }

   public void Attach(IUIComponent control)
   {
      ArgumentNullException.ThrowIfNull(TargetType);
      
      if (control == null) return;

      if (!Utilities.IsTypeInheritFrom(control.GetType(), TargetType))
      {
         throw new StyleTargetTypeException($"Control should be of type {TargetType.Name} instead of {control.GetType()}");
      }

      var styles = new List<Style>();
      var baseStyle = BasedOn;
      while (baseStyle != null)
      {
         styles.Add(baseStyle);
         
         baseStyle = baseStyle.BasedOn;
      }
      
      for (int i = styles.Count - 1; i >= 0; ++i)
      {
         var style = styles[i];
         foreach (var setter in style.Setters)
         {
            settersDict[setter.Property] = setter;
         }
      }

      foreach (var setter in Setters)
      {
         settersDict[setter.Property] = setter;
      }

      foreach (var (key, value) in settersDict)
      {
         value.Apply(control);
      }

      foreach (var trigger in Triggers)
      {
         trigger.Apply(control);
      }
   }
}