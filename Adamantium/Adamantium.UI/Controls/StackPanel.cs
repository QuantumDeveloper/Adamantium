using System;

namespace Adamantium.UI.Controls;

public class StackPanel:Panel
{
   public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
      typeof (Orientation), typeof (StackPanel), new PropertyMetadata(Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));


   public Orientation Orientation
   {
      get => GetValue<Orientation>(OrientationProperty);
      set => SetValue(OrientationProperty, value);
   }

   protected override Size MeasureOverride(Size availableSize)
   {
      double childAvailableWidth = double.PositiveInfinity;
      double childAvailableHeight = double.PositiveInfinity;

      if (Orientation == Orientation.Vertical)
      {
         childAvailableWidth = availableSize.Width;

         if (!double.IsNaN(Width))
         {
            childAvailableWidth = Width;
         }

         childAvailableWidth = Math.Min(childAvailableWidth, MaxWidth);
         childAvailableWidth = Math.Max(childAvailableWidth, MinWidth);
      }
      else
      {
         childAvailableHeight = availableSize.Height;

         if (!double.IsNaN(Height))
         {
            childAvailableHeight = Height;
         }

         childAvailableHeight = Math.Min(childAvailableHeight, MaxHeight);
         childAvailableHeight = Math.Max(childAvailableHeight, MinHeight);
      }

      double measuredWidth = 0;
      double measuredHeight = 0;
         

      foreach (var child in Children)
      {
         child.Measure(new Size(childAvailableWidth, childAvailableHeight));
         Size size = child.DesiredSize;

         if (Orientation == Orientation.Vertical)
         {
            measuredHeight += size.Height;
            measuredWidth = Math.Max(measuredWidth, size.Width);
         }
         else
         {
            measuredWidth += size.Width;
            measuredHeight = Math.Max(measuredHeight, size.Height);
         }
      }

      return new Size(measuredWidth, measuredHeight);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      double arrangedWidth = finalSize.Width;
      double arrangedHeight = finalSize.Height;

      if (Orientation == Orientation.Vertical)
      {
         arrangedHeight = 0;
      }
      else
      {
         arrangedWidth = 0;
      }

      foreach (var child in Children)
      {
         double childWidth = child.DesiredSize.Width;
         double childHeight = child.DesiredSize.Height;

         if (Orientation == Orientation.Vertical)
         {
            double width = Math.Max(childWidth, arrangedWidth);
            Rect childFinal = new Rect(0, arrangedHeight, width, childHeight);
            child.Arrange(childFinal);
            arrangedWidth = Math.Max(arrangedWidth, childWidth);
            arrangedHeight += childHeight;
         }
         else
         {
            double height = Math.Max(childHeight, arrangedHeight);
            Rect childFinal = new Rect(arrangedWidth, 0, childWidth, height);
            child.Arrange(childFinal);
               
            arrangedWidth += childWidth;
            arrangedHeight = Math.Max(arrangedHeight, childHeight);
         }
      }

      if (Orientation == Orientation.Vertical)
      {
         arrangedHeight = Math.Max(arrangedHeight, finalSize.Height);
      }
      else
      {
         arrangedWidth = Math.Max(arrangedWidth, finalSize.Width);
      }

      return new Size(arrangedWidth, arrangedHeight);
   }
}