using Adamantium.ProceduralGeometry;
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

   // The chrome every framed control needs. Declared ONCE here rather than re-registered by each control that happens
   // to draw a frame: a re-registration makes a DIFFERENT property that merely shares a name, so a control that forgot
   // one had a style setter naming a property it did not have - which throws when the theme is attached, not when the
   // style is written. A control whose look wants another default overrides the METADATA (see TextBoxBase); it does not
   // declare the property again. Controls that are not Controls - Border, Decorator, Panel - still carry their own.
   public static readonly AdamantiumProperty BorderBrushProperty = AdamantiumProperty.Register(nameof(BorderBrush),
      typeof(Brush), typeof(Control),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty BorderThicknessProperty = AdamantiumProperty.Register(
      nameof(BorderThickness), typeof(Thickness), typeof(Control),
      new PropertyMetadata(default(Thickness), PropertyMetadataOptions.AffectsMeasure));

   public static readonly AdamantiumProperty CornerRadiusProperty = AdamantiumProperty.Register(nameof(CornerRadius),
      typeof(CornerRadius), typeof(Control),
      new PropertyMetadata(default(CornerRadius), PropertyMetadataOptions.AffectsRender,
         (d, _) => NotifyOwnCornersChanged(d)));   // these corners are also the CLIP's, when it clips - see the note there

   public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
      typeof(Thickness), typeof(Control),
      new PropertyMetadata(default(Thickness),
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   public Brush Background
   {
      get => GetValue<Brush>(BackgroundProperty);
      set => SetValue(BackgroundProperty, value);
   }

   public Brush BorderBrush
   {
      get => GetValue<Brush>(BorderBrushProperty);
      set => SetValue(BorderBrushProperty, value);
   }

   public Thickness BorderThickness
   {
      get => GetValue<Thickness>(BorderThicknessProperty);
      set => SetValue(BorderThicknessProperty, value);
   }

   public CornerRadius CornerRadius
   {
      get => GetValue<CornerRadius>(CornerRadiusProperty);
      set => SetValue(CornerRadiusProperty, value);
   }

   /// <inheritdoc />
   protected override Vector4F OwnCornerRadii()
   {
      var r = CornerRadius;
      return new Vector4F((float)r.TopLeft, (float)r.TopRight, (float)r.BottomRight, (float)r.BottomLeft);
   }

   public Thickness Padding
   {
      get => GetValue<Thickness>(PaddingProperty);
      set => SetValue(PaddingProperty, value);
   }
}