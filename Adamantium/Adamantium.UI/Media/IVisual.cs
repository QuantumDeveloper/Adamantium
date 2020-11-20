using System;
using System.Collections.ObjectModel;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public interface IVisual : IComponent
   {
      bool IsAttachedToVisualTree { get; }

      Int32 ZIndex { get; set; }

      Visibility Visibility { get; set; }

      IVisual VisualParent { get; }

      ReadOnlyCollection<IVisual> VisualChildren { get; }

      Rect Bounds { get; }

      Rect ClipRectangle { get; }

      Point ClipPosition { get; set; }

      Point Location { get; set; }

      void OnRender(DrawingContext context);
   }
}
