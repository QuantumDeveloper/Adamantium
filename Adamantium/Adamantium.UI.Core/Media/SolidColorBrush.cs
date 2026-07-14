using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

public sealed class SolidColorBrush: Brush
{
   public SolidColorBrush()
   {
      
   }
   public SolidColorBrush(Color color)
   {
      Color = color;
   }
   
   public SolidColorBrush(Color color, double opacity)
   {
      Color = color;
      Opacity = opacity;
   }

   public SolidColorBrush(string color)
   {
      Color = GetColorFromString(color);
   }

   // PAINT: recolouring a brush re-bakes the units that paint with it; it never changes their shape (see Brush.Opacity).
   public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Color),
      typeof(Color), typeof(SolidColorBrush), new PropertyMetadata(Colors.Transparent, PropertyMetadataOptions.AffectsPaint));

   public Color Color
   {
      get => GetValue<Color>(ColorProperty);
      set { if (IsFrozen) return; SetValue(ColorProperty, value); }
   }

   protected override Brush CreateFrozenCore() => AsFrozen(new SolidColorBrush(Color, Opacity));

   private Color GetColorFromString(string color)
   {
      return Colors.Get(color);
   }

   public override string ToString()
   {
      return $"Color: {Color}, Opacity: {Opacity}";
   }
}