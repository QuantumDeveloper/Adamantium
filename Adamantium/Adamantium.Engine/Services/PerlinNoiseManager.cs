using System;
using System.Collections.Generic;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Noise;

namespace Adamantium.Engine.Services
{
   public class PerlinNoiseManager
   {
      private Randomizer randomizer;

      public PerlinNoiseManager()
      {
         randomizer = new Randomizer();
      }

      public void GenerateNoiseMap(int size, int seed, GraphicsDevice device, Mesh geometry, Vector4F modifiers)
      {
         PerlinNoise.SetSeed(seed);
         uint octaveCount = (uint)Math.Floor(Math.Log(size + 1, 2));

         for (int i = 0; i < geometry.Points.Length; ++i)
         {
            float noise = PerlinNoise.GetMultioctave3DNoiseValue(geometry.Points[i], modifiers, 0, octaveCount);

            if ((noise < randomizer.NextFloat(-0.5f, -0.7f)) || (noise > randomizer.NextFloat(0.5f, 0.7f)))
            {
               //noise *= 0.2f * (float)Math.Exp((Math.Abs(0.0f - noise)));
               noise *= 0.2f * (float)Math.Pow(1.5f, (Math.Abs(0.0f - noise)));
            }
            else
            {
               //noise *= 0.001f * (float)Math.Exp((Math.Abs(0.0f - noise)));
               noise *= 0.05f * (float)Math.Pow(1.5f, (Math.Abs(0.0f - noise)));
            }

            geometry.Points[i] += geometry.Normals[i] * noise;
            geometry.Normals[i] = Vector3F.Normalize(geometry.Points[i]);
         }


      }

      public float GenerateDensity(Vector3F vertex)
      {
         float density = 0;
         density = -vertex.Y;
         
         /*Vector3F warp = new Vector3F();
         warp.X = warp.Y = warp.Z = Perlin3D.Get3DNoiseValue(vertex * 0.004f);
         vertex += warp * 8;*/
         
         //density += Perlin3D.GetMultioctave3DNoiseValue(vertex, Vector4F.One, 0, 3);
         
         density += PerlinNoise.Get3DNoiseValue(vertex * 4.03f) * 0.25f;
         density += PerlinNoise.Get3DNoiseValue(vertex * 1.96f) * 0.50f;
         density += PerlinNoise.Get3DNoiseValue(vertex * 1.01f) * 1.00f;
         
         return density;
      }

      private void GenerateDensities(ref GridCell gridCell)
      {
         for (int i = 0; i < 8; ++i)
         {
            gridCell.Densities[i] = GenerateDensity(gridCell.Vertexes[i]);
         }
      }

      private List<GridTriangle> GenerateSurface(GridCell gridCell)
      {
         GenerateDensities(ref gridCell);

         return MarchingCubes.ProcessGridCell(gridCell, 0.5f);
      }

      public List<Vector3F> GenerateChunk(Vector3F chunkOrigin, float chunkSize, int voxelsInChunk)
      {
         List<GridTriangle> triangleList;
         List<Vector3F> vertexList = new List<Vector3F>();
         GridCell gridCell = new GridCell();
         
         float increment = chunkSize / voxelsInChunk;

         for (float x = chunkOrigin.X; x < (chunkOrigin.X + chunkSize); x += increment)
         {
            for (float y = chunkOrigin.Y; y < (chunkOrigin.Y + chunkSize); y += increment)
            {
               for (float z = chunkOrigin.Z; z < (chunkOrigin.Z + chunkSize); z += increment)
               {
                  gridCell.Vertexes[0].X = x;
                  gridCell.Vertexes[0].Y = y;
                  gridCell.Vertexes[0].Z = z + increment;

                  gridCell.Vertexes[1].X = x + increment;
                  gridCell.Vertexes[1].Y = y;
                  gridCell.Vertexes[1].Z = z + increment;

                  gridCell.Vertexes[2].X = x + increment;
                  gridCell.Vertexes[2].Y = y;
                  gridCell.Vertexes[2].Z = z;

                  gridCell.Vertexes[3].X = x;
                  gridCell.Vertexes[3].Y = y;
                  gridCell.Vertexes[3].Z = z;

                  gridCell.Vertexes[4].X = x;
                  gridCell.Vertexes[4].Y = y + increment;
                  gridCell.Vertexes[4].Z = z + increment;

                  gridCell.Vertexes[5].X = x + increment;
                  gridCell.Vertexes[5].Y = y + increment;
                  gridCell.Vertexes[5].Z = z + increment;

                  gridCell.Vertexes[6].X = x + increment;
                  gridCell.Vertexes[6].Y = y + increment;
                  gridCell.Vertexes[6].Z = z;

                  gridCell.Vertexes[7].X = x;
                  gridCell.Vertexes[7].Y = y + increment;
                  gridCell.Vertexes[7].Z = z;

                  triangleList = GenerateSurface(gridCell);
                  foreach (GridTriangle gridTriangle in triangleList)
                  {
                     vertexList.Add(gridTriangle.Vertexes[0]);
                     vertexList.Add(gridTriangle.Vertexes[1]);
                     vertexList.Add(gridTriangle.Vertexes[2]);
                  }
               }
            }
         }

         return vertexList;
      }

      public List<Vector3F> GenerateBlockOfChunks(Vector3F blockOrigin, int blockSize, float chunkSize, int voxelsInChunk)
      {
         List<Vector3F> blockVertexList = new List<Vector3F>();
         List<Vector3F> chunkVertexList;

         for (float x = blockOrigin.X; x < (blockOrigin.X + blockSize); ++x)
         {
            for (float y = blockOrigin.Y; y < (blockOrigin.Y + blockSize); ++y)
            {
               for (float z = blockOrigin.Z; z < (blockOrigin.Z + blockSize); ++z)
               {
                  Vector3F chunkOrigin = new Vector3F(x, y, z);
                  chunkVertexList = GenerateChunk(chunkOrigin, chunkSize, voxelsInChunk);

                  foreach (Vector3F vertex in chunkVertexList)
                  {
                     blockVertexList.Add(vertex);
                  }
               }
            }
         }

         return blockVertexList;
      }
   }
}
