using Adamantium.Core.Collections;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using Polygon = Adamantium.UI.Controls.Polygon;
using Shape = Adamantium.UI.Controls.Shape;

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
            
            var polygon = new Polygon();
            polygon.Width = 200;
            polygon.Points = new PointsCollection();
            polygon.Points.Add(new Vector2(10, 80));
            polygon.Points.Add(new Vector2(190, 80));
            polygon.Points.Add(new Vector2(30, 190));
            polygon.Points.Add(new Vector2(100, 10));
            polygon.Points.Add(new Vector2(170, 190));
            polygon.Fill = Brushes.Red;
            polygon.FillRule = FillRule.NonZero;
            polygon.HorizontalAlignment = HorizontalAlignment.Left;
            polygon.ClipToBounds = false;
            polygon.StrokeThickness = 0;
            polygon.Stroke = Brushes.Black;
            polygon.StrokeDashArray = new TrackingCollection<double>() { 20, 10, 10, 10, 4, 20 };

            var border = new Border();
            border.Width = 250;
            border.Height = 150;
            //border.CornerRadius = new CornerRadius(0, 15, 20, 5);
            border.CornerRadius = new CornerRadius(0, 0, 0, 0);
            border.BorderThickness = new Thickness(4, 2, 0, 1);
            border.BorderBrush = Brushes.Blue;
            border.Background = Brushes.Gray;

            var baseStyle = new Style(typeof(Path));
            baseStyle.Setters.Add(new PropertySetter(Shape.FillProperty, Brushes.Crimson));

            var style = new Style(typeof(Path), baseStyle);
            style.Setters.Add(new PropertySetter(Shape.FillProperty, Brushes.Green));

            path = new Path();
            path.Style = style;
            path.HorizontalAlignment = HorizontalAlignment.Center;
            path.VerticalAlignment = VerticalAlignment.Center;
            path.StrokeThickness = 2;
            path.Stroke = Brushes.CornflowerBlue;
            path.StrokeLineJoin = PenLineJoin.Miter;
            CombinedGeometry combinedGeometry = new CombinedGeometry();
            combinedGeometry.GeometryCombineMode = GeometryCombineMode.Xor;
            var innerCombinedGeometry = new CombinedGeometry();
            innerCombinedGeometry.GeometryCombineMode = GeometryCombineMode.Exclude;
            innerCombinedGeometry.Geometry1 = new EllipseGeometry(new Vector2(100), 80, 80);
            innerCombinedGeometry.Geometry2 = new EllipseGeometry(new Vector2(100), 65, 65);
            //innerCombinedGeometry.Geometry1 = new RectangleGeometry(new Rect(0, 0, 50, 50));
            //innerCombinedGeometry.Geometry2 = new RectangleGeometry(new Rect(100, 0, 50, 50));
            
            var innerCombinedGeometry2 = new CombinedGeometry();
            innerCombinedGeometry2.GeometryCombineMode = GeometryCombineMode.Exclude;
            innerCombinedGeometry2.Geometry1 = new EllipseGeometry(new Vector2(200, 100), 80, 80);
            innerCombinedGeometry2.Geometry2 = new EllipseGeometry(new Vector2(200, 100), 65, 65);

            var rectangleGeometry = new RectangleGeometry(new Rect(30, 93, 140, 15));
            rectangleGeometry.Transform = new Transform();    
            rectangleGeometry.Transform.RotationAngle = 135;    
            rectangleGeometry.Transform.RotationCenterX = 100;
            rectangleGeometry.Transform.RotationCenterY = 100;
            
            var streamGeometry = new StreamGeometry();
            streamGeometry.FillRule = FillRule.NonZero;
            var context = streamGeometry.Open();
            context.BeginFigure(new Vector2(30, 78), true, true).
                LineTo(130, 78).
                LineTo(115, 93).
                LineTo(170, 93).
                LineTo(170, 108).
                LineTo(90, 108).
                LineTo(105, 93).
                LineTo(30, 93);
            
            streamGeometry.Transform = new Transform();
            streamGeometry.Transform.RotationAngle = 135;
            streamGeometry.Transform.RotationCenterX = 100;
            streamGeometry.Transform.RotationCenterY = 100;
            

            combinedGeometry.Geometry1 = innerCombinedGeometry;
            combinedGeometry.Geometry2 = innerCombinedGeometry2;
            //combinedGeometry.Geometry2 = streamGeometry;

            
            rectangleGeometry = new RectangleGeometry(new Rect(50, 50, 50, 50));
            CombinedGeometry combinedGeometry2 = new CombinedGeometry();
            combinedGeometry2.GeometryCombineMode = GeometryCombineMode.Union;
            combinedGeometry2.Geometry1 = combinedGeometry;
            combinedGeometry2.Geometry2 = rectangleGeometry;
            
            path.Data = combinedGeometry;
            //path.Data = polygonGeometry;
            
            //var xamlIcon = "M3,4H17V8H20L23,12V17H21A3,3 0 0,1 18,20A3,3 0 0,1 15,17H9A3,3 0 0,1 6,20A3,3 0 0,1 3,17H1V6C1,4.89 1.9,4 3,4M17,9.5V12H21.47L19.5,9.5H17M6,15.5A1.5,1.5 0 0,0 4.5,17A1.5,1.5 0 0,0 6,18.5A1.5,1.5 0 0,0 7.5,17A1.5,1.5 0 0,0 6,15.5M18,15.5A1.5,1.5 0 0,0 16.5,17A1.5,1.5 0 0,0 18,18.5A1.5,1.5 0 0,0 19.5,17A1.5,1.5 0 0,0 18,15.5M8,14L14,8L12.59,6.58L8,11.17L5.91,9.08L4.5,10.5L8,14Z";
            //var xamlIcon = "M14.12,10H19V8.2H15.38L13.38,4.87C13.08,4.37 12.54,4.03 11.92,4.03C11.74,4.03 11.58,4.06 11.42,4.11L6,5.8V11H7.8V7.33L9.91,6.67L6,22H7.8L10.67,13.89L13,17V22H14.8V15.59L12.31,11.05L13.04,8.18M14,3.8C15,3.8 15.8,3 15.8,2C15.8,1 15,0.2 14,0.2C13,0.2 12.2,1 12.2,2C12.2,3 13,3.8 14,3.8Z";
            var xamlIcon = "M9,12C9,11.19 9.3,10.5 9.89,9.89C10.5,9.3 11.19,9 12,9C12.81,9 13.5,9.3 14.11,9.89C14.7,10.5 15,11.19 15,12C15,12.81 14.7,13.5 14.11,14.11C13.5,14.7 12.81,15 12,15C11.19,15 10.5,14.7 9.89,14.11C9.3,13.5 9,12.81 9,12M5.53,8.44L7.31,10.22L5.53,12L7.31,13.78L5.53,15.56L2,12L5.53,8.44M8.44,18.47L10.22,16.69L12,18.47L13.78,16.69L15.56,18.47L12,22L8.44,18.47M18.47,15.56L16.69,13.78L18.47,12L16.69,10.22L18.47,8.44L22,12L18.47,15.56M15.56,5.53L13.78,7.31L12,5.53L10.22,7.31L8.44,5.53L12,2L15.56,5.53Z";
            //path.Data = Geometry.Parse(xamlIcon);
            
            //path.Data.Transform = new Transform();
            //path.Data.Transform.ScaleX = 5;
            //path.Data.Transform.ScaleY = 5;
            
            path.Stroke = Brushes.Crimson;
            //path.StrokeThickness = 4;
            //path.StrokeDashArray = new TrackingCollection<double>() { 5 };

            grid.Background = Brushes.White;
            //grid.Children.Add(polygon);
            //grid.Children.Add(border);
            grid.Children.Add(path);

            Content = grid;
        }
    }
}