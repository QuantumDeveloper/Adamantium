using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public class SolidColorBrush:Brush
   {
      public SolidColorBrush(Color color)
      {
         Color = color;
      }

      public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Opacity),
         typeof(Color), typeof(SolidColorBrush), new PropertyMetadata(Colors.Transparent));

      public Color Color
      {
         get { return GetValue<Color>(ColorProperty); }
         set { SetValue(ColorProperty, value);}
      }

   }

}
