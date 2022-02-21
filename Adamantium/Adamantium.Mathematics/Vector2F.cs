﻿using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Adamantium.Mathematics
{
   /// <summary>
   /// Represents a two dimensional mathematical vector.
   /// </summary>
   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct Vector2F : IEquatable<Vector2F>, IFormattable
   {
      /// <summary>
      /// The size of the <see cref="Vector2F"/> type, in bytes.
      /// </summary>
      public static readonly int SizeInBytes = Marshal.SizeOf(typeof(Vector2F));

      /// <summary>
      /// A <see cref="Vector2F"/> with all of its components set to zero.
      /// </summary>
      public static readonly Vector2F Zero;

      /// <summary>
      /// The X unit <see cref="Vector2F"/> (1, 0).
      /// </summary>
      public static readonly Vector2F UnitX = new Vector2F(1.0f, 0.0f);

      /// <summary>
      /// The Y unit <see cref="Vector2F"/> (0, 1).
      /// </summary>
      public static readonly Vector2F UnitY = new Vector2F(0.0f, 1.0f);

      /// <summary>
      /// A <see cref="Vector2F"/> with all of its components set to one.
      /// </summary>
      public static readonly Vector2F One = new Vector2F(1.0f, 1.0f);

      public float X;

      public float Y;

      public Vector2F(float value)
      {
         X = Y = value;
      }

      public Vector2F(float x, float y)
      {
         X = x;
         Y = y;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="Vector2F"/> struct.
      /// </summary>
      /// <param name="values">The values to assign to the X and Y components of the vector. This must be an array with two elements.</param>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="values"/> contains more or less than two elements.</exception>
      public Vector2F(float[] values)
      {
         if (values == null)
            throw new ArgumentNullException(nameof(values));
         if (values.Length < 2)
            throw new ArgumentOutOfRangeException("values", "There must be not less than two input values for Vector2F.");

         X = values[0];
         Y = values[1];
      }

      /// <summary>
      /// Gets a value indicting whether this instance is normalized.
      /// </summary>
      public bool IsNormalized => MathHelper.IsOne((X * X) + (Y * Y));

      /// <summary>
      /// Gets a value indicting whether this vector is zero
      /// </summary>
      public bool IsZero => X == 0 && Y == 0;

      /// <summary>
      /// Gets or sets the component at the specified index.
      /// </summary>
      /// <value>The value of the X or Y component, depending on the index.</value>
      /// <param name="index">The index of the component to access. Use 0 for the X component and 1 for the Y component.</param>
      /// <returns>The value of the component at the specified index.</returns>
      /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the <paramref name="index"/> is out of the range [0, 1].</exception>
      public float this[int index]
      {
         get
         {
            switch (index)
            {
               case 0: return X;
               case 1: return Y;
            }

            throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vector2F run from 0 to 1, inclusive.");
         }

         set
         {
            switch (index)
            {
               case 0: X = value; break;
               case 1: Y = value; break;
               default: throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vector2F run from 0 to 1, inclusive.");
            }
         }
      }

      /// <summary>
      /// Calculates the length of the vector.
      /// </summary>
      /// <returns>The length of the vector.</returns>
      /// <remarks>
      /// <see cref="Vector2F.LengthSquared"/> may be preferred when only the relative length is needed
      /// and speed is of the essence.
      /// </remarks>
      public float Length()
      {
         return (float)Math.Sqrt((X * X) + (Y * Y));
      }

      /// <summary>
      /// Calculates the squared length of the vector.
      /// </summary>
      /// <returns>The squared length of the vector.</returns>
      /// <remarks>
      /// This method may be preferred to <see cref="Vector2F.Length"/> when only a relative length is needed
      /// and speed is of the essence.
      /// </remarks>
      public float LengthSquared()
      {
         return (X * X) + (Y * Y);
      }

      /// <summary>
      /// Converts the vector into a unit vector.
      /// </summary>
      public void Normalize()
      {
         float length = Length();
         if (!MathHelper.IsZero(length))
         {
            float inv = 1.0f / length;
            X *= inv;
            Y *= inv;
         }
      }

      /// <summary>
      /// Creates an array containing the elements of the vector.
      /// </summary>
      /// <returns>A two-element array containing the components of the vector.</returns>
      public float[] ToArray()
      {
         return new [] { X, Y };
      }
      
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool IsCollinear(Vector2F vector)
      {
         return MathHelper.NearEqual(X * vector.Y, Y * vector.X);
      }

      /// <summary>
      /// Adds two vectors.
      /// </summary>
      /// <param name="left">The first vector to add.</param>
      /// <param name="right">The second vector to add.</param>
      /// <param name="result">When the method completes, contains the sum of the two vectors.</param>
      public static void Add(ref Vector2F left, ref Vector2F right, out Vector2F result)
      {
         result = new Vector2F(left.X + right.X, left.Y + right.Y);
      }

      /// <summary>
      /// Adds two vectors.
      /// </summary>
      /// <param name="left">The first vector to add.</param>
      /// <param name="right">The second vector to add.</param>
      /// <returns>The sum of the two vectors.</returns>
      public static Vector2F Add(Vector2F left, Vector2F right)
      {
         return new Vector2F(left.X + right.X, left.Y + right.Y);
      }

      /// <summary>
      /// Perform a component-wise addition
      /// </summary>
      /// <param name="left">The input vector</param>
      /// <param name="right">The scalar value to be added to elements</param>
      /// <param name="result">The vector with added scalar for each element.</param>
      public static void Add(ref Vector2F left, ref float right, out Vector2F result)
      {
         result = new Vector2F(left.X + right, left.Y + right);
      }

      /// <summary>
      /// Perform a component-wise addition
      /// </summary>
      /// <param name="left">The input vector</param>
      /// <param name="right">The scalar value to be added to elements</param>
      /// <returns>The vector with added scalar for each element.</returns>
      public static Vector2F Add(Vector2F left, float right)
      {
         return new Vector2F(left.X + right, left.Y + right);
      }

      /// <summary>
      /// Subtracts two vectors.
      /// </summary>
      /// <param name="left">The first vector to subtract.</param>
      /// <param name="right">The second vector to subtract.</param>
      /// <param name="result">When the method completes, contains the difference of the two vectors.</param>
      public static void Subtract(ref Vector2F left, ref Vector2F right, out Vector2F result)
      {
         result = new Vector2F(left.X - right.X, left.Y - right.Y);
      }

      /// <summary>
      /// Subtracts two vectors.
      /// </summary>
      /// <param name="left">The first vector to subtract.</param>
      /// <param name="right">The second vector to subtract.</param>
      /// <returns>The difference of the two vectors.</returns>
      public static Vector2F Subtract(Vector2F left, Vector2F right)
      {
         return new Vector2F(left.X - right.X, left.Y - right.Y);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="left">The input vector</param>
      /// <param name="right">The scalar value to be subtraced from elements</param>
      /// <param name="result">The vector with subtracted scalar for each element.</param>
      public static void Subtract(ref Vector2F left, ref float right, out Vector2F result)
      {
         result = new Vector2F(left.X - right, left.Y - right);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="left">The input vector</param>
      /// <param name="right">The scalar value to be subtraced from elements</param>
      /// <returns>The vector with subtracted scalar for each element.</returns>
      public static Vector2F Subtract(Vector2F left, float right)
      {
         return new Vector2F(left.X - right, left.Y - right);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="left">The scalar value to be subtraced from elements</param>
      /// <param name="right">The input vector</param>
      /// <param name="result">The vector with subtracted scalar for each element.</param>
      public static void Subtract(ref float left, ref Vector2F right, out Vector2F result)
      {
         result = new Vector2F(left - right.X, left - right.Y);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="left">The scalar value to be subtraced from elements</param>
      /// <param name="right">The input vector</param>
      /// <returns>The vector with subtracted scalar for each element.</returns>
      public static Vector2F Subtract(float left, Vector2F right)
      {
         return new Vector2F(left - right.X, left - right.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <param name="result">When the method completes, contains the scaled vector.</param>
      public static void Multiply(ref Vector2F value, float scale, out Vector2F result)
      {
         result = new Vector2F(value.X * scale, value.Y * scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      public static Vector2F Multiply(Vector2F value, float scale)
      {
         return new Vector2F(value.X * scale, value.Y * scale);
      }

      /// <summary>
      /// Multiplies a vector with another by performing component-wise multiplication.
      /// </summary>
      /// <param name="left">The first vector to multiply.</param>
      /// <param name="right">The second vector to multiply.</param>
      /// <param name="result">When the method completes, contains the multiplied vector.</param>
      public static void Multiply(ref Vector2F left, ref Vector2F right, out Vector2F result)
      {
         result = new Vector2F(left.X * right.X, left.Y * right.Y);
      }

      /// <summary>
      /// Multiplies a vector with another by performing component-wise multiplication.
      /// </summary>
      /// <param name="left">The first vector to multiply.</param>
      /// <param name="right">The second vector to multiply.</param>
      /// <returns>The multiplied vector.</returns>
      public static Vector2F Multiply(Vector2F left, Vector2F right)
      {
         return new Vector2F(left.X * right.X, left.Y * right.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <param name="result">When the method completes, contains the scaled vector.</param>
      public static void Divide(ref Vector2F value, float scale, out Vector2F result)
      {
         result = new Vector2F(value.X / scale, value.Y / scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      public static Vector2F Divide(Vector2F value, float scale)
      {
         return new Vector2F(value.X / scale, value.Y / scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <param name="value">The vector to scale.</param>
      /// <param name="result">When the method completes, contains the scaled vector.</param>
      public static void Divide(float scale, ref Vector2F value, out Vector2F result)
      {
         result = new Vector2F(scale / value.X, scale / value.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      public static Vector2F Divide(float scale, Vector2F value)
      {
         return new Vector2F(scale / value.X, scale / value.Y);
      }

      /// <summary>
      /// Reverses the direction of a given vector.
      /// </summary>
      /// <param name="value">The vector to negate.</param>
      /// <param name="result">When the method completes, contains a vector facing in the opposite direction.</param>
      public static void Negate(ref Vector2F value, out Vector2F result)
      {
         result = new Vector2F(-value.X, -value.Y);
      }

      /// <summary>
      /// Reverses the direction of a given vector.
      /// </summary>
      /// <param name="value">The vector to negate.</param>
      /// <returns>A vector facing in the opposite direction.</returns>
      public static Vector2F Negate(Vector2F value)
      {
         return new Vector2F(-value.X, -value.Y);
      }

      /// <summary>
      /// Returns a <see cref="Vector2F"/> containing the 2D Cartesian coordinates of a point specified in Barycentric coordinates relative to a 2D triangle.
      /// </summary>
      /// <param name="value1">A <see cref="Vector2F"/> containing the 2D Cartesian coordinates of vertex 1 of the triangle.</param>
      /// <param name="value2">A <see cref="Vector2F"/> containing the 2D Cartesian coordinates of vertex 2 of the triangle.</param>
      /// <param name="value3">A <see cref="Vector2F"/> containing the 2D Cartesian coordinates of vertex 3 of the triangle.</param>
      /// <param name="amount1">Barycentric coordinate b2, which expresses the weighting factor toward vertex 2 (specified in <paramref name="value2"/>).</param>
      /// <param name="amount2">Barycentric coordinate b3, which expresses the weighting factor toward vertex 3 (specified in <paramref name="value3"/>).</param>
      /// <param name="result">When the method completes, contains the 2D Cartesian coordinates of the specified point.</param>
      public static void Barycentric(ref Vector2F value1, ref Vector2F value2, ref Vector2F value3, float amount1, float amount2, out Vector2F result)
      {
         result = new Vector2F((value1.X + (amount1 * (value2.X - value1.X))) + (amount2 * (value3.X - value1.X)),
             (value1.Y + (amount1 * (value2.Y - value1.Y))) + (amount2 * (value3.Y - value1.Y)));
      }

      /// <summary>
      /// Returns a <see cref="Vector2F"/> containing the 2D Cartesian coordinates of a point specified in Barycentric coordinates relative to a 2D triangle.
      /// </summary>
      /// <param name="value1">A <see cref="Vector2F"/> containing the 2D Cartesian coordinates of vertex 1 of the triangle.</param>
      /// <param name="value2">A <see cref="Vector2F"/> containing the 2D Cartesian coordinates of vertex 2 of the triangle.</param>
      /// <param name="value3">A <see cref="Vector2F"/> containing the 2D Cartesian coordinates of vertex 3 of the triangle.</param>
      /// <param name="amount1">Barycentric coordinate b2, which expresses the weighting factor toward vertex 2 (specified in <paramref name="value2"/>).</param>
      /// <param name="amount2">Barycentric coordinate b3, which expresses the weighting factor toward vertex 3 (specified in <paramref name="value3"/>).</param>
      /// <returns>A new <see cref="Vector2F"/> containing the 2D Cartesian coordinates of the specified point.</returns>
      public static Vector2F Barycentric(Vector2F value1, Vector2F value2, Vector2F value3, float amount1, float amount2)
      {
         Vector2F result;
         Barycentric(ref value1, ref value2, ref value3, amount1, amount2, out result);
         return result;
      }

      /// <summary>
      /// Restricts a value to be within a specified range.
      /// </summary>
      /// <param name="value">The value to clamp.</param>
      /// <param name="min">The minimum value.</param>
      /// <param name="max">The maximum value.</param>
      /// <param name="result">When the method completes, contains the clamped value.</param>
      public static void Clamp(ref Vector2F value, ref Vector2F min, ref Vector2F max, out Vector2F result)
      {
         float x = value.X;
         x = (x > max.X) ? max.X : x;
         x = (x < min.X) ? min.X : x;

         float y = value.Y;
         y = (y > max.Y) ? max.Y : y;
         y = (y < min.Y) ? min.Y : y;

         result = new Vector2F(x, y);
      }

      /// <summary>
      /// Restricts a value to be within a specified range.
      /// </summary>
      /// <param name="value">The value to clamp.</param>
      /// <param name="min">The minimum value.</param>
      /// <param name="max">The maximum value.</param>
      /// <returns>The clamped value.</returns>
      public static Vector2F Clamp(Vector2F value, Vector2F min, Vector2F max)
      {
         Vector2F result;
         Clamp(ref value, ref min, ref max, out result);
         return result;
      }

      /// <summary>
      /// Saturates this instance in the range [0,1]
      /// </summary>
      public void Saturate()
      {
         X = X < 0.0f ? 0.0f : X > 1.0f ? 1.0f : X;
         Y = Y < 0.0f ? 0.0f : Y > 1.0f ? 1.0f : Y;
      }

      /// <summary>
      /// Calculates the distance between two vectors.
      /// </summary>
      /// <param name="value1">The first vector.</param>
      /// <param name="value2">The second vector.</param>
      /// <param name="result">When the method completes, contains the distance between the two vectors.</param>
      /// <remarks>
      /// <see cref="Vector2F.DistanceSquared(ref Vector2F, ref Vector2F, out float)"/> may be preferred when only the relative distance is needed
      /// and speed is of the essence.
      /// </remarks>
      public static void Distance(ref Vector2F value1, ref Vector2F value2, out float result)
      {
         float x = value1.X - value2.X;
         float y = value1.Y - value2.Y;

         result = (float)Math.Sqrt((x * x) + (y * y));
      }

      /// <summary>
      /// Calculates the distance between two vectors.
      /// </summary>
      /// <param name="value1">The first vector.</param>
      /// <param name="value2">The second vector.</param>
      /// <returns>The distance between the two vectors.</returns>
      /// <remarks>
      /// <see cref="Vector2F.DistanceSquared(Vector2F, Vector2F)"/> may be preferred when only the relative distance is needed
      /// and speed is of the essence.
      /// </remarks>
      public static float Distance(Vector2F value1, Vector2F value2)
      {
         float x = value1.X - value2.X;
         float y = value1.Y - value2.Y;

         return (float)Math.Sqrt((x * x) + (y * y));
      }

      /// <summary>
      /// Calculates the squared distance between two vectors.
      /// </summary>
      /// <param name="value1">The first vector.</param>
      /// <param name="value2">The second vector</param>
      /// <param name="result">When the method completes, contains the squared distance between the two vectors.</param>
      /// <remarks>Distance squared is the value before taking the square root. 
      /// Distance squared can often be used in place of distance if relative comparisons are being made. 
      /// For example, consider three points A, B, and C. To determine whether B or C is further from A, 
      /// compare the distance between A and B to the distance between A and C. Calculating the two distances 
      /// involves two square roots, which are computationally expensive. However, using distance squared 
      /// provides the same information and avoids calculating two square roots.
      /// </remarks>
      public static void DistanceSquared(ref Vector2F value1, ref Vector2F value2, out float result)
      {
         float x = value1.X - value2.X;
         float y = value1.Y - value2.Y;

         result = (x * x) + (y * y);
      }

      /// <summary>
      /// Calculates the squared distance between two vectors.
      /// </summary>
      /// <param name="value1">The first vector.</param>
      /// <param name="value2">The second vector.</param>
      /// <returns>The squared distance between the two vectors.</returns>
      /// <remarks>Distance squared is the value before taking the square root. 
      /// Distance squared can often be used in place of distance if relative comparisons are being made. 
      /// For example, consider three points A, B, and C. To determine whether B or C is further from A, 
      /// compare the distance between A and B to the distance between A and C. Calculating the two distances 
      /// involves two square roots, which are computationally expensive. However, using distance squared 
      /// provides the same information and avoids calculating two square roots.
      /// </remarks>
      public static float DistanceSquared(Vector2F value1, Vector2F value2)
      {
         float x = value1.X - value2.X;
         float y = value1.Y - value2.Y;

         return (x * x) + (y * y);
      }

      /// <summary>
      /// Calculates the dot product of two vectors.
      /// </summary>
      /// <param name="left">First source vector.</param>
      /// <param name="right">Second source vector.</param>
      /// <param name="result">When the method completes, contains the dot product of the two vectors.</param>
      public static void Dot(ref Vector2F left, ref Vector2F right, out float result)
      {
         result = (left.X * right.X) + (left.Y * right.Y);
      }

      /// <summary>
      /// Calculates the dot product of two vectors.
      /// </summary>
      /// <param name="left">First source vector.</param>
      /// <param name="right">Second source vector.</param>
      /// <returns>The dot product of the two vectors.</returns>
      public static float Dot(Vector2F left, Vector2F right)
      {
         return (left.X * right.X) + (left.Y * right.Y);
      }

      /// <summary>
      /// Converts the vector into a unit vector.
      /// </summary>
      /// <param name="value">The vector to normalize.</param>
      /// <param name="result">When the method completes, contains the normalized vector.</param>
      public static void Normalize(ref Vector2F value, out Vector2F result)
      {
         result = value;
         result.Normalize();
      }

      /// <summary>
      /// Converts the vector into a unit vector.
      /// </summary>
      /// <param name="value">The vector to normalize.</param>
      /// <returns>The normalized vector.</returns>
      public static Vector2F Normalize(Vector2F value)
      {
         value.Normalize();
         return value;
      }

      /// <summary>
      /// Performs a linear interpolation between two vectors.
      /// </summary>
      /// <param name="start">Start vector.</param>
      /// <param name="end">End vector.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <param name="result">When the method completes, contains the linear interpolation of the two vectors.</param>
      /// <remarks>
      /// Passing <paramref name="amount"/> a value of 0 will cause <paramref name="start"/> to be returned; a value of 1 will cause <paramref name="end"/> to be returned. 
      /// </remarks>
      public static void Lerp(ref Vector2F start, ref Vector2F end, float amount, out Vector2F result)
      {
         result.X = MathHelper.Lerp(start.X, end.X, amount);
         result.Y = MathHelper.Lerp(start.Y, end.Y, amount);
      }

      /// <summary>
      /// Performs a linear interpolation between two vectors.
      /// </summary>
      /// <param name="start">Start vector.</param>
      /// <param name="end">End vector.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <returns>The linear interpolation of the two vectors.</returns>
      /// <remarks>
      /// Passing <paramref name="amount"/> a value of 0 will cause <paramref name="start"/> to be returned; a value of 1 will cause <paramref name="end"/> to be returned. 
      /// </remarks>
      public static Vector2F Lerp(Vector2F start, Vector2F end, float amount)
      {
         Vector2F result;
         Lerp(ref start, ref end, amount, out result);
         return result;
      }

      /// <summary>
      /// Performs a cubic interpolation between two vectors.
      /// </summary>
      /// <param name="start">Start vector.</param>
      /// <param name="end">End vector.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <param name="result">When the method completes, contains the cubic interpolation of the two vectors.</param>
      public static void SmoothStep(ref Vector2F start, ref Vector2F end, float amount, out Vector2F result)
      {
         amount = MathHelper.SmoothStep(amount);
         Lerp(ref start, ref end, amount, out result);
      }

      /// <summary>
      /// Performs a cubic interpolation between two vectors.
      /// </summary>
      /// <param name="start">Start vector.</param>
      /// <param name="end">End vector.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <returns>The cubic interpolation of the two vectors.</returns>
      public static Vector2F SmoothStep(Vector2F start, Vector2F end, float amount)
      {
         Vector2F result;
         SmoothStep(ref start, ref end, amount, out result);
         return result;
      }

      /// <summary>
      /// Performs a Hermite spline interpolation.
      /// </summary>
      /// <param name="value1">First source position vector.</param>
      /// <param name="tangent1">First source tangent vector.</param>
      /// <param name="value2">Second source position vector.</param>
      /// <param name="tangent2">Second source tangent vector.</param>
      /// <param name="amount">Weighting factor.</param>
      /// <param name="result">When the method completes, contains the result of the Hermite spline interpolation.</param>
      public static void Hermite(ref Vector2F value1, ref Vector2F tangent1, ref Vector2F value2, ref Vector2F tangent2, float amount, out Vector2F result)
      {
         float squared = amount * amount;
         float cubed = amount * squared;
         float part1 = ((2.0f * cubed) - (3.0f * squared)) + 1.0f;
         float part2 = (-2.0f * cubed) + (3.0f * squared);
         float part3 = (cubed - (2.0f * squared)) + amount;
         float part4 = cubed - squared;

         result.X = (((value1.X * part1) + (value2.X * part2)) + (tangent1.X * part3)) + (tangent2.X * part4);
         result.Y = (((value1.Y * part1) + (value2.Y * part2)) + (tangent1.Y * part3)) + (tangent2.Y * part4);
      }

      /// <summary>
      /// Performs a Hermite spline interpolation.
      /// </summary>
      /// <param name="value1">First source position vector.</param>
      /// <param name="tangent1">First source tangent vector.</param>
      /// <param name="value2">Second source position vector.</param>
      /// <param name="tangent2">Second source tangent vector.</param>
      /// <param name="amount">Weighting factor.</param>
      /// <returns>The result of the Hermite spline interpolation.</returns>
      public static Vector2F Hermite(Vector2F value1, Vector2F tangent1, Vector2F value2, Vector2F tangent2, float amount)
      {
         Vector2F result;
         Hermite(ref value1, ref tangent1, ref value2, ref tangent2, amount, out result);
         return result;
      }

      /// <summary>
      /// Performs a Catmull-Rom interpolation using the specified positions.
      /// </summary>
      /// <param name="value1">The first position in the interpolation.</param>
      /// <param name="value2">The second position in the interpolation.</param>
      /// <param name="value3">The third position in the interpolation.</param>
      /// <param name="value4">The fourth position in the interpolation.</param>
      /// <param name="amount">Weighting factor.</param>
      /// <param name="result">When the method completes, contains the result of the Catmull-Rom interpolation.</param>
      public static void CatmullRom(ref Vector2F value1, ref Vector2F value2, ref Vector2F value3, ref Vector2F value4, float amount, out Vector2F result)
      {
         float squared = amount * amount;
         float cubed = amount * squared;

         result.X = 0.5f * ((((2.0f * value2.X) + ((-value1.X + value3.X) * amount)) +
         (((((2.0f * value1.X) - (5.0f * value2.X)) + (4.0f * value3.X)) - value4.X) * squared)) +
         ((((-value1.X + (3.0f * value2.X)) - (3.0f * value3.X)) + value4.X) * cubed));

         result.Y = 0.5f * ((((2.0f * value2.Y) + ((-value1.Y + value3.Y) * amount)) +
             (((((2.0f * value1.Y) - (5.0f * value2.Y)) + (4.0f * value3.Y)) - value4.Y) * squared)) +
             ((((-value1.Y + (3.0f * value2.Y)) - (3.0f * value3.Y)) + value4.Y) * cubed));
      }

      /// <summary>
      /// Performs a Catmull-Rom interpolation using the specified positions.
      /// </summary>
      /// <param name="value1">The first position in the interpolation.</param>
      /// <param name="value2">The second position in the interpolation.</param>
      /// <param name="value3">The third position in the interpolation.</param>
      /// <param name="value4">The fourth position in the interpolation.</param>
      /// <param name="amount">Weighting factor.</param>
      /// <returns>A vector that is the result of the Catmull-Rom interpolation.</returns>
      public static Vector2F CatmullRom(Vector2F value1, Vector2F value2, Vector2F value3, Vector2F value4, float amount)
      {
         Vector2F result;
         CatmullRom(ref value1, ref value2, ref value3, ref value4, amount, out result);
         return result;
      }

      /// <summary>
      /// Returns a vector containing the largest components of the specified vectors.
      /// </summary>
      /// <param name="left">The first source vector.</param>
      /// <param name="right">The second source vector.</param>
      /// <param name="result">When the method completes, contains an new vector composed of the largest components of the source vectors.</param>
      public static void Max(ref Vector2F left, ref Vector2F right, out Vector2F result)
      {
         result.X = (left.X > right.X) ? left.X : right.X;
         result.Y = (left.Y > right.Y) ? left.Y : right.Y;
      }

      /// <summary>
      /// Returns a vector containing the largest components of the specified vectors.
      /// </summary>
      /// <param name="left">The first source vector.</param>
      /// <param name="right">The second source vector.</param>
      /// <returns>A vector containing the largest components of the source vectors.</returns>
      public static Vector2F Max(Vector2F left, Vector2F right)
      {
         Vector2F result;
         Max(ref left, ref right, out result);
         return result;
      }

       /// <summary>
       /// Find maximum value from current <see cref="Vector3F"/>
       /// </summary>
       /// <param name="value"></param>
       /// <returns>Maximum value from all components</returns>
       public static float Max(Vector2F value)
       {
           return Max(ref value);
       }

       /// <summary>
       /// Find maximum value from current <see cref="Vector3F"/>
       /// </summary>
       /// <param name="value"></param>
       /// <returns>Maximum value from all components</returns>
       public static float Max(ref Vector2F value)
       {
           float max = value[0];
           for (int i = 1; i < 2; ++i)
           {
               if (value[i] > max)
               {
                   max = value[i];
               }
           }
           return max;
       }

       public static float Average(ref Vector2F value)
       {
           return (value.X + value.Y) / 2;
       }


       public static float Average(Vector2F value)
       {
           return Average(ref value);
       }

        /// <summary>
        /// Returns a vector containing the smallest components of the specified vectors.
        /// </summary>
        /// <param name="left">The first source vector.</param>
        /// <param name="right">The second source vector.</param>
        /// <param name="result">When the method completes, contains an new vector composed of the smallest components of the source vectors.</param>
        public static void Min(ref Vector2F left, ref Vector2F right, out Vector2F result)
      {
         result.X = (left.X < right.X) ? left.X : right.X;
         result.Y = (left.Y < right.Y) ? left.Y : right.Y;
      }

      /// <summary>
      /// Returns a vector containing the smallest components of the specified vectors.
      /// </summary>
      /// <param name="left">The first source vector.</param>
      /// <param name="right">The second source vector.</param>
      /// <returns>A vector containing the smallest components of the source vectors.</returns>
      public static Vector2F Min(Vector2F left, Vector2F right)
      {
         Vector2F result;
         Min(ref left, ref right, out result);
         return result;
      }

      /// <summary>
      /// Returns the reflection of a vector off a surface that has the specified normal. 
      /// </summary>
      /// <param name="vector">The source vector.</param>
      /// <param name="normal">Normal of the surface.</param>
      /// <param name="result">When the method completes, contains the reflected vector.</param>
      /// <remarks>Reflect only gives the direction of a reflection off a surface, it does not determine 
      /// whether the original vector was close enough to the surface to hit it.</remarks>
      public static void Reflect(ref Vector2F vector, ref Vector2F normal, out Vector2F result)
      {
         float dot = (vector.X * normal.X) + (vector.Y * normal.Y);

         result.X = vector.X - ((2.0f * dot) * normal.X);
         result.Y = vector.Y - ((2.0f * dot) * normal.Y);
      }

      /// <summary>
      /// Returns the reflection of a vector off a surface that has the specified normal. 
      /// </summary>
      /// <param name="vector">The source vector.</param>
      /// <param name="normal">Normal of the surface.</param>
      /// <returns>The reflected vector.</returns>
      /// <remarks>Reflect only gives the direction of a reflection off a surface, it does not determine 
      /// whether the original vector was close enough to the surface to hit it.</remarks>
      public static Vector2F Reflect(Vector2F vector, Vector2F normal)
      {
         Vector2F result;
         Reflect(ref vector, ref normal, out result);
         return result;
      }

      public static float Determinant(Vector2F v1, Vector2F v2)
      {
         return v1.X * v2.Y - v1.Y * v2.X;
      }

      /// <summary>
      /// Orthogonalizes a list of vectors.
      /// </summary>
      /// <param name="destination">The list of orthogonalized vectors.</param>
      /// <param name="source">The list of vectors to orthogonalize.</param>
      /// <remarks>
      /// <para>Orthogonalization is the process of making all vectors orthogonal to each other. This
      /// means that any given vector in the list will be orthogonal to any other given vector in the
      /// list.</para>
      /// <para>Because this method uses the modified Gram-Schmidt process, the resulting vectors
      /// tend to be numerically unstable. The numeric stability decreases according to the vectors
      /// position in the list so that the first vector is the most stable and the last vector is the
      /// least stable.</para>
      /// </remarks>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="destination"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
      public static void Orthogonalize(Vector2F[] destination, params Vector2F[] source)
      {
         //Uses the modified Gram-Schmidt process.
         //q1 = m1
         //q2 = m2 - ((q1 ⋅ m2) / (q1 ⋅ q1)) * q1
         //q3 = m3 - ((q1 ⋅ m3) / (q1 ⋅ q1)) * q1 - ((q2 ⋅ m3) / (q2 ⋅ q2)) * q2
         //q4 = m4 - ((q1 ⋅ m4) / (q1 ⋅ q1)) * q1 - ((q2 ⋅ m4) / (q2 ⋅ q2)) * q2 - ((q3 ⋅ m4) / (q3 ⋅ q3)) * q3
         //q5 = ...

         if (source == null)
            throw new ArgumentNullException("source");
         if (destination == null)
            throw new ArgumentNullException("destination");
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException("destination", "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            Vector2F newvector = source[i];

            for (int r = 0; r < i; ++r)
            {
               newvector -= (Vector2F.Dot(destination[r], newvector) / Vector2F.Dot(destination[r], destination[r])) * destination[r];
            }

            destination[i] = newvector;
         }
      }

      /// <summary>
      /// Orthonormalizes a list of vectors.
      /// </summary>
      /// <param name="destination">The list of orthonormalized vectors.</param>
      /// <param name="source">The list of vectors to orthonormalize.</param>
      /// <remarks>
      /// <para>Orthonormalization is the process of making all vectors orthogonal to each
      /// other and making all vectors of unit length. This means that any given vector will
      /// be orthogonal to any other given vector in the list.</para>
      /// <para>Because this method uses the modified Gram-Schmidt process, the resulting vectors
      /// tend to be numerically unstable. The numeric stability decreases according to the vectors
      /// position in the list so that the first vector is the most stable and the last vector is the
      /// least stable.</para>
      /// </remarks>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="destination"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
      public static void Orthonormalize(Vector2F[] destination, params Vector2F[] source)
      {
         //Uses the modified Gram-Schmidt process.
         //Because we are making unit vectors, we can optimize the math for orthogonalization
         //and simplify the projection operation to remove the division.
         //q1 = m1 / |m1|
         //q2 = (m2 - (q1 ⋅ m2) * q1) / |m2 - (q1 ⋅ m2) * q1|
         //q3 = (m3 - (q1 ⋅ m3) * q1 - (q2 ⋅ m3) * q2) / |m3 - (q1 ⋅ m3) * q1 - (q2 ⋅ m3) * q2|
         //q4 = (m4 - (q1 ⋅ m4) * q1 - (q2 ⋅ m4) * q2 - (q3 ⋅ m4) * q3) / |m4 - (q1 ⋅ m4) * q1 - (q2 ⋅ m4) * q2 - (q3 ⋅ m4) * q3|
         //q5 = ...

         if (source == null)
            throw new ArgumentNullException("source");
         if (destination == null)
            throw new ArgumentNullException("destination");
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException("destination", "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            Vector2F newvector = source[i];

            for (int r = 0; r < i; ++r)
            {
               newvector -= Vector2F.Dot(destination[r], newvector) * destination[r];
            }

            newvector.Normalize();
            destination[i] = newvector;
         }
      }

      /// <summary>
      /// Transforms a 2D vector by the given <see cref="QuaternionF"/> rotation.
      /// </summary>
      /// <param name="vector">The vector to rotate.</param>
      /// <param name="rotation">The <see cref="QuaternionF"/> rotation to apply.</param>
      /// <param name="result">When the method completes, contains the transformed <see cref="Vector4F"/>.</param>
      public static void Transform(ref Vector2F vector, ref QuaternionF rotation, out Vector2F result)
      {
         float x = rotation.X + rotation.X;
         float y = rotation.Y + rotation.Y;
         float z = rotation.Z + rotation.Z;
         float wz = rotation.W * z;
         float xx = rotation.X * x;
         float xy = rotation.X * y;
         float yy = rotation.Y * y;
         float zz = rotation.Z * z;

         result = new Vector2F((vector.X * (1.0f - yy - zz)) + (vector.Y * (xy - wz)), (vector.X * (xy + wz)) + (vector.Y * (1.0f - xx - zz)));
      }

      /// <summary>
      /// Transforms a 2D vector by the given <see cref="QuaternionF"/> rotation.
      /// </summary>
      /// <param name="vector">The vector to rotate.</param>
      /// <param name="rotation">The <see cref="QuaternionF"/> rotation to apply.</param>
      /// <returns>The transformed <see cref="Vector4F"/>.</returns>
      public static Vector2F Transform(Vector2F vector, QuaternionF rotation)
      {
         Vector2F result;
         Transform(ref vector, ref rotation, out result);
         return result;
      }

      /// <summary>
      /// Transforms an array of vectors by the given <see cref="QuaternionF"/> rotation.
      /// </summary>
      /// <param name="source">The array of vectors to transform.</param>
      /// <param name="rotation">The <see cref="QuaternionF"/> rotation to apply.</param>
      /// <param name="destination">The array for which the transformed vectors are stored.
      /// This array may be the same array as <paramref name="source"/>.</param>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="destination"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
      public static void Transform(Vector2F[] source, ref QuaternionF rotation, Vector2F[] destination)
      {
         if (source == null)
            throw new ArgumentNullException(nameof(source));
         if (destination == null)
            throw new ArgumentNullException(nameof(destination));
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

         float x = rotation.X + rotation.X;
         float y = rotation.Y + rotation.Y;
         float z = rotation.Z + rotation.Z;
         float wz = rotation.W * z;
         float xx = rotation.X * x;
         float xy = rotation.X * y;
         float yy = rotation.Y * y;
         float zz = rotation.Z * z;

         float num1 = (1.0f - yy - zz);
         float num2 = (xy - wz);
         float num3 = (xy + wz);
         float num4 = (1.0f - xx - zz);

         for (int i = 0; i < source.Length; ++i)
         {
            destination[i] = new Vector2F(
                (source[i].X * num1) + (source[i].Y * num2),
                (source[i].X * num3) + (source[i].Y * num4));
         }
      }

      /// <summary>
      /// Transforms a 2D vector by the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="vector">The source vector.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <param name="result">When the method completes, contains the transformed <see cref="Vector4F"/>.</param>
      public static void Transform(ref Vector2F vector, ref Matrix4x4F transform, out Vector4F result)
      {
         result = new Vector4F(
             (vector.X * transform.M11) + (vector.Y * transform.M21) + transform.M41,
             (vector.X * transform.M12) + (vector.Y * transform.M22) + transform.M42,
             (vector.X * transform.M13) + (vector.Y * transform.M23) + transform.M43,
             (vector.X * transform.M14) + (vector.Y * transform.M24) + transform.M44);
      }

      /// <summary>
      /// Transforms a 2D vector by the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="vector">The source vector.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <returns>The transformed <see cref="Vector4F"/>.</returns>
      public static Vector4F Transform(Vector2F vector, Matrix4x4F transform)
      {
         Vector4F result;
         Transform(ref vector, ref transform, out result);
         return result;
      }

      /// <summary>
      /// Transforms an array of 2D vectors by the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="source">The array of vectors to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <param name="destination">The array for which the transformed vectors are stored.</param>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="destination"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
      public static void Transform(Vector2F[] source, ref Matrix4x4F transform, Vector4F[] destination)
      {
         if (source == null)
            throw new ArgumentNullException("source");
         if (destination == null)
            throw new ArgumentNullException("destination");
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException("destination", "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            Transform(ref source[i], ref transform, out destination[i]);
         }
      }

      /// <summary>
      /// Performs a coordinate transformation using the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="coordinate">The coordinate vector to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <param name="result">When the method completes, contains the transformed coordinates.</param>
      /// <remarks>
      /// A coordinate transform performs the transformation with the assumption that the w component
      /// is one. The four dimensional vector obtained from the transformation operation has each
      /// component in the vector divided by the w component. This forces the w component to be one and
      /// therefore makes the vector homogeneous. The homogeneous vector is often preferred when working
      /// with coordinates as the w component can safely be ignored.
      /// </remarks>
      public static void TransformCoordinate(ref Vector2F coordinate, ref Matrix4x4F transform, out Vector2F result)
      {
         Vector4F vector = new Vector4F();
         vector.X = (coordinate.X * transform.M11) + (coordinate.Y * transform.M21) + transform.M41;
         vector.Y = (coordinate.X * transform.M12) + (coordinate.Y * transform.M22) + transform.M42;
         vector.Z = (coordinate.X * transform.M13) + (coordinate.Y * transform.M23) + transform.M43;
         vector.W = 1f / ((coordinate.X * transform.M14) + (coordinate.Y * transform.M24) + transform.M44);

         result = new Vector2F(vector.X * vector.W, vector.Y * vector.W);
      }

      /// <summary>
      /// Performs a coordinate transformation using the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="coordinate">The coordinate vector to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <returns>The transformed coordinates.</returns>
      /// <remarks>
      /// A coordinate transform performs the transformation with the assumption that the w component
      /// is one. The four dimensional vector obtained from the transformation operation has each
      /// component in the vector divided by the w component. This forces the w component to be one and
      /// therefore makes the vector homogeneous. The homogeneous vector is often preferred when working
      /// with coordinates as the w component can safely be ignored.
      /// </remarks>
      public static Vector2F TransformCoordinate(Vector2F coordinate, Matrix4x4F transform)
      {
         Vector2F result;
         TransformCoordinate(ref coordinate, ref transform, out result);
         return result;
      }

      /// <summary>
      /// Performs a coordinate transformation on an array of vectors using the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="source">The array of coordinate vectors to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <param name="destination">The array for which the transformed vectors are stored.
      /// This array may be the same array as <paramref name="source"/>.</param>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="destination"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
      /// <remarks>
      /// A coordinate transform performs the transformation with the assumption that the w component
      /// is one. The four dimensional vector obtained from the transformation operation has each
      /// component in the vector divided by the w component. This forces the w component to be one and
      /// therefore makes the vector homogeneous. The homogeneous vector is often preferred when working
      /// with coordinates as the w component can safely be ignored.
      /// </remarks>
      public static void TransformCoordinate(Vector2F[] source, ref Matrix4x4F transform, Vector2F[] destination)
      {
         if (source == null)
            throw new ArgumentNullException("source");
         if (destination == null)
            throw new ArgumentNullException("destination");
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException("destination", "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            TransformCoordinate(ref source[i], ref transform, out destination[i]);
         }
      }

      /// <summary>
      /// Performs a normal transformation using the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="normal">The normal vector to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <param name="result">When the method completes, contains the transformed normal.</param>
      /// <remarks>
      /// A normal transform performs the transformation with the assumption that the w component
      /// is zero. This causes the fourth row and fourth column of the matrix to be unused. The
      /// end result is a vector that is not translated, but all other transformation properties
      /// apply. This is often preferred for normal vectors as normals purely represent direction
      /// rather than location because normal vectors should not be translated.
      /// </remarks>
      public static void TransformNormal(ref Vector2F normal, ref Matrix4x4F transform, out Vector2F result)
      {
         result = new Vector2F(
             (normal.X * transform.M11) + (normal.Y * transform.M21),
             (normal.X * transform.M12) + (normal.Y * transform.M22));
      }

      /// <summary>
      /// Performs a normal transformation using the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="normal">The normal vector to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <returns>The transformed normal.</returns>
      /// <remarks>
      /// A normal transform performs the transformation with the assumption that the w component
      /// is zero. This causes the fourth row and fourth column of the matrix to be unused. The
      /// end result is a vector that is not translated, but all other transformation properties
      /// apply. This is often preferred for normal vectors as normals purely represent direction
      /// rather than location because normal vectors should not be translated.
      /// </remarks>
      public static Vector2F TransformNormal(Vector2F normal, Matrix4x4F transform)
      {
         Vector2F result;
         TransformNormal(ref normal, ref transform, out result);
         return result;
      }

      /// <summary>
      /// Performs a normal transformation on an array of vectors using the given <see cref="Matrix4x4F"/>.
      /// </summary>
      /// <param name="source">The array of normal vectors to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4F"/>.</param>
      /// <param name="destination">The array for which the transformed vectors are stored.
      /// This array may be the same array as <paramref name="source"/>.</param>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="destination"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
      /// <remarks>
      /// A normal transform performs the transformation with the assumption that the w component
      /// is zero. This causes the fourth row and fourth column of the matrix to be unused. The
      /// end result is a vector that is not translated, but all other transformation properties
      /// apply. This is often preferred for normal vectors as normals purely represent direction
      /// rather than location because normal vectors should not be translated.
      /// </remarks>
      public static void TransformNormal(Vector2F[] source, ref Matrix4x4F transform, Vector2F[] destination)
      {
         if (source == null)
            throw new ArgumentNullException("source");
         if (destination == null)
            throw new ArgumentNullException("destination");
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException("destination", "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            TransformNormal(ref source[i], ref transform, out destination[i]);
         }
      }

      /// <summary>
      /// Adds two vectors.
      /// </summary>
      /// <param name="left">The first vector to add.</param>
      /// <param name="right">The second vector to add.</param>
      /// <returns>The sum of the two vectors.</returns>
      public static Vector2F operator +(Vector2F left, Vector2F right)
      {
         return new Vector2F(left.X + right.X, left.Y + right.Y);
      }

      /// <summary>
      /// Multiplies a vector with another by performing component-wise multiplication equivalent to <see cref="Multiply(ref Vector2F,ref Vector2F,out Vector2F)"/>.
      /// </summary>
      /// <param name="left">The first vector to multiply.</param>
      /// <param name="right">The second vector to multiply.</param>
      /// <returns>The multiplication of the two vectors.</returns>
      public static Vector2F operator *(Vector2F left, Vector2F right)
      {
         return new Vector2F(left.X * right.X, left.Y * right.Y);
      }

      /// <summary>
      /// Assert a vector (return it unchanged).
      /// </summary>
      /// <param name="value">The vector to assert (unchanged).</param>
      /// <returns>The asserted (unchanged) vector.</returns>
      public static Vector2F operator +(Vector2F value)
      {
         return value;
      }

      /// <summary>
      /// Subtracts two vectors.
      /// </summary>
      /// <param name="left">The first vector to subtract.</param>
      /// <param name="right">The second vector to subtract.</param>
      /// <returns>The difference of the two vectors.</returns>
      public static Vector2F operator -(Vector2F left, Vector2F right)
      {
         return new Vector2F(left.X - right.X, left.Y - right.Y);
      }

      /// <summary>
      /// Reverses the direction of a given vector.
      /// </summary>
      /// <param name="value">The vector to negate.</param>
      /// <returns>A vector facing in the opposite direction.</returns>
      public static Vector2F operator -(Vector2F value)
      {
         return new Vector2F(-value.X, -value.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      public static Vector2F operator *(float scale, Vector2F value)
      {
         return new Vector2F(value.X * scale, value.Y * scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      public static Vector2F operator *(Vector2F value, float scale)
      {
         return new Vector2F(value.X * scale, value.Y * scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      public static Vector2F operator /(Vector2F value, float scale)
      {
         return new Vector2F(value.X / scale, value.Y / scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <param name="value">The vector to scale.</param>  
      /// <returns>The scaled vector.</returns>
      public static Vector2F operator /(float scale, Vector2F value)
      {
         return new Vector2F(scale / value.X, scale / value.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      public static Vector2F operator /(Vector2F value, Vector2F scale)
      {
         return new Vector2F(value.X / scale.X, value.Y / scale.Y);
      }

      /// <summary>
      /// Perform a component-wise addition
      /// </summary>
      /// <param name="value">The input vector.</param>
      /// <param name="scalar">The scalar value to be added on elements</param>
      /// <returns>The vector with added scalar for each element.</returns>
      public static Vector2F operator +(Vector2F value, float scalar)
      {
         return new Vector2F(value.X + scalar, value.Y + scalar);
      }

      /// <summary>
      /// Perform a component-wise addition
      /// </summary>
      /// <param name="value">The input vector.</param>
      /// <param name="scalar">The scalar value to be added on elements</param>
      /// <returns>The vector with added scalar for each element.</returns>
      public static Vector2F operator +(float scalar, Vector2F value)
      {
         return new Vector2F(scalar + value.X, scalar + value.Y);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="value">The input vector.</param>
      /// <param name="scalar">The scalar value to be subtraced from elements</param>
      /// <returns>The vector with subtraced scalar from each element.</returns>
      public static Vector2F operator -(Vector2F value, float scalar)
      {
         return new Vector2F(value.X - scalar, value.Y - scalar);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="value">The input vector.</param>
      /// <param name="scalar">The scalar value to be subtraced from elements</param>
      /// <returns>The vector with subtraced scalar from each element.</returns>
      public static Vector2F operator -(float scalar, Vector2F value)
      {
         return new Vector2F(scalar - value.X, scalar - value.Y);
      }

      /// <summary>
      /// Tests for equality between two objects.
      /// </summary>
      /// <param name="left">The first value to compare.</param>
      /// <param name="right">The second value to compare.</param>
      /// <returns><c>true</c> if <paramref name="left"/> has the same value as <paramref name="right"/>; otherwise, <c>false</c>.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool operator ==(Vector2F left, Vector2F right)
      {
         return left.Equals(ref right);
      }

      /// <summary>
      /// Tests for inequality between two objects.
      /// </summary>
      /// <param name="left">The first value to compare.</param>
      /// <param name="right">The second value to compare.</param>
      /// <returns><c>true</c> if <paramref name="left"/> has a different value than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool operator !=(Vector2F left, Vector2F right)
      {
         return !left.Equals(ref right);
      }

      public static explicit operator Vector2(Vector2F value)
      {
         return new Vector2(value.X, value.Y);
      }
      
      /// <summary>
      /// Performs an explicit conversion from <see cref="Vector2F"/> to <see cref="Vector3F"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static explicit operator Vector3F(Vector2F value)
      {
         return new Vector3F(value, 0.0f);
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="Vector2F"/> to <see cref="Vector4F"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static explicit operator Vector4F(Vector2F value)
      {
         return new Vector4F(value, 0.0f, 0.0f);
      }

      /// <summary>
      /// Returns a <see cref="System.String"/> that represents this instance.
      /// </summary>
      /// <returns>
      /// A <see cref="System.String"/> that represents this instance.
      /// </returns>
      public override string ToString()
      {
         return String.Format(CultureInfo.CurrentCulture, "X:{0} Y:{1}", X, Y);
      }

      /// <summary>
      /// Returns a <see cref="System.String"/> that represents this instance.
      /// </summary>
      /// <param name="format">The format.</param>
      /// <returns>
      /// A <see cref="System.String"/> that represents this instance.
      /// </returns>
      public string ToString(string format)
      {
         if (format == null)
            return ToString();

         return String.Format(CultureInfo.CurrentCulture, "X:{0} Y:{1}", X.ToString(format, CultureInfo.CurrentCulture), Y.ToString(format, CultureInfo.CurrentCulture));
      }

      /// <summary>
      /// Returns a <see cref="System.String"/> that represents this instance.
      /// </summary>
      /// <param name="formatProvider">The format provider.</param>
      /// <returns>
      /// A <see cref="System.String"/> that represents this instance.
      /// </returns>
      public string ToString(IFormatProvider formatProvider)
      {
         return String.Format(formatProvider, "X:{0} Y:{1}", X, Y);
      }

      /// <summary>
      /// Returns a <see cref="System.String"/> that represents this instance.
      /// </summary>
      /// <param name="format">The format.</param>
      /// <param name="formatProvider">The format provider.</param>
      /// <returns>
      /// A <see cref="System.String"/> that represents this instance.
      /// </returns>
      public string ToString(string format, IFormatProvider formatProvider)
      {
         if (format == null)
            ToString(formatProvider);

         return String.Format(formatProvider, "X:{0} Y:{1}", X.ToString(format, formatProvider), Y.ToString(format, formatProvider));
      }

      /// <summary>
      /// Returns a hash code for this instance.
      /// </summary>
      /// <returns>
      /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
      /// </returns>
      public override int GetHashCode()
      {
         unchecked
         {
            return (X.GetHashCode() * 397) ^ Y.GetHashCode();
         }
      }

      /// <summary>
      /// Determines whether the specified <see cref="Vector2F"/> is equal to this instance.
      /// </summary>
      /// <param name="other">The <see cref="Vector2F"/> to compare with this instance.</param>
      /// <returns>
      /// 	<c>true</c> if the specified <see cref="Vector2F"/> is equal to this instance; otherwise, <c>false</c>.
      /// </returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Equals(ref Vector2F other)
      {
         return MathHelper.NearEqual(other.X, X) && MathHelper.NearEqual(other.Y, Y);
      }

      /// <summary>
      /// Determines whether the specified <see cref="Vector2F"/> is equal to this instance.
      /// </summary>
      /// <param name="other">The <see cref="Vector2F"/> to compare with this instance.</param>
      /// <returns>
      /// 	<c>true</c> if the specified <see cref="Vector2F"/> is equal to this instance; otherwise, <c>false</c>.
      /// </returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Equals(Vector2F other)
      {
         return Equals(ref other);
      }

      /// <summary>
      /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
      /// </summary>
      /// <param name="value">The <see cref="System.Object"/> to compare with this instance.</param>
      /// <returns>
      /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
      /// </returns>
      public override bool Equals(object value)
      {
         if (!(value is Vector2F))
            return false;

         var strongValue = (Vector2F)value;
         return Equals(ref strongValue);
      }

   }
}
