using System;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Media;

[TypeParser(typeof(BrushParser))]
public abstract class Brush: AdamantiumComponent
{
   protected Brush()
   {
      // Any property change on the brush itself (Opacity here; Color on a SolidColorBrush; StartPoint/EndPoint on a
      // gradient) changes how it paints, so notify. A gradient also raises Changed for its stops (see GradientBrush).
      PropertyChanged += (_, _) => RaiseChanged();
   }

   /// <summary>Raised when the brush's appearance changes - a property here, or (for a gradient) a stop's Offset/Color.
   /// An element that draws with the brush subscribes to this and re-renders; see AdamantiumComponent's AffectsRender
   /// handling, which keeps the element hooked to whatever brush its render property currently holds. This is what lets
   /// an ANIMATED brush (e.g. a looping shimmer sweeping a gradient) repaint without the element polling.</summary>
   public event EventHandler Changed;

   protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

   public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
      typeof (Double), typeof (Brush), new PropertyMetadata(1.0));

   public Double Opacity
   {
      get => GetValue<Double>(OpacityProperty);
      set => SetValue(OpacityProperty, value);
   }
}