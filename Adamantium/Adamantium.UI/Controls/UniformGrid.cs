using System;

namespace Adamantium.UI.Controls
{
   public class UniformGrid:Panel
   {
      public static readonly AdamantiumProperty RowsProperty = AdamantiumProperty.Register(nameof(Rows), typeof (Int32),
         typeof (UniformGrid),
         new PropertyMetadata(0, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

      public static readonly AdamantiumProperty ColumnsProperty = AdamantiumProperty.Register(nameof(Columns), typeof(Int32), typeof(UniformGrid),
         new PropertyMetadata(0, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

      public Int32 Rows
      {
         get { return GetValue<Int32>(RowsProperty); }
         set { SetValue(RowsProperty, value);}
      }


      public Int32 Columns
      {
         get { return GetValue<Int32>(ColumnsProperty); }
         set { SetValue(ColumnsProperty, value); }
      }

      public UniformGrid() { }

      protected override Size MeasureOverride(Size availableSize)
      {
         return base.MeasureOverride(availableSize);
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
         return base.ArrangeOverride(finalSize);
      }
   }
}
