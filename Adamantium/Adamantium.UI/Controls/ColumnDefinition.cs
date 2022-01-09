using System;

namespace Adamantium.UI.Controls;

public class ColumnDefinition: DefinitionBase
{
   public static readonly AdamantiumProperty MaxWidthProperty = AdamantiumProperty.Register(nameof(MaxWidth),
      typeof(Double), typeof(ColumnDefinition), new PropertyMetadata(Double.PositiveInfinity));

   public static readonly AdamantiumProperty MinWidthProperty = AdamantiumProperty.Register(nameof(MinWidth),
      typeof(Double), typeof(ColumnDefinition), new PropertyMetadata(0.0));

   public static readonly AdamantiumProperty WidthProperty = AdamantiumProperty.Register(nameof(Width),
      typeof(GridLength), typeof(ColumnDefinition), new PropertyMetadata(new GridLength(1, GridUnitType.Star)));

   public Double MaxWidth
   {
      get => GetValue<Double>(MaxWidthProperty);
      set => SetValue(MaxWidthProperty, value);
   }

   public Double MinWidth
   {
      get => GetValue<Double>(MinWidthProperty);
      set => SetValue(MinWidthProperty, value);
   }

   public GridLength Width
   {
      get => GetValue<GridLength>(WidthProperty);
      set => SetValue(WidthProperty, value);
   }

   public Double ActualWidth { get; internal set; }

   public ColumnDefinition()
   {

   }

   public ColumnDefinition(GridLength width)
   {
      Width = width;
   }

   public ColumnDefinition(double value, GridUnitType unitType)
   {
      Width = new GridLength(value, unitType);
   }
}