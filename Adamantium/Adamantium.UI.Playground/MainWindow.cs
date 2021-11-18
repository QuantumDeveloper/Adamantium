using Adamantium.Core.Collections;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using Polygon = Adamantium.UI.Controls.Polygon;
using Rectangle = Adamantium.UI.Controls.Rectangle;

namespace Adamantium.UI.Playground
{
    public class MainWindow : Window
    {
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
            line.LineThickness = 4;
            line.Width = 500;
            line.Height = 350;
            line.VerticalAlignment = VerticalAlignment.Center;
            line.HorizontalAlignment = HorizontalAlignment.Center;
            line.Fill = Brushes.Coral;

            var polygon = new Polygon();
            polygon.Width = 100;
            polygon.Points = new TrackingCollection<Vector2D>();
            polygon.Points.Add(new Vector2D(10, 80));
            polygon.Points.Add(new Vector2D(190, 80));
            polygon.Points.Add(new Vector2D(30, 190));
            polygon.Points.Add(new Vector2D(100, 10));
            polygon.Points.Add(new Vector2D(170, 190));
            polygon.Fill = Brushes.Crimson;
            polygon.FillRule = FillRule.EvenOdd;
            polygon.HorizontalAlignment = HorizontalAlignment.Left;
            polygon.ClipToBounds = false;
            polygon.StrokeThickness = 4;
            polygon.Stroke = Brushes.Black;
                
            grid.Background = Brushes.White;
            grid.Children.Add(rectangle);
            grid.Children.Add(ellipse);
            grid.Children.Add(line);
            grid.Children.Add(polygon);

            Content = grid;
        }
    }
}