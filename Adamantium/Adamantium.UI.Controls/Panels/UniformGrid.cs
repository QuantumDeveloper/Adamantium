using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core.Input;

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

   /// <summary>Every cell here comes from the child's INDEX, so navigation is the same arithmetic the arrange uses:
   /// sideways is index ±1 within the row, up and down are ±one row. A sideways step must not fall off the end of a
   /// line into the next one, which plain index arithmetic would happily do.</summary>
   public override IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction)
   {
      if (!IsArrow(direction)) return base.Navigate(from, direction);
      if (from is not MeasurableUIComponent child) return null;

      var index = Children.IndexOf(child);
      if (index < 0) return null;

      GetDimensions(out var rows, out var columns);
      if (rows == 0 || columns == 0) return null;

      var row = index / columns + (IsVertical(direction) ? (IsForward(direction) ? 1 : -1) : 0);
      var column = index % columns + (IsVertical(direction) ? 0 : IsForward(direction) ? 1 : -1);
      if (row < 0 || row >= rows || column < 0 || column >= columns) return null;

      var next = row * columns + column;
      return next >= 0 && next < Children.Count ? Children[next] : null;
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
