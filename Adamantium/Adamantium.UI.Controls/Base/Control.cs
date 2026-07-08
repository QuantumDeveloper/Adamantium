using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Base;

public class Control : TemplatedUIComponent, IControl
{
   public Control()
   {

   }

   // Foreground is the INHERITED property declared on UIComponent; keep Control's neutral Transparent default (a default
   // never cascades - only an explicit set does), while preserving Inherits so an ancestor's set value flows into the
   // control's content.
   static Control()
   {
      ForegroundProperty.OverrideMetadata(typeof(Control),
         new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.Inherits | PropertyMetadataOptions.AffectsRender));
   }

   public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
      typeof(Brush), typeof(Control),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public Brush Background
   {
      get => GetValue<Brush>(BackgroundProperty);
      set => SetValue(BackgroundProperty, value);
   }
}