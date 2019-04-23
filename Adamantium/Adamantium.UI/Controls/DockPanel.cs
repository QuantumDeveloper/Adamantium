using System;

namespace Adamantium.UI.Controls
{
   public class DockPanel:Panel
   {
      public  static readonly AdamantiumProperty DockProperty = AdamantiumProperty.RegisterAttached("Dock", typeof(Dock), typeof(UIComponent));

      public static readonly AdamantiumProperty LastFildFillProperty = AdamantiumProperty.Register(nameof(LastChildFill), typeof(Boolean), typeof(DockPanel),
         new PropertyMetadata(true));

      public Boolean LastChildFill
      {
         get { return GetValue<Boolean>(LastFildFillProperty); }
         set { SetValue(LastFildFillProperty, value);}
      }

      public static Dock GetDock(UIComponent element)
      {
         return element.GetValue<Dock>(DockProperty);
      }

      public static void SetDock(UIComponent element, Dock position)
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
            //(unless the element should fill the remaning space because of LastChildFill)
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
}
