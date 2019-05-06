using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public class SolidColorBrush:Brush
   {
      public SolidColorBrush(ColorRGBA color)
      {
         Color = color;
      }

      public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Opacity),
         typeof(ColorRGBA), typeof(SolidColorBrush), new PropertyMetadata(Colors.Transparent));

      public ColorRGBA Color
      {
         get { return GetValue<ColorRGBA>(ColorProperty); }
         set { SetValue(ColorProperty, value);}
      }

   }

}
