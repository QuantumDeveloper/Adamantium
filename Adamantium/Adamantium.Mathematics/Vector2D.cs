using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Adamantium.Mathematics
{
   public struct Vector2D
   {
      /// <summary>
      /// The size of the <see cref="Vector2D"/> type, in bytes.
      /// </summary>
      public static readonly int SizeInBytes = Marshal.SizeOf(typeof(Vector2D));

      /// <summary>
      /// A <see cref="Vector2D"/> with all of its components set to zero.
      /// </summary>
      public static readonly Vector2D Zero;

      /// <summary>
      /// The X unit <see cref="Vector2D"/> (1, 0).
      /// </summary>
      public static readonly Vector2D UnitX = new(1.0f, 0.0f);

      /// <summary>
      /// The Y unit <see cref="Vector2D"/> (0, 1).
      /// </summary>
      public static readonly Vector2D UnitY = new(0.0f, 1.0f);

      /// <summary>
      /// A <see cref="Vector2D"/> with all of its components set to one.
      /// </summary>
      public static readonly Vector2D One = new(1.0f, 1.0f);

      public double X;

      public double Y;

      public Vector2D(double value)
      {
         X = Y = value;
      }

      public Vector2D(double x, double y)
      {
         X = x;
         Y = y;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="Vector2D"/> struct.
      /// </summary>
      /// <param name="values">The values to assign to the X and Y components of the vector. This must be an array with two elements.</param>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="values"/> contains more or less than two elements.</exception>
      public Vector2D(double[] values)
      {
         if (values == null)
            throw new ArgumentNullException(nameof(values));
         if (values.Length < 2)
            throw new ArgumentOutOfRangeException(nameof(values), "There must be not less than two input values for Vector2D.");

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
      public double this[int index]
      {
         get
         {
            switch (index)
            {
               case 0: return X;
               case 1: return Y;
            }

            throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vector2D run from 0 to 1, inclusive.");
         }

         set
         {
            switch (index)
            {
               case 0: X = value; break;
               case 1: Y = value; break;
               default: throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vector2D run from 0 to 1, inclusive.");
            }
         }
      }

      /// <summary>
      /// Calculates the length of the vector.
      /// </summary>
      /// <returns>The length of the vector.</returns>
      /// <remarks>
      /// <see cref="Vector2D.LengthSquared"/> may be preferred when only the relative length is needed
      /// and speed is of the essence.
      /// </remarks>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public double Length()
      {
         return Math.Sqrt((X * X) + (Y * Y));
      }

      /// <summary>
      /// Calculates the squared length of the vector.
      /// </summary>
      /// <returns>The squared length of the vector.</returns>
      /// <remarks>
      /// This method may be preferred to <see cref="Vector2D.Length"/> when only a relative length is needed
      /// and speed is of the essence.
      /// </remarks>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public double LengthSquared()
      {
         return (X * X) + (Y * Y);
      }

      /// <summary>
      /// Converts the vector into a unit vector.
      /// </summary>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public void Normalize()
      {
         double length = Length();
         if (!MathHelper.IsZero(length))
         {
            double inv = 1.0f / length;
            X *= inv;
            Y *= inv;
         }
      }

      /// <summary>
      /// Creates an array containing the elements of the vector.
      /// </summary>
      /// <returns>A two-element array containing the components of the vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public double[] ToArray()
      {
         return new[] { X, Y };
      }
      
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool IsCollinear(Vector2D vector)
      {
         return MathHelper.NearEqual(X * vector.Y, Y * vector.X);
      }

      /// <summary>
      /// Adds two vectors.
      /// </summary>
      /// <param name="left">The first vector to add.</param>
      /// <param name="right">The second vector to add.</param>
      /// <param name="result">When the method completes, contains the sum of the two vectors.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Add(ref Vector2D left, ref Vector2D right, out Vector2D result)
      {
         result = new Vector2D(left.X + right.X, left.Y + right.Y);
      }

      /// <summary>
      /// Adds two vectors.
      /// </summary>
      /// <param name="left">The first vector to add.</param>
      /// <param name="right">The second vector to add.</param>
      /// <returns>The sum of the two vectors.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Add(Vector2D left, Vector2D right)
      {
         return new(left.X + right.X, left.Y + right.Y);
      }

      /// <summary>
      /// Perform a component-wise addition
      /// </summary>
      /// <param name="left">The input vector</param>
      /// <param name="right">The scalar value to be added to elements</param>
      /// <param name="result">The vector with added scalar for each element.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Add(ref Vector2D left, ref double right, out Vector2D result)
      {
         result = new Vector2D(left.X + right, left.Y + right);
      }

      /// <summary>
      /// Perform a component-wise addition
      /// </summary>
      /// <param name="left">The input vector</param>
      /// <param name="right">The scalar value to be added to elements</param>
      /// <returns>The vector with added scalar for each element.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Add(Vector2D left, double right)
      {
         return new(left.X + right, left.Y + right);
      }

      /// <summary>
      /// Subtracts two vectors.
      /// </summary>
      /// <param name="left">The first vector to subtract.</param>
      /// <param name="right">The second vector to subtract.</param>
      /// <param name="result">When the method completes, contains the difference of the two vectors.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Subtract(ref Vector2D left, ref Vector2D right, out Vector2D result)
      {
         result = new Vector2D(left.X - right.X, left.Y - right.Y);
      }

      /// <summary>
      /// Subtracts two vectors.
      /// </summary>
      /// <param name="left">The first vector to subtract.</param>
      /// <param name="right">The second vector to subtract.</param>
      /// <returns>The difference of the two vectors.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Subtract(Vector2D left, Vector2D right)
      {
         return new(left.X - right.X, left.Y - right.Y);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="left">The input vector</param>
      /// <param name="right">The scalar value to be subtraced from elements</param>
      /// <param name="result">The vector with subtracted scalar for each element.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Subtract(ref Vector2D left, ref double right, out Vector2D result)
      {
         result = new Vector2D(left.X - right, left.Y - right);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="left">The input vector</param>
      /// <param name="right">The scalar value to be subtraced from elements</param>
      /// <returns>The vector with subtracted scalar for each element.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Subtract(Vector2D left, double right)
      {
         return new(left.X - right, left.Y - right);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="left">The scalar value to be subtraced from elements</param>
      /// <param name="right">The input vector</param>
      /// <param name="result">The vector with subtracted scalar for each element.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Subtract(ref double left, ref Vector2D right, out Vector2D result)
      {
         result = new Vector2D(left - right.X, left - right.Y);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="left">The scalar value to be subtraced from elements</param>
      /// <param name="right">The input vector</param>
      /// <returns>The vector with subtracted scalar for each element.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Subtract(double left, Vector2D right)
      {
         return new(left - right.X, left - right.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <param name="result">When the method completes, contains the scaled vector.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Multiply(ref Vector2D value, double scale, out Vector2D result)
      {
         result = new Vector2D(value.X * scale, value.Y * scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Multiply(Vector2D value, double scale)
      {
         return new(value.X * scale, value.Y * scale);
      }

      /// <summary>
      /// Multiplies a vector with another by performing component-wise multiplication.
      /// </summary>
      /// <param name="left">The first vector to multiply.</param>
      /// <param name="right">The second vector to multiply.</param>
      /// <param name="result">When the method completes, contains the multiplied vector.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Multiply(ref Vector2D left, ref Vector2D right, out Vector2D result)
      {
         result = new Vector2D(left.X * right.X, left.Y * right.Y);
      }

      /// <summary>
      /// Multiplies a vector with another by performing component-wise multiplication.
      /// </summary>
      /// <param name="left">The first vector to multiply.</param>
      /// <param name="right">The second vector to multiply.</param>
      /// <returns>The multiplied vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Multiply(Vector2D left, Vector2D right)
      {
         return new(left.X * right.X, left.Y * right.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <param name="result">When the method completes, contains the scaled vector.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Divide(ref Vector2D value, double scale, out Vector2D result)
      {
         result = new Vector2D(value.X / scale, value.Y / scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Divide(Vector2D value, double scale)
      {
         return new(value.X / scale, value.Y / scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <param name="value">The vector to scale.</param>
      /// <param name="result">When the method completes, contains the scaled vector.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Divide(double scale, ref Vector2D value, out Vector2D result)
      {
         result = new Vector2D(scale / value.X, scale / value.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Divide(double scale, Vector2D value)
      {
         return new(scale / value.X, scale / value.Y);
      }

      /// <summary>
      /// Reverses the direction of a given vector.
      /// </summary>
      /// <param name="value">The vector to negate.</param>
      /// <param name="result">When the method completes, contains a vector facing in the opposite direction.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Negate(ref Vector2D value, out Vector2D result)
      {
         result = new Vector2D(-value.X, -value.Y);
      }

      /// <summary>
      /// Reverses the direction of a given vector.
      /// </summary>
      /// <param name="value">The vector to negate.</param>
      /// <returns>A vector facing in the opposite direction.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Negate(Vector2D value)
      {
         return new(-value.X, -value.Y);
      }

      /// <summary>
      /// Returns a <see cref="Vector2D"/> containing the 2D Cartesian coordinates of a point specified in Barycentric coordinates relative to a 2D triangle.
      /// </summary>
      /// <param name="value1">A <see cref="Vector2D"/> containing the 2D Cartesian coordinates of vertex 1 of the triangle.</param>
      /// <param name="value2">A <see cref="Vector2D"/> containing the 2D Cartesian coordinates of vertex 2 of the triangle.</param>
      /// <param name="value3">A <see cref="Vector2D"/> containing the 2D Cartesian coordinates of vertex 3 of the triangle.</param>
      /// <param name="amount1">Barycentric coordinate b2, which expresses the weighting factor toward vertex 2 (specified in <paramref name="value2"/>).</param>
      /// <param name="amount2">Barycentric coordinate b3, which expresses the weighting factor toward vertex 3 (specified in <paramref name="value3"/>).</param>
      /// <param name="result">When the method completes, contains the 2D Cartesian coordinates of the specified point.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Barycentric(ref Vector2D value1, ref Vector2D value2, ref Vector2D value3, double amount1, double amount2, out Vector2D result)
      {
         result = new Vector2D((value1.X + (amount1 * (value2.X - value1.X))) + (amount2 * (value3.X - value1.X)),
             (value1.Y + (amount1 * (value2.Y - value1.Y))) + (amount2 * (value3.Y - value1.Y)));
      }

      /// <summary>
      /// Returns a <see cref="Vector2D"/> containing the 2D Cartesian coordinates of a point specified in Barycentric coordinates relative to a 2D triangle.
      /// </summary>
      /// <param name="value1">A <see cref="Vector2D"/> containing the 2D Cartesian coordinates of vertex 1 of the triangle.</param>
      /// <param name="value2">A <see cref="Vector2D"/> containing the 2D Cartesian coordinates of vertex 2 of the triangle.</param>
      /// <param name="value3">A <see cref="Vector2D"/> containing the 2D Cartesian coordinates of vertex 3 of the triangle.</param>
      /// <param name="amount1">Barycentric coordinate b2, which expresses the weighting factor toward vertex 2 (specified in <paramref name="value2"/>).</param>
      /// <param name="amount2">Barycentric coordinate b3, which expresses the weighting factor toward vertex 3 (specified in <paramref name="value3"/>).</param>
      /// <returns>A new <see cref="Vector2D"/> containing the 2D Cartesian coordinates of the specified point.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Barycentric(Vector2D value1, Vector2D value2, Vector2D value3, double amount1, double amount2)
      {
         Barycentric(ref value1, ref value2, ref value3, amount1, amount2, out var result);
         return result;
      }

      /// <summary>
      /// Restricts a value to be within a specified range.
      /// </summary>
      /// <param name="value">The value to clamp.</param>
      /// <param name="min">The minimum value.</param>
      /// <param name="max">The maximum value.</param>
      /// <param name="result">When the method completes, contains the clamped value.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Clamp(ref Vector2D value, ref Vector2D min, ref Vector2D max, out Vector2D result)
      {
         double x = value.X;
         x = (x > max.X) ? max.X : x;
         x = (x < min.X) ? min.X : x;

         double y = value.Y;
         y = (y > max.Y) ? max.Y : y;
         y = (y < min.Y) ? min.Y : y;

         result = new Vector2D(x, y);
      }

      /// <summary>
      /// Restricts a value to be within a specified range.
      /// </summary>
      /// <param name="value">The value to clamp.</param>
      /// <param name="min">The minimum value.</param>
      /// <param name="max">The maximum value.</param>
      /// <returns>The clamped value.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Clamp(Vector2D value, Vector2D min, Vector2D max)
      {
         Clamp(ref value, ref min, ref max, out var result);
         return result;
      }

      /// <summary>
      /// Saturates this instance in the range [0,1]
      /// </summary>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
      /// <see cref="Vector2D.DistanceSquared(ref Vector2D, ref Vector2D, out double)"/> may be preferred when only the relative distance is needed
      /// and speed is of the essence.
      /// </remarks>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Distance(ref Vector2D value1, ref Vector2D value2, out double result)
      {
         double x = value1.X - value2.X;
         double y = value1.Y - value2.Y;

         result = Math.Sqrt((x * x) + (y * y));
      }

      /// <summary>
      /// Calculates the distance between two vectors.
      /// </summary>
      /// <param name="value1">The first vector.</param>
      /// <param name="value2">The second vector.</param>
      /// <returns>The distance between the two vectors.</returns>
      /// <remarks>
      /// <see cref="Vector2D.DistanceSquared(Vector2D, Vector2D)"/> may be preferred when only the relative distance is needed
      /// and speed is of the essence.
      /// </remarks>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static double Distance(Vector2D value1, Vector2D value2)
      {
         double x = value1.X - value2.X;
         double y = value1.Y - value2.Y;

         return Math.Sqrt((x * x) + (y * y));
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void DistanceSquared(ref Vector2D value1, ref Vector2D value2, out double result)
      {
         double x = value1.X - value2.X;
         double y = value1.Y - value2.Y;

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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static double DistanceSquared(Vector2D value1, Vector2D value2)
      {
         double x = value1.X - value2.X;
         double y = value1.Y - value2.Y;

         return (x * x) + (y * y);
      }

      /// <summary>
      /// Calculates the dot product of two vectors.
      /// </summary>
      /// <param name="left">First source vector.</param>
      /// <param name="right">Second source vector.</param>
      /// <param name="result">When the method completes, contains the dot product of the two vectors.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Dot(ref Vector2D left, ref Vector2D right, out double result)
      {
         result = (left.X * right.X) + (left.Y * right.Y);
      }

      /// <summary>
      /// Calculates the dot product of two vectors.
      /// </summary>
      /// <param name="left">First source vector.</param>
      /// <param name="right">Second source vector.</param>
      /// <returns>The dot product of the two vectors.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static double Dot(Vector2D left, Vector2D right)
      {
         return (left.X * right.X) + (left.Y * right.Y);
      }

      /// <summary>
      /// Converts the vector into a unit vector.
      /// </summary>
      /// <param name="value">The vector to normalize.</param>
      /// <param name="result">When the method completes, contains the normalized vector.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Normalize(ref Vector2D value, out Vector2D result)
      {
         result = value;
         result.Normalize();
      }

      /// <summary>
      /// Converts the vector into a unit vector.
      /// </summary>
      /// <param name="value">The vector to normalize.</param>
      /// <returns>The normalized vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Normalize(Vector2D value)
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Lerp(ref Vector2D start, ref Vector2D end, double amount, out Vector2D result)
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Lerp(Vector2D start, Vector2D end, double amount)
      {
         Lerp(ref start, ref end, amount, out var result);
         return result;
      }

      /// <summary>
      /// Performs a cubic interpolation between two vectors.
      /// </summary>
      /// <param name="start">Start vector.</param>
      /// <param name="end">End vector.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <param name="result">When the method completes, contains the cubic interpolation of the two vectors.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void SmoothStep(ref Vector2D start, ref Vector2D end, double amount, out Vector2D result)
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D SmoothStep(Vector2D start, Vector2D end, double amount)
      {
         SmoothStep(ref start, ref end, amount, out var result);
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Hermite(ref Vector2D value1, ref Vector2D tangent1, ref Vector2D value2, ref Vector2D tangent2, double amount, out Vector2D result)
      {
         double squared = amount * amount;
         double cubed = amount * squared;
         double part1 = ((2.0f * cubed) - (3.0f * squared)) + 1.0f;
         double part2 = (-2.0f * cubed) + (3.0f * squared);
         double part3 = (cubed - (2.0f * squared)) + amount;
         double part4 = cubed - squared;

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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Hermite(Vector2D value1, Vector2D tangent1, Vector2D value2, Vector2D tangent2, double amount)
      {
         Hermite(ref value1, ref tangent1, ref value2, ref tangent2, amount, out var result);
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void CatmullRom(ref Vector2D value1, ref Vector2D value2, ref Vector2D value3, ref Vector2D value4, double amount, out Vector2D result)
      {
         double squared = amount * amount;
         double cubed = amount * squared;

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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D CatmullRom(Vector2D value1, Vector2D value2, Vector2D value3, Vector2D value4, double amount)
      {
         CatmullRom(ref value1, ref value2, ref value3, ref value4, amount, out var result);
         return result;
      }

      /// <summary>
      /// Returns a vector containing the largest components of the specified vectors.
      /// </summary>
      /// <param name="left">The first source vector.</param>
      /// <param name="right">The second source vector.</param>
      /// <param name="result">When the method completes, contains an new vector composed of the largest components of the source vectors.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Max(ref Vector2D left, ref Vector2D right, out Vector2D result)
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Max(Vector2D left, Vector2D right)
      {
         Max(ref left, ref right, out var result);
         return result;
      }

      /// <summary>
      /// Returns a vector containing the smallest components of the specified vectors.
      /// </summary>
      /// <param name="left">The first source vector.</param>
      /// <param name="right">The second source vector.</param>
      /// <param name="result">When the method completes, contains an new vector composed of the smallest components of the source vectors.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Min(ref Vector2D left, ref Vector2D right, out Vector2D result)
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)] 
      public static Vector2D Min(Vector2D left, Vector2D right)
      {
         Min(ref left, ref right, out var result);
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Reflect(ref Vector2D vector, ref Vector2D normal, out Vector2D result)
      {
         double dot = (vector.X * normal.X) + (vector.Y * normal.Y);

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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Reflect(Vector2D vector, Vector2D normal)
      {
         Reflect(ref vector, ref normal, out var result);
         return result;
      }

      public static double Determinant(Vector2D v1, Vector2D v2)
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Orthogonalize(Vector2D[] destination, params Vector2D[] source)
      {
         //Uses the modified Gram-Schmidt process.
         //q1 = m1
         //q2 = m2 - ((q1 ⋅ m2) / (q1 ⋅ q1)) * q1
         //q3 = m3 - ((q1 ⋅ m3) / (q1 ⋅ q1)) * q1 - ((q2 ⋅ m3) / (q2 ⋅ q2)) * q2
         //q4 = m4 - ((q1 ⋅ m4) / (q1 ⋅ q1)) * q1 - ((q2 ⋅ m4) / (q2 ⋅ q2)) * q2 - ((q3 ⋅ m4) / (q3 ⋅ q3)) * q3
         //q5 = ...

         if (source == null)
            throw new ArgumentNullException(nameof(source));
         if (destination == null)
            throw new ArgumentNullException(nameof(destination));
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            Vector2D newvector = source[i];

            for (int r = 0; r < i; ++r)
            {
               newvector -= (Vector2D.Dot(destination[r], newvector) / Vector2D.Dot(destination[r], destination[r])) * destination[r];
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Orthonormalize(Vector2D[] destination, params Vector2D[] source)
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
            throw new ArgumentNullException(nameof(source));
         if (destination == null)
            throw new ArgumentNullException(nameof(destination));
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            Vector2D newvector = source[i];

            for (int r = 0; r < i; ++r)
            {
               newvector -= Vector2D.Dot(destination[r], newvector) * destination[r];
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
      /// <param name="result">When the method completes, contains the transformed <see cref="Vector4D"/>.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Transform(ref Vector2D vector, ref QuaternionF rotation, out Vector2D result)
      {
         double x = rotation.X + rotation.X;
         double y = rotation.Y + rotation.Y;
         double z = rotation.Z + rotation.Z;
         double wz = rotation.W * z;
         double xx = rotation.X * x;
         double xy = rotation.X * y;
         double yy = rotation.Y * y;
         double zz = rotation.Z * z;

         result = new Vector2D((vector.X * (1.0f - yy - zz)) + (vector.Y * (xy - wz)), (vector.X * (xy + wz)) + (vector.Y * (1.0f - xx - zz)));
      }

      /// <summary>
      /// Transforms a 2D vector by the given <see cref="QuaternionF"/> rotation.
      /// </summary>
      /// <param name="vector">The vector to rotate.</param>
      /// <param name="rotation">The <see cref="QuaternionF"/> rotation to apply.</param>
      /// <returns>The transformed <see cref="Vector4D"/>.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D Transform(Vector2D vector, QuaternionF rotation)
      {
         Transform(ref vector, ref rotation, out var result);
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Transform(Vector2D[] source, ref QuaternionF rotation, Vector2D[] destination)
      {
         if (source == null)
            throw new ArgumentNullException(nameof(source));
         if (destination == null)
            throw new ArgumentNullException(nameof(destination));
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

         double x = rotation.X + rotation.X;
         double y = rotation.Y + rotation.Y;
         double z = rotation.Z + rotation.Z;
         double wz = rotation.W * z;
         double xx = rotation.X * x;
         double xy = rotation.X * y;
         double yy = rotation.Y * y;
         double zz = rotation.Z * z;

         double num1 = (1.0f - yy - zz);
         double num2 = (xy - wz);
         double num3 = (xy + wz);
         double num4 = (1.0f - xx - zz);

         for (int i = 0; i < source.Length; ++i)
         {
            destination[i] = new Vector2D(
                (source[i].X * num1) + (source[i].Y * num2),
                (source[i].X * num3) + (source[i].Y * num4));
         }
      }

      /// <summary>
      /// Transforms a 2D vector by the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="vector">The source vector.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
      /// <param name="result">When the method completes, contains the transformed <see cref="Vector4D"/>.</param>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Transform(ref Vector2D vector, ref Matrix4x4D transform, out Vector4D result)
      {
         result = new Vector4D(
             (vector.X * transform.M11) + (vector.Y * transform.M21) + transform.M41,
             (vector.X * transform.M12) + (vector.Y * transform.M22) + transform.M42,
             (vector.X * transform.M13) + (vector.Y * transform.M23) + transform.M43,
             (vector.X * transform.M14) + (vector.Y * transform.M24) + transform.M44);
      }

      /// <summary>
      /// Transforms a 2D vector by the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="vector">The source vector.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
      /// <returns>The transformed <see cref="Vector4D"/>.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector4D Transform(Vector2D vector, Matrix4x4D transform)
      {
         Transform(ref vector, ref transform, out var result);
         return result;
      }

      /// <summary>
      /// Transforms an array of 2D vectors by the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="source">The array of vectors to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
      /// <param name="destination">The array for which the transformed vectors are stored.</param>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="destination"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void Transform(Vector2D[] source, ref Matrix4x4D transform, Vector4D[] destination)
      {
         if (source == null)
            throw new ArgumentNullException(nameof(source));
         if (destination == null)
            throw new ArgumentNullException(nameof(destination));
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            Transform(ref source[i], ref transform, out destination[i]);
         }
      }

      /// <summary>
      /// Performs a coordinate transformation using the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="coordinate">The coordinate vector to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
      /// <param name="result">When the method completes, contains the transformed coordinates.</param>
      /// <remarks>
      /// A coordinate transform performs the transformation with the assumption that the w component
      /// is one. The four dimensional vector obtained from the transformation operation has each
      /// component in the vector divided by the w component. This forces the w component to be one and
      /// therefore makes the vector homogeneous. The homogeneous vector is often preferred when working
      /// with coordinates as the w component can safely be ignored.
      /// </remarks>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void TransformCoordinate(ref Vector2D coordinate, ref Matrix4x4D transform, out Vector2D result)
      {
         Vector4D vector = new Vector4D();
         vector.X = (coordinate.X * transform.M11) + (coordinate.Y * transform.M21) + transform.M41;
         vector.Y = (coordinate.X * transform.M12) + (coordinate.Y * transform.M22) + transform.M42;
         vector.Z = (coordinate.X * transform.M13) + (coordinate.Y * transform.M23) + transform.M43;
         vector.W = 1f / ((coordinate.X * transform.M14) + (coordinate.Y * transform.M24) + transform.M44);

         result = new Vector2D(vector.X * vector.W, vector.Y * vector.W);
      }

      /// <summary>
      /// Performs a coordinate transformation using the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="coordinate">The coordinate vector to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
      /// <returns>The transformed coordinates.</returns>
      /// <remarks>
      /// A coordinate transform performs the transformation with the assumption that the w component
      /// is one. The four dimensional vector obtained from the transformation operation has each
      /// component in the vector divided by the w component. This forces the w component to be one and
      /// therefore makes the vector homogeneous. The homogeneous vector is often preferred when working
      /// with coordinates as the w component can safely be ignored.
      /// </remarks>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D TransformCoordinate(Vector2D coordinate, Matrix4x4D transform)
      {
         TransformCoordinate(ref coordinate, ref transform, out var result);
         return result;
      }

      /// <summary>
      /// Performs a coordinate transformation on an array of vectors using the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="source">The array of coordinate vectors to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void TransformCoordinate(Vector2D[] source, ref Matrix4x4D transform, Vector2D[] destination)
      {
         if (source == null)
            throw new ArgumentNullException(nameof(source));
         if (destination == null)
            throw new ArgumentNullException(nameof(destination));
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

         for (int i = 0; i < source.Length; ++i)
         {
            TransformCoordinate(ref source[i], ref transform, out destination[i]);
         }
      }

      /// <summary>
      /// Performs a normal transformation using the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="normal">The normal vector to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
      /// <param name="result">When the method completes, contains the transformed normal.</param>
      /// <remarks>
      /// A normal transform performs the transformation with the assumption that the w component
      /// is zero. This causes the fourth row and fourth column of the matrix to be unused. The
      /// end result is a vector that is not translated, but all other transformation properties
      /// apply. This is often preferred for normal vectors as normals purely represent direction
      /// rather than location because normal vectors should not be translated.
      /// </remarks>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void TransformNormal(ref Vector2D normal, ref Matrix4x4D transform, out Vector2D result)
      {
         result = new Vector2D(
             (normal.X * transform.M11) + (normal.Y * transform.M21),
             (normal.X * transform.M12) + (normal.Y * transform.M22));
      }

      /// <summary>
      /// Performs a normal transformation using the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="normal">The normal vector to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
      /// <returns>The transformed normal.</returns>
      /// <remarks>
      /// A normal transform performs the transformation with the assumption that the w component
      /// is zero. This causes the fourth row and fourth column of the matrix to be unused. The
      /// end result is a vector that is not translated, but all other transformation properties
      /// apply. This is often preferred for normal vectors as normals purely represent direction
      /// rather than location because normal vectors should not be translated.
      /// </remarks>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D TransformNormal(Vector2D normal, Matrix4x4D transform)
      {
         TransformNormal(ref normal, ref transform, out var result);
         return result;
      }

      /// <summary>
      /// Performs a normal transformation on an array of vectors using the given <see cref="Matrix4x4D"/>.
      /// </summary>
      /// <param name="source">The array of normal vectors to transform.</param>
      /// <param name="transform">The transformation <see cref="Matrix4x4D"/>.</param>
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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static void TransformNormal(Vector2D[] source, ref Matrix4x4D transform, Vector2D[] destination)
      {
         if (source == null)
            throw new ArgumentNullException(nameof(source));
         if (destination == null)
            throw new ArgumentNullException(nameof(destination));
         if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

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
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator +(Vector2D left, Vector2D right)
      {
         return new(left.X + right.X, left.Y + right.Y);
      }

      /// <summary>
      /// Multiplies a vector with another by performing component-wise multiplication equivalent to <see cref="Multiply(ref Vector2D,ref Vector2D,out Vector2D)"/>.
      /// </summary>
      /// <param name="left">The first vector to multiply.</param>
      /// <param name="right">The second vector to multiply.</param>
      /// <returns>The multiplication of the two vectors.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator *(Vector2D left, Vector2D right)
      {
         return new(left.X * right.X, left.Y * right.Y);
      }

      /// <summary>
      /// Assert a vector (return it unchanged).
      /// </summary>
      /// <param name="value">The vector to assert (unchanged).</param>
      /// <returns>The asserted (unchanged) vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator +(Vector2D value)
      {
         return value;
      }

      /// <summary>
      /// Subtracts two vectors.
      /// </summary>
      /// <param name="left">The first vector to subtract.</param>
      /// <param name="right">The second vector to subtract.</param>
      /// <returns>The difference of the two vectors.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator -(Vector2D left, Vector2D right)
      {
         return new(left.X - right.X, left.Y - right.Y);
      }

      /// <summary>
      /// Reverses the direction of a given vector.
      /// </summary>
      /// <param name="value">The vector to negate.</param>
      /// <returns>A vector facing in the opposite direction.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator -(Vector2D value)
      {
         return new(-value.X, -value.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator *(double scale, Vector2D value)
      {
         return new(value.X * scale, value.Y * scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator *(Vector2D value, double scale)
      {
         return new(value.X * scale, value.Y * scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator /(Vector2D value, double scale)
      {
         return new(value.X / scale, value.Y / scale);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <param name="value">The vector to scale.</param>  
      /// <returns>The scaled vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator /(double scale, Vector2D value)
      {
         return new(scale / value.X, scale / value.Y);
      }

      /// <summary>
      /// Scales a vector by the given value.
      /// </summary>
      /// <param name="value">The vector to scale.</param>
      /// <param name="scale">The amount by which to scale the vector.</param>
      /// <returns>The scaled vector.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator /(Vector2D value, Vector2D scale)
      {
         return new(value.X / scale.X, value.Y / scale.Y);
      }

      /// <summary>
      /// Perform a component-wise addition
      /// </summary>
      /// <param name="value">The input vector.</param>
      /// <param name="scalar">The scalar value to be added on elements</param>
      /// <returns>The vector with added scalar for each element.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator +(Vector2D value, double scalar)
      {
         return new(value.X + scalar, value.Y + scalar);
      }

      /// <summary>
      /// Perform a component-wise addition
      /// </summary>
      /// <param name="value">The input vector.</param>
      /// <param name="scalar">The scalar value to be added on elements</param>
      /// <returns>The vector with added scalar for each element.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator +(double scalar, Vector2D value)
      {
         return new(scalar + value.X, scalar + value.Y);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="value">The input vector.</param>
      /// <param name="scalar">The scalar value to be subtracted from elements</param>
      /// <returns>The vector with subtracted scalar from each element.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator -(Vector2D value, double scalar)
      {
         return new(value.X - scalar, value.Y - scalar);
      }

      /// <summary>
      /// Perform a component-wise subtraction
      /// </summary>
      /// <param name="value">The input vector.</param>
      /// <param name="scalar">The scalar value to be subtracted from elements</param>
      /// <returns>The vector with subtracted scalar from each element.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static Vector2D operator -(double scalar, Vector2D value)
      {
         return new(scalar - value.X, scalar - value.Y);
      }

      /// <summary>
      /// Tests for equality between two objects.
      /// </summary>
      /// <param name="left">The first value to compare.</param>
      /// <param name="right">The second value to compare.</param>
      /// <returns><c>true</c> if <paramref name="left"/> has the same value as <paramref name="right"/>; otherwise, <c>false</c>.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool operator ==(Vector2D left, Vector2D right)
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
      public static bool operator !=(Vector2D left, Vector2D right)
      {
         return !left.Equals(ref right);
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="Vector2D"/> to <see cref="Vector3D"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static explicit operator Vector3D(Vector2D value)
      {
         return new(value, 0.0f);
      }
      
      /// <summary>
      /// Performs an explicit conversion from <see cref="Vector2D"/> to <see cref="Vector3F"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static explicit operator Vector3F(Vector2D value)
      {
         return new(value, 0.0f);
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="Vector2D"/> to <see cref="Vector4D"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static explicit operator Vector4D(Vector2D value)
      {
         return new(value, 0.0f, 0.0f);
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
      /// Determines whether the specified <see cref="Vector2D"/> is equal to this instance.
      /// </summary>
      /// <param name="other">The <see cref="Vector2D"/> to compare with this instance.</param>
      /// <returns>
      /// 	<c>true</c> if the specified <see cref="Vector2D"/> is equal to this instance; otherwise, <c>false</c>.
      /// </returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Equals(ref Vector2D other)
      {
         return MathHelper.NearEqual(other.X, X) && MathHelper.NearEqual(other.Y, Y);
      }

      /// <summary>
      /// Determines whether the specified <see cref="Vector2D"/> is equal to this instance.
      /// </summary>
      /// <param name="other">The <see cref="Vector2D"/> to compare with this instance.</param>
      /// <returns>
      /// 	<c>true</c> if the specified <see cref="Vector2D"/> is equal to this instance; otherwise, <c>false</c>.
      /// </returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Equals(Vector2D other)
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
         if (!(value is Vector2D))
            return false;

         var strongValue = (Vector2D)value;
         return Equals(ref strongValue);
      }

      public static implicit operator Vector2F(Vector2D value)
      {
         return new((float)value.X, (float)value.Y);
      }
      
      public static implicit operator Point(Vector2D value)
      {
         return new((float)value.X, (float)value.Y);
      }

   }
}
