using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Controls
{
   public class Canvas : Panel
   {
      public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.RegisterAttached("Left",
         typeof(Double), typeof(UIComponent), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsArrange));

      public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.RegisterAttached("Top",
         typeof(Double), typeof(UIComponent), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsArrange));

      public static readonly AdamantiumProperty RightProperty = AdamantiumProperty.RegisterAttached("Right",
         typeof(Double), typeof(UIComponent), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsArrange));

      public static readonly AdamantiumProperty BottomProperty = AdamantiumProperty.RegisterAttached("Bottom",
         typeof(Double), typeof(UIComponent), new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsArrange));


      public static Double GetLeft(AdamantiumComponent element)
      {
         return element.GetValue<Double>(LeftProperty);
      }

      public static void SetLeft(AdamantiumComponent element, Double value)
      {
         element.SetValue(LeftProperty, value);
      }

      public static Double GetTop(AdamantiumComponent element)
      {
         return element.GetValue<Double>(TopProperty);
      }

      public static void SetTop(AdamantiumComponent element, Double value)
      {
         element.SetValue(TopProperty, value);
      }

      public static Double GetRight(AdamantiumComponent element)
      {
         return element.GetValue<Double>(RightProperty);
      }

      public static void SetRight(AdamantiumComponent element, Double value)
      {
         element.SetValue(RightProperty, value);
      }

      public static Double GetBottom(AdamantiumComponent element)
      {
         return element.GetValue<Double>(BottomProperty);
      }

      public static void SetBottom(AdamantiumComponent element, Double value)
      {
         element.SetValue(BottomProperty, value);
      }

      public Canvas()
      {

      }

      protected override Size MeasureOverride(Size availableSize)
      {
         availableSize = new Size(double.PositiveInfinity, double.PositiveInfinity);

         foreach (var child in Children)
         {
            child.Measure(availableSize);
         }

         return new Size();
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
         foreach (var child in Children)
         {
            double x = 0.0;
            double y = 0.0;
            double elementLeft = GetLeft(child);

            if (!double.IsNaN(elementLeft))
            {
               x = elementLeft;
            }
            else
            {
               // Arrange with right.
               double elementRight = GetRight(child);
               if (!double.IsNaN(elementRight))
               {
                  x = finalSize.Width - (child.DesiredSize.Width + elementRight);
               }
            }

            double elementTop = GetTop(child);
            if (!double.IsNaN(elementTop))
            {
               y = elementTop;
            }
            else
            {
               double elementBottom = GetBottom(child);
               if (!double.IsNaN(elementBottom))
               {
                  y = finalSize.Height - (child.DesiredSize.Height + elementBottom);
               }
            }

            child.Arrange(new Rect(new Vector2D(x, y), child.DesiredSize));
         }

         return finalSize;
      }
   }
}
