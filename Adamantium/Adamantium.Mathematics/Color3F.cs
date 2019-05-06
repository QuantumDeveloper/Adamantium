using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Adamantium.Mathematics
{
   /// <summary>
   /// Represents a color in the form of rgb.
   /// </summary>
   [StructLayout(LayoutKind.Sequential, Pack = 4)]
   public struct Color3F : IEquatable<Color3F>, IFormattable
   {
      private const string toStringFormat = "Red:{0} Green:{1} Blue:{2}";

      /// <summary>
      /// The Black color (0, 0, 0).
      /// </summary>
      public static readonly Color3F Black = new Color3F(0.0f, 0.0f, 0.0f);

      /// <summary>
      /// The White color (1, 1, 1, 1).
      /// </summary>
      public static readonly Color3F White = new Color3F(1.0f, 1.0f, 1.0f);

      /// <summary>
      /// The red component of the color.
      /// </summary>
      public float Red;

      /// <summary>
      /// The green component of the color.
      /// </summary>
      public float Green;

      /// <summary>
      /// The blue component of the color.
      /// </summary>
      public float Blue;

      /// <summary>
      /// Initializes a new instance of the <see cref="Color3F"/> struct.
      /// </summary>
      /// <param name="value">The value that will be assigned to all components.</param>
      public Color3F(float value)
      {
         Red = Green = Blue = value;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="Color3F"/> struct.
      /// </summary>
      /// <param name="red">The red component of the color.</param>
      /// <param name="green">The green component of the color.</param>
      /// <param name="blue">The blue component of the color.</param>
      public Color3F(float red, float green, float blue)
      {
         Red = red;
         Green = green;
         Blue = blue;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="Color3F"/> struct.
      /// </summary>
      /// <param name="value">The red, green, and blue components of the color.</param>
      public Color3F(Vector3F value)
      {
         Red = value.X;
         Green = value.Y;
         Blue = value.Z;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="Color3F"/> struct.
      /// </summary>
      /// <param name="rgb">A packed integer containing all three color components in RGB order.
      /// The alpha component is ignored.</param>
      public Color3F(int rgb)
      {
         Blue = ((rgb >> 16) & 255) / 255.0f;
         Green = ((rgb >> 8) & 255) / 255.0f;
         Red = (rgb & 255) / 255.0f;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="Color3F"/> struct.
      /// </summary>
      /// <param name="values">The values to assign to the red, green, and blue components of the color. This must be an array with three elements.</param>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <c>null</c>.</exception>
      /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="values"/> contains more or less than four elements.</exception>
      public Color3F(float[] values)
      {
         if (values == null)
            throw new ArgumentNullException("values");
         if (values.Length != 3)
            throw new ArgumentOutOfRangeException("values", "There must be three and only three input values for Color3.");

         Red = values[0];
         Green = values[1];
         Blue = values[2];
      }

      /// <summary>
      /// Gets or sets the component at the specified index.
      /// </summary>
      /// <value>The value of the red, green, or blue component, depending on the index.</value>
      /// <param name="index">The index of the component to access. Use 0 for the red component, 1 for the green component, and 2 for the blue component.</param>
      /// <returns>The value of the component at the specified index.</returns>
      /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the <paramref name="index"/> is out of the range [0, 2].</exception>
      public float this[int index]
      {
         get
         {
            switch (index)
            {
               case 0: return Red;
               case 1: return Green;
               case 2: return Blue;
            }

            throw new ArgumentOutOfRangeException("index", "Indices for Color3 run from 0 to 2, inclusive.");
         }

         set
         {
            switch (index)
            {
               case 0: Red = value; break;
               case 1: Green = value; break;
               case 2: Blue = value; break;
               default: throw new ArgumentOutOfRangeException("index", "Indices for Color3 run from 0 to 2, inclusive.");
            }
         }
      }

      /// <summary>
      /// Converts the color into a packed integer.
      /// </summary>
      /// <returns>A packed integer containing all three color components.
      /// The alpha channel is set to 255.</returns>
      public int ToRgba()
      {
         uint a = 255;
         uint r = (uint)(Red * 255.0f) & 255;
         uint g = (uint)(Green * 255.0f) & 255;
         uint b = (uint)(Blue * 255.0f) & 255;

         uint value = r;
         value |= g << 8;
         value |= b << 16;
         value |= a << 24;

         return (int)value;
      }

      /// <summary>
      /// Converts the color into a packed integer.
      /// </summary>
      /// <returns>A packed integer containing all three color components.
      /// The alpha channel is set to 255.</returns>
      public int ToBgra()
      {
         uint a = 255;
         uint r = (uint)(Red * 255.0f) & 255;
         uint g = (uint)(Green * 255.0f) & 255;
         uint b = (uint)(Blue * 255.0f) & 255;

         uint value = b;
         value |= g << 8;
         value |= r << 16;
         value |= a << 24;

         return (int)value;
      }

      /// <summary>
      /// Converts the color into a three component vector.
      /// </summary>
      /// <returns>A three component vector containing the red, green, and blue components of the color.</returns>
      public Vector3F ToVector3()
      {
         return new Vector3F(Red, Green, Blue);
      }

      /// <summary>
      /// Creates an array containing the elements of the color.
      /// </summary>
      /// <returns>A three-element array containing the components of the color.</returns>
      public float[] ToArray()
      {
         return new float[] { Red, Green, Blue };
      }

      /// <summary>
      /// Adds two colors.
      /// </summary>
      /// <param name="left">The first color to add.</param>
      /// <param name="right">The second color to add.</param>
      /// <param name="result">When the method completes, completes the sum of the two colors.</param>
      public static void Add(ref Color3F left, ref Color3F right, out Color3F result)
      {
         result.Red = left.Red + right.Red;
         result.Green = left.Green + right.Green;
         result.Blue = left.Blue + right.Blue;
      }

      /// <summary>
      /// Adds two colors.
      /// </summary>
      /// <param name="left">The first color to add.</param>
      /// <param name="right">The second color to add.</param>
      /// <returns>The sum of the two colors.</returns>
      public static Color3F Add(Color3F left, Color3F right)
      {
         return new Color3F(left.Red + right.Red, left.Green + right.Green, left.Blue + right.Blue);
      }

      /// <summary>
      /// Subtracts two colors.
      /// </summary>
      /// <param name="left">The first color to subtract.</param>
      /// <param name="right">The second color to subtract.</param>
      /// <param name="result">WHen the method completes, contains the difference of the two colors.</param>
      public static void Subtract(ref Color3F left, ref Color3F right, out Color3F result)
      {
         result.Red = left.Red - right.Red;
         result.Green = left.Green - right.Green;
         result.Blue = left.Blue - right.Blue;
      }

      /// <summary>
      /// Subtracts two colors.
      /// </summary>
      /// <param name="left">The first color to subtract.</param>
      /// <param name="right">The second color to subtract</param>
      /// <returns>The difference of the two colors.</returns>
      public static Color3F Subtract(Color3F left, Color3F right)
      {
         return new Color3F(left.Red - right.Red, left.Green - right.Green, left.Blue - right.Blue);
      }

      /// <summary>
      /// Modulates two colors.
      /// </summary>
      /// <param name="left">The first color to modulate.</param>
      /// <param name="right">The second color to modulate.</param>
      /// <param name="result">When the method completes, contains the modulated color.</param>
      public static void Modulate(ref Color3F left, ref Color3F right, out Color3F result)
      {
         result.Red = left.Red * right.Red;
         result.Green = left.Green * right.Green;
         result.Blue = left.Blue * right.Blue;
      }

      /// <summary>
      /// Modulates two colors.
      /// </summary>
      /// <param name="left">The first color to modulate.</param>
      /// <param name="right">The second color to modulate.</param>
      /// <returns>The modulated color.</returns>
      public static Color3F Modulate(Color3F left, Color3F right)
      {
         return new Color3F(left.Red * right.Red, left.Green * right.Green, left.Blue * right.Blue);
      }

      /// <summary>
      /// Scales a color.
      /// </summary>
      /// <param name="value">The color to scale.</param>
      /// <param name="scale">The amount by which to scale.</param>
      /// <param name="result">When the method completes, contains the scaled color.</param>
      public static void Scale(ref Color3F value, float scale, out Color3F result)
      {
         result.Red = value.Red * scale;
         result.Green = value.Green * scale;
         result.Blue = value.Blue * scale;
      }

      /// <summary>
      /// Scales a color.
      /// </summary>
      /// <param name="value">The color to scale.</param>
      /// <param name="scale">The amount by which to scale.</param>
      /// <returns>The scaled color.</returns>
      public static Color3F Scale(Color3F value, float scale)
      {
         return new Color3F(value.Red * scale, value.Green * scale, value.Blue * scale);
      }

      /// <summary>
      /// Negates a color.
      /// </summary>
      /// <param name="value">The color to negate.</param>
      /// <param name="result">When the method completes, contains the negated color.</param>
      public static void Negate(ref Color3F value, out Color3F result)
      {
         result.Red = 1.0f - value.Red;
         result.Green = 1.0f - value.Green;
         result.Blue = 1.0f - value.Blue;
      }

      /// <summary>
      /// Negates a color.
      /// </summary>
      /// <param name="value">The color to negate.</param>
      /// <returns>The negated color.</returns>
      public static Color3F Negate(Color3F value)
      {
         return new Color3F(1.0f - value.Red, 1.0f - value.Green, 1.0f - value.Blue);
      }

      /// <summary>
      /// Restricts a value to be within a specified range.
      /// </summary>
      /// <param name="value">The value to clamp.</param>
      /// <param name="min">The minimum value.</param>
      /// <param name="max">The maximum value.</param>
      /// <param name="result">When the method completes, contains the clamped value.</param>
      public static void Clamp(ref Color3F value, ref Color3F min, ref Color3F max, out Color3F result)
      {
         float red = value.Red;
         red = (red > max.Red) ? max.Red : red;
         red = (red < min.Red) ? min.Red : red;

         float green = value.Green;
         green = (green > max.Green) ? max.Green : green;
         green = (green < min.Green) ? min.Green : green;

         float blue = value.Blue;
         blue = (blue > max.Blue) ? max.Blue : blue;
         blue = (blue < min.Blue) ? min.Blue : blue;

         result = new Color3F(red, green, blue);
      }

      /// <summary>
      /// Restricts a value to be within a specified range.
      /// </summary>
      /// <param name="value">The value to clamp.</param>
      /// <param name="min">The minimum value.</param>
      /// <param name="max">The maximum value.</param>
      /// <returns>The clamped value.</returns>
      public static Color3F Clamp(Color3F value, Color3F min, Color3F max)
      {
         Color3F result;
         Clamp(ref value, ref min, ref max, out result);
         return result;
      }

      /// <summary>
      /// Performs a linear interpolation between two colors.
      /// </summary>
      /// <param name="start">Start color.</param>
      /// <param name="end">End color.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <param name="result">When the method completes, contains the linear interpolation of the two colors.</param>
      /// <remarks>
      /// Passing <paramref name="amount"/> a value of 0 will cause <paramref name="start"/> to be returned; a value of 1 will cause <paramref name="end"/> to be returned. 
      /// </remarks>
      public static void Lerp(ref Color3F start, ref Color3F end, float amount, out Color3F result)
      {
         result.Red = MathHelper.Lerp(start.Red, end.Red, amount);
         result.Green = MathHelper.Lerp(start.Green, end.Green, amount);
         result.Blue = MathHelper.Lerp(start.Blue, end.Blue, amount);
      }

      /// <summary>
      /// Performs a linear interpolation between two colors.
      /// </summary>
      /// <param name="start">Start color.</param>
      /// <param name="end">End color.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <returns>The linear interpolation of the two colors.</returns>
      /// <remarks>
      /// Passing <paramref name="amount"/> a value of 0 will cause <paramref name="start"/> to be returned; a value of 1 will cause <paramref name="end"/> to be returned. 
      /// </remarks>
      public static Color3F Lerp(Color3F start, Color3F end, float amount)
      {
         Color3F result;
         Lerp(ref start, ref end, amount, out result);
         return result;
      }

      /// <summary>
      /// Performs a cubic interpolation between two colors.
      /// </summary>
      /// <param name="start">Start color.</param>
      /// <param name="end">End color.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <param name="result">When the method completes, contains the cubic interpolation of the two colors.</param>
      public static void SmoothStep(ref Color3F start, ref Color3F end, float amount, out Color3F result)
      {
         amount = MathHelper.SmoothStep(amount);
         Lerp(ref start, ref end, amount, out result);
      }

      /// <summary>
      /// Performs a cubic interpolation between two colors.
      /// </summary>
      /// <param name="start">Start color.</param>
      /// <param name="end">End color.</param>
      /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/>.</param>
      /// <returns>The cubic interpolation of the two colors.</returns>
      public static Color3F SmoothStep(Color3F start, Color3F end, float amount)
      {
         Color3F result;
         SmoothStep(ref start, ref end, amount, out result);
         return result;
      }

      /// <summary>
      /// Returns a color containing the smallest components of the specified colors.
      /// </summary>
      /// <param name="left">The first source color.</param>
      /// <param name="right">The second source color.</param>
      /// <param name="result">When the method completes, contains an new color composed of the largest components of the source colors.</param>
      public static void Max(ref Color3F left, ref Color3F right, out Color3F result)
      {
         result.Red = (left.Red > right.Red) ? left.Red : right.Red;
         result.Green = (left.Green > right.Green) ? left.Green : right.Green;
         result.Blue = (left.Blue > right.Blue) ? left.Blue : right.Blue;
      }

      /// <summary>
      /// Returns a color containing the largest components of the specified colors.
      /// </summary>
      /// <param name="left">The first source color.</param>
      /// <param name="right">The second source color.</param>
      /// <returns>A color containing the largest components of the source colors.</returns>
      public static Color3F Max(Color3F left, Color3F right)
      {
         Color3F result;
         Max(ref left, ref right, out result);
         return result;
      }

      /// <summary>
      /// Returns a color containing the smallest components of the specified colors.
      /// </summary>
      /// <param name="left">The first source color.</param>
      /// <param name="right">The second source color.</param>
      /// <param name="result">When the method completes, contains an new color composed of the smallest components of the source colors.</param>
      public static void Min(ref Color3F left, ref Color3F right, out Color3F result)
      {
         result.Red = (left.Red < right.Red) ? left.Red : right.Red;
         result.Green = (left.Green < right.Green) ? left.Green : right.Green;
         result.Blue = (left.Blue < right.Blue) ? left.Blue : right.Blue;
      }

      /// <summary>
      /// Returns a color containing the smallest components of the specified colors.
      /// </summary>
      /// <param name="left">The first source color.</param>
      /// <param name="right">The second source color.</param>
      /// <returns>A color containing the smallest components of the source colors.</returns>
      public static Color3F Min(Color3F left, Color3F right)
      {
         Color3F result;
         Min(ref left, ref right, out result);
         return result;
      }

      /// <summary>
      /// Adjusts the contrast of a color.
      /// </summary>
      /// <param name="value">The color whose contrast is to be adjusted.</param>
      /// <param name="contrast">The amount by which to adjust the contrast.</param>
      /// <param name="result">When the method completes, contains the adjusted color.</param>
      public static void AdjustContrast(ref Color3F value, float contrast, out Color3F result)
      {
         result.Red = 0.5f + contrast * (value.Red - 0.5f);
         result.Green = 0.5f + contrast * (value.Green - 0.5f);
         result.Blue = 0.5f + contrast * (value.Blue - 0.5f);
      }

      /// <summary>
      /// Adjusts the contrast of a color.
      /// </summary>
      /// <param name="value">The color whose contrast is to be adjusted.</param>
      /// <param name="contrast">The amount by which to adjust the contrast.</param>
      /// <returns>The adjusted color.</returns>
      public static Color3F AdjustContrast(Color3F value, float contrast)
      {
         return new Color3F(
             0.5f + contrast * (value.Red - 0.5f),
             0.5f + contrast * (value.Green - 0.5f),
             0.5f + contrast * (value.Blue - 0.5f));
      }

      /// <summary>
      /// Adjusts the saturation of a color.
      /// </summary>
      /// <param name="value">The color whose saturation is to be adjusted.</param>
      /// <param name="saturation">The amount by which to adjust the saturation.</param>
      /// <param name="result">When the method completes, contains the adjusted color.</param>
      public static void AdjustSaturation(ref Color3F value, float saturation, out Color3F result)
      {
         float grey = value.Red * 0.2125f + value.Green * 0.7154f + value.Blue * 0.0721f;

         result.Red = grey + saturation * (value.Red - grey);
         result.Green = grey + saturation * (value.Green - grey);
         result.Blue = grey + saturation * (value.Blue - grey);
      }

      /// <summary>
      /// Adjusts the saturation of a color.
      /// </summary>
      /// <param name="value">The color whose saturation is to be adjusted.</param>
      /// <param name="saturation">The amount by which to adjust the saturation.</param>
      /// <returns>The adjusted color.</returns>
      public static Color3F AdjustSaturation(Color3F value, float saturation)
      {
         float grey = value.Red * 0.2125f + value.Green * 0.7154f + value.Blue * 0.0721f;

         return new Color3F(
             grey + saturation * (value.Red - grey),
             grey + saturation * (value.Green - grey),
             grey + saturation * (value.Blue - grey));
      }

      /// <summary>
      /// Computes the premultiplied value of the provided color.
      /// </summary>
      /// <param name="value">The non-premultiplied value.</param>
      /// <param name="alpha">The color alpha.</param>
      /// <param name="result">The premultiplied result.</param>
      public static void Premultiply(ref Color3F value, float alpha, out Color3F result)
      {
         result.Red = value.Red * alpha;
         result.Green = value.Green * alpha;
         result.Blue = value.Blue * alpha;
      }

      /// <summary>
      /// Computes the premultiplied value of the provided color.
      /// </summary>
      /// <param name="value">The non-premultiplied value.</param>
      /// <param name="alpha">The color alpha.</param>
      /// <returns>The premultiplied color.</returns>
      public static Color3F Premultiply(Color3F value, float alpha)
      {
         Color3F result;
         Premultiply(ref value, alpha, out result);
         return result;
      }

      /// <summary>
      /// Adds two colors.
      /// </summary>
      /// <param name="left">The first color to add.</param>
      /// <param name="right">The second color to add.</param>
      /// <returns>The sum of the two colors.</returns>
      public static Color3F operator +(Color3F left, Color3F right)
      {
         return new Color3F(left.Red + right.Red, left.Green + right.Green, left.Blue + right.Blue);
      }

      /// <summary>
      /// Assert a color (return it unchanged).
      /// </summary>
      /// <param name="value">The color to assert (unchanged).</param>
      /// <returns>The asserted (unchanged) color.</returns>
      public static Color3F operator +(Color3F value)
      {
         return value;
      }

      /// <summary>
      /// Subtracts two colors.
      /// </summary>
      /// <param name="left">The first color to subtract.</param>
      /// <param name="right">The second color to subtract.</param>
      /// <returns>The difference of the two colors.</returns>
      public static Color3F operator -(Color3F left, Color3F right)
      {
         return new Color3F(left.Red - right.Red, left.Green - right.Green, left.Blue - right.Blue);
      }

      /// <summary>
      /// Negates a color.
      /// </summary>
      /// <param name="value">The color to negate.</param>
      /// <returns>A negated color.</returns>
      public static Color3F operator -(Color3F value)
      {
         return new Color3F(-value.Red, -value.Green, -value.Blue);
      }

      /// <summary>
      /// Scales a color.
      /// </summary>
      /// <param name="scale">The factor by which to scale the color.</param>
      /// <param name="value">The color to scale.</param>
      /// <returns>The scaled color.</returns>
      public static Color3F operator *(float scale, Color3F value)
      {
         return new Color3F(value.Red * scale, value.Green * scale, value.Blue * scale);
      }

      /// <summary>
      /// Scales a color.
      /// </summary>
      /// <param name="value">The factor by which to scale the color.</param>
      /// <param name="scale">The color to scale.</param>
      /// <returns>The scaled color.</returns>
      public static Color3F operator *(Color3F value, float scale)
      {
         return new Color3F(value.Red * scale, value.Green * scale, value.Blue * scale);
      }

      /// <summary>
      /// Modulates two colors.
      /// </summary>
      /// <param name="left">The first color to modulate.</param>
      /// <param name="right">The second color to modulate.</param>
      /// <returns>The modulated color.</returns>
      public static Color3F operator *(Color3F left, Color3F right)
      {
         return new Color3F(left.Red * right.Red, left.Green * right.Green, left.Blue * right.Blue);
      }

      /// <summary>
      /// Tests for equality between two objects.
      /// </summary>
      /// <param name="left">The first value to compare.</param>
      /// <param name="right">The second value to compare.</param>
      /// <returns><c>true</c> if <paramref name="left"/> has the same value as <paramref name="right"/>; otherwise, <c>false</c>.</returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public static bool operator ==(Color3F left, Color3F right)
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
      public static bool operator !=(Color3F left, Color3F right)
      {
         return !left.Equals(ref right);
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="Color3F"/> to <see cref="Color4F"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static explicit operator Color4F(Color3F value)
      {
         return new Color4F(value.Red, value.Green, value.Blue, 1.0f);
      }

      /// <summary>
      /// Performs an implicit conversion from <see cref="Color3F"/> to <see cref="Vector3F"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static implicit operator Vector3F(Color3F value)
      {
         return new Vector3F(value.Red, value.Green, value.Blue);
      }

      /// <summary>
      /// Performs an implicit conversion from <see cref="Vector3F"/> to <see cref="Color3F"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static implicit operator Color3F(Vector3F value)
      {
         return new Color3F(value.X, value.Y, value.Z);
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="System.Int32"/> to <see cref="Color3F"/>.
      /// </summary>
      /// <param name="value">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static explicit operator Color3F(int value)
      {
         return new Color3F(value);
      }

      /// <summary>
      /// Returns a <see cref="System.String"/> that represents this instance.
      /// </summary>
      /// <returns>
      /// A <see cref="System.String"/> that represents this instance.
      /// </returns>
      public override string ToString()
      {
         return ToString(CultureInfo.CurrentCulture);
      }

      /// <summary>
      /// Returns a <see cref="System.String"/> that represents this instance.
      /// </summary>
      /// <param name="format">The format to apply to each channel element (float)</param>
      /// <returns>
      /// A <see cref="System.String"/> that represents this instance.
      /// </returns>
      public string ToString(string format)
      {
         return ToString(format, CultureInfo.CurrentCulture);
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
         return string.Format(formatProvider, toStringFormat, Red, Green, Blue);
      }

      /// <summary>
      /// Returns a <see cref="System.String"/> that represents this instance.
      /// </summary>
      /// <param name="format">The format to apply to each channel element (float).</param>
      /// <param name="formatProvider">The format provider.</param>
      /// <returns>
      /// A <see cref="System.String"/> that represents this instance.
      /// </returns>
      public string ToString(string format, IFormatProvider formatProvider)
      {
         if (format == null)
            return ToString(formatProvider);

         return string.Format(formatProvider,
                              toStringFormat,
                              Red.ToString(format, formatProvider),
                              Green.ToString(format, formatProvider),
                              Blue.ToString(format, formatProvider));
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
            var hashCode = Red.GetHashCode();
            hashCode = (hashCode * 397) ^ Green.GetHashCode();
            hashCode = (hashCode * 397) ^ Blue.GetHashCode();
            return hashCode;
         }
      }

      /// <summary>
      /// Determines whether the specified <see cref="Color3F"/> is equal to this instance.
      /// </summary>
      /// <param name="other">The <see cref="Color3F"/> to compare with this instance.</param>
      /// <returns>
      /// <c>true</c> if the specified <see cref="Color3F"/> is equal to this instance; otherwise, <c>false</c>.
      /// </returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Equals(ref Color3F other)
      {
         return Red == other.Red && Green == other.Green && Blue == other.Blue;
      }

      /// <summary>
      /// Determines whether the specified <see cref="Color3F"/> is equal to this instance.
      /// </summary>
      /// <param name="other">The <see cref="Color3F"/> to compare with this instance.</param>
      /// <returns>
      /// <c>true</c> if the specified <see cref="Color3F"/> is equal to this instance; otherwise, <c>false</c>.
      /// </returns>
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      public bool Equals(Color3F other)
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
         if (!(value is Color3F))
            return false;

         var strongValue = (Color3F)value;
         return Equals(ref strongValue);
      }

   }
}
