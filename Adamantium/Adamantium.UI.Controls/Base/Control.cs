using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Base;

public class Control : TemplatedUIComponent, IControl
{
   public Control()
   {

   }
   
   public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
      typeof(Brush), typeof(Control),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty ForegroundProperty = AdamantiumProperty.Register(nameof(Foreground),
      typeof(Brush), typeof(Control),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));
   

   public Brush Background
   {
      get => GetValue<Brush>(BackgroundProperty);
      set => SetValue(BackgroundProperty, value);
   }

   public Brush Foreground
   {
      get => GetValue<Brush>(ForegroundProperty);
      set => SetValue(ForegroundProperty, value);
   }
}