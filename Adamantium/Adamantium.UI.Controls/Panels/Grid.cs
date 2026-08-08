using System.Collections.Specialized;
using System.Diagnostics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Panels;

public class Grid: Panel
{
   /// <summary>A cell here is stated, not derived, so an arrow moves to the NEAREST child that stays in the same line -
   /// the same row for a sideways move, the same column for a vertical one. Nearest by cell, not by pixels: the grid's
   /// own coordinates are what its children were placed by, and a row of different-sized cells would otherwise navigate
   /// in an order the layout never used.</summary>
   public override IUIComponent Navigate(IUIComponent from, FocusNavigationDirection direction)
   {
      if (!IsArrow(direction)) return base.Navigate(from, direction);
      if (from is not MeasurableUIComponent child || Children.IndexOf(child) < 0) return null;

      var vertical = IsVertical(direction);
      var forward = IsForward(direction);
      var line = vertical ? GetColumn(child) : GetRow(child);
      var position = vertical ? GetRow(child) : GetColumn(child);

      IMeasurableComponent nearest = null;
      var nearestPosition = 0;
      foreach (var candidate in Children)
      {
         if (ReferenceEquals(candidate, child)) continue;
         if ((vertical ? GetColumn(candidate) : GetRow(candidate)) != line) continue;

         var candidatePosition = vertical ? GetRow(candidate) : GetColumn(candidate);
         if (forward ? candidatePosition <= position : candidatePosition >= position) continue;
         if (nearest != null && (forward ? candidatePosition >= nearestPosition : candidatePosition <= nearestPosition)) continue;

         nearest = candidate;
         nearestPosition = candidatePosition;
      }

      return nearest;
   }

   // Which cell a child occupies is consumed by the GRID, never by the child - so a change here has to re-run the
   // PARENT's layout. Without that the grid stays measure-valid at an unchanged constraint, early-returns, and the child
   // keeps the cell it was last put in: a trigger that moved a part to another cell appeared to do nothing at all.
   private const PropertyMetadataOptions CellOptions =
      PropertyMetadataOptions.AffectsParentMeasure | PropertyMetadataOptions.AffectsParentArrange;

   public static readonly AdamantiumProperty ColumnProperty = AdamantiumProperty.RegisterAttached("Column",
      typeof(Int32), typeof(AdamantiumComponent), new PropertyMetadata(0, CellOptions));

   public static readonly AdamantiumProperty ColumnSpanProperty = AdamantiumProperty.RegisterAttached("ColumnSpan",
      typeof(Int32), typeof(AdamantiumComponent), new PropertyMetadata(1, CellOptions));

   public static readonly AdamantiumProperty RowProperty = AdamantiumProperty.RegisterAttached("Row",
      typeof(Int32), typeof(AdamantiumComponent), new PropertyMetadata(0, CellOptions));

   public static readonly AdamantiumProperty RowSpanProperty = AdamantiumProperty.RegisterAttached("RowSpan",
      typeof(Int32), typeof(AdamantiumComponent), new PropertyMetadata(1, CellOptions));

   public static readonly AdamantiumProperty ShowGridLinesProperty =
      AdamantiumProperty.Register(nameof(ShowGridLines), typeof(Boolean), typeof(Grid),
         new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

   public static readonly AdamantiumProperty IndividualRowSpacingProperty = AdamantiumProperty.Register(nameof(IndividualRowSpacingProperty),
      typeof(Boolean), typeof(Grid), new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.BindsTwoWayByDefault));

   public static readonly AdamantiumProperty IndividualColumnSpacingProperty = AdamantiumProperty.Register(nameof(IndividualColumnSpacingProperty),
      typeof(Boolean), typeof(Grid), new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.BindsTwoWayByDefault));

   public static readonly AdamantiumProperty RowSpacingProperty = AdamantiumProperty.Register(nameof(RowSpacing),
      typeof(Double), typeof(Grid), new PropertyMetadata(0d, PropertyMetadataOptions.AffectsMeasure|PropertyMetadataOptions.BindsTwoWayByDefault));

   public static readonly AdamantiumProperty ColumnSpacingProperty = AdamantiumProperty.Register(nameof(ColumnSpacing),
      typeof(Double), typeof(Grid), new PropertyMetadata(0d, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.BindsTwoWayByDefault));

   public Boolean ShowGridLines
   {
      get => GetValue<Boolean>(ShowGridLinesProperty);
      set => SetValue(ShowGridLinesProperty, value);
   }

   public static Int32 GetColumn(IAdamantiumComponent element)
   {
      return element.GetValue<Int32>(ColumnProperty);
   }

   public static Int32 GetRow(IAdamantiumComponent element)
   {
      return element.GetValue<Int32>(RowProperty);
   }

   public static void SetColumn(IAdamantiumComponent element, Int32 value)
   {
      element.SetValue(ColumnProperty, value);
   }

   public static void SetRow(IAdamantiumComponent element, Int32 value)
   {
      element.SetValue(RowProperty, value);
   }

   public static Int32 GetColumnSpan(IAdamantiumComponent element)
   {
      return element.GetValue<Int32>(ColumnSpanProperty);
   }

   public static Int32 GetRowSpan(IAdamantiumComponent element)
   {
      return element.GetValue<Int32>(RowSpanProperty);
   }

   public static void SetColumnSpan(IAdamantiumComponent element, Int32 value)
   {
      element.SetValue(ColumnSpanProperty, value);
   }

   public static void SetRowSpan(IAdamantiumComponent element, Int32 value)
   {
      element.SetValue(RowSpanProperty, value);
   }

   public Boolean IndividualRowSpacing
   {
      get => GetValue<Boolean>(IndividualRowSpacingProperty);
      set => SetValue(IndividualRowSpacingProperty, value);
   }

   public Boolean IndividualColumnSpacing
   {
      get => GetValue<Boolean>(IndividualColumnSpacingProperty);
      set => SetValue(IndividualColumnSpacingProperty, value);
   }

   public Double RowSpacing
   {
      get => GetValue<Double>(RowSpacingProperty);
      set => SetValue(RowSpacingProperty, value);
   }

   public Double ColumnSpacing
   {
      get => GetValue<Double>(ColumnSpacingProperty);
      set => SetValue(ColumnSpacingProperty, value);
   }

   // Real dependency properties (not plain CLR): the AUML string form <Grid RowDefinitions="Auto,*"> is applied inside a
   // ControlTemplate via a by-name SetValue, which only reaches AdamantiumProperties (a plain CLR property was silently
   // dropped -> no rows). Default null (no shared mutable collection); the getter lazily creates one so
   // grid.RowDefinitions.Add(...) works without an explicit assignment. The callback keeps the mirror field in sync and
   // hooks CollectionChanged / each definition's PropertyChanged so row/column edits re-lay-out.
   public static readonly AdamantiumProperty RowDefinitionsProperty = AdamantiumProperty.Register(nameof(RowDefinitions),
      typeof(RowDefinitions), typeof(Grid), new PropertyMetadata(null, OnRowDefinitionsChanged));

   public static readonly AdamantiumProperty ColumnDefinitionsProperty = AdamantiumProperty.Register(nameof(ColumnDefinitions),
      typeof(ColumnDefinitions), typeof(Grid), new PropertyMetadata(null, OnColumnDefinitionsChanged));

   // Mirror of the DP value, for internal reads (measure/arrange) without triggering the getter's lazy creation.
   private RowDefinitions _rowDefinitions;
   private ColumnDefinitions _columnDefinitions;

   public RowDefinitions RowDefinitions
   {
      get
      {
         if (_rowDefinitions == null) SetValue(RowDefinitionsProperty, new RowDefinitions());
         return _rowDefinitions;
      }
      set => SetValue(RowDefinitionsProperty, value);
   }

   public ColumnDefinitions ColumnDefinitions
   {
      get
      {
         if (_columnDefinitions == null) SetValue(ColumnDefinitionsProperty, new ColumnDefinitions());
         return _columnDefinitions;
      }
      set => SetValue(ColumnDefinitionsProperty, value);
   }

   private static void OnRowDefinitionsChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      var grid = (Grid)a;
      if (grid._rowDefinitions != null)
      {
         grid._rowDefinitions.CollectionChanged -= grid.RowDefinitions_CollectionChanged;
         foreach (RowDefinition def in grid._rowDefinitions) def.PropertyChanged -= grid.Definition_PropertyChanged;
      }
      grid._rowDefinitions = e.NewValue as RowDefinitions;
      if (grid._rowDefinitions != null)
      {
         grid._rowDefinitions.CollectionChanged += grid.RowDefinitions_CollectionChanged;
         foreach (RowDefinition def in grid._rowDefinitions) def.PropertyChanged += grid.Definition_PropertyChanged;
      }
      // The getter lazily creates an EMPTY collection during measure (reading RowDefinitions.Count); that must NOT dirty
      // the in-flight layout pass. Only a real change to the row set - a populated assign (string form) or clearing
      // populated rows - needs a relayout.
      if ((e.OldValue as RowDefinitions)?.Count > 0 || grid._rowDefinitions?.Count > 0) grid.InvalidateMeasure();
   }

   private static void OnColumnDefinitionsChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
   {
      var grid = (Grid)a;
      if (grid._columnDefinitions != null)
      {
         grid._columnDefinitions.CollectionChanged -= grid.ColumnDefinitions_CollectionChanged;
         foreach (ColumnDefinition def in grid._columnDefinitions) def.PropertyChanged -= grid.Definition_PropertyChanged;
      }
      grid._columnDefinitions = e.NewValue as ColumnDefinitions;
      if (grid._columnDefinitions != null)
      {
         grid._columnDefinitions.CollectionChanged += grid.ColumnDefinitions_CollectionChanged;
         foreach (ColumnDefinition def in grid._columnDefinitions) def.PropertyChanged += grid.Definition_PropertyChanged;
      }
      if ((e.OldValue as ColumnDefinitions)?.Count > 0 || grid._columnDefinitions?.Count > 0) grid.InvalidateMeasure();
   }

   private void RowDefinitions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
   {
      if (e.NewItems != null)
      {
         foreach (var item in e.NewItems)
         {
            var definition = item as RowDefinition;
            if (definition != null) definition.PropertyChanged += Definition_PropertyChanged;
         }
      }

      if (e.OldItems != null)
      {
         foreach (var item in e.OldItems)
         {
            var definition = item as RowDefinition;
            if (definition != null) definition.PropertyChanged -= Definition_PropertyChanged;
         }
      }
      InvalidateMeasure();
   }

   private void ColumnDefinitions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
   {
      if (e.NewItems != null)
      {
         foreach (var item in e.NewItems)
         {
            var definition = item as ColumnDefinition;
            if (definition != null) definition.PropertyChanged += Definition_PropertyChanged;
         }
      }

      if (e.OldItems != null)
      {
         foreach (var item in e.OldItems)
         {
            var definition = item as ColumnDefinition;
            if (definition != null) definition.PropertyChanged -= Definition_PropertyChanged;
         }
      }
      InvalidateMeasure();
   }

   private void Definition_PropertyChanged(object sender, AdamantiumPropertyChangedEventArgs e)
   {
      InvalidateMeasure();
   }

   private bool IsDefinitionsEmpty =>
      (_rowDefinitions == null || _rowDefinitions.Count == 0) && (_columnDefinitions == null || _columnDefinitions.Count == 0);

   //Zero-based Group index
   private const int MaxGroupIndex = 2;
   private const double MaxStartsCount = 1e30; //Constraint to 
   private const double MaxDefinitionSize = 1e27; //Maximum possible size for each Grid definition

   private bool replaceRowStarsWithAuto;
   private bool replaceColStarsWithAuto;

   private bool hasStarRows;
   private bool hasStarColumns;

   private readonly Dictionary<int, List<GridCell>> cellsDictionary = new Dictionary<int, List<GridCell>>();
   readonly Dictionary<SpanData, Double> store = new Dictionary<SpanData, double>();

   private GridSegment[] rowSegments;
   private GridSegment[] colSegments;
   private GridCell[] gridCells;

   private Size childSize;
   Stopwatch measureTimer;

   /*
   * Algorithm main goal to measure each logical element only once to speedup Measure and Arrange passes and skip unneccessary calculations
   * It breaks Grid on 3 groups starting from 0 to 2.
   * Zero group:
   * AutoPixel, AutoAuto, PixelAuto, PixelPixel will be calculated first, because all these cells could be measured without any additional actions.
   * First group
   * AutoStar and StarAuto cells will be measured twice: first - with ignoring of star values because they are undefined yet,
   * then all star rows and columns must be measured again without ignoring anything.
   * Second group:
   * StarPixel, PixelStar, StarStar cells will be measured last, because at that time we could definitely say how much space each cell takes.
   */


   private void PrepareInnerData(Size availableSize)
   {
      replaceRowStarsWithAuto = double.IsPositiveInfinity(availableSize.Height);
      replaceColStarsWithAuto = double.IsPositiveInfinity(availableSize.Width);

      int colCount = ColumnDefinitions.Count;
      int rowCount = RowDefinitions.Count;

      bool emptyRows = rowCount == 0;
      bool emptyCols = colCount == 0;

      if (emptyRows)
      {
         rowCount = 1;
      }

      if (emptyCols)
      {
         colCount = 1;
      }

      ClearCells();
      CreateSegments(rowCount, colCount);

      if (emptyRows)
      {
         rowSegments[0] = new GridSegment(0, 0, double.PositiveInfinity, GridUnitType.Star, 0) {Stars = 1.0};
         rowSegments[0].MeasuredSize = Double.PositiveInfinity;
         rowSegments[0].MeasureType = replaceRowStarsWithAuto ? InnerGridUnitType.Auto : InnerGridUnitType.Star;
      }
      else
      {
         for (int i = 0; i < rowCount; ++i)
         {
            RowDefinition def = RowDefinitions[i];
            GridLength height = def.Height;

            double margin = IndividualRowSpacing ? def.Margin : RowSpacing;

            var padding = def.Padding;
            double minSize = def.MinHeight;
            double maxSize = Math.Min(def.MaxHeight, MaxDefinitionSize);
               
            if (i == rowCount - 1)
            {
               margin = 0;
            }

            var segment = new GridSegment(0, def.MinHeight, def.MaxHeight, height.GridUnitType, i);

            switch (def.Height.GridUnitType)
            {
               case GridUnitType.Pixel:
                  segment.MeasureType = InnerGridUnitType.Pixel;
                  var measured = def.Height.Value;
                  minSize = Math.Max(minSize, Math.Min(measured, maxSize));
                  minSize = Math.Max(minSize-padding, 0);
                  break;
               case GridUnitType.Auto:
                  segment.MeasureType = InnerGridUnitType.Auto;
                  break;
               case GridUnitType.Star:
                  segment.MeasureType = replaceRowStarsWithAuto ? InnerGridUnitType.Auto : InnerGridUnitType.Star;
                  segment.Stars = Math.Min(height.Value, MaxStartsCount);
                  break;
            }

            maxSize = Math.Max(0, maxSize - padding);

            segment.Min = minSize;
            segment.Max = maxSize;
            segment.Margin = margin;
            segment.Padding = padding;

            rowSegments[i] = segment;
         }
      }

      if (emptyCols)
      {
         colSegments[0] = new GridSegment(0, 0, double.PositiveInfinity, GridUnitType.Star, 0) {Stars = 1.0};
         colSegments[0].MeasuredSize = Double.PositiveInfinity;
         colSegments[0].MeasureType = replaceColStarsWithAuto ? InnerGridUnitType.Auto : InnerGridUnitType.Star;
      }
      else
      {
         for (int i = 0; i < colCount; ++i)
         {
            var def = ColumnDefinitions[i];
            var width = def.Width;

            double margin = IndividualColumnSpacing ? def.Margin : ColumnSpacing;

            var padding = def.Padding;

            if (i == rowCount - 1)
            {
               margin = 0;
            }

            double minSize = def.MinWidth;
            double maxSize = Math.Min(def.MaxWidth, MaxDefinitionSize);

            var segment = new GridSegment(0, def.MinWidth, def.MaxWidth, width.GridUnitType, i);
            switch (def.Width.GridUnitType)
            {
               case GridUnitType.Pixel:
                  segment.MeasureType = InnerGridUnitType.Pixel;
                  var measured = def.Width.Value;
                  minSize = Math.Max(minSize, Math.Min(measured, maxSize));
                  minSize = Math.Max(minSize - padding, 0);
                  break;
               case GridUnitType.Auto:
                  segment.MeasureType = InnerGridUnitType.Auto;
                  break;
               case GridUnitType.Star:
                  segment.MeasureType = replaceColStarsWithAuto ? InnerGridUnitType.Auto : InnerGridUnitType.Star;
                  segment.Stars = Math.Min(width.Value, MaxStartsCount);
                  break;
            }

            maxSize = Math.Max(0, maxSize - padding);

            segment.Min = minSize;
            segment.Max = maxSize;
            segment.Margin = margin;
            segment.Padding = padding;

            colSegments[i] = segment;
         }
      }

      if (Children.Count > 0)
      {
         gridCells = new GridCell[Children.Count];
         int i = 0;
         foreach (var child in Children)
         {
            int col = Math.Min(GetColumn(child), colCount - 1);
            int row = Math.Min(GetRow(child), rowCount - 1);
            int colspan = Math.Min(GetColumnSpan(child), colCount - col);
            int rowspan = Math.Min(GetRowSpan(child), rowCount - row);

            var cell = new GridCell(row, col, rowspan, colspan, i)
            {
               ColumnType = DefineSpanCellType(colSegments, col, colspan),
               RowType = DefineSpanCellType(rowSegments, row, rowspan)
            };

            hasStarRows |= cell.ContainsStarRows;
            hasStarColumns |= cell.ContainsStarColumns;

            if (!cell.ContainsStarRows && !cell.ContainsStarColumns)
            {
               cell.GroupIndex = 0;
            }
            else if ((cell.ContainsStarRows && cell.ContainsAutoColumns) || (cell.ContainsAutoRows && cell.ContainsStarColumns))
            {
               cell.GroupIndex = 1;
            }
            else
            {
               cell.GroupIndex = 2;
            }

            AddCell(cell);
            gridCells[i] = cell;
            i++;
         }
      }
   }

   private void ClearCells()
   {
      cellsDictionary.Clear();
   }

   private void CreateSegments(int rowCount, int colCount)
   {
      rowSegments = new GridSegment[rowCount];
      colSegments = new GridSegment[colCount];
   }

   private void AddCell(GridCell gridCell)
   {
      List<GridCell> innerCells;
      if (cellsDictionary.TryGetValue(gridCell.GroupIndex, out innerCells))
      {
         cellsDictionary[gridCell.GroupIndex].Add(gridCell);
      }
      else
      {
         cellsDictionary.Add(gridCell.GroupIndex, new List<GridCell>() {gridCell});
      }
   }

   private InnerGridUnitType DefineSpanCellType(GridSegment[] segment, int startIndex, int count)
   {
      InnerGridUnitType cellType = InnerGridUnitType.None;
      int endIndex = startIndex + count;
      for (int i = startIndex; i < endIndex; ++i)
      {
         cellType |= segment[i].MeasureType;
      }
      return cellType;
   }

   private double ClampSegment(double current, double min, double max)
   {
      if (current > max)
      {
         current = max;
      }
      else if (current < min)
      {
         current = min;
      }
      return current;
   }

   /// <summary>
   /// Measures the control and its child elements as part of a layout pass.
   /// </summary>
   /// <param name="availableSize">The size available to the control.</param>
   /// <returns>The desired size for the control.</returns>
   protected override Size MeasureOverride(Size availableSize)
   {
      measureTimer = Stopwatch.StartNew();
      PrepareInnerData(availableSize);
      var size = MeasureGrid(availableSize);
      return size;
   }

   /// <summary>
   /// Positions child elements as part of a layout pass.
   /// </summary>
   /// <param name="finalSize">The size available to the control.</param>
   /// <returns>The actual size used.</returns>
   protected override Size ArrangeOverride(Size finalSize)
   {
      Stopwatch arrangeTimer = Stopwatch.StartNew();

      if (IsDefinitionsEmpty)
      {
         foreach (var child in Children)
         {
            child.Arrange(new Rect(finalSize));
         }
      }
      else
      {
         CalculateFinalGridSize(rowSegments, finalSize.Height, true);
         CalculateFinalGridSize(colSegments, finalSize.Width, false);

         int index = 0;
         foreach (var child in Children)
         {
            var cell = gridCells[index];

            var childFinalX = colSegments[cell.ColumnIndex].Offset + colSegments[cell.ColumnIndex].Padding.TopLeft;
            var childFinalY = rowSegments[cell.RowIndex].Offset + rowSegments[cell.RowIndex].Padding.TopLeft;

            var childFinalW = GetArrangeSize(colSegments, cell.ColumnIndex, cell.ColSpan);
            var childFinalH = GetArrangeSize(rowSegments, cell.RowIndex, cell.RowSpan);

            // A Grid must never hand a child a non-finite rect. Arrange REJECTS one by throwing, and that exception
            // unwinds the whole layout pass: every element after the offending one keeps its default slot, so a window
            // renders with its content piled at the origin. Offsets can inherit a NaN from a track size, so the
            // coordinates need the same guard the sizes get in GetArrangeSize.
            if (!double.IsFinite(childFinalX)) childFinalX = 0;
            if (!double.IsFinite(childFinalY)) childFinalY = 0;
            if (!double.IsFinite(childFinalW)) childFinalW = 0;
            if (!double.IsFinite(childFinalH)) childFinalH = 0;

            var rect = new Rect(childFinalX, childFinalY, childFinalW, childFinalH);

            child.Arrange(rect);
            index++;
         }
      }
         
      //Debug.WriteLine("Grid arrange time = " + arrangeTimer.ElapsedMilliseconds);
      return finalSize;
   }
      
   protected override void OnRender(IDrawingContext context)
   {
      if (Background != Brushes.Transparent)
      {
         context.ForControl(this).DrawRectangle(Background, new Rect(new Size(ActualWidth, ActualHeight)));
      }

      if (ShowGridLines && rowSegments != null)
      {
         var lineBrush = Brushes.Black;
            
         //for (int i = 1; i < RowDefinitions.Count; ++i)
         //{
         //   context.DrawRectangle(this, lineBrush,
         //      new Rect(new Point(0, RowDefinitions[i].Offset), new Size(ActualWidth, 1)));
         //   context.DrawRectangle(this, lineBrush,
         //      new Rect(new Point(0, RowDefinitions[i].Offset+ RowDefinitions[i].ActualHeight), new Size(ActualWidth, 1)));
         //}
         //for (int i = 1; i < ColumnDefinitions.Count; ++i)
         //{
         //   context.DrawRectangle(this, lineBrush,
         //      new Rect(new Point(ColumnDefinitions[i].Offset, 0), new Size(1, ActualHeight)));
         //}

         if (rowSegments.Length > 1)
         {
            for (int i = 1; i < rowSegments.Length; ++i)
            {
               context.ForControl(this)
                  .DrawRectangle(lineBrush,
                  new Rect(new Vector2(0, rowSegments[i].Offset), new Size(ActualWidth, 1)))
                  .DrawRectangle(lineBrush,
                  new Rect(new Vector2(0, rowSegments[i].Offset + rowSegments[i].FullSize),
                     new Size(ActualWidth, 1)));
            }
         }
         if (colSegments.Length > 0)
         {
            for (int i = 1; i < colSegments.Length; ++i)
            {
               context.ForControl(this)
                  .DrawRectangle(lineBrush,
                  new Rect(new Vector2(colSegments[i].Offset, 0), new Size(1, ActualHeight)))
                  .DrawRectangle(lineBrush,
                  new Rect(new Vector2(colSegments[i].Offset + colSegments[i].FullSize, 0), new Size(1, ActualHeight)));
            }
         }
      }
   }

   private double GetArrangeSize(GridSegment[] segments, int start, int count)
   {
      double arrangedSize = 0;

      if (count == 1)
      {
         var segment = segments[start];
         arrangedSize = segment.Min;
      }
      else
      {
         var startSegment = segments[start];
         var endSegment = segments[start + count - 1];

         arrangedSize = endSegment.Offset + endSegment.FullSize - startSegment.Offset;
         arrangedSize -= endSegment.Padding.BottomRight - startSegment.Padding.TopLeft;
      }

      // Never hand back a NEGATIVE or non-finite size: Arrange rejects such a rect by throwing, which aborts the whole
      // pass and leaves every element after the offending one in its default slot. NB Math.Max(0, NaN) is NaN, so the
      // NaN case needs its own test rather than a clamp.
      return double.IsNaN(arrangedSize) || arrangedSize < 0 ? 0 : arrangedSize;
   }

   private double CalculateTotalSize(GridSegment[] segments)
   {
      double size = 0;
      for (int i = 0; i < segments.Length; ++i)
      {
         size += segments[i].FullSizeWithMargin;
         
         // TODO: check if this is correct
         /*
         if (segments[i].IsAuto)
         {
            size += segments[i].FullSizeWithMargin;
         }
         else if (segments[i].IsStar)
         {
            size += segments[i].MeasuredSize + segments[i].Padding + segments[i].Margin;
         }
         */
      }
      return size;
   }

   private void CalculateFinalGridSize(GridSegment[] segments, double finalSize, bool isRow)
   {
      double totalTakenSize = 0;
      double stars = 0;
      for (int i = 0; i < segments.Length; ++i)
      {
         if (!segments[i].IsStar)
         {
            totalTakenSize += segments[i].FullSizeWithMargin;
         }
         else
         {
            stars += segments[i].Stars;
            totalTakenSize += segments[i].Margin + segments[i].Padding;
         }
      }

      // Spare room is the star tracks' to share; Auto keeps what the measure gave it, and a shortfall overflows.
      var availableSize = Math.Max(finalSize - totalTakenSize, 0);
      if (availableSize > 0 && stars > 0)   // a "0*" track alone makes stars 0, and the division NaN
      {
         foreach (var starSegment in segments.Where(x => x.IsStar))
         {
            starSegment.Min = Math.Max(availableSize / stars * starSegment.Stars, 0);
         }
      }

      Double offset = 0.0;
      for (int i = 0; i < segments.Length; ++i)
      {
         var gridSegment = segments[i];
         gridSegment.Offset = offset;

         if (isRow && RowDefinitions.Count > 0)
         {
            _rowDefinitions[i].ActualHeight = gridSegment.FullSize;
            _rowDefinitions[i].Offset = offset;
         }
         else if (!isRow && ColumnDefinitions.Count > 0)
         {
            _columnDefinitions[i].ActualWidth = gridSegment.FullSize;
            _columnDefinitions[i].Offset = offset;
         }
         
         offset += gridSegment.FullSizeWithMargin;
      }
   }

   private Size MeasureGrid(Size availableSize)
   {
      MeasureCells(0);
      MeasureCells(1, true);

      if (hasStarRows)
      {
         CalculateStarSegments(rowSegments, availableSize.Height);
      }

      if (hasStarColumns)
      {
         CalculateStarSegments(colSegments, availableSize.Width);
      }
      MeasureCells(1);
      MeasureCells(2);
      
      double desiredX = CalculateTotalSize(colSegments);
      double desiredY = CalculateTotalSize(rowSegments);
      //Debug.WriteLine("Grid measure time = " + measureTimer.ElapsedMilliseconds);

      // if (!double.IsInfinity(availableSize.Width))
      // {
      //    desiredX = Math.Max(availableSize.Width, desiredX);
      // }
      //
      // if (!double.IsInfinity(availableSize.Height))
      // {
      //    desiredY = Math.Max(availableSize.Height, desiredY);
      // }

      return new Size(desiredX, desiredY);
   }

   private void MeasureCells(int groupIndex, bool ignoreStarSize = false)
   {
      if (groupIndex > MaxGroupIndex)
         return;

      if (!cellsDictionary.ContainsKey(groupIndex)) return;

      var list = cellsDictionary[groupIndex];
      
      foreach (var cell in list)
      {
         var row = rowSegments[cell.RowIndex];
         var col = colSegments[cell.ColumnIndex];
         bool ignoreMeasuredRow = false;
         bool ignoreMeasuredColumn = false;

         if (!cell.ContainsStarRows && cell.ContainsAutoRows)
         {
            childSize.Height = double.PositiveInfinity;
         }
         else
         {
            childSize.Height = CalculateSizeForRange(rowSegments, cell.RowIndex, cell.RowSpan);
            if (ignoreStarSize && cell.ContainsStarRows)
            {
               ignoreMeasuredRow = true;
            }
         }

         if (!cell.ContainsStarColumns && cell.ContainsAutoColumns)
         {
            childSize.Width = double.PositiveInfinity;
         }
         else
         {
            childSize.Width = CalculateSizeForRange(colSegments, cell.ColumnIndex, cell.ColSpan);
            if (ignoreStarSize && cell.ContainsStarColumns)
            {
               ignoreMeasuredColumn = true;
            }
         }

         var element = Children[cell.ChildIndex];
         element.Measure(childSize);
         var desired = element.DesiredSize;

         if (!ignoreMeasuredRow)
         {
            if (cell.RowSpan == 1)
            {
               // Pixel rows are fixed by their definition - content does not resize them. Auto/Star rows grow to fit
               // the LARGEST child in the row (hence Math.Max, not an overwrite that lets the last child win).
               if (!row.IsPixel)
                  row.Min = Math.Max(row.Min, ClampSegment(desired.Height, 0, row.Max));
            }
            else
            {
               RegisterSpan(new SpanData(rowSegments, cell.RowIndex, cell.RowSpan), desired.Height);
            }
         }

         if (!ignoreMeasuredColumn)
         {
            if (cell.ColSpan == 1)
            {
               if (!col.IsPixel)
                  col.Min = Math.Max(col.Min, ClampSegment(desired.Width, 0, col.Max));
            }
            else
            {
               RegisterSpan(new SpanData(colSegments, cell.ColumnIndex, cell.ColSpan), desired.Width);
            }
         }
      }
      CalculateSpan();
   }

   private void RegisterSpan(SpanData span, double value)
   {
      if (store.ContainsKey(span))
      {
         var currentValue = store[span];

         //Store maximum span value for one GridSegment
         if (currentValue < value)
         {
            store[span] = value;
         }
      }
      else
      {
         store.Add(span, value);
      }
   }

   private void CalculateSpan()
   {
      if (store.Count <= 0) return;
      foreach (var kvp in store)
      {
         var span = kvp.Key;
         double cumulativeMinRangeSize = 0;

         for (int i = span.Start; i < span.End; ++i)
         {
            cumulativeMinRangeSize += span.Segments[i].Min;
         }

         if (kvp.Value > cumulativeMinRangeSize)
         {
            double controlSize = kvp.Value;
            for (int i = span.Start; i < span.End; ++i)
            {
               if (!span.Segments[i].IsAuto)
               {
                  double size = Math.Min(controlSize/(span.End-i), span.Segments[i].Max);
                  if (size > span.Segments[i].Min)
                  {
                     span.Segments[i].Min = size;
                  }
               }
               controlSize -= span.Segments[i].Min;
            }
         }
      }

      store.Clear();
   }

   private double CalculateSizeForRange(GridSegment[] segment, int start, int count)
   {
      double size = 0;
         
      if (count == 1)
      {
         size += segment[start].IsStar ? segment[start].MeasuredSize : segment[start].Min;
      }
      else
      {
         int end = start + count;
         for (int i = start; i < end; ++i)
         {
            if (i == start)
            {
               size += !segment[i].IsStar ? segment[i].Min : segment[i].MeasuredSize;
               size += segment[i].Padding.BottomRight + segment[i].Margin;
            }
            else if (i == end - 1)
            {
               size += !segment[i].IsStar ? segment[i].Min : segment[i].MeasuredSize;
               size += segment[i].Padding.TopLeft;
            }
            else
            {
               size += !segment[i].IsStar ? segment[i].FullSizeWithMargin : segment[i].MeasuredSize + segment[i].Padding + segment[i].Margin;
            }
         }
      }
      return size;
   }

   //Calculates total amount of star rows/columns and distribute size between star segments
   private void CalculateStarSegments(GridSegment[] segment, double availableSize)
   {
      double allStars = 0;
      double takenSize = 0;
      for (int i = 0; i < segment.Length; ++i)
      {
         if (segment[i].IsStar)
         {
            allStars += segment[i].Stars;
            takenSize += segment[i].Margin + segment[i].Padding;
         }
         else
         {
            //takenSize += segment[i].Min;
            takenSize += segment[i].FullSizeWithMargin;
         }
      }

      var finalAvailableSize = Math.Max(availableSize - takenSize, 0);

      if (finalAvailableSize > 0 && allStars > 0 && !Double.IsInfinity(availableSize))
      {
         for (int i = 0; i < segment.Length; ++i)
         {
            if (segment[i].IsStar)
            {
               segment[i].MeasuredSize = Math.Max(((finalAvailableSize / allStars) * segment[i].Stars), 0);
            }
         }
      }
   }

   private class GridSegment
   {
      //Max possible size of GridSegment including padding
      public double Max;
      //Contains biggest measured size of GridSegment, excluding padding and margin
      public double Min;
      //Last Measured size value. Used for storing temp values
      public double MeasuredSize;

      public double FullSize => Min + Padding;

      public double FullSizeWithMargin => FullSize + Margin;

      //Number of stars for current GridSegment(could be 0 if its not a Star segment)
      public double Stars;
      //Original Definition type
      public GridUnitType OriginalType { get; }
      //Definition type calculated at Measured phase (because of span could contain several types merged into one)
      public InnerGridUnitType MeasureType { get; set; }

      public double Offset;

      public Double Margin;

      public HalfThickness Padding;

      public int Index;
      
      public Boolean IsAuto => OriginalType == GridUnitType.Auto;

      public Boolean IsStar => OriginalType == GridUnitType.Star;

      public Boolean IsPixel => OriginalType == GridUnitType.Pixel;

      public GridSegment(double size, double min, double max, GridUnitType originalType, int index)
      {
         Min = min;
         Max = max;
         MeasuredSize = size;
         Stars = 0;
         OriginalType = originalType;
         Offset = 0;
         Index = index;
      }
   }

   private class GridCell
   {
      public readonly int RowIndex;
      public readonly int ColumnIndex;
      public readonly int RowSpan;
      public readonly int ColSpan;
      public readonly int ChildIndex;

      //Contains combination of types by row and column including span definitions
      public InnerGridUnitType ColumnType;
      public InnerGridUnitType RowType;

      public bool ContainsAutoRows => (RowType & InnerGridUnitType.Auto) != 0;
      public bool ContainsAutoColumns => (ColumnType & InnerGridUnitType.Auto) != 0;

      public bool ContainsStarRows => (RowType & InnerGridUnitType.Star) != 0;
      public bool ContainsStarColumns => (ColumnType & InnerGridUnitType.Star) != 0;

      public int GroupIndex;

      public GridCell(int rowIndex, int columnIndex, int rowSpan, int colSpan, int childIndex)
      {
         RowIndex = rowIndex;
         ColumnIndex = columnIndex;
         RowSpan = rowSpan;
         ColSpan = colSpan;
         GroupIndex = -1;
         ChildIndex = childIndex;
      }
   }

   [Flags]
   private enum InnerGridUnitType {None = 0, Pixel = 1, Auto = 2, Star = 4 }

   private class SpanData
   {
      public SpanData(GridSegment[] segments, int start, int count)
      {
         Start = start;
         Count = count;
         Segments = segments;
      }

      //Span start index.
      public int Start { get; }

      //Span columns count
      public int Count { get; }

      //Span end index (start + count)
      public int End => Start + Count;

      //Grid segments array.
      public GridSegment[] Segments { get; }

      public override int GetHashCode()
      {
         int hash = Start ^ 333;
         hash &= Count ^ 333;
         hash &= Segments.GetHashCode();
         return (hash);
      }

      public override bool Equals(object obj)
      {
         return obj is SpanData sd
                && sd.Segments == Segments
                && sd.Start == Start
                && sd.Count == Count;
      }
   }
}