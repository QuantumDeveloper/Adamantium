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

      public void GenerateNoiseMap(int size, int seed, GraphicsDevice device, Mesh mesh, Vector4 modifiers)
      {
         PerlinNoise.SetSeed(seed);
         uint octaveCount = (uint)Math.Floor(Math.Log(size + 1, 2));

         for (int i = 0; i < mesh.Points.Length; ++i)
         {
            var noise = PerlinNoise.GetMultioctave3DNoiseValue(mesh.Points[i], modifiers, 0, octaveCount);

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

            mesh.Points[i] += (Vector3)mesh.Normals[i] * noise;
            mesh.Normals[i] = Vector3F.Normalize((Vector3F)mesh.Points[i]);
         }


      }

      public double GenerateDensity(Vector3 vertex)
      {
         double density = 0;
         density = -vertex.Y;
         
         /*Vector3F warp = new Vector3F();
         warp.X = warp.Y = warp.Z = Perlin3D.Get3DNoiseValue(vertex * 0.004f);
         vertex += warp * 8;*/
         
         //density += Perlin3D.GetMultioctave3DNoiseValue(vertex, Vector4F.One, 0, 3);
         
         density += PerlinNoise.Get3DNoiseValue(vertex * 4.03) * 0.25f;
         density += PerlinNoise.Get3DNoiseValue(vertex * 1.96) * 0.50f;
         density += PerlinNoise.Get3DNoiseValue(vertex * 1.01) * 1.00f;
         
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

      public List<Vector3> GenerateChunk(Vector3 chunkOrigin, float chunkSize, int voxelsInChunk)
      {
         var vertexList = new List<Vector3>();
         GridCell gridCell = new GridCell();
         
         float increment = chunkSize / voxelsInChunk;

         for (double x = chunkOrigin.X; x < (chunkOrigin.X + chunkSize); x += increment)
         {
            for (double y = chunkOrigin.Y; y < (chunkOrigin.Y + chunkSize); y += increment)
            {
               for (double z = chunkOrigin.Z; z < (chunkOrigin.Z + chunkSize); z += increment)
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

                  var triangleList = GenerateSurface(gridCell);
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

      public List<Vector3> GenerateBlockOfChunks(Vector3 blockOrigin, int blockSize, float chunkSize, int voxelsInChunk)
      {
         var blockVertexList = new List<Vector3>();

         for (double x = blockOrigin.X; x < (blockOrigin.X + blockSize); ++x)
         {
            for (double y = blockOrigin.Y; y < (blockOrigin.Y + blockSize); ++y)
            {
               for (double z = blockOrigin.Z; z < (blockOrigin.Z + blockSize); ++z)
               {
                  var chunkOrigin = new Vector3(x, y, z);
                  var chunkVertexList = GenerateChunk(chunkOrigin, chunkSize, voxelsInChunk);

                  foreach (var vertex in chunkVertexList)
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
