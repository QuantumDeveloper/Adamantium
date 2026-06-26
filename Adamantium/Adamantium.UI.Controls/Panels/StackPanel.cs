using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

public class StackPanel : Panel
{
   public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
      typeof(Orientation), typeof(StackPanel),
      new PropertyMetadata(Orientation.Horizontal,
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));


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
      var horizontal = Orientation == Orientation.Horizontal;

      // Cross-axis = the CONTENT extent (DesiredSize: the tallest/widest child, or an explicit Width/Height), clamped to
      // the slot - never the full slot on its own. A stack only occupies what it stacks, so it must not report (and
      // therefore hit-test) cross space it doesn't use: a horizontal Stretch stack otherwise swallowed the whole window
      // height for input, blocking everything beneath it. Children are still given the full cross extent so they can
      // align within it. (Stacking axis = the running content sum - a stack never fills along its orientation either.)
      double cross = horizontal
         ? Math.Min(finalSize.Height, DesiredSize.Height)
         : Math.Min(finalSize.Width, DesiredSize.Width);

      double main = 0;
      foreach (var child in Children)
      {
         if (horizontal)
         {
            child.Arrange(new Rect(main, 0, child.DesiredSize.Width, cross));
            main += child.DesiredSize.Width;
         }
         else
         {
            child.Arrange(new Rect(0, main, cross, child.DesiredSize.Height));
            main += child.DesiredSize.Height;
         }
      }

      return horizontal ? new Size(main, cross) : new Size(cross, main);
   }
}