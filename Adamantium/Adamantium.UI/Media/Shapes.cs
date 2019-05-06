using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Point = Adamantium.Mathematics.Point;

namespace Adamantium.UI.Media
{
   public static class Shapes
   {
      /// <summary>
      /// Create array of points representing filled 2D circle with texture coordinates
      /// </summary>
      /// <param name="center">center of the circle</param>
      /// <param name="radius">circle radius</param>
      /// <param name="color">color of the circle</param>
      /// <returns>array of type <see cref="VertexPositionColor"/></returns>
      /// <remarks> To draw it, use TriangleStrip primitive type</remarks>
      public static Shape FilledEllipse(Point center, Size radius, bool needOptimization = true)
      {
         Shape circle = new Shape();

         double x1 = center.X + (radius.Width * Math.Cos(0));
         double y1 = center.Y + (radius.Height * Math.Sin(0));

         int n = 360;
         for (int i = 0; i <= n; i += 3)
         {
            //Get next agnle (i+1) because if we eill get angle based on current i. we will get x1, y1 and x2, y2 the same
            //and in current situation we will always get diffrent positions for x1 y1 and x2 y2
            double angle = (float)(i+1) / n * 2 * Math.PI;

            double x2 = center.X + (radius.Width * (float)Math.Cos(angle));
            double y2 = center.Y + (radius.Height*(float) Math.Sin(angle));
            
            var vertex1 = new VertexPositionTexture(new Vector3D(center.X, center.Y, 0), Vector2F.Zero);
            
            var vertex2 = new VertexPositionTexture(new Vector3D(x1, y1, 0), Vector2F.Zero);
            
            var vertex3 = new VertexPositionTexture(new Vector3D(x2, y2, 0), Vector2F.Zero);

            vertex1.UV = new Vector2D(0.5f + (vertex1.Position.X - center.X) / (2 * radius.Width),
               0.5f + (vertex1.Position.Y - center.Y) / (2 * radius.Height));
            vertex2.UV = new Vector2D(0.5f + (vertex2.Position.X - center.X) / (2 * radius.Width),
               0.5f + (vertex2.Position.Y - center.Y) / (2 * radius.Height));
            vertex3.UV = new Vector2D(0.5f + (vertex3.Position.X - center.X) / (2 * radius.Width),
               0.5f + (vertex3.Position.Y - center.Y) / (2 * radius.Height));

            circle.VertexArray.Add(vertex1);
            circle.VertexArray.Add(vertex2);
            circle.VertexArray.Add(vertex3);

            x1 = x2;
            y1 = y2;
         }


         circle = OptimizeShape(circle, needOptimization);

         return circle;
      }

      public static Shape FilledEllipseSector(Point center, Size radius, float startAngle, float endAngle, bool needOptimization = true)
      {
         Shape circle = new Shape();
         
         double x1 = center.X + (radius.Width * Math.Cos(startAngle));
         double y1 = center.Y + (radius.Height * Math.Sin(startAngle));

         for (float i = startAngle; i < endAngle; i++)
         {
            float angle = (float)Math.PI * 2 / 360 * (i+1);

            double x2 = center.X + (radius.Width * Math.Cos(angle));
            double y2 = center.Y + (radius.Height * Math.Sin(angle));

            var vertex1 = new VertexPositionTexture(new Vector3D(center.X, center.Y, 0), Vector2F.Zero);
            var vertex2 = new VertexPositionTexture(new Vector3D(x1, y1, 0), Vector2F.Zero);
            var vertex3 = new VertexPositionTexture(new Vector3D(x2, y2, 0), Vector2F.Zero);

            vertex1.UV = new Vector2D(0.5f + (vertex1.Position.X - center.X) / (2 * radius.Width),
               0.5f + (vertex1.Position.Y - center.Y) / (2 * radius.Height));
            vertex2.UV = new Vector2D(0.5f + (vertex2.Position.X - center.X) / (2 * radius.Width),
               0.5f + (vertex2.Position.Y - center.Y) / (2 * radius.Height));
            vertex3.UV = new Vector2D(0.5f + (vertex3.Position.X - center.X) / (2 * radius.Width),
               0.5f + (vertex3.Position.Y - center.Y) / (2 * radius.Height));

            circle.VertexArray.Add(vertex1);
            circle.VertexArray.Add(vertex2);
            circle.VertexArray.Add(vertex3);

            y1 = y2;
            x1 = x2;
         }

         circle = OptimizeShape(circle, needOptimization);

         return circle;
      }

      /// <summary>
      /// Create array of points representing an empty 2D circle with stroke thickness
      /// </summary>
      /// <param name="center">center of the circle</param>
      /// <param name="radius">circle radius</param>
      /// <param name="strokeThickness">circle strock thickness</param>
      /// <param name="strockeColor">stroke color</param>
      /// <returns>array of type <see cref="VertexPositionColor"/></returns>
      /// <remarks> To draw it, use TriangleStrip primitive type</remarks>
      public static Shape EmptyCircle(Point center, Size radius, float strokeThickness,
         ColorRGBA strockeColor, bool needOptimization = true)
      {
         Shape circle = new Shape();
         circle.PrimitiveType = PrimitiveType.TriangleStrip;
         int n = 360;
         //draw full circle
         for (int i = 0; i <= n; i++)
         {
            double angle = (float)i / n * 2 * Math.PI;

            double x = center.X + ((radius.Width - strokeThickness) * Math.Cos(angle));
            double y = center.Y + ((radius.Height - strokeThickness) * Math.Sin(angle));
            circle.VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));

            x = center.X + ((radius.Width) * Math.Cos(angle));
            y = center.Y + ((radius.Height) * Math.Sin(angle));
            circle.VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));
         }

         circle = OptimizeShape(circle, needOptimization);

         return circle;
      }


      /// <summary>
      /// Create array of points representing an empty 2D circle sector with stroke thickness
      /// </summary>
      /// <param name="center">center of the circle</param>
      /// <param name="radius">circle radius</param>
      /// <param name="startAngle">start angle in degrees</param>
      /// <param name="endAngle">end angle in degrees</param>
      /// <param name="strokeThickness">circle strock thickness</param>
      /// <param name="needOptimization"></param>
      /// <returns>array of type <see cref="VertexPositionColor"/></returns>
      /// <remarks> To draw it, use TriangleStrip primitive type</remarks>
      public static Shape EmptyCircleSector(Point center, Size radius, float startAngle, float endAngle, float strokeThickness, bool needOptimization = true)
      {
         Shape circle = new Shape();
         circle.PrimitiveType = PrimitiveType.TriangleStrip;
         float step = 1f;
         //draw sector of the circle
         if (startAngle < endAngle)
         {
            for (float i = startAngle; i <= endAngle; i += step)
            {
               double angle = (float) Math.PI*2/360*i;

               double x = center.X + ((radius.Width - strokeThickness)* Math.Cos(angle));
               double y = center.Y + ((radius.Height - strokeThickness)* Math.Sin(angle));
               circle.VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));

               x = center.X + ((radius.Width)*(float) Math.Cos(angle));
               y = center.Y + ((radius.Height)*(float) Math.Sin(angle));
               circle.VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));
            }
         }
         else
         {
            for (float i = startAngle; i >= endAngle; i -= step)
            {
               double angle = (float)Math.PI * 2 / 360 * i;

               double x = center.X + ((radius.Width - strokeThickness) * Math.Cos(angle));
               double y = center.Y + ((radius.Height - strokeThickness) * Math.Sin(angle));
               circle.VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));

               x = center.X + ((radius.Width) * (float)Math.Cos(angle));
               y = center.Y + ((radius.Height) * (float)Math.Sin(angle));
               circle.VertexArray.Add(new VertexPositionTexture(new Vector3D(x, y, 0), new Vector2D()));
            }
         }

         circle = OptimizeShape(circle, needOptimization);

         return circle;
      }

      public static Shape OptimizeShape(Shape shape, bool needOptimization = true)
      {
         if (needOptimization)
         {
            Dictionary<VertexPositionTexture, Int32> dict = new Dictionary<VertexPositionTexture, Int32>();
            List<VertexPositionTexture> tmp = new List<VertexPositionTexture>();
            int decrement = 0;
            //переменная decrement нужна для того, чтобы хранить сколько раз уже встречались одни и те же значения в вершинном буфере
            //В итоге при записи в индексный массив это значение отнимается от текущего индекса цикла.
            //Таким образом максимльное значение в индексном массиве не будет превышать количество вершин в вершинном массиве
            for (int i = 0; i < shape.VertexArray.Count; i++)
            {
               if (dict.ContainsKey(shape.VertexArray[i]))
               {
                  shape.IndexArray.Add(dict[shape.VertexArray[i]]);
                  decrement++;
               }
               else
               {
                  dict.Add(shape.VertexArray[i], i - decrement);
                  tmp.Add(shape.VertexArray[i]);
                  shape.IndexArray.Add(i - decrement);
               }
            }
            shape.VertexArray = tmp;
         }
         else
         {
            for (int i = 0; i < shape.VertexArray.Count; i++)
            {
               shape.IndexArray.Add(i);
            }
         }
         return shape;
      }

   }
}
