using System;

namespace Adamantium.UI.Controls
{
   public abstract class DefinitionBase:DependencyComponent
   {
      public static readonly AdamantiumProperty SharedSizeGroupProperty =
         AdamantiumProperty.Register(nameof(SharedSizeGroup), typeof (String), typeof (DefinitionBase),
            new PropertyMetadata(String.Empty));

      public static readonly AdamantiumProperty MarginProperty = AdamantiumProperty.Register(nameof(Margin),
         typeof(Double), typeof(DefinitionBase), new PropertyMetadata(0d));

      public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
         typeof(HalfThickness), typeof(DefinitionBase), new PropertyMetadata(new HalfThickness(0)));



      public String SharedSizeGroup
      {
         get { return GetValue<String>(SharedSizeGroupProperty); }
         set { SetValue(SharedSizeGroupProperty, value); }
      }

      public Double Margin
      {
         get { return GetValue<Double>(MarginProperty); }
         set { SetValue(MarginProperty, value); }
      }

      public HalfThickness Padding
      {
         get { return GetValue<HalfThickness>(PaddingProperty); }
         set { SetValue(PaddingProperty, value); }
      }

      internal Double Offset { get; set; }
   }
}
