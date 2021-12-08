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
            
            var rectangle = new Rectangle();
            rectangle.CornerRadius = new CornerRadius(0, 40, 0, 40);
            rectangle.Width = 200;
            rectangle.Height = 550;
            rectangle.HorizontalAlignment = HorizontalAlignment.Right;
            rectangle.Fill = Brushes.Chocolate;
            rectangle.Margin = new Thickness(0, 0, 1, 0);
            rectangle.Stroke = Brushes.CornflowerBlue;
            rectangle.StrokeThickness = 5;
            rectangle.ClipToBounds = false;
            rectangle.StrokeDashArray = new TrackingCollection<double>() { 10, 5 };

            var ellipse = new Ellipse();
            ellipse.Width = 150;
            ellipse.Height = 150;
            ellipse.Stretch = Stretch.UniformToFill;
            ellipse.HorizontalAlignment = HorizontalAlignment.Left;
            ellipse.VerticalAlignment = VerticalAlignment.Stretch;
            ellipse.Fill = Brushes.Crimson;
            ellipse.Margin = new Thickness(1, 0, 0, 1);
            ellipse.StrokeThickness = 5;
            ellipse.Stroke = Brushes.Green;

            var line = new Line();
            line.X1 = 100;
            line.Y1 = 20;
            line.X2 = 500;
            line.Y2 = 300;
            line.StrokeThickness = 4;
            line.Width = 500;
            line.Height = 350;
            line.VerticalAlignment = VerticalAlignment.Center;
            line.HorizontalAlignment = HorizontalAlignment.Center;
            line.Fill = Brushes.Coral;

            var polygon = new Polygon();
            polygon.Width = 100;
            polygon.Points = new TrackingCollection<Vector2>();
            polygon.Points.Add(new Vector2(10, 80));
            polygon.Points.Add(new Vector2(190, 80));
            polygon.Points.Add(new Vector2(30, 190));
            polygon.Points.Add(new Vector2(100, 10));
            polygon.Points.Add(new Vector2(170, 190));
            polygon.Fill = Brushes.Crimson;
            polygon.FillRule = FillRule.EvenOdd;
            polygon.HorizontalAlignment = HorizontalAlignment.Left;
            polygon.ClipToBounds = false;
            polygon.StrokeThickness = 20;
            polygon.Stroke = Brushes.Black;
            //polygon.StrokeDashArray = new TrackingCollection<double>() { 25, 15, 5 };

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
            //grid.Children.Add(rectangle);
            //grid.Children.Add(ellipse);
            //grid.Children.Add(line);
            //grid.Children.Add(polygon);
            grid.Children.Add(path);

            Content = grid;
        }

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
            
            path.InvalidateMeasure();
        }
    }
}