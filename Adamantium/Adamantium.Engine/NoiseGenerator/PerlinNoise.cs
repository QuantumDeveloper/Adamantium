using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Noise
{
   public static class PerlinNoise
   {
      private const int permutationTableSize = 1024;

      //permutation table
      public static int[] permutationTable;

      //pseudorandom hash modifiers
      public static int mX;
      public static int mY;
      public static int mZ;

      // gradients' set
      public static List<Vector3F> gradientSet;

      static PerlinNoise()
      {
         permutationTable = new int[permutationTableSize];
         gradientSet = new List<Vector3F>();

         // fill the gradients' set
         for (int x = -1; x <= 1; ++x) // from -1 to 1
         {
            for (int y = -1; y <= 1; ++y)
            {
               for (int z = -1; z <= 1; ++z)
               {
                  if ((x != 0) || (y != 0) || (z != 0))
                  {
                     gradientSet.Add(new Vector3F(x, y, z));
                  }
               }
            }
         }

         SetSeed();
      }

      public static void SetSeed(int? seed = null)
      {
         Random rand = ((seed == null) ? new Random() : new Random((int)seed));

         byte[] tempArray = new byte[1024];
         rand.NextBytes(tempArray);
         FillPermutationArray(tempArray);

         mX = rand.Next();
         mY = rand.Next();
         mZ = rand.Next();
      }

      private static void FillPermutationArray(byte[] array)
      {
         for (int i = 0; i < array.Length; ++i)
         {
            permutationTable[i] = array[i];
         }
      }

      private static Vector3F GetGradient(int x, int y, int z, bool isLessGradients = false)
      {
         // pick random cell in permutation table (cells 0 to 'permutationTableSize')
         int index = (int)((x * mX) ^ (y * mY) + z * mZ + (mX * mY * mZ)) & (permutationTableSize - 1);

         if (isLessGradients == false)
         {
            // pick random cell in gradientSet vector
            index = permutationTable[index] & (gradientSet.Count - 1);

            // return the content of the picked cell
            return gradientSet[index];
         }
         else
         {
            // ALTERNATIVE IMPLEMENTATION FOR 12 GRADIENT VECTORS
            index = permutationTable[index] & 11;

            switch (index)
            {
               case 0: return new Vector3F(0, 1, 1);
               case 1: return new Vector3F(0, 1, -1);
               case 2: return new Vector3F(0, -1, 1);
               case 3: return new Vector3F(0, -1, -1);
               case 4: return new Vector3F(1, 0, 1);
               case 5: return new Vector3F(1, 0, -1);
               case 6: return new Vector3F(-1, 0, 1);
               case 7: return new Vector3F(-1, 0, -1);
               case 8: return new Vector3F(1, 1, 0);
               case 9: return new Vector3F(1, -1, 0);
               case 10: return new Vector3F(-1, 1, 0);
               default: return new Vector3F(-1, -1, 0);
            }
         }
      }

      private static int FastFloor(float d)
      {
         int res = (int)d;

         if (d < 0)
         {
            if (res != d)
            {
               --res;
            }
         }

         return res;
      }

      private static float FastPow(float value, uint pow)
      {
         float powOfValue = 1;

         for (uint i = 0; i < pow; ++i)
         {
            powOfValue *= value;
         }

         return powOfValue;
      }

      private static float BlendingCurve(float d)
      {
         return (d * d * d * (d * (d * 6.0f - 15.0f) + 10.0f));
      }

      private static float Interpolation(float a, float b, float t)
      {
         return ((1.0f - t) * a + t * b);
      }

      public static float Get3DNoiseValue(Vector3F point)
      {
         return Get3DNoiseValue(point.X, point.Y, point.Z);
      }

      public static float Get3DNoiseValue(float x, float y, float z)
      {
         // find unit grid cell containing point
         int floorX = FastFloor(x);
         int floorY = FastFloor(y);
         int floorZ = FastFloor(z);

         // get relative XYZ coordinates of point in cell
         float relX = x - floorX;
         float relY = y - floorY;
         float relZ = z - floorZ;

         //gradients of cube vertices
         Vector3F g000 = GetGradient(floorX, floorY, floorZ);
         Vector3F g001 = GetGradient(floorX, floorY, floorZ + 1);
         Vector3F g010 = GetGradient(floorX, floorY + 1, floorZ);
         Vector3F g011 = GetGradient(floorX, floorY + 1, floorZ + 1);
         Vector3F g100 = GetGradient(floorX + 1, floorY, floorZ);
         Vector3F g101 = GetGradient(floorX + 1, floorY, floorZ + 1);
         Vector3F g110 = GetGradient(floorX + 1, floorY + 1, floorZ);
         Vector3F g111 = GetGradient(floorX + 1, floorY + 1, floorZ + 1);

         // noise contribution from each of the eight corner
         float n000 = Vector3F.Dot(g000, new Vector3F(relX, relY, relZ));
         float n100 = Vector3F.Dot(g100, new Vector3F(relX - 1, relY, relZ));
         float n010 = Vector3F.Dot(g010, new Vector3F(relX, relY - 1, relZ));
         float n110 = Vector3F.Dot(g110, new Vector3F(relX - 1, relY - 1, relZ));
         float n001 = Vector3F.Dot(g001, new Vector3F(relX, relY, relZ - 1));
         float n101 = Vector3F.Dot(g101, new Vector3F(relX - 1, relY, relZ - 1));
         float n011 = Vector3F.Dot(g011, new Vector3F(relX, relY - 1, relZ - 1));
         float n111 = Vector3F.Dot(g111, new Vector3F(relX - 1, relY - 1, relZ - 1));

         // compute the fade curve value for each x, y, z
         float u = BlendingCurve(relX);
         float v = BlendingCurve(relY);
         float w = BlendingCurve(relZ);

         // interpolate along x the contribution from each of the corners
         float nx00 = Interpolation(n000, n100, u);
         float nx01 = Interpolation(n001, n101, u);
         float nx10 = Interpolation(n010, n110, u);
         float nx11 = Interpolation(n011, n111, u);

         // interpolate the four results along y
         float nxy0 = Interpolation(nx00, nx10, v);
         float nxy1 = Interpolation(nx01, nx11, v);

         // interpolate the two last results along z
         float nxyz = Interpolation(nxy0, nxy1, w);

         return nxyz;
      }

      // w = pers
      public static float GetMultioctave3DNoiseValue(Vector3F point, Vector4F modifiers, uint startOctaveNumber, uint octaveCount)
      {
         float res = 0;
         
         for (uint i = startOctaveNumber; i < (startOctaveNumber + octaveCount); ++i)
         {
            var powOf2 = FastPow(2, i);
            //res += (powOf2 * Get3DNoiseValue(x / powOf2, y / powOf2, z / powOf2));
            res += modifiers.W * (Get3DNoiseValue(point.X / (powOf2 * modifiers.X), point.Y / (powOf2 * modifiers.Y), point.Z / (powOf2 * modifiers.Z)));
         }

         return res;
      }

      public static float GetMultioctave3DNoiseValueFromSphere(Vector3F point, Vector4F modifiers, uint startOctaveNumber, uint octaveCount, uint radius)
      {
         // convert to sphere coordinates
         float d = FastPow(point.X, 2) + FastPow(point.Y, 2) + FastPow(point.Z, 2);

         d = (float)Math.Sqrt(d);

         float zd = point.Z / d;

         float theta = (float)Math.Acos(zd);
         float phi = (float)Math.Atan2(point.Y, point.X);

         float sx = radius * (float)Math.Sin(theta) * (float)Math.Cos(phi);
         float sy = radius * (float)Math.Sin(theta) * (float)Math.Sin(phi);
         float sz = radius * (float)Math.Cos(theta);

         Vector3F spherePoint = new Vector3F(sx, sy, sz);

         return GetMultioctave3DNoiseValue(spherePoint, modifiers, startOctaveNumber, octaveCount);
      }
   }
}

//private void generateButton_Click(object sender, RoutedEventArgs e)
//{
//   rawtile = new List<float[,]>();
//   size = Convert.ToInt32(powOf2TextBox.Text);
//   int half_size = size / 2;
//   pow = (int)Math.Floor(Math.Log(size, 2));

//   for (int i = 0; i < plainCnt; ++i)
//   {
//      rawtile.Add(new float[size, size]);
//   }

//   var watch = System.Diagnostics.Stopwatch.StartNew();

//   Perlin3D.setSeed(Convert.ToInt32(seedTextBox.Text));

//   Parallel.For(0, plainCnt, index => { CalculateNoise(half_size, index); });

//   watch.Stop();
//   var elapsedMs = watch.ElapsedMilliseconds;

//   labelMs.Content = elapsedMs.ToString();

//   normalizeArray();

//   DrawAll();
//}

//private void CalculateNoise(int halfSize, int index, uint startOctaveNumber, uint octaveCount)
//{
//   if (octaveCount == 0)
//   {
//      octaveCount = 1;
//   }

//   uint octaveEnd = startOctaveNumber + octaveCount - 1;

//   for (int i = 0; i < size; ++i)
//   {
//      for (int j = 0; j < size; ++j)
//      {
//         // center of cube is moved to the center of coordinates and its coords transformed to sphere coords
//         /*
//         *    5
//         * 1  2  3  4
//         *    6
//         */
//         switch (index)
//         {
//            case 0:
//               rawtile[0][i, j] = Perlin3D.getMultioctave3DNoiseValueFromSphere(0 - halfSize, halfSize - j, i - halfSize, startOctaveNumber, octaveEnd, (uint)halfSize);
//               break;
//            case 1:
//               rawtile[1][i, j] = Perlin3D.getMultioctave3DNoiseValueFromSphere(j - halfSize, 0 - halfSize, i - halfSize, startOctaveNumber, octaveEnd, (uint)halfSize);
//               break;
//            case 2:
//               rawtile[2][i, j] = Perlin3D.getMultioctave3DNoiseValueFromSphere(halfSize, j - halfSize, i - halfSize, startOctaveNumber, octaveEnd, (uint)halfSize);
//               break;
//            case 3:
//               rawtile[3][i, j] = Perlin3D.getMultioctave3DNoiseValueFromSphere(halfSize - j, halfSize, i - halfSize, startOctaveNumber, octaveEnd, (uint)halfSize);
//               break;
//            case 4:
//               rawtile[4][i, j] = Perlin3D.getMultioctave3DNoiseValueFromSphere(j - halfSize, i - halfSize, halfSize, startOctaveNumber, octaveEnd, (uint)halfSize);
//               break;
//            case 5:
//               rawtile[5][i, j] = Perlin3D.getMultioctave3DNoiseValueFromSphere(j - halfSize, halfSize - i, 0 - halfSize, startOctaveNumber, octaveEnd, (uint)halfSize);
//               break;
//         }
//      }
//   }
//}

//private void normalizeArray()
//{
//   double min = minNoise();
//   double max = maxNoise();

//   for (int a = 0; a < plainCnt; ++a)
//   {
//      for (int i = 0; i < size; ++i)
//      {
//         for (int j = 0; j < size; ++j)
//         {
//            rawtile[a][i, j] = ((rawtile[a][i, j] - min) / (max - min));
//         }
//      }
//   }
//}