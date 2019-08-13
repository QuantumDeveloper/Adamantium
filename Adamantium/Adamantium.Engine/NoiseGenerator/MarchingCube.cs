using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Engine.Core;
//using Adamantium.Engine.Core.Effects;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Mathematics;
using Noise;
//using Buffer = Adamantium.Engine.Graphics.Buffer;
//using Texture2D = Adamantium.Engine.Graphics.Texture2D;

namespace Adamantium.Engine.NoiseGenerator
{
   public class MarchingCube:DisposableObject
   {
      public readonly Vector3F cubeStep;
      private PerlinNoiseManager manager;
      //public Texture2D lookupTexture;
      private GraphicsDevice graphicsDevice;
      private EntityWorld entityWorld;
      //private Buffer<Vector3F> vertex;
//      private Effect MarchingCubesEffect;
//      private VertexInputLayout MClayout;
      private Vector3F blockOrigin = Vector3F.Zero;
      public Vector3F[] decals;
      private OpenSimplexNoise noise;

      public MarchingCube(GraphicsDevice device, EntityWorld entityWorld)
      {
//         graphicsDevice = device.MainDevice;
//         this.entityWorld = entityWorld;
//         PerlinNoise.SetSeed(455);
//         noise = new OpenSimplexNoise(455);
//         MClayout = VertexInputLayout.New<VertexPosition>(0);
//         CreateLookUpTexture2D();
//         var result = EffectData.Load(@"Content\Effects\TerrainGenShaders\MarchingCubes.fx.compiled");
//         MarchingCubesEffect = new Effect(graphicsDevice, result);
      }


      private void FillDecals(float voxelSize)
      {
         decals = new Vector3F[8];
         //decals[0] = new Vector3F(0, 0, 0);
         //decals[1] = new Vector3F(voxelSize, 0, 0);
         //decals[2] = new Vector3F(voxelSize, voxelSize, 0);
         //decals[3] = new Vector3F(0, voxelSize, 0);
         //decals[4] = new Vector3F(0, 0, voxelSize);
         //decals[5] = new Vector3F(voxelSize, 0, voxelSize);
         //decals[6] = new Vector3F(voxelSize, voxelSize, voxelSize);
         //decals[7] = new Vector3F(0, voxelSize, voxelSize);

         decals[0] = new Vector3F(0, 0, voxelSize);
         decals[1] = new Vector3F(voxelSize, 0, voxelSize);
         decals[2] = new Vector3F(voxelSize, 0, 0);
         decals[3] = new Vector3F(0, 0, 0);
         decals[4] = new Vector3F(0, voxelSize, voxelSize);
         decals[5] = new Vector3F(voxelSize, voxelSize, voxelSize);
         decals[6] = new Vector3F(voxelSize, voxelSize, 0);
         decals[7] = new Vector3F(0, voxelSize, 0);
      }

      private void CreateVertexGrid(float chunkSize, int voxelsInChunk)
      {
         var voxelSize = chunkSize / voxelsInChunk;
         Stopwatch time = Stopwatch.StartNew();
         List<Vector3F> positions = new List<Vector3F>();
         for (float x = blockOrigin.X; x < chunkSize; x += voxelSize)
         {
            for (float y = blockOrigin.Y; y < chunkSize; y += voxelSize)
            {
               for (float z = blockOrigin.Z; z < chunkSize; z += voxelSize)
               {
                  {
                     positions.Add(new Vector3F(x, y, z));
                  }
               }
            }
         }


         time.Stop();
         var elapsed = time.ElapsedMilliseconds;
//         if (vertex != null)
//         {
//            RemoveAndDispose(ref vertex);
//         }
//         vertex = ToDispose(Buffer.Vertex.New(graphicsDevice, positions.ToArray(), ResourceUsage.Dynamic));
      }

      private void CreateLookUpTexture2D()
      {
         GCHandle handle = GCHandle.Alloc(MarchingCubes.TrianglesTable, GCHandleType.Pinned);
         try
         {
            var ptr = handle.AddrOfPinnedObject();
            DataBox box = new DataBox(ptr, 16* sizeof(int), 0);
            //lookupTexture = ToDispose(Texture2D.New(graphicsDevice, box,16, 256, SurfaceFormat.R32.SInt));
         }
         finally
         {
            handle.Free();
         }
         
      }

      public void Draw(float chunkSize, int voxelsInChunk, int technique, int blockCount)
      {
         CreateVertexGrid(chunkSize, voxelsInChunk);
         FillDecals(chunkSize/voxelsInChunk);
         //Perlin params
//         MarchingCubesEffect.Parameters["permutationTable"].SetValue(PerlinNoise.permutationTable);
//         MarchingCubesEffect.Parameters["gradientSet"].SetValue(PerlinNoise.gradientSet.ToArray());
//         MarchingCubesEffect.Parameters["mX"].SetValue(PerlinNoise.mX);
//         MarchingCubesEffect.Parameters["mY"].SetValue(PerlinNoise.mY);
//         MarchingCubesEffect.Parameters["mZ"].SetValue(PerlinNoise.mZ);

         //Open simplex params
//         MarchingCubesEffect.Parameters["perm"].SetValue(noise.perm);
//         MarchingCubesEffect.Parameters["permGradIndex3D"].SetValue(noise.perm3D);
//         MarchingCubesEffect.Parameters["Gradients3D"].SetValue(OpenSimplexHLSLpreparation.Gradients3D);

         //MC params
//         MarchingCubesEffect.Parameters["EdgeTable"].SetValue(MarchingCubes.EdgeTable);
//         MarchingCubesEffect.Parameters["tritableTex"].SetResource(lookupTexture);
//         MarchingCubesEffect.Parameters["decal"].SetValue(decals);
//         MarchingCubesEffect.Parameters["isolevel"].SetValue(-0.5f);
         string name = technique == 0 ? "Root Perlin MC" : "Root Simplex MC";
         var root = entityWorld.CreateEntity(name);
         for (int k = 0; k < blockCount; ++k)
         {
            for (int i = 0; i < blockCount; ++i)
            {
               for (int j = 0; j < blockCount; ++j)
               {

//                  Buffer<VertexPositionNormalTexture> streamOut =
//                     Buffer.New<VertexPositionNormalTexture>(graphicsDevice,
//                        voxelsInChunk*voxelsInChunk*voxelsInChunk*15,
//                        BufferFlags.StreamOutput | BufferFlags.VertexBuffer);
//                  graphicsDevice.SetVertexBuffer(vertex);
//                  graphicsDevice.VertexInputLayout = MClayout;
//                  graphicsDevice.SetStreamOutputTarget(streamOut, 0);
//
//                  MarchingCubesEffect.Parameters["origin"].SetValue(blockOrigin);
//
//                  Stopwatch timer = Stopwatch.StartNew();
//                  MarchingCubesEffect.Techniques[0].Passes[0].Apply();
//                  graphicsDevice.Draw(PrimitiveType.PointList, vertex.ElementCount);
//                  MarchingCubesEffect.Techniques[0].Passes[0].UnApply(true);
//                  timer.Stop();
//                  var ms = timer.ElapsedMilliseconds;

                  //var data = streamOut.GetData();

                  name = technique == 0 ? "Perlin MC" : "Simplex MC";
                  var entity = entityWorld.CreateEntity(name, root);

                  var collider = new BoxCollider();
                  collider.CalculateFromPoints(new [] { blockOrigin, blockOrigin + chunkSize, });
                  entity.AddComponent(collider);

                  //var geometry = new CelestialBodyGeometry(streamOut, null, PrimitiveType.TriangleList);
                  //entity.AddComponent(geometry);

                  blockOrigin.X += 0.5f;
               }
               blockOrigin.Z += 0.5f;
               blockOrigin.X = 0.0f;
            }
            blockOrigin.Y += 0.5f;
         }
         blockOrigin.Y = 0;
      }
   }
}
