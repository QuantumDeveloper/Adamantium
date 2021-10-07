using System;
using System.Runtime.CompilerServices;

namespace Adamantium.Mathematics
{
    public static class MathHelper
    {
        /// <summary>
        /// The value for which all absolute numbers smaller than are considered equal to zero.
        /// </summary>
        public const float ZeroToleranceF = 1e-6f;

        /// <summary>
        /// The value for which all absolute numbers smaller than are considered equal to zero.
        /// </summary>
        public static double ZeroToleranceD = 1e-9;

        /// <summary>
        /// A value specifying the approximation of π which is 180 degrees.
        /// </summary>
        public const float Pi = (float)Math.PI;

        /// <summary>
        /// A value specifying the approximation of 2π which is 360 degrees.
        /// </summary>
        public const float TwoPi = (float)(2 * Math.PI);

        /// <summary>
        /// A value specifying the approximation of π/2 which is 90 degrees.
        /// </summary>
        public const float PiOverTwo = (float)(Math.PI / 2);

        /// <summary>
        /// A value specifying the approximation of π/4 which is 45 degrees.
        /// </summary>
        public const float PiOverFour = (float)(Math.PI / 4);

        public const float FloatNormal = (1 << 23) * Single.Epsilon;

        public const double DoubleNormal = (1L << 52) * Double.Epsilon;

        /// <summary>
        /// Checks if a and b are almost equals, taking into account the magnitude of floating point numbers (unlike <see cref="WithinEpsilon"/> method). See Remarks.
        /// See remarks.
        /// </summary>
        /// <param name="a">The left value to compare.</param>
        /// <param name="b">The right value to compare.</param>
        /// <returns><c>true</c> if a almost equal to b, <c>false</c> otherwise</returns>
        /// <remarks>
        /// The code is using the technique described by Bruce Dawson in 
        /// <a href="http://randomascii.wordpress.com/2012/02/25/comparing-floating-point-numbers-2012-edition/">Comparing Floating point numbers 2012 edition</a>. 
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearEqual(float a, float b)
        {
            return WithinEpsilon(a, b, ZeroToleranceF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearEqual(double a, double b)
        {
            // Check if the numbers are really close -- needed
            // when comparing numbers near zero.
            //if (IsZero(a - b))
            //    return true;

            //// Original from Bruce Dawson: http://randomascii.wordpress.com/2012/02/25/comparing-floating-point-numbers-2012-edition/
            //Int64 aInt = *(Int64*)&a;
            //Int64 bInt = *(Int64*)&b;

            //// Different signs means they do not match.
            //if ((aInt < 0) != (bInt < 0))
            //    return false;

            //// Find the difference in ULPs.
            //Int64 ulp = Math.Abs(aInt - bInt);

            //// Choose of maxUlp = 4
            //// according to http://code.google.com/p/googletest/source/browse/trunk/include/gtest/internal/gtest-internal.h
            //const int maxUlp = 4;
            //return ulp <= maxUlp;
            return WithinEpsilon(a, b, ZeroToleranceD);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearEqual(Vector3D a, Vector3D b)
        {
            return WithinEpsilon(a, b, ZeroToleranceD);
        }

        /// <summary>
        /// Checks if a - b are almost equals within a float epsilon.
        /// </summary>
        /// <param name="a">The left value to compare.</param>
        /// <param name="b">The right value to compare.</param>
        /// <param name="epsilon">Epsilon value</param>
        /// <returns><c>true</c> if a almost equal to b within a float epsilon, <c>false</c> otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WithinEpsilon(float a, float b, float epsilon)
        {
            float absA = Math.Abs(a);
            float absB = Math.Abs(b);
            float diff = Math.Abs(a - b);

            if (a == b)
            { 
                // shortcut, handles infinities
                return true;
            }

            if (a == 0 || b == 0 || (absA + absB < FloatNormal))
            {
                // a or b is zero or both are extremely close to it
                // relative error is less meaningful here
                return diff < epsilon; //*FloatNormal;
            }

            // use relative error
            return diff / Math.Min((absA + absB), Single.MaxValue) < epsilon;
        }

        /// <summary>
        /// Checks if a - b are almost equals within a float epsilon.
        /// </summary>
        /// <param name="a">The left value to compare.</param>
        /// <param name="b">The right value to compare.</param>
        /// <param name="epsilon">Epsilon value</param>
        /// <returns><c>true</c> if a almost equal to b within a float epsilon, <c>false</c> otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining|MethodImplOptions.AggressiveOptimization)]
        public static bool WithinEpsilon(double a, double b, double epsilon)
        {
            double absA = Math.Abs(a);
            double absB = Math.Abs(b);
            double diff = Math.Abs(a - b);

            if (a == b)
            { 
                // shortcut, handles infinities
                return true;
            }

            if (a == 0 || b == 0 || (absA + absB < DoubleNormal))
            {
                // a or b is zero or both are extremely close to it
                // relative error is less meaningful here
                return diff < epsilon;
            }

            // use relative error
            return diff / Math.Min((absA + absB), Double.MaxValue) < epsilon;
        }

        public static bool WithinEpsilon(Vector3F a, Vector3F b, float epsilon)
        {
            return WithinEpsilon(a.X, b.X, epsilon) && WithinEpsilon(a.Y, b.Y, epsilon) && WithinEpsilon(a.Z, b.Z, epsilon);
        }

        public static bool WithinEpsilon(Vector3D a, Vector3D b, double epsilon)
        {
            return WithinEpsilon(a.X, b.X, epsilon) && WithinEpsilon(a.Y, b.Y, epsilon) && WithinEpsilon(a.Z, b.Z, epsilon);
        }
        
        public static bool WithinEpsilon(Vector2D a, Vector2D b, double epsilon)
        {
            return WithinEpsilon(a.X, b.X, epsilon) && WithinEpsilon(a.Y, b.Y, epsilon);
        }

        /// <summary>
        /// Interpolates between two values using a linear function by a given amount.
        /// </summary>
        /// <remarks>
        /// See http://www.encyclopediaofmath.org/index.php/Linear_interpolation and
        /// http://fgiesen.wordpress.com/2012/08/15/linear-interpolation-past-present-and-future/
        /// </remarks>
        /// <param name="from">Value to interpolate from.</param>
        /// <param name="to">Value to interpolate to.</param>
        /// <param name="amount">Interpolation amount.</param>
        /// <returns>The result of linear interpolation of values based on the amount.</returns>
        public static double Lerp(double from, double to, double amount)
        {
            return (1 - amount) * from + amount * to;
        }

        /// <summary>
        /// Interpolates between two values using a linear function by a given amount.
        /// </summary>
        /// <remarks>
        /// See http://www.encyclopediaofmath.org/index.php/Linear_interpolation and
        /// http://fgiesen.wordpress.com/2012/08/15/linear-interpolation-past-present-and-future/
        /// </remarks>
        /// <param name="from">Value to interpolate from.</param>
        /// <param name="to">Value to interpolate to.</param>
        /// <param name="amount">Interpolation amount.</param>
        /// <returns>The result of linear interpolation of values based on the amount.</returns>
        public static float Lerp(float from, float to, float amount)
        {
            return (1 - amount) * from + amount * to;
        }

        /// <summary>
        /// Interpolates between two values using a linear function by a given amount.
        /// </summary>
        /// <remarks>
        /// See http://www.encyclopediaofmath.org/index.php/Linear_interpolation and
        /// http://fgiesen.wordpress.com/2012/08/15/linear-interpolation-past-present-and-future/
        /// </remarks>
        /// <param name="from">Value to interpolate from.</param>
        /// <param name="to">Value to interpolate to.</param>
        /// <param name="amount">Interpolation amount.</param>
        /// <returns>The result of linear interpolation of values based on the amount.</returns>
        public static byte Lerp(byte from, byte to, float amount)
        {
            return (byte)Lerp((float)from, (float)to, amount);
        }

        /// <summary>
        /// Performs smooth (cubic Hermite) interpolation between 0 and 1.
        /// </summary>
        /// <remarks>
        /// See https://en.wikipedia.org/wiki/Smoothstep
        /// </remarks>
        /// <param name="amount">Value between 0 and 1 indicating interpolation amount.</param>
        public static float SmoothStep(float amount)
        {
            if (amount <= 0)
                return 0;
            else if (amount >= 1)
                return 1;
            else
                return amount * amount * (3 - (2 * amount));
        }

        /// <summary>
        /// Performs smooth (cubic Hermite) interpolation between 0 and 1.
        /// </summary>
        /// <remarks>
        /// See https://en.wikipedia.org/wiki/Smoothstep
        /// </remarks>
        /// <param name="amount">Value between 0 and 1 indicating interpolation amount.</param>
        public static double SmoothStep(double amount)
        {
            if (amount <= 0)
                return 0;
            else if (amount >= 1)
                return 1;
            else
                return amount * amount * (3 - (2 * amount));
        }

        /// <summary>
        /// Performs a smooth(er) interpolation between 0 and 1 with 1st and 2nd order derivatives of zero at endpoints.
        /// </summary>
        /// <remarks>
        /// See https://en.wikipedia.org/wiki/Smoothstep
        /// </remarks>
        /// <param name="amount">Value between 0 and 1 indicating interpolation amount.</param>
        public static float SmootherStep(float amount)
        {
            if (amount <= 0)
                return 0;
            else if (amount >= 1)
                return 1;
            else
                return amount * amount * amount * (amount * ((amount * 6) - 15) + 10);
        }

        /// <summary>
        /// Calculates the modulo of the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="modulo">The modulo.</param>
        /// <returns>The result of the modulo applied to value</returns>
        public static float Mod(float value, float modulo)
        {
            if (modulo == 0.0f)
            {
                return value;
            }

            return value % modulo;
        }


        /// <summary>
        /// Calculates the modulo 2*PI of the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the modulo applied to value</returns>
        public static float Mod2PI(float value)
        {
            return Mod(value, TwoPi);
        }


        /// <summary>
        /// Determines whether the specified value is close to zero (0.0f).
        /// </summary>
        /// <param name="a">The floating value.</param>
        /// <returns><c>true</c> if the specified value is close to zero (0.0f); otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZero(float a)
        {
            return NearEqual(a, ZeroToleranceF);
        }

        /// <summary>
        /// Determines whether the specified value is close to zero (0.0f).
        /// </summary>
        /// <param name="a">The floating value.</param>
        /// <returns><c>true</c> if the specified value is close to zero (0.0f); otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZero(double a)
        {
            return NearEqual(a, ZeroToleranceD);
        }

        /// <summary>
        /// Determines whether the specified value is close to one (1.0f).
        /// </summary>
        /// <param name="a">The floating value.</param>
        /// <returns><c>true</c> if the specified value is close to one (1.0f); otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOne(float a)
        {
            return IsZero(a - 1.0f);
        }

        /// <summary>
        /// Determines whether the specified value is close to one (1.0f).
        /// </summary>
        /// <param name="a">The floating value.</param>
        /// <returns><c>true</c> if the specified value is close to one (1.0f); otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOne(double a)
        {
            return IsZero(a - 1.0f);
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        /// <param name="degree">The value to convert.</param>
        /// <returns>The converted value.</returns>
        public static float DegreesToRadians(float degree)
        {
            return degree * (Pi / 180.0f);
        }
        
        public static float DegreesToRadians(double degree)
        {
            return (float)(degree * (Pi / 180.0f));
        }

        /// <summary>
        /// Converts radians to degrees.
        /// </summary>
        /// <param name="radian">The value to convert.</param>
        /// <returns>The converted value.</returns>
        public static float RadiansToDegrees(float radian)
        {
            return radian * (180.0f / Pi);
        }

        /// <summary>
        /// Clamps the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>The result of clamping a value between min and max</returns>
        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        /// <summary>
        /// Clamps the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>The result of clamping a value between min and max</returns>
        public static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }

        /// <summary>
        /// Wraps the specified value into a range [min, max]
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>Result of the wrapping.</returns>
        public static int Wrap(int value, int min, int max)
        {
            if (min > max)
            {
                min = max;
            }

            if (max < min)
            {
                max = min;
            }

            // Code from http://stackoverflow.com/a/707426/1356325
            int rangeSize = max - min + 1;

            if (value < min)
                value += rangeSize * ((min - value) / rangeSize + 1);

            return min + (value - min) % rangeSize;
        }

        /// <summary>
        /// Wraps the specified value into a range [min, max[
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>Result of the wrapping.</returns>
        public static float Wrap(float value, float min, float max)
        {
            if (min > max)
            {
                min = max;
            }

            if (max < min)
            {
                max = min;
            }

            if (NearEqual(min, max)) return min;

            double mind = min;
            double maxd = max;
            double valued = value;

            var rangeSize = maxd - mind;
            return (float)(mind + (valued - mind) - (rangeSize * Math.Floor((valued - mind) / rangeSize)));
        }

        /// <summary>
        /// Wraps the specified value into a range [min, max[
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>Result of the wrapping.</returns>
        public static double Wrap(double value, double min, double max)
        {
            if (min > max)
            {
                min = max;
            }

            if (max < min)
            {
                max = min;
            }

            if (NearEqual(min, max)) return min;

            double mind = min;
            double maxd = max;
            double valued = value;

            var rangeSize = maxd - mind;
            return (mind + (valued - mind) - (rangeSize * Math.Floor((valued - mind) / rangeSize)));
        }

        public static float AngleBetween2D(Vector3F vector0, Vector3F vector1)
        {
            var vector2 = vector1 - vector0;
            var angle = (float)Math.Atan2(vector2.Y, vector2.X);
            return angle;
        }

        public static float Cross2D(Vector3F v0, Vector3F v1)
        {
            return v0.X * v1.Y - v1.X * v0.Y;
        }

        public static float AngleBetween(Vector3F vector0, Vector3F vector1, Vector3F normal)
        {
            var dot = Vector3F.Dot(vector0, vector1);
            var det = Vector3F.Dot(Vector3F.Cross(vector1, vector0), normal);
            var angle = (float)Math.Atan2(det, dot);
            return angle;
        }

        public static float AngleBetween(Vector3F vector0, Vector3F vector1, bool is360Degrees = true)
        {
            float angle = 0;
            if (is360Degrees)
            {
                var dot = Vector3F.Dot(vector0, vector1);
                var determinant = Determinant(vector0, vector1);
                angle = (float)Math.Atan2(determinant, dot);
                angle = RadiansToDegrees(angle);
                if (angle < 0)
                {
                    angle = 360 + angle;
                }
            }
            else
            {
                var dot = Vector3F.Dot(vector0, vector1);
                angle = (float)Math.Acos(dot);
            }

            return angle;
        }

        public static float AngleBetween(LineSegment2D s1, LineSegment2D s2, bool is360Degrees = true)
        {
            float angle = 0;
            if (is360Degrees)
            {
                var dot = Vector2D.Dot(s1.DirectionNormalized, s2.DirectionNormalized);
                var determinant = Determinant(s1.DirectionNormalized, s2.DirectionNormalized);
                angle = (float)Math.Atan2(determinant, dot);
                angle = RadiansToDegrees(angle);
                if (angle < 0)
                {
                    angle = 360 + angle;
                }
            }
            else
            {
                var dot = Vector2D.Dot(s1.DirectionNormalized, s2.DirectionNormalized);
                angle = (float)Math.Acos(dot);
            }

            return angle;
        }

        public static float Determinant(Vector3F v1, Vector3F v2)
        {
            return v1.X * v2.Y - v1.Y * v2.X;
        }

        public static double Determinant(Vector3D v1, Vector3D v2)
        {
            return v1.X * v2.Y - v1.Y * v2.X;
        }
        
        public static double Determinant(Vector2D v1, Vector2D v2)
        {
            return v1.X * v2.Y - v1.Y * v2.X;
        }

        public static bool IsClockwise(Vector2F point0, Vector2F point1, Vector2F point2)
        {
            var edge0 = point1 - point0;
            var edge1 = point2 - point0;

            var det = Vector2F.Determinant(edge0, edge1);

            return det > 0;

        }

        public static bool IsClockwise(Vector2D point0, Vector2D point1, Vector2D point2, Vector3F viewVector)
        {
            var edge0 = point1 - point0;
            var edge1 = point2 - point0;

            var n = Vector3F.Cross((Vector3F)edge0, (Vector3F)edge1);
            n.Normalize();
            var angle = Vector3F.Dot(n, viewVector);

            return angle > 0;

        }

        public static QuaternionF GetRotationFromMatrix(Matrix4x4F matrix)
        {
            Vector3F scale;
            Vector3F pos;
            QuaternionF orientation;
            matrix.Decompose(out scale, out orientation, out pos);
            return orientation;
        }

        /// <summary>
        /// Calculate Quaternion which will define rotation from one point to another face to face
        /// </summary>
        /// <param name="objectPosition">objectPosition is your object's position</param>
        /// <param name="targetPosition">objectToFacePosition is the position of the object to face</param>
        /// <param name="upVector">upVector is the nominal "up" vector (typically Vector3.Y)</param>
        /// <remarks>Note: this does not work when objectPosition is straight below or straight above objectToFacePosition</remarks>
        /// <returns></returns>
        public static QuaternionF RotateToFace(ref Vector3F targetPosition, ref Vector3F objectPosition, ref Vector3F upVector)
        {
            Vector3F D = (objectPosition - targetPosition);
            Vector3F right = Vector3F.Normalize(Vector3F.Cross(upVector, D));
            Vector3F backward = Vector3F.Normalize(Vector3F.Cross(right, upVector));
            Vector3F up = Vector3F.Cross(backward, right);
            Matrix4x4F rotationMatrix = new Matrix4x4F(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0, backward.X, backward.Y, backward.Z, 0, 0, 0, 0, 1);
            QuaternionF orientation;
            QuaternionF.RotationMatrix(ref rotationMatrix, out orientation);
            return orientation;
        }

        public static QuaternionF RelativeRotation(QuaternionF a, QuaternionF b)
        {
            return QuaternionF.Invert(a) * b;
        }

        // --- DIVER MATH ---
        
        public static bool IsOneDiver(double a)
        {
            return IsZero(a - 1.0);
        }
        
        public static double RadiansToDegrees(double radians)
        {
            return ((radians * 180.0) / System.Math.PI);
        }
        
        public static double Dot(Vector2D v1, Vector2D v2)
        {
            return (v1.X * v2.X + v1.Y * v2.Y);
        }
        
        public static Vector2D LineSegmentToVector(Vector2D start, Vector2D end)
        {
            return new Vector2D(end.X - start.X, end.Y - start.Y);
        }

        // find angle in degrees between to segments
        public static double DetermineAngleInDegrees(Vector2D v1Start, Vector2D v1End, Vector2D v2Start, Vector2D v2End)
        {
            Vector2D v1 = LineSegmentToVector(v1Start, v1End);
            Vector2D v2 = LineSegmentToVector(v2Start, v2End);

            double dot = Dot(v1, v2);

            double v1Length = v1.Length();
            double v2Length = v2.Length();

            double cos = dot / (v1Length * v2Length);

            sbyte sign = cos < 0 ? (sbyte)-1 : (sbyte)1;

            if (IsOneDiver(System.Math.Abs(cos)))
            {
                cos = sign;
            }

            double rad = System.Math.Acos(cos);

            return RadiansToDegrees(rad);
        }

        public static double PointToLineDistance(Vector2D start, Vector2D end, Vector2D point)
        {
            var a = (end.Y - start.Y) * point.X;
            var b = (end.X - start.X) * point.Y;
            var c = end.X * start.Y;
            var d = end.Y * start.X;

            var e = Math.Pow((end.Y - start.Y), 2);
            var f = Math.Pow((end.X - start.X), 2);

            var num = Math.Abs(a - b + c - d);
            var den = Math.Sqrt(e + f);

            return (num / den);
        }
    }
}
