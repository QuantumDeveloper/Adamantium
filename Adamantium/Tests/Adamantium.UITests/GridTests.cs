using System;
using System.Diagnostics;
using Adamantium.Engine.Graphics;
using Adamantium.UI;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Media;
using NUnit.Framework;
using Rectangle = Adamantium.UI.Controls.Rectangle;

namespace Adamantium.UITests
{
   public class GridTests
   {
      private class LayoutPoker : Panel
      {
         public Size MeasureResult = new Size(0, 0);
         public Size MeasureArg = new Size(0, 0);
         public Size ArrangeResult = new Size(0, 0);
         public Size ArrangeArg = new Size(0, 0);
         public Func<Size> ArrangeFunc;
         public Func<Size> MeasureFunc;

         protected override Size MeasureOverride(Size availableSize)
         {
            MeasureArg = availableSize;
            MeasureResult = MeasureFunc != null ? MeasureFunc() : MeasureResult;
            Debug.WriteLine($"Panel available size is {availableSize}");
            return MeasureResult;
         }
         
         protected override Size ArrangeOverride(Size finalSize)
         {
            ArrangeArg = finalSize;
            ArrangeResult = ArrangeFunc != null ? ArrangeFunc() : ArrangeResult;
            Debug.WriteLine($"Panel final size is {finalSize}");
            return ArrangeResult;
         }
      }

      [Test]
      public void ComplexSpanMeasuredTest()
      {
         var rectangle = new Rectangle();
         rectangle.Margin = new Thickness(10, 20, 10, 20);
         rectangle.Width = 300;
         rectangle.Height = 700;
         rectangle.Stretch = Stretch.None;
         rectangle.HorizontalAlignment = HorizontalAlignment.Center;
         rectangle.VerticalAlignment = VerticalAlignment.Stretch;
         rectangle.CornerRadius = new CornerRadius(5);
         rectangle.StrokeThickness = 2;
         rectangle.MinWidth = 15;
         rectangle.MinHeight = 15;
         rectangle.Name = "rect1";
         rectangle.Stroke = Brushes.Black;
         rectangle.Fill = Brushes.DarkSeaGreen;

         Thumb thumb = new Thumb();
         thumb.Width = 100;
         thumb.Height = 55;
         thumb.HorizontalAlignment = HorizontalAlignment.Center;
         thumb.Name = "thumb";
         thumb.Background = Brushes.DarkBlue;

         Border border = new Border();
         border.Name = "border";
         border.Width = 800;
         border.Height = 720;
         border.HorizontalAlignment = HorizontalAlignment.Stretch;
         border.BorderBrush = Brushes.Red;
         border.BorderThickness = new Thickness(2);

         Rectangle rectangle2 = new Rectangle();
         rectangle2.Width = 100;
         rectangle2.Height = 40;
         rectangle2.Name = "rect2";
         rectangle2.StrokeThickness = 10;
         rectangle2.Stroke = Brushes.LightBlue;
         rectangle2.Fill = Brushes.Green;
         rectangle2.HorizontalAlignment = HorizontalAlignment.Stretch;
         rectangle2.VerticalAlignment = VerticalAlignment.Stretch;


         Ellipse el = new Ellipse();
         el.Name = "el";
         el.Width = 350;
         el.Height = 350;
         el.VerticalAlignment = VerticalAlignment.Top;
         el.HorizontalAlignment = HorizontalAlignment.Left;
         el.Fill = Brushes.DarkOrange;
         rectangle.Width = 300;
         rectangle.HorizontalAlignment = HorizontalAlignment.Center;
         rectangle.VerticalAlignment = VerticalAlignment.Stretch;


         Grid grid = new Grid();
         grid.Name = "grid1";
         StackPanel stackpanel = new StackPanel();
         grid.Background = Brushes.CornflowerBlue;
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto), MinHeight = 10 });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(4, GridUnitType.Pixel), MinHeight = 4, MaxHeight = 4 });
         grid.RowDefinitions.Add(new RowDefinition() { MinHeight = 100 });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(100, GridUnitType.Auto) });
         grid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 100, Width = new GridLength(0, GridUnitType.Auto) });
         grid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 4, MaxWidth = 4, Width = new GridLength(4, GridUnitType.Pixel) });
         grid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 100 });
         grid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 150, Width = new GridLength(1, GridUnitType.Star) });
         Grid.SetRow(stackpanel, 1);
         Grid.SetColumn(stackpanel, 1);
         grid.ShowGridLines = true;
         rectangle.Height = 200;
         grid.Children.Add(rectangle);
         Rectangle rect10 = new Rectangle() { Height = 50, Width = 80, Fill = Brushes.Red };
         rect10.VerticalAlignment = VerticalAlignment.Top;
         grid.Children.Add(rectangle2);
         grid.Children.Add(rect10);
         grid.Children.Add(el);
         el.VerticalAlignment = VerticalAlignment.Center;
         Grid.SetColumn(rectangle, 0);
         Grid.SetRow(rectangle, 0);
         Grid.SetColumn(el, 2);
         Grid.SetRow(el, 0);
         rectangle2.Height = 25;
         Grid.SetRow(rectangle2, 1);
         Grid.SetRowSpan(rectangle, 2);
         Grid.SetRow(rect10, 4);
         Grid.SetRowSpan(el, 2);
         el.HorizontalAlignment = HorizontalAlignment.Center;
         el.Stroke = Brushes.DarkViolet;
         el.StrokeThickness = 22;

         var rt = new RenderTargetPanel();
         rt.MinWidth = 200;
         rt.MinHeight = 200;
         grid.Children.Add(rt);
         Grid.SetColumn(rt, 2);
         Grid.SetRow(rt, 1);

         rt.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
         rt.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
         rt.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
         rt.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

         Rectangle rectN = new Rectangle();
         rectN.Width = 50;
         rectN.Height = 20;
         rectN.CornerRadius = new CornerRadius(4);
         rectN.Fill = Brushes.Red;
         rectN.HorizontalAlignment = HorizontalAlignment.Center;
         rectN.VerticalAlignment = VerticalAlignment.Center;
         rt.Children.Add(rectN);

         Ellipse ellipseN = new Ellipse();
         ellipseN.Width = 50;
         ellipseN.Height = 50;
         ellipseN.Fill = Brushes.Blue;
         ellipseN.HorizontalAlignment = HorizontalAlignment.Center;
         ellipseN.VerticalAlignment = VerticalAlignment.Center;
         rt.Children.Add(ellipseN);
         Grid.SetRow(ellipseN, 1);
         Grid.SetColumn(ellipseN, 1);

         GridSplitter gridSplitter = new GridSplitter();
         gridSplitter.ResizeDirection = ResizeDirection.Columns;
         gridSplitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
         gridSplitter.Background = Brushes.Red;
         //gridSplitter.DeferredResizeEnabled = true;
         grid.Children.Add(gridSplitter);
         Grid.SetColumn(gridSplitter, 1);
         GridSplitter splitter = new GridSplitter();
         splitter.ResizeDirection = ResizeDirection.Rows;
         splitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
         //splitter.DeferredResizeEnabled = true;

         splitter.Name = "gridsplitter";
         splitter.Background = Brushes.DarkGoldenrod;
         splitter.MinWidth = 35;
         splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
         //splitter.ResizeDirection = ResizeDirection.Columns;
         grid.Children.Add(splitter);
         Grid.SetColumn(splitter, 0);
         Grid.SetRow(splitter, 2);
         grid.Children.Add(thumb);
         thumb.HorizontalAlignment = HorizontalAlignment.Right;
         Grid.SetRow(thumb, 1);

         StackPanel stackpanel2 = new StackPanel();
         stackpanel2.VerticalAlignment = VerticalAlignment.Top;
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 70, Fill = Brushes.Yellow });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 150, Fill = Brushes.White });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 90, Fill = Brushes.Yellow });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 30, Fill = Brushes.Teal });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         grid.Children.Add(stackpanel2);
         Grid.SetColumn(stackpanel2, 2);
         Grid.SetColumnSpan(stackpanel2, 2);

         StackPanel stackpanel3 = new StackPanel();
         stackpanel3.Background = Brushes.DarkOrchid;
         stackpanel3.Name = "Stack3";
         stackpanel3.Height = 200;
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 200, Fill = Brushes.Teal });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         grid.Children.Add(stackpanel3);
         Grid.SetColumn(stackpanel3, 0);
         stackpanel3.VerticalAlignment = VerticalAlignment.Stretch;
         Grid.SetRow(stackpanel3, 3);
         Grid.SetColumnSpan(stackpanel3, 4);

         grid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
         
         Assert.AreEqual(new Size(2124, 604), grid.DesiredSize);

         /*WPF code for check
          * 
          * 
         var rectangle = new Rectangle();
         rectangle.Margin = new Thickness(10, 20, 10, 20);
         rectangle.Width = 300;
         rectangle.Height = 700;
         rectangle.Stretch = Stretch.None;
         rectangle.HorizontalAlignment = HorizontalAlignment.Center;
         rectangle.VerticalAlignment = VerticalAlignment.Stretch;
         rectangle.RadiusX = 5;
         rectangle.RadiusY = 5;
         rectangle.StrokeThickness = 2;
         rectangle.MinWidth = 15;
         rectangle.MinHeight = 15;
         rectangle.Name = "rect1";
         rectangle.Stroke = Brushes.Black;
         rectangle.Fill = Brushes.DarkSeaGreen;

         Thumb thumb = new Thumb();
         thumb.Width = 100;
         thumb.Height = 55;
         thumb.HorizontalAlignment = HorizontalAlignment.Center;
         thumb.Name = "thumb";
         thumb.Background = Brushes.DarkBlue;

         Border border = new Border();
         border.Name = "border";
         border.Width = 800;
         border.Height = 720;
         border.HorizontalAlignment = HorizontalAlignment.Stretch;
         border.BorderBrush = Brushes.Red;
         border.BorderThickness = new Thickness(2);

         Rectangle rectangle2 = new Rectangle();
         rectangle2.Width = 100;
         rectangle2.Height = 40;
         rectangle2.Name = "rect2";
         rectangle2.StrokeThickness = 10;
         rectangle2.Stroke = Brushes.LightBlue;
         rectangle2.Fill = Brushes.Green;
         rectangle2.HorizontalAlignment = HorizontalAlignment.Stretch;
         rectangle2.VerticalAlignment = VerticalAlignment.Stretch;


         Ellipse el = new Ellipse();
         el.Name = "el";
         el.Width = 350;
         el.Height = 350;
         el.VerticalAlignment = VerticalAlignment.Top;
         el.HorizontalAlignment = HorizontalAlignment.Left;
         el.Fill = Brushes.DarkOrange;
         rectangle.Width = 300;
         rectangle.HorizontalAlignment = HorizontalAlignment.Center;
         rectangle.VerticalAlignment = VerticalAlignment.Stretch;


         Grid grid = new Grid();
         grid.Name = "grid1";
         StackPanel stackpanel = new StackPanel();
         stackpanel.Orientation = Orientation.Horizontal;
         grid.Background = Brushes.CornflowerBlue;
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto), MinHeight = 10 });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(4, GridUnitType.Pixel), MinHeight = 4, MaxHeight = 4 });
         grid.RowDefinitions.Add(new RowDefinition() { MinHeight = 100 });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(100, GridUnitType.Auto) });
         grid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 100, Width = new GridLength(0, GridUnitType.Auto) });
         grid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 4, MaxWidth = 4, Width = new GridLength(4, GridUnitType.Pixel) });
         grid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 100 });
         grid.ColumnDefinitions.Add(new ColumnDefinition() { MinWidth = 150, Width = new GridLength(1, GridUnitType.Star) });
         Grid.SetRow(stackpanel, 1);
         Grid.SetColumn(stackpanel, 1);
         grid.ShowGridLines = true;
         rectangle.Height = 200;
         grid.Children.Add(rectangle);
         Rectangle rect10 = new Rectangle() { Height = 50, Width = 80, Fill = Brushes.Red };
         rect10.VerticalAlignment = VerticalAlignment.Top;
         grid.Children.Add(rectangle2);
         grid.Children.Add(rect10);
         grid.Children.Add(el);
         el.VerticalAlignment = VerticalAlignment.Center;
         Grid.SetColumn(rectangle, 0);
         Grid.SetRow(rectangle, 0);
         Grid.SetColumn(el, 2);
         Grid.SetRow(el, 0);
         rectangle2.Height = 25;
         Grid.SetRow(rectangle2, 1);
         Grid.SetRowSpan(rectangle, 2);
         Grid.SetRow(rect10, 4);
         Grid.SetRowSpan(el, 2);
         el.HorizontalAlignment = HorizontalAlignment.Center;
         el.Stroke = Brushes.DarkViolet;
         el.StrokeThickness = 22;

         var rt = new Grid();
         rt.MinWidth = 200;
         rt.MinHeight = 200;
         grid.Children.Add(rt);
         Grid.SetColumn(rt, 2);
         Grid.SetRow(rt, 1);

         rt.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
         rt.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
         rt.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
         rt.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

         Rectangle rectN = new Rectangle();
         rectN.Width = 50;
         rectN.Height = 20;
         rectN.RadiusX = 4;
         rectN.RadiusY = 4;
         rectN.Fill = Brushes.Red;
         rectN.HorizontalAlignment = HorizontalAlignment.Center;
         rectN.VerticalAlignment = VerticalAlignment.Center;
         rt.Children.Add(rectN);

         Ellipse ellipseN = new Ellipse();
         ellipseN.Width = 50;
         ellipseN.Height = 50;
         ellipseN.Fill = Brushes.Blue;
         ellipseN.HorizontalAlignment = HorizontalAlignment.Center;
         ellipseN.VerticalAlignment = VerticalAlignment.Center;
         rt.Children.Add(ellipseN);
         Grid.SetRow(ellipseN, 1);
         Grid.SetColumn(ellipseN, 1);

         GridSplitter gridSplitter = new GridSplitter();
         gridSplitter.ResizeDirection = GridResizeDirection.Columns;
         gridSplitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
         gridSplitter.Background = Brushes.Red;
         //gridSplitter.DeferredResizeEnabled = true;
         grid.Children.Add(gridSplitter);
         Grid.SetColumn(gridSplitter, 1);
         GridSplitter splitter = new GridSplitter();
         splitter.ResizeDirection = GridResizeDirection.Rows;
         splitter.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
         //splitter.DeferredResizeEnabled = true;

         splitter.Name = "gridsplitter";
         splitter.Background = Brushes.DarkGoldenrod;
         splitter.MinWidth = 35;
         splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
         //splitter.ResizeDirection = ResizeDirection.Columns;
         grid.Children.Add(splitter);
         Grid.SetColumn(splitter, 0);
         Grid.SetRow(splitter, 2);
         grid.Children.Add(thumb);
         thumb.HorizontalAlignment = HorizontalAlignment.Right;
         Grid.SetRow(thumb, 1);

         StackPanel stackpanel2 = new StackPanel();
         stackpanel2.Orientation = Orientation.Horizontal;
         stackpanel2.VerticalAlignment = VerticalAlignment.Top;
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 70, Fill = Brushes.Yellow });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 150, Fill = Brushes.White });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 90, Fill = Brushes.Yellow });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 30, Fill = Brushes.Teal });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel2.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         grid.Children.Add(stackpanel2);
         Grid.SetColumn(stackpanel2, 2);
         Grid.SetColumnSpan(stackpanel2, 2);

         StackPanel stackpanel3 = new StackPanel();
         stackpanel3.Orientation = Orientation.Horizontal;
         stackpanel3.Background = Brushes.DarkOrchid;
         stackpanel3.Name = "Stack3";
         stackpanel3.Height = 200;
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 200, Fill = Brushes.Teal });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Teal });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.Yellow });
         stackpanel3.Children.Add(new Rectangle() { Width = 150, Height = 50, Fill = Brushes.White });
         grid.Children.Add(stackpanel3);
         Grid.SetColumn(stackpanel3, 0);
         stackpanel3.VerticalAlignment = VerticalAlignment.Stretch;
         Grid.SetRow(stackpanel3, 3);
         Grid.SetColumnSpan(stackpanel3, 4);

         grid.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         var desiredSize = grid.DesiredSize;
          * 
          */

      }
      
      [Test]
      public void CalculatesColSpanCorrectly()
      {

         Grid grid = new Grid();
         grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
         grid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
         grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

         grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
         grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

         grid.Children.Add(new Border() {Width = 100, Height = 25, [Grid.ColumnSpanProperty] = 3});
         grid.Children.Add(new Border() {Width = 150, Height = 25, [Grid.RowProperty] = 1});
         grid.Children.Add(new Border() {Width = 50, Height = 25, [Grid.ColumnProperty] = 2, [Grid.RowProperty] = 1});


         grid.Measure(Size.Infinity);

         grid.InvalidateMeasure();

         grid.Measure(Size.Infinity);

         grid.Arrange(new Rect(grid.DesiredSize));

         Assert.AreEqual(new Size(204, 50), grid.Bounds.Size);
         Assert.AreEqual(150, grid.ColumnDefinitions[0].ActualWidth);
         Assert.AreEqual(4, grid.ColumnDefinitions[1].ActualWidth);
         Assert.AreEqual(50, grid.ColumnDefinitions[2].ActualWidth);
         Assert.AreEqual(new Rect(52, 0, 100, 25), grid.Children[0].Bounds);
         Assert.AreEqual(new Rect(0, 25, 150, 25), grid.Children[1].Bounds);
         Assert.AreEqual(new Rect(154, 25, 50, 25), grid.Children[2].Bounds);

      }

      [Test]
      public void ComputeActualWidth()
      {
         var c = new Grid();

         Assert.AreEqual(new Size(0, 0), c.DesiredSize);
         Assert.AreEqual(new Size(0, 0), new Size(c.ActualWidth, c.ActualHeight));

         c.MaxWidth = 25;
         c.Width = 50;
         c.MinHeight = 33;

         Assert.AreEqual(new Size(0, 0), c.DesiredSize);
         Assert.AreEqual(new Size(0, 0), new Size(c.ActualWidth, c.ActualHeight));

         c.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(25, 33), c.DesiredSize);
         Assert.AreEqual(new Size(0, 0), new Size(c.ActualWidth, c.ActualHeight));
      }

      [Test]
      public void ChildlessMeasureTest()
      {
         Grid g = new Grid();

         g.Measure(new Size(200, 200));

         Assert.AreEqual(new Size(0, 0), g.DesiredSize);
      }

      [Test]
      public void ChildlessWidthHeightMeasureTest()
      {
         Grid g = new Grid();

         g.Width = 300;
         g.Height = 300;

         g.Measure(new Size(200, 200));

         Assert.AreEqual(new Size(200, 200), g.DesiredSize);
      }

      [Test]
      public void ChildlessMarginTest()
      {
         Grid g = new Grid();

         g.Margin = new Thickness(5);

         g.Measure(new Size(200, 200));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);
      }

      [Test]
      public void Childless_ColumnDefinition_Width_constSize_singleColumn()
      {
         Grid g = new Grid();

         ColumnDefinition def;

         def = new ColumnDefinition();
         def.Width = new GridLength(200);
         g.ColumnDefinitions.Add(def);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(200, 0), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(100, 0), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_ColumnDefinition_Width_constSize_singleColumn()
      {
         Grid g = new Grid();

         ColumnDefinition def;

         def = new ColumnDefinition();
         def.Width = new GridLength(200);
         g.ColumnDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(210, 10), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(100, 10), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_ColumnDefinition_Width_constSize_multiColumn()
      {
         Grid g = new Grid();

         ColumnDefinition def;

         def = new ColumnDefinition();
         def.Width = new GridLength(200);
         g.ColumnDefinitions.Add(def);

         def = new ColumnDefinition();
         def.Width = new GridLength(200);
         g.ColumnDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(410, 10), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(100, 10), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_ColumnDefinition_Width_autoSize_singleColumn()
      {
         Grid g = new Grid();

         ColumnDefinition def;

         def = new ColumnDefinition();
         def.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_ColumnDefinition_Width_autoSize_constSize_multiColumn()
      {
         Grid g = new Grid();

         ColumnDefinition def;

         def = new ColumnDefinition();
         def.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(def);

         def = new ColumnDefinition();
         def.Width = new GridLength(200);
         g.ColumnDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(210, 10), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(100, 10), g.DesiredSize);
      }


      [Test]
      public void ChildlessMargin_ColumnDefinition_Width_starSize_singleColumn()
      {
         Grid g = new Grid();

         ColumnDefinition def;

         def = new ColumnDefinition();
         def.Width = new GridLength(2, GridUnitType.Star);
         g.ColumnDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_ColumnDefinition_Width_starSize_constSize_multiColumn()
      {
         Grid g = new Grid();

         ColumnDefinition def;

         def = new ColumnDefinition();
         def.Width = new GridLength(2, GridUnitType.Star);
         g.ColumnDefinitions.Add(def);

         def = new ColumnDefinition();
         def.Width = new GridLength(200);
         g.ColumnDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(210, 10), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(100, 10), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_RowDefinition_Height_constSize_singleRow()
      {
         Grid g = new Grid();

         RowDefinition def;

         def = new RowDefinition();
         def.Height = new GridLength(200);
         g.RowDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(10, 210), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(10, 100), g.DesiredSize);
      }

      [Test]
      public void ArrangeTestChildless()
      {
         Grid g = new Grid();
         g.RowDefinitions.Add(new RowDefinition());
         g.RowDefinitions.Add(new RowDefinition());

         g.Measure(new Size(3000, 2000));
         g.Arrange(new Rect(g.DesiredSize));
         Assert.AreEqual(Size.Zero, g.Bounds.Size);
      }

      [Test]
      public void ChildlessMargin_RowDefinition_Height_constSize_multiRow()
      {
         Grid g = new Grid();

         RowDefinition def;

         def = new RowDefinition();
         def.Height = new GridLength(200);
         g.RowDefinitions.Add(def);

         def = new RowDefinition();
         def.Height = new GridLength(200);
         g.RowDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(10, 410), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(10, 100), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_RowDefinition_Height_autoSize_singleRow()
      {
         Grid g = new Grid();

         RowDefinition def;

         def = new RowDefinition();
         def.Height = GridLength.Auto;
         g.RowDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_RowDefinition_Height_autoSize_constSize_multiRow()
      {
         Grid g = new Grid();

         RowDefinition def;

         def = new RowDefinition();
         def.Height = GridLength.Auto;
         g.RowDefinitions.Add(def);

         def = new RowDefinition();
         def.Height = new GridLength(200);
         g.RowDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(10, 210), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(10, 100), g.DesiredSize);
      }

      [Test]
      public void EmptyRowDefinitionsFixedMeasuredSize()
      {
         Grid grid = new Grid();
         grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(100, GridUnitType.Pixel)));
         grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(10, GridUnitType.Pixel)));
         grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(100, GridUnitType.Pixel)));

         GridSplitter splitter = new GridSplitter();
         splitter.Width = 10;
         splitter.Background = Brushes.Gray;
         grid.Children.Add(splitter);
         Grid.SetColumn(splitter, 1);

         Rectangle rect = new Rectangle();
         rect.Fill = Brushes.Red;
         grid.Children.Add(rect);
         Grid.SetColumn(rect, 2);

         grid.Measure(new Size(210, 100));

         Assert.AreEqual(new Size(210, 0), grid.DesiredSize);
      }

      [Test]
      public void EmptyColumnDefinitionsFixedMeasuredSize()
      {
         Grid grid = new Grid();
         grid.RowDefinitions.Add(new RowDefinition(new GridLength(100, GridUnitType.Pixel)));
         grid.RowDefinitions.Add(new RowDefinition(new GridLength(10, GridUnitType.Pixel)));
         grid.RowDefinitions.Add(new RowDefinition(new GridLength(100, GridUnitType.Pixel)));

         GridSplitter splitter = new GridSplitter();
         splitter.Width = 10;
         splitter.Background = Brushes.Gray;
         grid.Children.Add(splitter);
         Grid.SetRow(splitter, 1);

         Rectangle rect = new Rectangle();
         rect.Fill = Brushes.Red;
         grid.Children.Add(rect);
         Grid.SetRow(rect, 2);

         grid.Measure(new Size(100, 210));

         Assert.AreEqual(new Size(10, 210), grid.DesiredSize);
      }

      [Test]
      public void StarRowMeasureInfinity()
      {
         Grid grid = new Grid();
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(100, GridUnitType.Pixel) });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(10, GridUnitType.Pixel) });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

         GridSplitter splitter = new GridSplitter();
         splitter.Width = 10;
         splitter.Background = Brushes.Gray;
         grid.Children.Add(splitter);
         Grid.SetRow(splitter, 1);

         Rectangle rect = new Rectangle();
         rect.Fill = Brushes.Red;
         grid.Children.Add(rect);
         Grid.SetRow(rect, 2);

         grid.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         Assert.AreEqual(new Size(10, 110), grid.DesiredSize);
      }

      [Test]
      public void StarRowMeasureInfinityFixedControlSize()
      {
         Grid grid = new Grid();
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(100, GridUnitType.Pixel) });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(10, GridUnitType.Pixel) });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

         GridSplitter splitter = new GridSplitter();
         splitter.Width = 10;
         splitter.Background = Brushes.Gray;
         grid.Children.Add(splitter);
         Grid.SetRow(splitter, 1);

         Rectangle rect = new Rectangle();
         rect.Width = 100;
         rect.Height = 50;
         rect.Fill = Brushes.Red;
         grid.Children.Add(rect);
         Grid.SetRow(rect, 2);

         grid.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         Assert.AreEqual(new Size(100, 160), grid.DesiredSize);
      }

      [Test]
      public void StarRowMeasureDefinedSizeFixedControlSize()
      {
         Grid grid = new Grid();
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(100, GridUnitType.Pixel) });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(10, GridUnitType.Pixel) });
         grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

         GridSplitter splitter = new GridSplitter();
         splitter.Width = 10;
         splitter.Background = Brushes.Gray;
         grid.Children.Add(splitter);
         Grid.SetRow(splitter, 1);

         Rectangle rect = new Rectangle();
         rect.Width = 100;
         rect.Height = 50;
         rect.Fill = Brushes.Red;
         grid.Children.Add(rect);
         Grid.SetRow(rect, 2);

         grid.Measure(new Size(50, 20));
         Assert.AreEqual(new Size(50, 20), grid.DesiredSize);
      }

      [Test]
      public void EmptyRowDefinitionsInfiniteMeasuredSize()
      {
         Grid grid = new Grid();
         grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(100, GridUnitType.Pixel)));
         grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(10, GridUnitType.Pixel)));
         grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(100, GridUnitType.Pixel)));

         GridSplitter splitter = new GridSplitter();
         splitter.Width = 10;
         splitter.Background = Brushes.Gray;
         grid.Children.Add(splitter);
         Grid.SetColumn(splitter, 1);

         Rectangle rect = new Rectangle();
         rect.Fill = Brushes.Red;
         grid.Children.Add(rect);
         Grid.SetColumn(rect, 2);

         grid.Measure(Size.Infinity);

         Assert.AreEqual(new Size(210, 0), grid.DesiredSize);
      }

      [Test]
      public void EmptyColumnDefinitionsInfiniteMeasuredSize()
      {
         Grid grid = new Grid();
         grid.RowDefinitions.Add(new RowDefinition(new GridLength(100, GridUnitType.Pixel)));
         grid.RowDefinitions.Add(new RowDefinition(new GridLength(10, GridUnitType.Pixel)));
         grid.RowDefinitions.Add(new RowDefinition(new GridLength(100, GridUnitType.Pixel)));

         GridSplitter splitter = new GridSplitter();
         splitter.Width = 10;
         splitter.Background = Brushes.Gray;
         grid.Children.Add(splitter);
         Grid.SetColumn(splitter, 1);

         Rectangle rect = new Rectangle();
         rect.Fill = Brushes.Red;
         grid.Children.Add(rect);
         Grid.SetColumn(rect, 2);

         grid.Measure(new Size(0, 210));
         grid.Arrange(new Rect(grid.DesiredSize));

         Assert.AreEqual(new Size(0, 210), grid.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_RowDefinition_Height_starSize_singleRow()
      {
         Grid g = new Grid();

         RowDefinition def;

         def = new RowDefinition();
         def.Height = new GridLength(2, GridUnitType.Star);
         g.RowDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(10, 10), g.DesiredSize);
      }

      [Test]
      public void ChildlessMargin_RowDefinition_Height_starSize_constSize_multiRow()
      {
         Grid g = new Grid();

         RowDefinition def;

         def = new RowDefinition();
         def.Height = new GridLength(2, GridUnitType.Star);
         g.RowDefinitions.Add(def);

         def = new RowDefinition();
         def.Height = new GridLength(200);
         g.RowDefinitions.Add(def);

         g.Margin = new Thickness(5);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(10, 210), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(10, 100), g.DesiredSize);
      }


      // 3 children, two columns, two rows.  the columns
      // are Auto sized, the rows are absolute (200 pixels
      // each).
      // 
      // +-------------------+
      // |                   |
      // |     child1        |
      // |                   |
      // +--------+----------+
      // |        |          |
      // | child2 |  child3  |
      // |        |          |
      // +--------+----------+
      //
      // child1 has colspan of 2
      // child2 and 3 are explicitly sized (width = 150 and 200, respectively)
      //
      [Test]
      public void ComplexLayout1()
      {
         Grid g = new Grid();

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(cdef);

         Canvas child1, child2, child3;
         ContentControl mc;

         // child1
         child1 = new Canvas();
         child1.Width = 200;
         child1.Height = 200;
         mc = new ContentControl {Content = child1};
         Grid.SetRow(mc, 0);
         Grid.SetColumn(mc, 0);
         Grid.SetColumnSpan(mc, 2);
         g.Children.Add(mc);

         // child2
         child2 = new Canvas();
         child2.Width = 150;
         child2.Height = 200;
         mc = new ContentControl {Content = child2};
         Grid.SetRow(mc, 0);
         Grid.SetColumn(mc, 0);
         g.Children.Add(mc);

         // child3
         child3 = new Canvas();
         child3.Width = 200;
         child3.Height = 200;
         mc = new ContentControl {Content = child3};
         Grid.SetRow(mc, 0);
         Grid.SetColumn(mc, 0);
         g.Children.Add(mc);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         //g.CheckMeasureArgs("#MeasureOverrideArg", new Size(inf, 200), new Size(inf, 200), new Size(inf, 200));
         //g.Reset();
         Assert.AreEqual(new Size(200, 400), g.DesiredSize);
      }

      [Test]
      public void ComplexLayout2()
      {
         Grid g = new Grid();

         RowDefinition rdef;
         ColumnDefinition cdef;

         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = new GridLength(200);
         g.ColumnDefinitions.Add(cdef);

         g.Margin = new Thickness(5);

         LayoutPoker c = new LayoutPoker();

         Grid.SetRow(c, 0);
         Grid.SetColumn(c, 0);

         g.Children.Add(c);

         c.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         // first test with the child sized larger than the row/column definitions
         c.Width = 400;
         c.Height = 400;

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(400, c.Width);
         Assert.AreEqual(400, c.Height);

         Assert.AreEqual(new Size(200, 200), c.DesiredSize);
         Assert.AreEqual(new Size(400, 400), c.MeasureArg);
         Assert.AreEqual(new Size(210, 210), g.DesiredSize);

         g.Measure(new Size(100, 100));

         Assert.AreEqual(new Size(100, 100), g.DesiredSize);
         Assert.AreEqual(new Size(400, 400), c.MeasureArg);
         Assert.AreEqual(new Size(200, 200), c.DesiredSize);

         // now test with the child sized smaller than the row/column definitions
         c.Width = 100;
         c.Height = 100;

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(100, 100), c.MeasureArg);
         Assert.AreEqual(new Size(210, 210), g.DesiredSize);
      }


      [Test]
      public void ArrangeTest()
      {
         Grid g = new Grid();

         RowDefinition rdef;
         ColumnDefinition cdef;

         rdef = new RowDefinition();
         rdef.Height = new GridLength(50);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = new GridLength(100);
         g.ColumnDefinitions.Add(cdef);

         g.Margin = new Thickness(5);

         var r = new Border();
         ContentControl mc = new ContentControl {Content = r};
         Grid.SetRow(mc, 0);
         Grid.SetColumn(mc, 0);

         g.Children.Add(mc);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         //g.CheckMeasureArgs("#MeasureOverrideArg", new Size(100, 50));
         //g.Reset();
         Assert.AreEqual(new Size(0, 0), new Size(r.ActualWidth, r.ActualHeight));
         Assert.AreEqual(new Size(0, 0), new Size(g.ActualWidth, g.ActualHeight));

         g.Arrange(new Rect(0, 0, g.DesiredSize.Width, g.DesiredSize.Height));
         //g.CheckRowHeights("#RowHeights", 50);
         Assert.AreEqual(new Size(0, 0), r.DesiredSize);
         Assert.AreEqual(new Size(110, 60), g.DesiredSize);

         //Assert.AreEqual(new Rect(0, 0, 100, 50).ToString(), LayoutInformation.GetLayoutSlot(r).ToString(), "slot");
         Assert.AreEqual(new Size(100, 50), new Size(r.ActualWidth, r.ActualHeight));
         Assert.AreEqual(new Size(100, 50), new Size(g.ActualWidth, g.ActualHeight));
      }

      [Test]
      public void ArrangeTest_TwoChildren()
      {
         Grid g = new Grid();

         RowDefinition rdef;
         ColumnDefinition cdef;

         rdef = new RowDefinition();
         rdef.Height = new GridLength(50);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = new GridLength(100);
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = new GridLength(20);
         g.ColumnDefinitions.Add(cdef);

         g.Margin = new Thickness(5);

         var ra = new Border();
         var rb = new Border();

         Grid.SetRow(ra, 0);
         Grid.SetColumn(ra, 0);

         Grid.SetRow(rb, 0);
         Grid.SetColumn(rb, 1);

         g.Children.Add(ra);
         g.Children.Add(rb);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(0, 0), ra.DesiredSize);
         Assert.AreEqual(new Size(0, 0), rb.DesiredSize);
         Assert.AreEqual(new Size(130, 60), g.DesiredSize);

         g.Arrange(new Rect(0, 0, g.DesiredSize.Width, g.DesiredSize.Height));

         Assert.AreEqual(new Size(0, 0), ra.DesiredSize);
         Assert.AreEqual(new Size(130, 60), g.DesiredSize);

         //Assert.AreEqual(new Rect(0, 0, 100, 50).ToString(), LayoutInformation.GetLayoutSlot(ra).ToString(), "slot");
         Assert.AreEqual(new Size(100, 50), new Size(ra.ActualWidth, ra.ActualHeight));
         Assert.AreEqual(new Size(20, 50), new Size(rb.ActualWidth, rb.ActualHeight));
         Assert.AreEqual(new Size(120, 50), new Size(g.ActualWidth, g.ActualHeight));
      }

      [Test]
      public void ArrangeDefaultDefinitions()
      {
         Grid grid = new Grid();

         Border b = new Border();
         b.Background = Brushes.Red;

         Border b2 = new Border();
         b2.Background = Brushes.Green;
         b2.Width = b2.Height = 50;

         grid.Children.Add(new ContentControl {Content = b});
         grid.Children.Add(new ContentControl {Content = b2});

         //grid.Measure(new Size(inf, inf));
         //grid.CheckMeasureArgs("#MeasureOverrideArg", new Size(inf, inf), new Size(inf, inf));
         //grid.Reset();

         grid.Measure(new Size(400, 300));
         //grid.CheckMeasureArgs("#MeasureOverrideArg 2", new Size(400, 300), new Size(400, 300));
         //grid.Reset();

         grid.Width = 100;
         grid.Height = 100;

         grid.Measure(new Size(400, 300));
         //grid.CheckMeasureArgs("#MeasureOverrideArg 3", new Size(100, 100), new Size(100, 100));
         //grid.Reset();
         grid.Arrange(new Rect(0, 0, grid.DesiredSize.Width, grid.DesiredSize.Height));

         Assert.AreEqual(new Size(100, 100), grid.RenderSize);
         Assert.AreEqual(new Size(100, 100), b.RenderSize);
         Assert.AreEqual(new Size(50, 50), b2.RenderSize);
      }

      [Test]
      public void DefaultDefinitions()
      {
         Grid grid = new Grid();

         grid.Children.Add(new Border());

         Assert.IsTrue(grid.ColumnDefinitions != null);
         Assert.IsTrue(grid.RowDefinitions != null);
         Assert.AreEqual(0, grid.ColumnDefinitions.Count);
         Assert.AreEqual(0, grid.RowDefinitions.Count);

         grid.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.IsTrue(grid.ColumnDefinitions != null);
         Assert.IsTrue(grid.RowDefinitions != null);
         Assert.AreEqual(0, grid.ColumnDefinitions.Count);
         Assert.AreEqual(0, grid.RowDefinitions.Count);

         grid.Arrange(new Rect(0, 0, grid.DesiredSize.Width, grid.DesiredSize.Height));

         Assert.IsTrue(grid.ColumnDefinitions != null);
         Assert.IsTrue(grid.RowDefinitions != null);
         Assert.AreEqual(0, grid.ColumnDefinitions.Count);
         Assert.AreEqual(0, grid.RowDefinitions.Count);
      }


      // 3 children, two columns, two rows.  the columns
      // are Auto sized, the rows are absolute (200 pixels
      // each).
      // 
      // +----------------------+
      // |                      |
      // |       child1         |
      // |                      |
      // +--------+--+----------+
      // |        |  |          |
      // | child2 |  |  child3  |
      // |        |  |          |
      // +--------+--+----------+
      //
      // child1 has colspan of 2
      // child2 and 3 are explicitly sized (width = 150 and 200, respectively)
      //
      [Test]
      public void IndividualColumnsSpacingTest()
      {
         Grid g = new Grid();
         g.IndividualColumnSpacing = true;

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Margin = 10;
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(cdef);

         Canvas child1, child2, child3;

         // child1
         child1 = new Canvas();
         child1.Width = 200;
         child1.Height = 200;
         Grid.SetRow(child1, 0);
         Grid.SetColumn(child1, 0);
         Grid.SetColumnSpan(child1, 2);
         g.Children.Add(child1);

         // child2
         child2 = new Canvas();
         child2.Width = 150;
         child2.Height = 200;
         Grid.SetRow(child2, 1);
         Grid.SetColumn(child2, 0);
         g.Children.Add(child2);

         // child3
         child3 = new Canvas();
         child3.Width = 200;
         child3.Height = 200;
         Grid.SetRow(child3, 1);
         Grid.SetColumn(child3, 1);
         g.Children.Add(child3);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         Assert.AreEqual(new Size(360, 400), g.DesiredSize);
      }

      [Test]
      public void IndividualRowSpacingTest()
      {
         Grid g = new Grid();
         g.IndividualColumnSpacing = true;
         g.IndividualRowSpacing = true;

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         rdef.Margin = 25;
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         rdef.Margin = 50;
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(0, GridUnitType.Auto);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Margin = 10;
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(cdef);

         Rectangle child1, child2, child3, child4;

         // child1
         child1 = new Rectangle();
         child1.Width = 200;
         child1.Height = 200;
         Grid.SetRow(child1, 0);
         Grid.SetColumn(child1, 0);
         Grid.SetColumnSpan(child1, 2);
         g.Children.Add(child1);

         // child2
         child2 = new Rectangle();
         child2.Width = 150;
         child2.Height = 200;
         Grid.SetRow(child2, 1);
         Grid.SetColumn(child2, 0);
         g.Children.Add(child2);

         // child3
         child3 = new Rectangle();
         child3.Width = 200;
         child3.Height = 200;
         Grid.SetRow(child3, 1);
         Grid.SetColumn(child3, 1);
         g.Children.Add(child3);

         // child4
         child4 = new Rectangle();
         child4.Width = 200;
         child4.Height = 300;
         Grid.SetRow(child4, 2);
         Grid.SetColumn(child4, 0);
         g.Children.Add(child4);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         Assert.AreEqual(new Size(410, 775), g.DesiredSize);
      }

      [Test]
      public void IndividualRowSpacingDisabledTest()
      {
         Grid g = new Grid();
         g.IndividualColumnSpacing = true;

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         rdef.Margin = 25;
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         rdef.Margin = 50;
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(0, GridUnitType.Auto);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Margin = 10;
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(cdef);

         Rectangle child1, child2, child3, child4;

         // child1
         child1 = new Rectangle();
         child1.Width = 200;
         child1.Height = 200;
         Grid.SetRow(child1, 0);
         Grid.SetColumn(child1, 0);
         Grid.SetColumnSpan(child1, 2);
         g.Children.Add(child1);

         // child2
         child2 = new Rectangle();
         child2.Width = 150;
         child2.Height = 200;
         Grid.SetRow(child2, 1);
         Grid.SetColumn(child2, 0);
         g.Children.Add(child2);

         // child3
         child3 = new Rectangle();
         child3.Width = 200;
         child3.Height = 200;
         Grid.SetRow(child3, 1);
         Grid.SetColumn(child3, 1);
         g.Children.Add(child3);

         // child4
         child4 = new Rectangle();
         child4.Width = 200;
         child4.Height = 300;
         Grid.SetRow(child4, 2);
         Grid.SetColumn(child4, 0);
         g.Children.Add(child4);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         Assert.AreEqual(new Size(410, 700), g.DesiredSize);
      }

      [Test]
      public void PaddingTest()
      {
         Grid g = new Grid();

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(0, GridUnitType.Auto);
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(0, GridUnitType.Auto);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Padding = new HalfThickness(10, 20);
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Padding = new HalfThickness(20, 20);
         g.ColumnDefinitions.Add(cdef);

         // child1
         var child1 = new Canvas();
         child1.Width = 200;
         child1.Height = 200;
         Grid.SetRow(child1, 0);
         Grid.SetColumn(child1, 0);
         Grid.SetColumnSpan(child1, 2);
         g.Children.Add(child1);

         // child2
         var child2 = new Canvas();
         child2.Width = 150;
         child2.Height = 200;
         Grid.SetRow(child2, 1);
         Grid.SetColumn(child2, 0);
         g.Children.Add(child2);

         // child3
         var child3 = new Canvas();
         child3.Width = 200;
         child3.Height = 200;
         Grid.SetRow(child3, 1);
         Grid.SetColumn(child3, 1);
         g.Children.Add(child3);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         Assert.AreEqual(new Size(420, 400), g.DesiredSize);
      }

      [Test]
      public void IndividualColumnsPaddingArrangeTest()
      {
         Grid g = new Grid();
         g.IndividualColumnSpacing = true;

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(0, GridUnitType.Auto);
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(0, GridUnitType.Auto);
         rdef.Padding = new HalfThickness(10, 20);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         //cdef.Margin = 10;
         
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Padding = new HalfThickness(20, 20);
         g.ColumnDefinitions.Add(cdef);

         // child1
         var child1 = new Canvas();
         child1.Width = 200;
         child1.Height = 200;
         child1.Background = Brushes.Red;
         Grid.SetRow(child1, 0);
         Grid.SetColumn(child1, 0);
         Grid.SetColumnSpan(child1, 2);
         g.Children.Add(child1);

         // child2
         var child2 = new Canvas();
         child2.Width = 150;
         child2.Height = 200;
         child2.Background = Brushes.Beige;
         Grid.SetRow(child2, 1);
         Grid.SetColumn(child2, 0);
         g.Children.Add(child2);

         // child3
         var child3 = new Canvas();
         child3.Width = 200;
         child3.Height = 200;
         child3.Background = Brushes.BlanchedAlmond;
         Grid.SetRow(child3, 1);
         Grid.SetColumn(child3, 1);
         g.Children.Add(child3);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         
         Assert.AreEqual(new Size(390, 430), g.DesiredSize);

         g.Arrange(new Rect(0, 0, g.DesiredSize.Width, g.DesiredSize.Height));

         Assert.AreEqual(new Rect(85, 0, 200, 200), child1.Bounds);
         Assert.AreEqual(new Rect(0, 210, 150, 200), child2.Bounds);
         Assert.AreEqual(new Rect(170, 210, 200, 200), child3.Bounds);

      }

      [Test]
      public void ColumnsSpacingPaddingArrangeTest()
      {
         Grid g = new Grid();
         g.ColumnSpacing = 20;

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(0, GridUnitType.Auto);
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(0, GridUnitType.Auto);
         rdef.Padding = new HalfThickness(10, 20);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;

         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Padding = new HalfThickness(20, 20);
         g.ColumnDefinitions.Add(cdef);

         // child1
         var child1 = new Canvas();
         child1.Width = 200;
         child1.Height = 200;
         child1.Background = Brushes.Red;
         Grid.SetRow(child1, 0);
         Grid.SetColumn(child1, 0);
         Grid.SetColumnSpan(child1, 2);
         g.Children.Add(child1);

         // child2
         var child2 = new Canvas();
         child2.Width = 150;
         child2.Height = 200;
         child2.Background = Brushes.Beige;
         Grid.SetRow(child2, 1);
         Grid.SetColumn(child2, 0);
         g.Children.Add(child2);

         // child3
         var child3 = new Canvas();
         child3.Width = 200;
         child3.Height = 200;
         child3.Background = Brushes.BlanchedAlmond;
         Grid.SetRow(child3, 1);
         Grid.SetColumn(child3, 1);
         g.Children.Add(child3);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));

         Assert.AreEqual(new Size(410, 430), g.DesiredSize);

         g.Arrange(new Rect(0, 0, g.DesiredSize.Width, g.DesiredSize.Height));

         Assert.AreEqual(new Rect(95, 0, 200, 200), child1.Bounds);
         Assert.AreEqual(new Rect(0, 210, 150, 200), child2.Bounds);
         Assert.AreEqual(new Rect(190, 210, 200, 200), child3.Bounds);

      }

      [Test]
      public void ColumnsSpacingTest()
      {
         Grid g = new Grid();
         g.ColumnSpacing = 50;

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Margin = 10;
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(cdef);

         Canvas child1, child2, child3;

         // child1
         child1 = new Canvas();
         child1.Width = 200;
         child1.Height = 200;
         Grid.SetRow(child1, 0);
         Grid.SetColumn(child1, 0);
         Grid.SetColumnSpan(child1, 2);
         g.Children.Add(child1);

         // child2
         child2 = new Canvas();
         child2.Width = 150;
         child2.Height = 200;
         Grid.SetRow(child2, 1);
         Grid.SetColumn(child2, 0);
         g.Children.Add(child2);

         // child3
         child3 = new Canvas();
         child3.Width = 200;
         child3.Height = 200;
         Grid.SetRow(child3, 1);
         Grid.SetColumn(child3, 1);
         g.Children.Add(child3);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         Assert.AreEqual(new Size(400, 400), g.DesiredSize);
      }

      [Test]
      public void RowsSpacingTest()
      {
         Grid g = new Grid();
         g.RowSpacing = 40;

         RowDefinition rdef;
         ColumnDefinition cdef;

         // Add rows
         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         rdef = new RowDefinition();
         rdef.Height = new GridLength(200);
         g.RowDefinitions.Add(rdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         cdef.Margin = 100;
         g.ColumnDefinitions.Add(cdef);

         cdef = new ColumnDefinition();
         cdef.Width = GridLength.Auto;
         g.ColumnDefinitions.Add(cdef);

         Canvas child1, child2, child3;

         // child1
         child1 = new Canvas();
         child1.Width = 200;
         child1.Height = 200;
         Grid.SetRow(child1, 0);
         Grid.SetColumn(child1, 0);
         Grid.SetColumnSpan(child1, 2);
         g.Children.Add(child1);

         // child2
         child2 = new Canvas();
         child2.Width = 150;
         child2.Height = 200;
         Grid.SetRow(child2, 1);
         Grid.SetColumn(child2, 0);
         g.Children.Add(child2);

         // child3
         child3 = new Canvas();
         child3.Width = 200;
         child3.Height = 200;
         Grid.SetRow(child3, 1);
         Grid.SetColumn(child3, 1);
         g.Children.Add(child3);

         g.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
         Assert.AreEqual(new Size(350, 440), g.DesiredSize);
      }
   }
}
