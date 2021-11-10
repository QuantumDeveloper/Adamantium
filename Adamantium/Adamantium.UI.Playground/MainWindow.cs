using Adamantium.UI.Controls;
using Adamantium.UI.Media;

namespace Adamantium.UI.Playground
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            Width = 1280;
            Height = 720;
            FillControls();
        }

        private void FillControls()
        {
            var grid = new Grid();
            var rectangle = new Rectangle();
            rectangle.CornerRadius = new CornerRadius(0, 10, 0, 10);
            rectangle.Width = 200;
            rectangle.Height = 50;
            rectangle.HorizontalAlignment = HorizontalAlignment.Right;
            rectangle.Fill = Brushes.Chocolate;
            rectangle.Margin = new Thickness(0, 0, 1, 0);

            var ellipse = new Ellipse();
            ellipse.Width = 150;
            ellipse.Height = 150;
            ellipse.HorizontalAlignment = HorizontalAlignment.Left;
            ellipse.VerticalAlignment = VerticalAlignment.Bottom;
            ellipse.Fill = Brushes.Crimson;
            ellipse.Margin = new Thickness(1, 0, 0, 1);

            var line = new Line();
            line.X1 = 100;
            line.Y1 = 20;
            line.X2 = 500;
            line.Y2 = 300;
            line.LineThickness = 5;
            line.Width = 500;
            line.Height = 350;
            line.VerticalAlignment = VerticalAlignment.Center;
            line.HorizontalAlignment = HorizontalAlignment.Center;
            line.Fill = Brushes.Indigo;
            
            grid.Background = Brushes.White;
            grid.Children.Add(rectangle);
            grid.Children.Add(ellipse);
            grid.Children.Add(line);

            Content = grid;
        }
    }
}