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
      public static List<Vector3> gradientSet;

      static PerlinNoise()
      {
         permutationTable = new int[permutationTableSize];
         gradientSet = new List<Vector3>();

         // fill the gradients' set
         for (int x = -1; x <= 1; ++x) // from -1 to 1
         {
            for (int y = -1; y <= 1; ++y)
            {
               for (int z = -1; z <= 1; ++z)
               {
                  if ((x != 0) || (y != 0) || (z != 0))
                  {
                     gradientSet.Add(new Vector3(x, y, z));
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

      private static Vector3 GetGradient(int x, int y, int z, bool isLessGradients = false)
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
               case 0: return new Vector3(0, 1, 1);
               case 1: return new Vector3(0, 1, -1);
               case 2: return new Vector3(0, -1, 1);
               case 3: return new Vector3(0, -1, -1);
               case 4: return new Vector3(1, 0, 1);
               case 5: return new Vector3(1, 0, -1);
               case 6: return new Vector3(-1, 0, 1);
               case 7: return new Vector3(-1, 0, -1);
               case 8: return new Vector3(1, 1, 0);
               case 9: return new Vector3(1, -1, 0);
               case 10: return new Vector3(-1, 1, 0);
               default: return new Vector3(-1, -1, 0);
            }
         }
      }

      private static int FastFloor(double d)
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

      private static double FastPow(double value, uint pow)
      {
         double powOfValue = 1;

         for (uint i = 0; i < pow; ++i)
         {
            powOfValue *= value;
         }

         return powOfValue;
      }

      private static double BlendingCurve(double d)
      {
         return (d * d * d * (d * (d * 6.0f - 15.0f) + 10.0f));
      }

      private static double Interpolation(double a, double b, double t)
      {
         return ((1.0f - t) * a + t * b);
      }

      public static double Get3DNoiseValue(Vector3 point)
      {
         return Get3DNoiseValue(point.X, point.Y, point.Z);
      }

      public static double Get3DNoiseValue(double x, double y, double z)
      {
         // find unit grid cell containing point
         int floorX = FastFloor(x);
         int floorY = FastFloor(y);
         int floorZ = FastFloor(z);

         // get relative XYZ coordinates of point in cell
         var relX = x - floorX;
         var relY = y - floorY;
         var relZ = z - floorZ;

         //gradients of cube vertices
         var g000 = GetGradient(floorX, floorY, floorZ);
         var g001 = GetGradient(floorX, floorY, floorZ + 1);
         var g010 = GetGradient(floorX, floorY + 1, floorZ);
         var g011 = GetGradient(floorX, floorY + 1, floorZ + 1);
         var g100 = GetGradient(floorX + 1, floorY, floorZ);
         var g101 = GetGradient(floorX + 1, floorY, floorZ + 1);
         var g110 = GetGradient(floorX + 1, floorY + 1, floorZ);
         var g111 = GetGradient(floorX + 1, floorY + 1, floorZ + 1);

         // noise contribution from each of the eight corner
         var n000 = Vector3.Dot(g000, new Vector3(relX, relY, relZ));
         var n100 = Vector3.Dot(g100, new Vector3(relX - 1, relY, relZ));
         var n010 = Vector3.Dot(g010, new Vector3(relX, relY - 1, relZ));
         var n110 = Vector3.Dot(g110, new Vector3(relX - 1, relY - 1, relZ));
         var n001 = Vector3.Dot(g001, new Vector3(relX, relY, relZ - 1));
         var n101 = Vector3.Dot(g101, new Vector3(relX - 1, relY, relZ - 1));
         var n011 = Vector3.Dot(g011, new Vector3(relX, relY - 1, relZ - 1));
         var n111 = Vector3.Dot(g111, new Vector3(relX - 1, relY - 1, relZ - 1));

         // compute the fade curve value for each x, y, z
         var u = BlendingCurve(relX);
         var v = BlendingCurve(relY);
         var w = BlendingCurve(relZ);

         // interpolate along x the contribution from each of the corners
         var nx00 = Interpolation(n000, n100, u);
         var nx01 = Interpolation(n001, n101, u);
         var nx10 = Interpolation(n010, n110, u);
         var nx11 = Interpolation(n011, n111, u);

         // interpolate the four results along y
         double nxy0 = Interpolation(nx00, nx10, v);
         double nxy1 = Interpolation(nx01, nx11, v);

         // interpolate the two last results along z
         double nxyz = Interpolation(nxy0, nxy1, w);

         return nxyz;
      }

      // w = pers
      public static double GetMultioctave3DNoiseValue(Vector3 point, Vector4 modifiers, uint startOctaveNumber, uint octaveCount)
      {
         double res = 0;
         
         for (uint i = startOctaveNumber; i < (startOctaveNumber + octaveCount); ++i)
         {
            var powOf2 = FastPow(2, i);
            //res += (powOf2 * Get3DNoiseValue(x / powOf2, y / powOf2, z / powOf2));
            res += modifiers.W * (Get3DNoiseValue(point.X / (powOf2 * modifiers.X), point.Y / (powOf2 * modifiers.Y), point.Z / (powOf2 * modifiers.Z)));
         }

         return res;
      }

      public static double GetMultioctave3DNoiseValueFromSphere(Vector3F point, Vector4 modifiers, uint startOctaveNumber, uint octaveCount, uint radius)
      {
         // convert to sphere coordinates
         double d = FastPow(point.X, 2) + FastPow(point.Y, 2) + FastPow(point.Z, 2);

         d = Math.Sqrt(d);

         double zd = point.Z / d;

         double theta = Math.Acos(zd);
         double phi = Math.Atan2(point.Y, point.X);

         double sx = radius * Math.Sin(theta) * Math.Cos(phi);
         double sy = radius * Math.Sin(theta) * Math.Sin(phi);
         double sz = radius * Math.Cos(theta);

         var spherePoint = new Vector3(sx, sy, sz);

         return GetMultioctave3DNoiseValue(spherePoint, modifiers, startOctaveNumber, octaveCount);
      }
   }
}

//private void generateButton_Click(object sender, RoutedEventArgs e)
//{
//   rawtile = new List<double[,]>();
//   size = Convert.ToInt32(powOf2TextBox.Text);
//   int half_size = size / 2;
//   pow = (int)Math.Floor(Math.Log(size, 2));

//   for (int i = 0; i < plainCnt; ++i)
//   {
//      rawtile.Add(new double[size, size]);
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