using System;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media.Imaging;
//using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.UI.Media
{
   public class PresentationItem:DisposableObject
   {
      public Brush RenderBrush { get; set; }
      public Brush StrokeBrush { get; set; }

      //public Buffer<VertexPositionTexture> RenderGeometry { get; set; }
      //public Buffer<VertexPositionTexture> StrokeGeometry { get; set; }

      //public Buffer<Int32> RenderGeometryIndices { get; set; }
      //public Buffer<Int32> StrokeGeometryIndices { get; set; }

      public PrimitiveType RenderGeometryType { get; }
      public PrimitiveType StrokeGeometryType { get; }

      public bool HasTexture { get; private set; }

      public Texture Texture;

      public PresentationItem(DrawingContext context, BitmapSource bitmapSource, Geometry renderGeometry, Geometry strokeGeometry, Brush renderBrush, Brush strokeBrush)
      {
         if (bitmapSource?.DXTexture != null)
         {
            Texture = bitmapSource.DXTexture;
            HasTexture = true;
         }
         //if (!renderGeometry.IsEmpty())
         //{
         //   RenderGeometry =
         //      ToDispose(Buffer.Vertex.New(context.D3DGraphicsDevice, renderGeometry.VertexArray.ToArray()));
         //   RenderGeometryIndices = ToDispose(Buffer.Index.New(context.D3DGraphicsDevice, renderGeometry.IndicesArray.ToArray()));
         //   RenderGeometryType = renderGeometry.PrimitiveType;
         //}
         //if (strokeGeometry!= null && !strokeGeometry.IsEmpty())
         //{
         //   StrokeGeometry =
         //      ToDispose(Buffer.Vertex.New(context.D3DGraphicsDevice, strokeGeometry.VertexArray.ToArray()));
         //   StrokeGeometryIndices = ToDispose(Buffer.Index.New(context.D3DGraphicsDevice, strokeGeometry.IndicesArray.ToArray()));
         //   StrokeGeometryType = strokeGeometry.PrimitiveType;
         //}

         RenderBrush = renderBrush;
         StrokeBrush = strokeBrush;
      }
   }
}
