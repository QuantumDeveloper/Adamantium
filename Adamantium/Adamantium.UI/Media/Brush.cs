using System;

namespace Adamantium.UI.Media
{
   public abstract class Brush:DependencyComponent
   {
      public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
         typeof (Double), typeof (Brush), new PropertyMetadata(1.0));

      public Double Opacity
      {
         get { return GetValue<Double>(OpacityProperty); }
         set { SetValue(OpacityProperty, value);}
      }

      //public static Brush Parse(string brush)
      //{
      //   //return new SolidColorBrush();
         
      //}

   }
}
