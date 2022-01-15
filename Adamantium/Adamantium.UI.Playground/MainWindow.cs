using System;
using Adamantium.Core.Collections;
using Adamantium.Engine.Compiler.Converter.AutoGenerated;
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
        private ArcSegment arcSegment;
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
            rectangle.StrokeDashOffset = 10;
            rectangle.ClipToBounds = false;
            rectangle.StartLineCap = PenLineCap.ConvexRound;
            rectangle.EndLineCap = PenLineCap.ConvexRound;
            rectangle.StrokeDashArray = new TrackingCollection<double>() { 0, 5 };

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
            ellipse.StrokeDashArray = new TrackingCollection<double>() { 12, 5 };

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
            polygon.StrokeThickness = 2;
            polygon.Stroke = Brushes.Black;
            polygon.StrokeDashArray = new TrackingCollection<double>() { 20, 10, 10, 10, 4, 20 };

            path = new Path();
            path.HorizontalAlignment = HorizontalAlignment.Center;
            path.VerticalAlignment = VerticalAlignment.Center;
            path.StrokeThickness = 0;
            path.Stroke = Brushes.CornflowerBlue;
            path.StrokeLineJoin = PenLineJoin.Miter;
            path.Fill = Brushes.Fuchsia;
            CombinedGeometry combinedGeometry = new CombinedGeometry();
            combinedGeometry.GeometryCombineMode = GeometryCombineMode.Union;
            var innerCombinedGeometry = new CombinedGeometry();
            innerCombinedGeometry.GeometryCombineMode = GeometryCombineMode.Exclude;
            /*innerCombinedGeometry.Geometry1 = new EllipseGeometry(new Vector2(100), 80, 80);
            innerCombinedGeometry.Geometry2 = new EllipseGeometry(new Vector2(100), 65, 65);*/
            
            innerCombinedGeometry.Geometry1 = new RectangleGeometry(new Rect(50, 50, 50, 50), new CornerRadius(0));
            innerCombinedGeometry.Geometry2 = new RectangleGeometry(new Rect(55, 55, 40, 40), new CornerRadius(0));

            var rectangleGeometry = new RectangleGeometry(new Rect(30, 93, 140, 15), new CornerRadius(0));
            rectangleGeometry.Transform = new Transform();
            rectangleGeometry.Transform.RotationAngle = 16;
            rectangleGeometry.Transform.RotationCenterX = 100;
            rectangleGeometry.Transform.RotationCenterY = 100;
            
            combinedGeometry.Geometry1 = innerCombinedGeometry;
            combinedGeometry.Geometry2 = rectangleGeometry;

            //innerCombinedGeometry.Geometry2 = rectangleGeometry;
            
            path.Data = innerCombinedGeometry;
            var pathGeometry = new PathGeometry();
            pathGeometry.FillRule = FillRule.NonZero;
            pathGeometry.IsClosed = false;
            var pathFigure = new PathFigure();
            pathFigure.StartPoint = new Vector2(200, 250);
            pathFigure.Segments = new PathSegmentCollection();
            // var segment = new PolylineSegment();
            // segment.Points = new PointsCollection();
            // segment.Points.Add(new Vector2(100, 10));
            // segment.Points.Add(new Vector2(200, 100));
            // pathFigure.Segments.Add(segment);
            var lineSegment = new LineSegment();
            lineSegment.Point = new Vector2(250, 250);
            arcSegment = new ArcSegment();
            arcSegment.Point = new Vector2(400, 270);
            arcSegment.Size = new Size(100, 50);
            arcSegment.RotationAngle = 46;
            arcSegment.IsLargeArc = true;
            arcSegment.SweepDirection = SweepDirection.Clockwise;
            
            var lineSegment2 = new LineSegment();
            lineSegment2.Point = new Vector2(1000, 270);
            
            pathFigure.Segments.Add(lineSegment);
            pathFigure.Segments.Add(arcSegment);
            pathFigure.Segments.Add(lineSegment2);

            // var bSplineSegment = new NurbsSegment();
            // bSplineSegment.Points = new PointsCollection();
            // bSplineSegment.Points.Add(new Vector2(150, 100));
            // bSplineSegment.Points.Add(new Vector2(250, 350));
            // bSplineSegment.Points.Add(new Vector2(370, 300));
            // bSplineSegment.Points.Add(new Vector2(490, 590));
            // bSplineSegment.Points.Add(new Vector2(510, 190));
            // bSplineSegment.IsUniform = false;
            // bSplineSegment.UseCustomDegree = true;
            // bSplineSegment.CustomDegree = 5;
            // pathFigure.Segments.Add(bSplineSegment);

            var cubic = new CubicBezierSegment();
            cubic.ControlPoint1 = new Vector2(400, 300);
            cubic.ControlPoint2 = new Vector2(800, 300);
            cubic.Point = new Vector2(800, 200);
            //pathFigure.Segments.Add(cubic);
            var quadratic = new QuadraticBezierSegment();
            quadratic.ControlPoint = new Vector2(820, 400);
            quadratic.Point = new Vector2(950, 400);
            //pathFigure.Segments.Add(quadratic);
            
            //segment.Points.Add(new Vector2(205, 50));
            pathGeometry.Figures = new PathFigureCollection();
            pathGeometry.Figures.Add(pathFigure);
            
            //path.Data = pathGeometry;
            
            KeyDown += OnKeyDown;

            var bspline = new BSpline();
            bspline.Points = new PointsCollection();
            bspline.Points.Add(new Vector2(10, 20));
            bspline.Points.Add(new Vector2(40, 0));
            bspline.Points.Add(new Vector2(50, 50));
            bspline.Points.Add(new Vector2(140, 100));
            bspline.Points.Add(new Vector2(40, 60));
            bspline.Points.Add(new Vector2(70, 5));
            bspline.Stroke = Brushes.Blue;
            bspline.StrokeThickness = 2;
            bspline.HorizontalAlignment = HorizontalAlignment.Center;
            bspline.VerticalAlignment = VerticalAlignment.Center;

            var border = new Border();
            border.CornerRadius = new CornerRadius(0, 10, 0, 10);
            border.BorderThickness = new Thickness(10, 5, 4, 0);
            border.Child = bspline;
            border.VerticalAlignment = VerticalAlignment.Center;
            border.HorizontalAlignment = HorizontalAlignment.Center;
            border.Background = Brushes.Crimson;
            border.BorderBrush = Brushes.CornflowerBlue;
            border.Width = 400;
            border.Height = 300;

            grid.Background = Brushes.White;
            // grid.Children.Add(rectangle);
            // grid.Children.Add(ellipse);
            // grid.Children.Add(line);
            // grid.Children.Add(polygon);
            grid.Children.Add(path);
            //grid.Children.Add(border);

            Content = grid;
        }

        private Polygon polygon;
        private Rectangle rectangle;
        private Ellipse ellipse;

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.RightArrow)
            {
                // var combined = path.Data as CombinedGeometry;
                // var mode = (int)combined.GeometryCombineMode;
                // if (mode == 3) mode = -1;
                //
                // mode++;
                // combined.GeometryCombineMode = (GeometryCombineMode)mode;
            }
            
            // if (e.Key == Key.LeftArrow)
            // {
            //     var combined = path.Data as CombinedGeometry;
            //     var mode = (int)combined.GeometryCombineMode;
            //     if (mode == 0) mode = 4;
            //
            //     mode--;
            //     combined.GeometryCombineMode = (GeometryCombineMode)mode;
            // }

            if (e.Key == Key.UpArrow)
            {
                // ++polygon.StrokeDashOffset;
                // Console.WriteLine($"OFFSET: {polygon.StrokeDashOffset}");
                arcSegment.RotationAngle++;
                //var geom = path.Data as PathGeometry;
                //var nurbs = geom.Figures[0].Segments[1] as ArcSegment;
                //nurbs.IsUniform = true;
                //nurbs.RotationAngle++;
                var com = path.Data as CombinedGeometry;
                com.Geometry2.Transform.RotationAngle++;
                //com.Geometry2.InvalidateGeometry();
                //path.Data.InvalidateGeometry();
                path.InvalidateMeasure();
            }

            if (e.Key == Key.DownArrow)
            {
                arcSegment.RotationAngle--;
                // var geom = path.Data as PathGeometry;
                // var nurbs = geom.Figures[0].Segments[1] as ArcSegment;
                // nurbs.RotationAngle--;
                //nurbs.IsUniform = false;
                
                var com = path.Data as CombinedGeometry;
                com.Geometry2.Transform.RotationAngle--;
                //com.Geometry2.InvalidateGeometry();
                //path.Data.InvalidateGeometry();
                path.InvalidateMeasure();
            }

            if (e.Key == Key.LeftArrow)
            {
                //polygon.FillRule = FillRule.EvenOdd;
                var geom = path.Data as PathGeometry;
                var nurbs = geom.Figures[0].Segments[0] as NurbsSegment;
                nurbs.CustomDegree--;
            }

            if (e.Key == Key.RightArrow)
            {
                polygon.FillRule = FillRule.NonZero;
                var geom = path.Data as PathGeometry;
                var nurbs = geom.Figures[0].Segments[0] as NurbsSegment;
                nurbs.CustomDegree++;
            }
        }
    }
}