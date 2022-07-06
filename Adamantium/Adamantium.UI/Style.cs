using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.UI.Controls;
using Adamantium.UI.Exceptions;
using Adamantium.UI.Input;

namespace Adamantium.UI;

public class Style : AdamantiumComponent
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
   
   public Selector Selector { get; set; }

   // TODO: Seems BasedOn does not needed anymore?
   public Style BasedOn { get; set; }

   // TODO: Seems TargetType does not needed anymore?
   public Type TargetType { get; set; }

   public SetterCollection Setters { get; }

   public TriggerCollection Triggers { get; }

   public void Attach(IInputComponent control)
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
      
      for (int i = styles.Count - 1; i >= 0; --i)
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

public class Selector
{
   public Selector()
   {
      
   }
   
   public Type TargetType { get; set; }
   
   public TrackingCollection<string> Classes {get;}
   
   public TrackingCollection<string> Ids { get; }
}