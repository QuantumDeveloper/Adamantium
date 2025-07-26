using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Media;

[TypeParser(typeof(BrushParser))]
public abstract class Brush: AdamantiumComponent
{
   public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
      typeof (Double), typeof (Brush), new PropertyMetadata(1.0));

   public Double Opacity
   {
      get => GetValue<Double>(OpacityProperty);
      set => SetValue(OpacityProperty, value);
   }
}