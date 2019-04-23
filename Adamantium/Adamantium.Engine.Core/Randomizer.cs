using System;
using System.Security.Cryptography;

namespace Adamantium.Engine.Core
{
   public class Randomizer:RandomNumberGenerator
   {
      private static RandomNumberGenerator randomizer;

      public Randomizer()
      {
         randomizer = Create();
      }

      public override void GetBytes(byte[] data)
      {
         randomizer.GetBytes(data);
      }

      ///<summary>
      /// Returns a random number between 0.0 and 1.0.
      ///</summary>
      public float NextFloat()
      {
         byte[] bytes = new byte[4];
         randomizer.GetBytes(bytes);
         return (float)BitConverter.ToUInt32(bytes, 0) / UInt32.MaxValue;
      }

      public float NextFloat(float minValue, float maxValue)
      {
         var result = NextFloat();
         return (result * (maxValue - minValue)) + minValue;
      }

      ///<summary>
      /// Returns a random number between 0.0 and 1.0.
      ///</summary>
      public double NextDouble()
      {
         byte[] bytes = new byte[4];
         randomizer.GetBytes(bytes);
         return (double) BitConverter.ToUInt32(bytes, 0) / UInt32.MaxValue;
      }

      public double NextDouble(double minValue, double maxValue)
      {
         double value = NextDouble();
         return (value * (maxValue - minValue)) + minValue;
      }

      ///<summary>
      /// Returns a random number within the specified range.
      ///</summary>
      /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
      ///<param name="maxValue">The exclusive upper bound of the random number returned. maxValue must be greater than or equal to minValue.</param>
      public int Next(int minValue, int maxValue)
      {
         return (int) Math.Round(NextDouble() * (maxValue - minValue - 1)) + minValue;
      }

      ///<summary>
      /// Returns a nonnegative random number.
      ///</summary>
      public int Next()
      {
         return Next(0, Int32.MaxValue);
      }

      ///<summary>
      /// Returns a nonnegative random number less than the specified maximum
      ///</summary>
      ///<param name="maxValue">The inclusive upper bound of the random number returned. maxValue must be greater than or equal 0</param>
      public int Next(int maxValue)
      {
         return Next(0, maxValue);
      }
   }
}
