using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

public class UniformGrid:Panel
{
   public static readonly AdamantiumProperty RowsProperty = AdamantiumProperty.Register(nameof(Rows), typeof (Int32),
      typeof (UniformGrid),
      new PropertyMetadata(0, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty ColumnsProperty = AdamantiumProperty.Register(nameof(Columns), typeof(Int32), typeof(UniformGrid),
      new PropertyMetadata(0, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   // The gap between cells - set ONCE on the grid instead of a Margin on every child (same names as Grid).
   public static readonly AdamantiumProperty RowSpacingProperty = AdamantiumProperty.Register(nameof(RowSpacing), typeof(Double), typeof(UniformGrid),
      new PropertyMetadata(0d, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty ColumnSpacingProperty = AdamantiumProperty.Register(nameof(ColumnSpacing), typeof(Double), typeof(UniformGrid),
      new PropertyMetadata(0d, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   public Int32 Rows
   {
      get => GetValue<Int32>(RowsProperty);
      set => SetValue(RowsProperty, value);
   }


   public Int32 Columns
   {
      get => GetValue<Int32>(ColumnsProperty);
      set => SetValue(ColumnsProperty, value);
   }

   /// <summary>The vertical gap between cell rows.</summary>
   public Double RowSpacing
   {
      get => GetValue<Double>(RowSpacingProperty);
      set => SetValue(RowSpacingProperty, value);
   }

   /// <summary>The horizontal gap between cell columns.</summary>
   public Double ColumnSpacing
   {
      get => GetValue<Double>(ColumnSpacingProperty);
      set => SetValue(ColumnSpacingProperty, value);
   }

   public UniformGrid() { }

   protected override Size MeasureOverride(Size availableSize)
   {
      GetDimensions(out var rows, out var columns);
      if (rows == 0 || columns == 0) return base.MeasureOverride(availableSize);

      // The spacing eats into the space available for cells, so each cell is (available - total gaps) / count.
      var cell = new Size(
         (availableSize.Width - (columns - 1) * ColumnSpacing) / columns,
         (availableSize.Height - (rows - 1) * RowSpacing) / rows);
      double maxWidth = 0, maxHeight = 0;
      foreach (var child in Children)
      {
         child.Measure(cell);
         if (child.DesiredSize.Width > maxWidth) maxWidth = child.DesiredSize.Width;
         if (child.DesiredSize.Height > maxHeight) maxHeight = child.DesiredSize.Height;
      }
      // Every cell is the largest child's size; the grid is that times the count, plus the gaps between them.
      return new Size(maxWidth * columns + (columns - 1) * ColumnSpacing,
                      maxHeight * rows + (rows - 1) * RowSpacing);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      GetDimensions(out var rows, out var columns);
      if (rows == 0 || columns == 0) return base.ArrangeOverride(finalSize);

      var cellWidth = (finalSize.Width - (columns - 1) * ColumnSpacing) / columns;
      var cellHeight = (finalSize.Height - (rows - 1) * RowSpacing) / rows;
      var index = 0;
      foreach (var child in Children)
      {
         var column = index % columns;
         var row = index / columns;
         child.Arrange(new Rect(column * (cellWidth + ColumnSpacing), row * (cellHeight + RowSpacing), cellWidth, cellHeight));
         index++;
      }
      return finalSize;
   }

   // Rows/Columns are filled in from the child count: set Columns and the rows follow (and vice versa); set neither and
   // the grid is as square as the count allows.
   private void GetDimensions(out int rows, out int columns)
   {
      rows = Rows;
      columns = Columns;
      var count = Children.Count;
      if (count == 0) { rows = 0; columns = 0; return; }
      if (columns > 0 && rows > 0) return;
      if (columns > 0) { rows = (count + columns - 1) / columns; return; }
      if (rows > 0) { columns = (count + rows - 1) / rows; return; }
      columns = (int)System.Math.Ceiling(System.Math.Sqrt(count));
      rows = (count + columns - 1) / columns;
   }
}
