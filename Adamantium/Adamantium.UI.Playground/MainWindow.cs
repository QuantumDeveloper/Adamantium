using System;
using Adamantium.Core.Collections;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.Media;
using Polygon = Adamantium.UI.Controls.Polygon;
using Rectangle = Adamantium.UI.Controls.Rectangle;

namespace Adamantium.UI.Playground
{
    public class MainWindow : Window
    {
        private Path path = null;
        public MainWindow()
        {
            Width = 1280;
            Height = 720;
            //MSAALevel = MSAALevel.X8;
            FillControls();
        }

        private void FillControls()
        {
            var grid = new Grid();
            
            rectangle = new Rectangle();
            rectangle.CornerRadius = new CornerRadius(0, 40, 0, 40);
            rectangle.Width = 200;
            rectangle.Height = 200;
            rectangle.HorizontalAlignment = HorizontalAlignment.Center;
            rectangle.Fill = Brushes.Chocolate;
            rectangle.Margin = new Thickness(0, 0, 1, 0);
            rectangle.Stroke = Brushes.CornflowerBlue;
            rectangle.StrokeThickness = 5;
            rectangle.StrokeDashOffset = -10;
            rectangle.ClipToBounds = false;
            //rectangle.StrokeDashArray = new TrackingCollection<double>() { 0, 5 };

            ellipse = new Ellipse();
            ellipse.Width = 150;
            ellipse.Height = 150;
            ellipse.Stretch = Stretch.UniformToFill;
            ellipse.HorizontalAlignment = HorizontalAlignment.Left;
            ellipse.VerticalAlignment = VerticalAlignment.Stretch;
            ellipse.Fill = Brushes.Crimson;
            ellipse.Margin = new Thickness(1, 0, 0, 1);
            ellipse.StrokeThickness = 5;
            ellipse.Stroke = Brushes.Green;
            //ellipse.StrokeDashArray = new TrackingCollection<double>() { 12, 5 };

            var line = new Line();
            line.X1 = 50;
            line.Y1 = 150;
            line.X2 = 250;
            line.Y2 = 150;
            line.StrokeThickness = 5;
            line.Width = 200;
            line.Height = 200;
            line.VerticalAlignment = VerticalAlignment.Center;
            line.HorizontalAlignment = HorizontalAlignment.Center;
            line.Stroke = Brushes.Black;
            line.StrokeThickness = 5;
            line.Fill = Brushes.Coral;

            polygon = new Polygon();
            polygon.Width = 200;
            polygon.Points = new TrackingCollection<Vector2>();
            polygon.Points.Add(new Vector2(10, 80));
            polygon.Points.Add(new Vector2(190, 80));
            polygon.Points.Add(new Vector2(30, 190));
            polygon.Points.Add(new Vector2(100, 10));
            polygon.Points.Add(new Vector2(170, 190));
            polygon.Fill = Brushes.Red;
            polygon.FillRule = FillRule.NonZero;
            polygon.HorizontalAlignment = HorizontalAlignment.Left;
            polygon.ClipToBounds = false;
            polygon.StrokeThickness = 2;
            polygon.Stroke = Brushes.Black;
            polygon.StrokeDashArray = new TrackingCollection<double>() { 20, 10, 10, 10, 4, 20 };

            path = new Path();
            path.HorizontalAlignment = HorizontalAlignment.Center;
            path.VerticalAlignment = VerticalAlignment.Center;
            CombinedGeometry geometry = new CombinedGeometry();
            geometry.GeometryCombineMode = GeometryCombineMode.Xor;
            geometry.Geometry1 = new RectangleGeometry(new Rect(0, 0, 450, 350), new CornerRadius(0));
            //geometry.Geometry2 = new RectangleGeometry(new Rect(100, 100, 500, 400), new CornerRadius(0));
            geometry.Geometry2 = new EllipseGeometry(new Rect(50, 50, 550, 350));
            path.Data = geometry;
            
            KeyDown += OnKeyDown;

            grid.Background = Brushes.White;
            grid.Children.Add(rectangle);
            grid.Children.Add(ellipse);
            //grid.Children.Add(line);
            grid.Children.Add(polygon);
            //grid.Children.Add(path);

            Content = grid;
        }

        private Polygon polygon;
        private Rectangle rectangle;
        private Ellipse ellipse;

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.RightArrow)
            {
                var combined = path.Data as CombinedGeometry;
                var mode = (int)combined.GeometryCombineMode;
                if (mode == 3) mode = -1;

                mode++;
                combined.GeometryCombineMode = (GeometryCombineMode)mode;
            }
            
            if (e.Key == Key.LeftArrow)
            {
                var combined = path.Data as CombinedGeometry;
                var mode = (int)combined.GeometryCombineMode;
                if (mode == 0) mode = 4;

                mode--;
                combined.GeometryCombineMode = (GeometryCombineMode)mode;
            }

            if (e.Key == Key.UpArrow)
            {
                ++polygon.StrokeDashOffset;
                Console.WriteLine($"OFFSET: {polygon.StrokeDashOffset}");
            }

            if (e.Key == Key.DownArrow)
            {
                --polygon.StrokeDashOffset;
                Console.WriteLine($"OFFSET: {polygon.StrokeDashOffset}");
            }

            path.InvalidateMeasure();
        }
    }
}