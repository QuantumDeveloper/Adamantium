using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public class Control:FrameworkElement
   {
      public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
         typeof (Brush), typeof (Control),
         new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

      public static readonly AdamantiumProperty ForegroundProperty = AdamantiumProperty.Register(nameof(Foreground),
         typeof(Brush), typeof(Control),
         new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));


      public Brush Background
      {
         get { return GetValue<Brush>(BackgroundProperty); }
         set { SetValue(BackgroundProperty, value);}
      }

      public Brush Foreground
      {
         get { return GetValue<Brush>(BackgroundProperty); }
         set { SetValue(BackgroundProperty, value); }
      }

      public Control()
      {
         
      }
   }
}
