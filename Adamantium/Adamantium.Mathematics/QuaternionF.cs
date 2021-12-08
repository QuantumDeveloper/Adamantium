﻿using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Adamantium.Mathematics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct QuaternionF : IEquatable<QuaternionF>, IFormattable
    {
        /// <summary>
        /// The size of the <see cref="QuaternionF"/> type, in bytes.
        /// </summary>
        public static readonly int SizeInBytes = Marshal.SizeOf(typeof(QuaternionF));

        /// <summary>
        /// A <see cref="QuaternionF"/> with all of its components set to zero.
        /// </summary>
        public static readonly QuaternionF Zero = new QuaternionF();

        /// <summary>
        /// A <see cref="QuaternionF"/> with all of its components set to one.
        /// </summary>
        public static readonly QuaternionF One = new QuaternionF(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>
        /// The identity <see cref="QuaternionF"/> (0, 0, 0, 1).
        /// </summary>
        public static readonly QuaternionF Identity = new QuaternionF(0.0f, 0.0f, 0.0f, 1.0f);

        /// <summary>
        /// The X component of the quaternion.
        /// </summary>
        public float X;

        /// <summary>
        /// The Y component of the quaternion.
        /// </summary>
        public float Y;

        /// <summary>
        /// The Z component of the quaternion.
        /// </summary>
        public float Z;

        /// <summary>
        /// The W component of the quaternion.
        /// </summary>
        public float W;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuaternionF"/> struct.
        /// </summary>
        /// <param name="value">The value that will be assigned to all components.</param>
        public QuaternionF(float value)
        {
            X = value;
            Y = value;
            Z = value;
            W = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QuaternionF"/> struct.
        /// </summary>
        /// <param name="value">A vector containing the values with which to initialize the components.</param>
        public QuaternionF(Vector4F value)
        {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
            W = value.W;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QuaternionF"/> struct.
        /// </summary>
        /// <param name="value">A vector containing the values with which to initialize the X, Y, and Z components.</param>
        /// <param name="w">Initial value for the W component of the quaternion.</param>
        public QuaternionF(Vector3F value, float w)
        {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
            W = w;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QuaternionF"/> struct.
        /// </summary>
        /// <param name="value">A vector containing the values with which to initialize the X and Y components.</param>
        /// <param name="z">Initial value for the Z component of the quaternion.</param>
        /// <param name="w">Initial value for the W component of the quaternion.</param>
        public QuaternionF(Vector2F value, float z, float w)
        {
            X = value.X;
            Y = value.Y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QuaternionF"/> struct.
        /// </summary>
        /// <param name="x">Initial value for the X component of the quaternion.</param>
        /// <param name="y">Initial value for the Y component of the quaternion.</param>
        /// <param name="z">Initial value for the Z component of the quaternion.</param>
        /// <param name="w">Initial value for the W component of the quaternion.</param>
        public QuaternionF(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QuaternionF"/> struct.
        /// </summary>
        /// <param name="values">The values to assign to the X, Y, Z, and W components of the quaternion. This must be an array with four elements.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="values"/> contains more or less than four elements.</exception>
        public QuaternionF(float[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length != 4)
                throw new ArgumentOutOfRangeException(nameof(values), "There must be four and only four input values for QuaternionF.");

            X = values[0];
            Y = values[1];
            Z = values[2];
            W = values[3];
        }

        /// <summary>
        /// Gets a value indicating whether this instance is equivalent to the identity quaternion.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is an identity quaternion; otherwise, <c>false</c>.
        /// </value>
        public bool IsIdentity => this.Equals(Identity);

        /// <summary>
        /// Gets a value indicting whether this instance is normalized.
        /// </summary>
        public bool IsNormalized => MathHelper.IsOne((X * X) + (Y * Y) + (Z * Z) + (W * W));

        /// <summary>
        /// Gets the angle of the quaternion.
        /// </summary>
        /// <value>The quaternion's angle.</value>
        public float Angle
        {
            get
            {
                float length = (X * X) + (Y * Y) + (Z * Z);
                if (MathHelper.IsZero(length))
                    return 0.0f;

                return (float)(2.0 * Math.Acos(MathHelper.Clamp(W, -1f, 1f)));
            }
        }

        /// <summary>
        /// Gets the axis components of the quaternion.
        /// </summary>
        /// <value>The axis components of the quaternion.</value>
        public Vector3F Axis
        {
            get
            {
                float length = (X * X) + (Y * Y) + (Z * Z);
                if (MathHelper.IsZero(length))
                    return Vector3F.UnitX;

                float inv = 1.0f / (float)Math.Sqrt(length);
                return new Vector3F(X * inv, Y * inv, Z * inv);
            }
        }

        /// <summary>
        /// Gets or sets the component at the specified index.
        /// </summary>
        /// <value>The value of the X, Y, Z, or W component, depending on the index.</value>
        /// <param name="index">The index of the component to access. Use 0 for the X component, 1 for the Y component, 2 for the Z component, and 3 for the W component.</param>
        /// <returns>The value of the component at the specified index.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the <paramref name="index"/> is out of the range [0, 3].</exception>
        public float this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return X;
                    case 1: return Y;
                    case 2: return Z;
                    case 3: return W;
                }

                throw new ArgumentOutOfRangeException(nameof(index), "Indices for QuaternionF run from 0 to 3, inclusive.");
            }

            set
            {
                switch (index)
                {
                    case 0: X = value; break;
                    case 1: Y = value; break;
                    case 2: Z = value; break;
                    case 3: W = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(index), "Indices for QuaternionF run from 0 to 3, inclusive.");
                }
            }
        }

        /// <summary>
        /// Conjugates the quaternion.
        /// </summary>
        public void Conjugate()
        {
            X = -X;
            Y = -Y;
            Z = -Z;
        }

        /// <summary>
        /// Conjugates and renormalizes the quaternion.
        /// </summary>
        public void Invert()
        {
            float lengthSq = LengthSquared();
            if (!MathHelper.IsZero(lengthSq))
            {
                lengthSq = 1.0f / lengthSq;

                X = -X * lengthSq;
                Y = -Y * lengthSq;
                Z = -Z * lengthSq;
                W = W * lengthSq;
            }
        }

        /// <summary>
        /// Calculates the length of the quaternion.
        /// </summary>
        /// <returns>The length of the quaternion.</returns>
        /// <remarks>
        /// <see cref="QuaternionF.LengthSquared"/> may be preferred when only the relative length is needed
        /// and speed is of the essence.
        /// </remarks>
        public float Length()
        {
            return (float)Math.Sqrt((X * X) + (Y * Y) + (Z * Z) + (W * W));
        }

        /// <summary>
        /// Calculates the squared length of the quaternion.
        /// </summary>
        /// <returns>The squared length of the quaternion.</returns>
        /// <remarks>
        /// This method may be preferred to <see cref="QuaternionF.Length"/> when only a relative length is needed
        /// and speed is of the essence.
        /// </remarks>
        public float LengthSquared()
        {
            return (X * X) + (Y * Y) + (Z * Z) + (W * W);
        }

        /// <summary>
        /// Converts the quaternion into a unit quaternion.
        /// </summary>
        public void Normalize()
        {
            float length = Length();
            if (!MathHelper.IsZero(length))
            {
                float inverse = 1.0f / length;
                X *= inverse;
                Y *= inverse;
                Z *= inverse;
                W *= inverse;
            }
        }

        /// <summary>
        /// Creates an array containing the elements of the quaternion.
        /// </summary>
        /// <returns>A four-element array containing the components of the quaternion.</returns>
        public float[] ToArray()
        {
            return new[] { X, Y, Z, W };
        }

        /// <summary>
        /// Adds two quaternions.
        /// </summary>
        /// <param name="left">The first quaternion to add.</param>
        /// <param name="right">The second quaternion to add.</param>
        /// <param name="result">When the method completes, contains the sum of the two quaternions.</param>
        public static void Add(ref QuaternionF left, ref QuaternionF right, out QuaternionF result)
        {
            result.X = left.X + right.X;
            result.Y = left.Y + right.Y;
            result.Z = left.Z + right.Z;
            result.W = left.W + right.W;
        }

        /// <summary>
        /// Adds two quaternions.
        /// </summary>
        /// <param name="left">The first quaternion to add.</param>
        /// <param name="right">The second quaternion to add.</param>
        /// <returns>The sum of the two quaternions.</returns>
        public static QuaternionF Add(QuaternionF left, QuaternionF right)
        {
            QuaternionF result;
            Add(ref left, ref right, out result);
            return result;
        }

        /// <summary>
        /// Subtracts two quaternions.
        /// </summary>
        /// <param name="left">The first quaternion to subtract.</param>
        /// <param name="right">The second quaternion to subtract.</param>
        /// <param name="result">When the method completes, contains the difference of the two quaternions.</param>
        public static void Subtract(ref QuaternionF left, ref QuaternionF right, out QuaternionF result)
        {
            result.X = left.X - right.X;
            result.Y = left.Y - right.Y;
            result.Z = left.Z - right.Z;
            result.W = left.W - right.W;
        }

        /// <summary>
        /// Subtracts two quaternions.
        /// </summary>
        /// <param name="left">The first quaternion to subtract.</param>
        /// <param name="right">The second quaternion to subtract.</param>
        /// <returns>The difference of the two quaternions.</returns>
        public static QuaternionF Subtract(QuaternionF left, QuaternionF right)
        {
            QuaternionF result;
            Subtract(ref left, ref right, out result);
            return result;
        }

        /// <summary>
        /// Scales a quaternion by the given value.
        /// </summary>
        /// <param name="value">The quaternion to scale.</param>
        /// <param name="scale">The amount by which to scale the quaternion.</param>
        /// <param name="result">When the method completes, contains the scaled quaternion.</param>
        public static void Multiply(ref QuaternionF value, float scale, out QuaternionF result)
        {
            result.X = value.X * scale;
            result.Y = value.Y * scale;
            result.Z = value.Z * scale;
            result.W = value.W * scale;
        }

        /// <summary>
        /// Scales a quaternion by the given value.
        /// </summary>
        /// <param name="value">The quaternion to scale.</param>
        /// <param name="scale">The amount by which to scale the quaternion.</param>
        /// <returns>The scaled quaternion.</returns>
        public static QuaternionF Multiply(QuaternionF value, float scale)
        {
            QuaternionF result;
            Multiply(ref value, scale, out result);
            return result;
        }

        /// <summary>
        /// Multiplies a quaternion by another.
        /// </summary>
        /// <param name="left">The first quaternion to multiply.</param>
        /// <param name="right">The second quaternion to multiply.</param>
        /// <param name="result">When the method completes, contains the multiplied quaternion.</param>
        public static void Multiply(ref QuaternionF left, ref QuaternionF right, out QuaternionF result)
        {
            float lx = left.X;
            float ly = left.Y;
            float lz = left.Z;
            float lw = left.W;
            float rx = right.X;
            float ry = right.Y;
            float rz = right.Z;
            float rw = right.W;
            float a = (ly * rz - lz * ry);
            float b = (lz * rx - lx * rz);
            float c = (lx * ry - ly * rx);
            float d = (lx * rx + ly * ry + lz * rz);
            result.X = (lx * rw + rx * lw) + a;
            result.Y = (ly * rw + ry * lw) + b;
            result.Z = (lz * rw + rz * lw) + c;
            result.W = lw * rw - d;
        }

        /// <summary>
        /// Multiplies a quaternion by another.
        /// </summary>
        /// <param name="left">The first quaternion to multiply.</param>
        /// <param name="right">The second quaternion to multiply.</param>
        /// <returns>The multiplied quaternion.</returns>
        public static QuaternionF Multiply(QuaternionF left, QuaternionF right)
        {
            Multiply(ref left, ref right, out var result);
            return result;
        }

        /// <summary>
        /// Reverses the direction of a given quaternion.
        /// </summary>
        /// <param name="value">The quaternion to negate.</param>
        /// <param name="result">When the method completes, contains a quaternion facing in the opposite direction.</param>
        public static void Negate(ref QuaternionF value, out QuaternionF result)
        {
            result.X = -value.X;
            result.Y = -value.Y;
            result.Z = -value.Z;
            result.W = -value.W;
        }

        /// <summary>
        /// Reverses the direction of a given quaternion.
        /// </summary>
        /// <param name="value">The quaternion to negate.</param>
        /// <returns>A quaternion facing in the opposite direction.</returns>
        public static QuaternionF Negate(QuaternionF value)
        {
            Negate(ref value, out var result);
            return result;
        }

        /// <summary>
        /// Convert <see cref="QuaternionF"/> to Eulerian angles
        /// </summary>
        /// <param name="rotation">rotation quaternion</param>
        /// <returns></returns>
        public static Vector3F Euler(QuaternionF rotation)
        {
            var axis = rotation.Axis;
            var angle = rotation.Angle;
            var euler = axis * angle;
            return new Vector3F(MathHelper.RadiansToDegrees(euler.X), MathHelper.RadiansToDegrees(euler.Y), MathHelper.RadiansToDegrees(euler.Z));
        }

        /// <summary>
        /// Returns a <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of a point specified in Barycentric coordinates relative to a 2D triangle.
        /// </summary>
        /// <param name="value1">A <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of vertex 1 of the triangle.</param>
        /// <param name="value2">A <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of vertex 2 of the triangle.</param>
        /// <param name="value3">A <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of vertex 3 of the triangle.</param>
        /// <param name="amount1">Barycentric coordinate b2, which expresses the weighting factor toward vertex 2 (specified in <paramref name="value2"/>).</param>
        /// <param name="amount2">Barycentric coordinate b3, which expresses the weighting factor toward vertex 3 (specified in <paramref name="value3"/>).</param>
        /// <param name="result">When the method completes, contains a new <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of the specified point.</param>
        public static void Barycentric(ref QuaternionF value1, ref QuaternionF value2, ref QuaternionF value3, float amount1, float amount2, out QuaternionF result)
        {
            Slerp(ref value1, ref value2, amount1 + amount2, out var start);
            Slerp(ref value1, ref value3, amount1 + amount2, out var end);
            Slerp(ref start, ref end, amount2 / (amount1 + amount2), out result);
        }

        /// <summary>
        /// Returns a <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of a point specified in Barycentric coordinates relative to a 2D triangle.
        /// </summary>
        /// <param name="value1">A <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of vertex 1 of the triangle.</param>
        /// <param name="value2">A <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of vertex 2 of the triangle.</param>
        /// <param name="value3">A <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of vertex 3 of the triangle.</param>
        /// <param name="amount1">Barycentric coordinate b2, which expresses the weighting factor toward vertex 2 (specified in <paramref name="value2"/>).</param>
        /// <param name="amount2">Barycentric coordinate b3, which expresses the weighting factor toward vertex 3 (specified in <paramref name="value3"/>).</param>
        /// <returns>A new <see cref="QuaternionF"/> containing the 4D Cartesian coordinates of the specified point.</returns>
        public static QuaternionF Barycentric(QuaternionF value1, QuaternionF value2, QuaternionF value3, float amount1, float amount2)
        {
            QuaternionF result;
            Barycentric(ref value1, ref value2, ref value3, amount1, amount2, out result);
            return result;
        }

        /// <summary>
        /// Conjugates a quaternion.
        /// </summary>
        /// <param name="value">The quaternion to conjugate.</param>
        /// <param name="result">When the method completes, contains the conjugated quaternion.</param>
        public static void Conjugate(ref QuaternionF value, out QuaternionF result)
        {
            result.X = -value.X;
            result.Y = -value.Y;
            result.Z = -value.Z;
            result.W = value.W;
        }

        /// <summary>
        /// Conjugates a quaternion.
        /// </summary>
        /// <param name="value">The quaternion to conjugate.</param>
        /// <returns>The conjugated quaternion.</returns>
        public static QuaternionF Conjugate(QuaternionF value)
        {
            Conjugate(ref value, out var result);
            return result;
        }

        /// <summary>
        /// Calculates the dot product of two quaternions.
        /// </summary>
        /// <param name="left">First source quaternion.</param>
        /// <param name="right">Second source quaternion.</param>
        /// <param name="result">When the method completes, contains the dot product of the two quaternions.</param>
        public static void Dot(ref QuaternionF left, ref QuaternionF right, out float result)
        {
            result = (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z) + (left.W * right.W);
        }

        /// <summary>
        /// Calculates the dot product of two quaternions.
        /// </summary>
        /// <param name="left">First source quaternion.</param>
        /// <param name="right">Second source quaternion.</param>
        /// <returns>The dot product of the two quaternions.</returns>
        public static float Dot(QuaternionF left, QuaternionF right)
        {
            return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z) + (left.W * right.W);
        }

        /// <summary>
        /// Exponentiates a quaternion.
        /// </summary>
        /// <param name="value">The quaternion to exponentiate.</param>
        /// <param name="result">When the method completes, contains the exponentiated quaternion.</param>
        public static void Exponential(ref QuaternionF value, out QuaternionF result)
        {
            float angle = (float)Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
            float sin = (float)Math.Sin(angle);

            if (!MathHelper.IsZero(sin))
            {
                float coeff = sin / angle;
                result.X = coeff * value.X;
                result.Y = coeff * value.Y;
                result.Z = coeff * value.Z;
            }
            else
            {
                result = value;
            }

            result.W = (float)Math.Cos(angle);
        }

        /// <summary>
        /// Exponentiates a quaternion.
        /// </summary>
        /// <param name="value">The quaternion to exponentiate.</param>
        /// <returns>The exponentiated quaternion.</returns>
        public static QuaternionF Exponential(QuaternionF value)
        {
            QuaternionF result;
            Exponential(ref value, out result);
            return result;
        }

        /// <summary>
        /// Conjugates and renormalizes the quaternion.
        /// </summary>
        /// <param name="value">The quaternion to conjugate and renormalize.</param>
        /// <param name="result">When the method completes, contains the conjugated and renormalized quaternion.</param>
        public static void Invert(ref QuaternionF value, out QuaternionF result)
        {
            result = value;
            result.Invert();
        }

        /// <summary>
        /// Conjugates and renormalizes the quaternion.
        /// </summary>
        /// <param name="value">The quaternion to conjugate and renormalize.</param>
        /// <returns>The conjugated and renormalized quaternion.</returns>
        public static QuaternionF Invert(QuaternionF value)
        {
            Invert(ref value, out var result);
            return result;
        }

        /// <summary>
        /// Performs a linear interpolation between two quaternions.
        /// </summary>
        /// <param name="start">Start quaternion.</param>
        /// <param name="end">End quaternion.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
        /// <param name="result">When the method completes, contains the linear interpolation of the two quaternions.</param>
        /// <remarks>
        /// This method performs the linear interpolation based on the following formula.
        /// <code>start + (end - start) * amount</code>
        /// Passing <paramref name="amount"/> a value of 0 will cause <paramref name="start"/> to be returned; a value of 1 will cause <paramref name="end"/> to be returned. 
        /// </remarks>
        public static void Lerp(ref QuaternionF start, ref QuaternionF end, float amount, out QuaternionF result)
        {
            float inverse = 1.0f - amount;

            if (Dot(start, end) >= 0.0f)
            {
                result.X = (inverse * start.X) + (amount * end.X);
                result.Y = (inverse * start.Y) + (amount * end.Y);
                result.Z = (inverse * start.Z) + (amount * end.Z);
                result.W = (inverse * start.W) + (amount * end.W);
            }
            else
            {
                result.X = (inverse * start.X) - (amount * end.X);
                result.Y = (inverse * start.Y) - (amount * end.Y);
                result.Z = (inverse * start.Z) - (amount * end.Z);
                result.W = (inverse * start.W) - (amount * end.W);
            }

            result.Normalize();
        }

        /// <summary>
        /// Performs a linear interpolation between two quaternion.
        /// </summary>
        /// <param name="start">Start quaternion.</param>
        /// <param name="end">End quaternion.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
        /// <returns>The linear interpolation of the two quaternions.</returns>
        /// <remarks>
        /// This method performs the linear interpolation based on the following formula.
        /// <code>start + (end - start) * amount</code>
        /// Passing <paramref name="amount"/> a value of 0 will cause <paramref name="start"/> to be returned; a value of 1 will cause <paramref name="end"/> to be returned. 
        /// </remarks>
        public static QuaternionF Lerp(QuaternionF start, QuaternionF end, float amount)
        {
            Lerp(ref start, ref end, amount, out var result);
            return result;
        }

        /// <summary>
        /// Calculates the natural logarithm of the specified quaternion.
        /// </summary>
        /// <param name="value">The quaternion whose logarithm will be calculated.</param>
        /// <param name="result">When the method completes, contains the natural logarithm of the quaternion.</param>
        public static void Logarithm(ref QuaternionF value, out QuaternionF result)
        {
            if (Math.Abs(value.W) < 1.0)
            {
                float angle = (float)Math.Acos(value.W);
                float sin = (float)Math.Sin(angle);

                if (!MathHelper.IsZero(sin))
                {
                    float coeff = angle / sin;
                    result.X = value.X * coeff;
                    result.Y = value.Y * coeff;
                    result.Z = value.Z * coeff;
                }
                else
                {
                    result = value;
                }
            }
            else
            {
                result = value;
            }

            result.W = 0.0f;
        }

        /// <summary>
        /// Calculates the natural logarithm of the specified quaternion.
        /// </summary>
        /// <param name="value">The quaternion whose logarithm will be calculated.</param>
        /// <returns>The natural logarithm of the quaternion.</returns>
        public static QuaternionF Logarithm(QuaternionF value)
        {
            Logarithm(ref value, out var result);
            return result;
        }

        /// <summary>
        /// Converts the quaternion into a unit quaternion.
        /// </summary>
        /// <param name="value">The quaternion to normalize.</param>
        /// <param name="result">When the method completes, contains the normalized quaternion.</param>
        public static void Normalize(ref QuaternionF value, out QuaternionF result)
        {
            QuaternionF temp = value;
            result = temp;
            result.Normalize();
        }

        /// <summary>
        /// Converts the quaternion into a unit quaternion.
        /// </summary>
        /// <param name="value">The quaternion to normalize.</param>
        /// <returns>The normalized quaternion.</returns>
        public static QuaternionF Normalize(QuaternionF value)
        {
            value.Normalize();
            return value;
        }

        /// <summary>
        /// Creates a quaternion given a rotation and an axis.
        /// </summary>
        /// <param name="axis">The axis of rotation.</param>
        /// <param name="angle">The angle of rotation.</param>
        /// <param name="result">When the method completes, contains the newly created quaternion.</param>
        public static void RotationAxis(ref Vector3F axis, float angle, out QuaternionF result)
        {
            Vector3F.Normalize(ref axis, out var normalized);

            float half = angle * 0.5f;
            float sin = (float)Math.Sin(half);
            float cos = (float)Math.Cos(half);

            result.X = normalized.X * sin;
            result.Y = normalized.Y * sin;
            result.Z = normalized.Z * sin;
            result.W = cos;
        }

        /// <summary>
        /// Creates a quaternion given a rotation and an axis.
        /// </summary>
        /// <param name="axis">The axis of rotation.</param>
        /// <param name="angle">The angle of rotation.</param>
        /// <returns>The newly created quaternion.</returns>
        public static QuaternionF RotationAxis(Vector3F axis, float angle)
        {
            QuaternionF result;
            RotationAxis(ref axis, angle, out result);
            return result;
        }

        /// <summary>
        /// Creates a quaternion given a rotation matrix.
        /// </summary>
        /// <param name="matrix">The rotation matrix.</param>
        /// <param name="result">When the method completes, contains the newly created quaternion.</param>
        public static void RotationMatrix(ref Matrix4x4F matrix, out QuaternionF result)
        {
            float sqrt;
            float half;
            float scale = matrix.M11 + matrix.M22 + matrix.M33;

            if (scale > 0.0f)
            {
                sqrt = (float)Math.Sqrt(scale + 1.0f);
                result.W = sqrt * 0.5f;
                sqrt = 0.5f / sqrt;

                result.X = (matrix.M23 - matrix.M32) * sqrt;
                result.Y = (matrix.M31 - matrix.M13) * sqrt;
                result.Z = (matrix.M12 - matrix.M21) * sqrt;
            }
            else if ((matrix.M11 >= matrix.M22) && (matrix.M11 >= matrix.M33))
            {
                sqrt = (float)Math.Sqrt(1.0f + matrix.M11 - matrix.M22 - matrix.M33);
                half = 0.5f / sqrt;

                result.X = 0.5f * sqrt;
                result.Y = (matrix.M12 + matrix.M21) * half;
                result.Z = (matrix.M13 + matrix.M31) * half;
                result.W = (matrix.M23 - matrix.M32) * half;
            }
            else if (matrix.M22 > matrix.M33)
            {
                sqrt = (float)Math.Sqrt(1.0f + matrix.M22 - matrix.M11 - matrix.M33);
                half = 0.5f / sqrt;

                result.X = (matrix.M21 + matrix.M12) * half;
                result.Y = 0.5f * sqrt;
                result.Z = (matrix.M32 + matrix.M23) * half;
                result.W = (matrix.M31 - matrix.M13) * half;
            }
            else
            {
                sqrt = (float)Math.Sqrt(1.0f + matrix.M33 - matrix.M11 - matrix.M22);
                half = 0.5f / sqrt;

                result.X = (matrix.M31 + matrix.M13) * half;
                result.Y = (matrix.M32 + matrix.M23) * half;
                result.Z = 0.5f * sqrt;
                result.W = (matrix.M12 - matrix.M21) * half;
            }
        }

        /// <summary>
        /// Creates a quaternion given a rotation matrix.
        /// </summary>
        /// <param name="matrix">The rotation matrix.</param>
        /// <param name="result">When the method completes, contains the newly created quaternion.</param>
        public static void RotationMatrix(ref Matrix3x3F matrix, out QuaternionF result)
        {
            float sqrt;
            float half;
            float scale = matrix.M11 + matrix.M22 + matrix.M33;

            if (scale > 0.0f)
            {
                sqrt = (float)Math.Sqrt(scale + 1.0f);
                result.W = sqrt * 0.5f;
                sqrt = 0.5f / sqrt;

                result.X = (matrix.M23 - matrix.M32) * sqrt;
                result.Y = (matrix.M31 - matrix.M13) * sqrt;
                result.Z = (matrix.M12 - matrix.M21) * sqrt;
            }
            else if ((matrix.M11 >= matrix.M22) && (matrix.M11 >= matrix.M33))
            {
                sqrt = (float)Math.Sqrt(1.0f + matrix.M11 - matrix.M22 - matrix.M33);
                half = 0.5f / sqrt;

                result.X = 0.5f * sqrt;
                result.Y = (matrix.M12 + matrix.M21) * half;
                result.Z = (matrix.M13 + matrix.M31) * half;
                result.W = (matrix.M23 - matrix.M32) * half;
            }
            else if (matrix.M22 > matrix.M33)
            {
                sqrt = (float)Math.Sqrt(1.0f + matrix.M22 - matrix.M11 - matrix.M33);
                half = 0.5f / sqrt;

                result.X = (matrix.M21 + matrix.M12) * half;
                result.Y = 0.5f * sqrt;
                result.Z = (matrix.M32 + matrix.M23) * half;
                result.W = (matrix.M31 - matrix.M13) * half;
            }
            else
            {
                sqrt = (float)Math.Sqrt(1.0f + matrix.M33 - matrix.M11 - matrix.M22);
                half = 0.5f / sqrt;

                result.X = (matrix.M31 + matrix.M13) * half;
                result.Y = (matrix.M32 + matrix.M23) * half;
                result.Z = 0.5f * sqrt;
                result.W = (matrix.M12 - matrix.M21) * half;
            }
        }

        /// <summary>
        /// Creates a left-handed, look-at quaternion.
        /// </summary>
        /// <param name="eye">The position of the viewer's eye.</param>
        /// <param name="target">The camera look-at target.</param>
        /// <param name="up">The camera's up vector.</param>
        /// <param name="result">When the method completes, contains the created look-at quaternion.</param>
        public static void LookAtLH(ref Vector3F eye, ref Vector3F target, ref Vector3F up, out QuaternionF result)
        {
            Matrix3x3F matrix;
            Matrix3x3F.LookAtLH(ref eye, ref target, ref up, out matrix);
            RotationMatrix(ref matrix, out result);
        }

        /// <summary>
        /// Creates a left-handed, look-at quaternion.
        /// </summary>
        /// <param name="eye">The position of the viewer's eye.</param>
        /// <param name="target">The camera look-at target.</param>
        /// <param name="up">The camera's up vector.</param>
        /// <returns>The created look-at quaternion.</returns>
        public static QuaternionF LookAtLH(Vector3F eye, Vector3F target, Vector3F up)
        {
            QuaternionF result;
            LookAtLH(ref eye, ref target, ref up, out result);
            return result;
        }

        /// <summary>
        /// Creates a left-handed, look-at quaternion.
        /// </summary>
        /// <param name="forward">The camera's forward direction.</param>
        /// <param name="up">The camera's up vector.</param>
        /// <param name="result">When the method completes, contains the created look-at quaternion.</param>
        public static void RotationLookAtLH(ref Vector3F forward, ref Vector3F up, out QuaternionF result)
        {
            Vector3F eye = Vector3F.Zero;
            QuaternionF.LookAtLH(ref eye, ref forward, ref up, out result);
        }

        /// <summary>
        /// Creates a left-handed, look-at quaternion.
        /// </summary>
        /// <param name="forward">The camera's forward direction.</param>
        /// <param name="up">The camera's up vector.</param>
        /// <returns>The created look-at quaternion.</returns>
        public static QuaternionF RotationLookAtLH(Vector3F forward, Vector3F up)
        {
            QuaternionF result;
            RotationLookAtLH(ref forward, ref up, out result);
            return result;
        }

        public static QuaternionF RotateToFace(ref Vector3F objectPosition, ref Vector3F targetPosition, ref Vector3F upVector)
        {
            return MathHelper.RotateToFace(ref objectPosition, ref targetPosition, ref upVector);
        }

        public static QuaternionF RotateToFace(Vector3F targetPosition, Vector3F objectPosition, Vector3F upVector)
        {
            return MathHelper.RotateToFace(ref targetPosition, ref objectPosition, ref upVector);
        }

        /// <summary>
        /// Creates a right-handed, look-at quaternion.
        /// </summary>
        /// <param name="eye">The position of the viewer's eye.</param>
        /// <param name="target">The camera look-at target.</param>
        /// <param name="up">The camera's up vector.</param>
        /// <param name="result">When the method completes, contains the created look-at quaternion.</param>
        public static void LookAtRH(ref Vector3F eye, ref Vector3F target, ref Vector3F up, out QuaternionF result)
        {
            Matrix3x3F matrix;
            Matrix3x3F.LookAtRH(ref eye, ref target, ref up, out matrix);
            RotationMatrix(ref matrix, out result);
        }

        /// <summary>
        /// Creates a right-handed, look-at quaternion.
        /// </summary>
        /// <param name="eye">The position of the viewer's eye.</param>
        /// <param name="target">The camera look-at target.</param>
        /// <param name="up">The camera's up vector.</param>
        /// <returns>The created look-at quaternion.</returns>
        public static QuaternionF LookAtRH(Vector3F eye, Vector3F target, Vector3F up)
        {
            QuaternionF result;
            LookAtRH(ref eye, ref target, ref up, out result);
            return result;
        }

        /// <summary>
        /// Creates a right-handed, look-at quaternion.
        /// </summary>
        /// <param name="forward">The camera's forward direction.</param>
        /// <param name="up">The camera's up vector.</param>
        /// <param name="result">When the method completes, contains the created look-at quaternion.</param>
        public static void RotationLookAtRH(ref Vector3F forward, ref Vector3F up, out QuaternionF result)
        {
            Vector3F eye = Vector3F.Zero;
            QuaternionF.LookAtRH(ref eye, ref forward, ref up, out result);
        }

        /// <summary>
        /// Creates a right-handed, look-at quaternion.
        /// </summary>
        /// <param name="forward">The camera's forward direction.</param>
        /// <param name="up">The camera's up vector.</param>
        /// <returns>The created look-at quaternion.</returns>
        public static QuaternionF RotationLookAtRH(Vector3F forward, Vector3F up)
        {
            QuaternionF result;
            RotationLookAtRH(ref forward, ref up, out result);
            return result;
        }

        /// <summary>
        /// Creates a left-handed spherical billboard that rotates around a specified object position.
        /// </summary>
        /// <param name="objectPosition">The position of the object around which the billboard will rotate.</param>
        /// <param name="cameraPosition">The position of the camera.</param>
        /// <param name="cameraUpVector">The up vector of the camera.</param>
        /// <param name="cameraForwardVector">The forward vector of the camera.</param>
        /// <param name="result">When the method completes, contains the created billboard quaternion.</param>
        public static void BillboardLH(ref Vector3F objectPosition, ref Vector3F cameraPosition, ref Vector3F cameraUpVector, ref Vector3F cameraForwardVector, out QuaternionF result)
        {
            Matrix3x3F matrix;
            Matrix3x3F.BillboardLH(ref objectPosition, ref cameraPosition, ref cameraUpVector, ref cameraForwardVector, out matrix);
            RotationMatrix(ref matrix, out result);
        }

        /// <summary>
        /// Creates a left-handed spherical billboard that rotates around a specified object position.
        /// </summary>
        /// <param name="objectPosition">The position of the object around which the billboard will rotate.</param>
        /// <param name="cameraPosition">The position of the camera.</param>
        /// <param name="cameraUpVector">The up vector of the camera.</param>
        /// <param name="cameraForwardVector">The forward vector of the camera.</param>
        /// <returns>The created billboard quaternion.</returns>
        public static QuaternionF BillboardLH(Vector3F objectPosition, Vector3F cameraPosition, Vector3F cameraUpVector, Vector3F cameraForwardVector)
        {
            QuaternionF result;
            BillboardLH(ref objectPosition, ref cameraPosition, ref cameraUpVector, ref cameraForwardVector, out result);
            return result;
        }

        /// <summary>
        /// Creates a quaternion given a rotation matrix.
        /// </summary>
        /// <param name="matrix">The rotation matrix.</param>
        /// <returns>The newly created quaternion.</returns>
        public static QuaternionF RotationMatrix(Matrix4x4F matrix)
        {
            QuaternionF result;
            RotationMatrix(ref matrix, out result);
            return result;
        }



        /// <summary>
        /// Creates a quaternion given a yaw, pitch, and roll value.
        /// </summary>
        /// <param name="yaw">The yaw of rotation (y-axis).</param>
        /// <param name="pitch">The pitch of rotation (x-axis).</param>
        /// <param name="roll">The roll of rotation (z-axiz).</param>
        /// <param name="result">When the method completes, contains the newly created quaternion.</param>
        public static void RotationYawPitchRoll(float yaw, float pitch, float roll, out QuaternionF result)
        {
            float halfRoll = roll * 0.5f;
            float halfPitch = pitch * 0.5f;
            float halfYaw = yaw * 0.5f;

            float sinRoll = (float)Math.Sin(halfRoll);
            float cosRoll = (float)Math.Cos(halfRoll);
            float sinPitch = (float)Math.Sin(halfPitch);
            float cosPitch = (float)Math.Cos(halfPitch);
            float sinYaw = (float)Math.Sin(halfYaw);
            float cosYaw = (float)Math.Cos(halfYaw);

            result.X = (cosYaw * sinPitch * cosRoll) + (sinYaw * cosPitch * sinRoll);
            result.Y = (sinYaw * cosPitch * cosRoll) - (cosYaw * sinPitch * sinRoll);
            result.Z = (cosYaw * cosPitch * sinRoll) - (sinYaw * sinPitch * cosRoll);
            result.W = (cosYaw * cosPitch * cosRoll) + (sinYaw * sinPitch * sinRoll);
        }

        /// <summary>
        /// Creates a quaternion given a yaw, pitch, and roll value.
        /// </summary>
        /// <param name="yaw">The yaw of rotation.</param>
        /// <param name="pitch">The pitch of rotation.</param>
        /// <param name="roll">The roll of rotation.</param>
        /// <returns>The newly created quaternion.</returns>
        public static QuaternionF RotationYawPitchRoll(float yaw, float pitch, float roll)
        {
            RotationYawPitchRoll(yaw, pitch, roll, out var result);
            return result;
        }

        /// <summary>
        /// Interpolates between two quaternions, using spherical linear interpolation.
        /// </summary>
        /// <param name="start">Start quaternion.</param>
        /// <param name="end">End quaternion.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
        /// <param name="result">When the method completes, contains the spherical linear interpolation of the two quaternions.</param>
        public static void Slerp(ref QuaternionF start, ref QuaternionF end, float amount, out QuaternionF result)
        {
            float opposite;
            float inverse;
            float dot = Dot(start, end);

            if (Math.Abs(dot) > 1.0f - MathHelper.ZeroToleranceF)
            {
                inverse = 1.0f - amount;
                opposite = amount * Math.Sign(dot);
            }
            else
            {
                float acos = (float)Math.Acos(Math.Abs(dot));
                float invSin = (float)(1.0 / Math.Sin(acos));

                inverse = (float)Math.Sin((1.0f - amount) * acos) * invSin;
                opposite = (float)Math.Sin(amount * acos) * invSin * Math.Sign(dot);
            }

            result.X = (inverse * start.X) + (opposite * end.X);
            result.Y = (inverse * start.Y) + (opposite * end.Y);
            result.Z = (inverse * start.Z) + (opposite * end.Z);
            result.W = (inverse * start.W) + (opposite * end.W);
        }

        /// <summary>
        /// Interpolates between two quaternions, using spherical linear interpolation.
        /// </summary>
        /// <param name="start">Start quaternion.</param>
        /// <param name="end">End quaternion.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
        /// <returns>The spherical linear interpolation of the two quaternions.</returns>
        public static QuaternionF Slerp(QuaternionF start, QuaternionF end, float amount)
        {
            QuaternionF result;
            Slerp(ref start, ref end, amount, out result);
            return result;
        }

        /// <summary>
        /// Interpolates between quaternions, using spherical quadrangle interpolation.
        /// </summary>
        /// <param name="value1">First source quaternion.</param>
        /// <param name="value2">Second source quaternion.</param>
        /// <param name="value3">Third source quaternion.</param>
        /// <param name="value4">Fourth source quaternion.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of interpolation.</param>
        /// <param name="result">When the method completes, contains the spherical quadrangle interpolation of the quaternions.</param>
        public static void Squad(ref QuaternionF value1, ref QuaternionF value2, ref QuaternionF value3, ref QuaternionF value4, float amount, out QuaternionF result)
        {
            QuaternionF start, end;
            Slerp(ref value1, ref value4, amount, out start);
            Slerp(ref value2, ref value3, amount, out end);
            Slerp(ref start, ref end, 2.0f * amount * (1.0f - amount), out result);
        }

        /// <summary>
        /// Interpolates between quaternions, using spherical quadrangle interpolation.
        /// </summary>
        /// <param name="value1">First source quaternion.</param>
        /// <param name="value2">Second source quaternion.</param>
        /// <param name="value3">Third source quaternion.</param>
        /// <param name="value4">Fourth source quaternion.</param>
        /// <param name="amount">Value between 0 and 1 indicating the weight of interpolation.</param>
        /// <returns>The spherical quadrangle interpolation of the quaternions.</returns>
        public static QuaternionF Squad(QuaternionF value1, QuaternionF value2, QuaternionF value3, QuaternionF value4, float amount)
        {
            QuaternionF result;
            Squad(ref value1, ref value2, ref value3, ref value4, amount, out result);
            return result;
        }

        /// <summary>
        /// Sets up control points for spherical quadrangle interpolation.
        /// </summary>
        /// <param name="value1">First source quaternion.</param>
        /// <param name="value2">Second source quaternion.</param>
        /// <param name="value3">Third source quaternion.</param>
        /// <param name="value4">Fourth source quaternion.</param>
        /// <returns>An array of three quaternions that represent control points for spherical quadrangle interpolation.</returns>
        public static QuaternionF[] SquadSetup(QuaternionF value1, QuaternionF value2, QuaternionF value3, QuaternionF value4)
        {
            QuaternionF q0 = (value1 + value2).LengthSquared() < (value1 - value2).LengthSquared() ? -value1 : value1;
            QuaternionF q2 = (value2 + value3).LengthSquared() < (value2 - value3).LengthSquared() ? -value3 : value3;
            QuaternionF q3 = (value3 + value4).LengthSquared() < (value3 - value4).LengthSquared() ? -value4 : value4;
            QuaternionF q1 = value2;

            QuaternionF q1Exp, q2Exp;
            Exponential(ref q1, out q1Exp);
            Exponential(ref q2, out q2Exp);

            QuaternionF[] results = new QuaternionF[3];
            results[0] = q1 * Exponential(-0.25f * (Logarithm(q1Exp * q2) + Logarithm(q1Exp * q0)));
            results[1] = q2 * Exponential(-0.25f * (Logarithm(q2Exp * q3) + Logarithm(q2Exp * q1)));
            results[2] = q2;

            return results;
        }

        /// <summary>
        /// Adds two quaternions.
        /// </summary>
        /// <param name="left">The first quaternion to add.</param>
        /// <param name="right">The second quaternion to add.</param>
        /// <returns>The sum of the two quaternions.</returns>
        public static QuaternionF operator +(QuaternionF left, QuaternionF right)
        {
            QuaternionF result;
            Add(ref left, ref right, out result);
            return result;
        }

        /// <summary>
        /// Subtracts two quaternions.
        /// </summary>
        /// <param name="left">The first quaternion to subtract.</param>
        /// <param name="right">The second quaternion to subtract.</param>
        /// <returns>The difference of the two quaternions.</returns>
        public static QuaternionF operator -(QuaternionF left, QuaternionF right)
        {
            QuaternionF result;
            Subtract(ref left, ref right, out result);
            return result;
        }

        /// <summary>
        /// Reverses the direction of a given quaternion.
        /// </summary>
        /// <param name="value">The quaternion to negate.</param>
        /// <returns>A quaternion facing in the opposite direction.</returns>
        public static QuaternionF operator -(QuaternionF value)
        {
            QuaternionF result;
            Negate(ref value, out result);
            return result;
        }

        /// <summary>
        /// Scales a quaternion by the given value.
        /// </summary>
        /// <param name="value">The quaternion to scale.</param>
        /// <param name="scale">The amount by which to scale the quaternion.</param>
        /// <returns>The scaled quaternion.</returns>
        public static QuaternionF operator *(float scale, QuaternionF value)
        {
            QuaternionF result;
            Multiply(ref value, scale, out result);
            return result;
        }

        /// <summary>
        /// Scales a quaternion by the given value.
        /// </summary>
        /// <param name="value">The quaternion to scale.</param>
        /// <param name="scale">The amount by which to scale the quaternion.</param>
        /// <returns>The scaled quaternion.</returns>
        public static QuaternionF operator *(QuaternionF value, float scale)
        {
            QuaternionF result;
            Multiply(ref value, scale, out result);
            return result;
        }

        /// <summary>
        /// Multiplies a quaternion by another.
        /// </summary>
        /// <param name="left">The first quaternion to multiply.</param>
        /// <param name="right">The second quaternion to multiply.</param>
        /// <returns>The multiplied quaternion.</returns>
        public static QuaternionF operator *(QuaternionF left, QuaternionF right)
        {
            QuaternionF result;
            Multiply(ref left, ref right, out result);
            return result;
        }

        /// <summary>
        /// Tests for equality between two objects.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns><c>true</c> if <paramref name="left"/> has the same value as <paramref name="right"/>; otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(QuaternionF left, QuaternionF right)
        {
            return left.Equals(ref right);
        }

        public static implicit operator Quaternion(QuaternionF orientation)
        {
            return new Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
        }

        /// <summary>
        /// Tests for inequality between two objects.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns><c>true</c> if <paramref name="left"/> has a different value than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(QuaternionF left, QuaternionF right)
        {
            return !left.Equals(ref right);
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, "X:{0} Y:{1} Z:{2} W:{3}", X, Y, Z, W);
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

            return string.Format(CultureInfo.CurrentCulture, "X:{0} Y:{1} Z:{2} W:{3}", X.ToString(format, CultureInfo.CurrentCulture),
                Y.ToString(format, CultureInfo.CurrentCulture), Z.ToString(format, CultureInfo.CurrentCulture), W.ToString(format, CultureInfo.CurrentCulture));
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
            return string.Format(formatProvider, "X:{0} Y:{1} Z:{2} W:{3}", X, Y, Z, W);
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
                return ToString(formatProvider);

            return string.Format(formatProvider, "X:{0} Y:{1} Z:{2} W:{3}", X.ToString(format, formatProvider),
                Y.ToString(format, formatProvider), Z.ToString(format, formatProvider), W.ToString(format, formatProvider));
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
                var hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Z.GetHashCode();
                hashCode = (hashCode * 397) ^ W.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="QuaternionF"/> is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="QuaternionF"/> to compare with this instance.</param>
        /// <returns>
        /// <c>true</c> if the specified <see cref="QuaternionF"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ref QuaternionF other)
        {
            return MathHelper.NearEqual(other.X, X) && MathHelper.NearEqual(other.Y, Y) && MathHelper.NearEqual(other.Z, Z) && MathHelper.NearEqual(other.W, W);
        }

        /// <summary>
        /// Determines whether the specified <see cref="QuaternionF"/> is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="QuaternionF"/> to compare with this instance.</param>
        /// <returns>
        /// <c>true</c> if the specified <see cref="QuaternionF"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(QuaternionF other)
        {
            return Equals(ref other);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
        /// </summary>
        /// <param name="value">The <see cref="System.Object"/> to compare with this instance.</param>
        /// <returns>
        /// <c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object value)
        {
            if (!(value is QuaternionF))
                return false;

            var strongValue = (QuaternionF)value;
            return Equals(ref strongValue);
        }

    }
}
