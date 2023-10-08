namespace Adamantium.UI.Media;

public sealed class SolidColorBrush: Brush
{
   public SolidColorBrush()
   {
      
   }
   public SolidColorBrush(Color color)
   {
      Color = color;
   }

   public SolidColorBrush(string color)
   {
      Color = GetColorFromString(color);
   }

   public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Opacity),
      typeof(Color), typeof(SolidColorBrush), new PropertyMetadata(Colors.Transparent));

   public Color Color
   {
      get => GetValue<Color>(ColorProperty);
      set => SetValue(ColorProperty, value);
   }

   private Color GetColorFromString(string color)
   {
      return Colors.Get(color);
   }
}