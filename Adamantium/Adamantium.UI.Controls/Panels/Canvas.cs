using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Panels;

public class Canvas : Panel
{
   // Placed by a CALLBACK, not by an arrange flag. Where a child sits is decided by this panel, so AffectsArrange marked
   // the wrong element: the child re-arranged inside the slot it already had and never moved. See OnPositionChanged for
   // why the panel is not marked either.
   public static readonly AdamantiumProperty LeftProperty = AdamantiumProperty.RegisterAttached("Left",
      typeof(Double), typeof(UIComponent), new PropertyMetadata(Double.NaN, OnPositionChanged));

   public static readonly AdamantiumProperty TopProperty = AdamantiumProperty.RegisterAttached("Top",
      typeof(Double), typeof(UIComponent), new PropertyMetadata(Double.NaN, OnPositionChanged));

   public static readonly AdamantiumProperty RightProperty = AdamantiumProperty.RegisterAttached("Right",
      typeof(Double), typeof(UIComponent), new PropertyMetadata(Double.NaN, OnPositionChanged));

   public static readonly AdamantiumProperty BottomProperty = AdamantiumProperty.RegisterAttached("Bottom",
      typeof(Double), typeof(UIComponent), new PropertyMetadata(Double.NaN, OnPositionChanged));


   public static Double GetLeft(IAdamantiumComponent element)
   {
      return element.GetValue<Double>(LeftProperty);
   }

   public static void SetLeft(IAdamantiumComponent element, Double value)
   {
      element.SetValue(LeftProperty, value);
   }

   public static Double GetTop(IAdamantiumComponent element)
   {
      return element.GetValue<Double>(TopProperty);
   }

   public static void SetTop(IAdamantiumComponent element, Double value)
   {
      element.SetValue(TopProperty, value);
   }

   public static Double GetRight(IAdamantiumComponent element)
   {
      return element.GetValue<Double>(RightProperty);
   }

   public static void SetRight(IAdamantiumComponent element, Double value)
   {
      element.SetValue(RightProperty, value);
   }

   public static Double GetBottom(IAdamantiumComponent element)
   {
      return element.GetValue<Double>(BottomProperty);
   }

   public static void SetBottom(IAdamantiumComponent element, Double value)
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
         PlaceChild(child, finalSize);
      }

      return finalSize;
   }

   /// <summary>Where one child goes, given the panel's final size. Left/Top win; Right/Bottom measure back from the far
   /// edge, which is the only reason the panel's size takes part at all.</summary>
   private static void PlaceChild(IMeasurableComponent child, Size finalSize)
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

      child.Arrange(new Rect(new Vector2(x, y), child.DesiredSize));
   }

   /// <summary>One child moved. Places THAT child and nobody else.
   ///
   /// <para>A canvas does not size itself from its contents - a child at any coordinate leaves the panel exactly as big
   /// as it was - so moving one is not a reason to re-arrange the rest. Marking the panel (AffectsParentArrange) would
   /// have worked, but it costs a pass over every child on every step of a drag, for a panel that is often used
   /// precisely because it holds a lot of freely positioned things.</para></summary>
   private static void OnPositionChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
   {
      if (d is not IMeasurableComponent { VisualParent: Canvas canvas } child) return;
      if (canvas.ActualWidth <= 0 && canvas.ActualHeight <= 0) return;   // not arranged yet: the first pass will place it

      PlaceChild(child, new Size(canvas.ActualWidth, canvas.ActualHeight));
   }
}