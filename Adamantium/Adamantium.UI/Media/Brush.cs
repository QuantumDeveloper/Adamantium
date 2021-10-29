using System;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Media
{
   public abstract class Brush: AdamantiumComponent
   {
      public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
         typeof (Double), typeof (Brush), new PropertyMetadata(1.0));

      public Double Opacity
      {
         get => GetValue<Double>(OpacityProperty);
         set => SetValue(OpacityProperty, value);
      }

      //public static Brush Parse(string brush)
      //{
      //   //return new SolidColorBrush();
         
      //}

   }
}
