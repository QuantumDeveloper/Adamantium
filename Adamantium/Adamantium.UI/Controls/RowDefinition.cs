using System;

namespace Adamantium.UI.Controls
{
   public class RowDefinition:DefinitionBase
   {
      public static readonly AdamantiumProperty MaxHeightProperty = AdamantiumProperty.Register(nameof(MaxHeight),
         typeof (Double), typeof (RowDefinition), new PropertyMetadata(Double.PositiveInfinity));

      public static readonly AdamantiumProperty MinHeightProperty = AdamantiumProperty.Register(nameof(MinHeight),
         typeof (Double), typeof (RowDefinition), new PropertyMetadata(0.0));

      public static readonly AdamantiumProperty HeightProperty = AdamantiumProperty.Register(nameof(Height),
         typeof(GridLength), typeof(RowDefinition), new PropertyMetadata(new GridLength(1, GridUnitType.Star)));


      public Double MaxHeight
      {
         get { return GetValue<Double>(MaxHeightProperty); }
         set { SetValue(MaxHeightProperty, value); }
      }

      public Double MinHeight
      {
         get { return GetValue<Double>(MinHeightProperty); }
         set { SetValue(MinHeightProperty, value); }
      }

      public GridLength Height
      {
         get { return GetValue<GridLength>(HeightProperty); }
         set { SetValue(HeightProperty, value); }
      }

      public Double ActualHeight { get; internal set; }

      public RowDefinition()
      {
         
      }

      public RowDefinition(GridLength height)
      {
         Height = height;
      }

      public RowDefinition(double value, GridUnitType unitType)
      {
         Height = new GridLength(value, unitType);
      }
   }
}
