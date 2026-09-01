using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

public class DockPanel:Panel
{
   // Both are read by THIS panel's measure and arrange: an edge decides how much of the remaining band a child eats and
   // therefore what is left for its siblings, so a change has to re-run the panel, not the child. Registered with no
   // metadata at all, they announced nothing - a Dock written at runtime (from code, a binding, a restored layout) left
   // the panel laid out by the previous edge until something unrelated invalidated it.
   public static readonly AdamantiumProperty DockProperty =
      AdamantiumProperty.RegisterAttached("Dock", typeof(Dock), typeof(UIComponent),
         new PropertyMetadata(default(Dock),
            PropertyMetadataOptions.AffectsParentMeasure | PropertyMetadataOptions.AffectsParentArrange));

   public static readonly AdamantiumProperty LastChildFillProperty = AdamantiumProperty.Register(nameof(LastChildFill),
      typeof(Boolean), typeof(DockPanel),
      new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   public Boolean LastChildFill
   {
      get => GetValue<Boolean>(LastChildFillProperty);
      set => SetValue(LastChildFillProperty, value);
   }

   public static Dock GetDock(IUIComponent element)
   {
      return element.GetValue<Dock>(DockProperty);
   }

   public static void SetDock(IUIComponent element, Dock position)
   {
      element.SetValue(DockProperty, position);
   }

   protected override Size MeasureOverride(Size availableSize)
   {
      double usedWidth = 0.0;
      double usedHeight = 0.0;
      double maxWidth = 0.0;
      double maxHeight = 0.0;

      foreach (var child in Children)
      {
         //Get the child desired size
         Size remainingSize = new Size(
            Math.Max(0.0, availableSize.Width - usedWidth),
            Math.Max(0.0, availableSize.Height - usedHeight));
         child.Measure(remainingSize);
         Size desiredSize = child.DesiredSize;

         //Decrease the remaining space for the rest of the children
         switch (GetDock(child))
         {
            case Dock.Left:
            case Dock.Right:
               maxHeight = Math.Max(maxHeight, usedHeight + desiredSize.Height);
               usedWidth += desiredSize.Width;
               break;
            case  Dock.Top:
            case Dock.Bottom:
               maxWidth = Math.Max(maxWidth, usedWidth + desiredSize.Width);
               usedHeight += desiredSize.Height;
               break;
         }
      }

      maxWidth = Math.Max(maxWidth, usedWidth);
      maxHeight = Math.Max(maxHeight, usedHeight);
      return new Size(maxWidth, maxHeight);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      double left = 0.0;
      double top = 0.0;
      double right = 0.0;
      double bottom = 0.0;

      var children = Children;
      int dockedCount = children.Count - (LastChildFill ? 1 : 0);
      int index = 0;

      foreach (var child in children)
      {
         //Calculate remaining space left to arrange element
         Rect remainingRect = new Rect(left, top, Math.Max(0.0, finalSize.Width - left - right),
            Math.Max(0.0, finalSize.Height - top - bottom));

         //Trim the remaining Rect to the docked size of the element
         //(unless the element should fill the remaining space because of LastChildFill)
         if (index < dockedCount)
         {
            Size desiredSize = child.DesiredSize;
            switch (GetDock(child))
            {
               case Dock.Left:
                  left += desiredSize.Width;
                  remainingRect = remainingRect.ReplaceWidth(desiredSize.Width);
                  break;
               case Dock.Top:
                  top += desiredSize.Height;
                  remainingRect = remainingRect.ReplaceHeight(desiredSize.Height);
                  break;
               case Dock.Right:
                  right += desiredSize.Width;
                  remainingRect = new Rect(Math.Max(0.0, finalSize.Width - right), remainingRect.Y, desiredSize.Width, remainingRect.Height);
                  break;
               case Dock.Bottom:
                  bottom += desiredSize.Height;
                  remainingRect = new Rect(remainingRect.X, Math.Max(0.0, finalSize.Height - bottom), remainingRect.Width, desiredSize.Height);
                  break;
            }
         }
         child.Arrange(remainingRect);
         index++;
      }

      return finalSize;
   }
}