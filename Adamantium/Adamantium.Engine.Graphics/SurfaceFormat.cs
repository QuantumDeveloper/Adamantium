using AdamantiumVulkan.Core;
using System;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// PixelFormat is equivalent to <see cref="AdamantiumVulkan.Format"/>.
   /// </summary>
   /// <remarks>
   /// This structure is implicitly castable to and from <see cref="AdamantiumVulkan.Format"/>, you can use it inplace where <see cref="AdamantiumVulkan.Format"/> is required
   /// and vice-versa.
   /// Usage is slightly different from <see cref="AdamantiumVulkan.Format"/>, as you have to select the type of the pixel format first (<example>Typeless, SInt</example>...etc)
   /// and then access the available pixel formats for this type. Example: PixelFormat.UNorm.R8.
   /// </remarks>
   /// <msdn-id>bb173059</msdn-id>	
   /// <unmanaged>DXGI_FORMAT</unmanaged>	
   /// <unmanaged-short>DXGI_FORMAT</unmanaged-short>	
   [StructLayout(LayoutKind.Sequential, Size = 4)]
   public struct SurfaceFormat : IEquatable<SurfaceFormat>
   {
      /// <summary>
      /// Gets the value as a <see cref="AdamantiumVulkan.Format"/> enum.
      /// </summary>
      public readonly Format Value;

      /// <summary>
      /// Internal constructor.
      /// </summary>
      /// <param name="format"></param>
      private SurfaceFormat(Format format)
      {
         Value = format;
      }

      /// <summary>
      /// Size in bytes of current Surface format
      /// </summary>
      public int SizeInBytes => FormatHelper.SizeOfInBytes(this);

      /// <summary>
      /// Corresponds to <see cref="Format.Unknown"/>
      /// </summary>
      public static readonly SurfaceFormat Unknown = new SurfaceFormat(Format.Unknown);

      /// <summary>
      /// DXGI.Format.A8 formats
      /// </summary>
      public static class A8
      {
         /// <summary>
         /// Corresponds to <see cref="Format.A8_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.A8_UNorm);

      }

      /// <summary>
      /// DXGI.Format.B5G5R5A1 formats
      /// </summary>
      public static class B5G5R5A1
      {
         /// <summary>
         /// Corresponds to <see cref="Format.B5G5R5A1_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B5G5R5A1_UNorm);

      }

      /// <summary>
      /// DXGI.Format.B5G6R5 formats
      /// </summary>
      public static class B5G6R5
      {
         /// <summary>
         /// Corresponds to <see cref="Format.B5G6R5_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B5G6R5_UNorm);

      }

      /// <summary>
      /// DXGI.Format.B8G8R8A8 formats
      /// </summary>
      public static class B8G8R8A8
      {
         /// <summary>
         /// Corresponds to <see cref="Format.B8G8R8A8_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.B8G8R8A8_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.B8G8R8A8_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B8G8R8A8_UNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.B8G8R8A8_UNorm_SRgb"/>
         /// </summary>
         public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.B8G8R8A8_UNorm_SRgb);

      }

      /// <summary>
      /// DXGI.Format.B8G8R8X8 formats
      /// </summary>
      public static class B8G8R8X8
      {
         /// <summary>
         /// Corresponds to <see cref="Format.B8G8R8X8_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.B8G8R8X8_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.B8G8R8X8_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B8G8R8X8_UNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.B8G8R8X8_UNorm_SRgb"/>
         /// </summary>
         public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.B8G8R8X8_UNorm_SRgb);

      }

      /// <summary>
      /// DXGI.Format.BC1 formats
      /// </summary>
      public static class BC1
      {
         /// <summary>
         /// Corresponds to <see cref="Format.BC1_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC1_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.BC1_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC1_UNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.BC1_UNorm_SRgb"/>
         /// </summary>
         public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.BC1_UNorm_SRgb);

      }

      /// <summary>
      /// DXGI.Format.BC2 formats
      /// </summary>
      public static class BC2
      {
         /// <summary>
         /// Corresponds to <see cref="Format.BC2_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC2_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.BC2_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC2_UNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.BC2_UNorm_SRgb"/>
         /// </summary>
         public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.BC2_UNorm_SRgb);

      }

      /// <summary>
      /// DXGI.Format.BC3 formats
      /// </summary>
      public static class BC3
      {
         /// <summary>
         /// Corresponds to <see cref="Format.BC3_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC3_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.BC3_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC3_UNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.BC3_UNorm_SRgb"/>
         /// </summary>
         public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.BC3_UNorm_SRgb);

      }

      /// <summary>
      /// DXGI.Format.BC4 formats
      /// </summary>
      public static class BC4
      {
         /// <summary>
         /// Corresponds to <see cref="Format.BC4_SNorm"/>
         /// </summary>
         public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.BC4_SNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.BC4_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC4_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.BC4_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC4_UNorm);

      }

      /// <summary>
      /// DXGI.Format.BC5 formats
      /// </summary>
      public static class BC5
      {
         /// <summary>
         /// Corresponds to <see cref="Format.BC5_SNorm"/>
         /// </summary>
         public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.BC5_SNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.BC5_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC5_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.BC5_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC5_UNorm);

      }

      /// <summary>
      /// DXGI.Format.BC6H formats
      /// </summary>
      public static class BC6H
      {
         /// <summary>
         /// Corresponds to <see cref="Format.BC6H_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC6H_Typeless);

      }

      /// <summary>
      /// DXGI.Format.BC7 formats
      /// </summary>
      public static class BC7
      {
         /// <summary>
         /// Corresponds to <see cref="Format.BC7_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC7_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.BC7_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC7_UNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.BC7_UNorm_SRgb"/>
         /// </summary>
         public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.BC7_UNorm_SRgb);

      }

      /// <summary>
      /// DXGI.Format.R10G10B10A2 formats
      /// </summary>
      public static class R10G10B10A2
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R10G10B10A2_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R10G10B10A2_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R10G10B10A2_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R10G10B10A2_UInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R10G10B10A2_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R10G10B10A2_UNorm);

      }

      /// <summary>
      /// DXGI.Format.R11G11B10 formats
      /// </summary>
      public static class R11G11B10
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R11G11B10_Float"/>
         /// </summary>
         public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R11G11B10_Float);

      }

      /// <summary>
      /// DXGI.Format.R16 formats
      /// </summary>
      public static class R16
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R16_Float"/>
         /// </summary>
         public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R16_Float);

         /// <summary>
         /// Corresponds to <see cref="Format.R16_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R16_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R16_SNorm"/>
         /// </summary>
         public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R16_SNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.R16_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R16_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R16_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R16_UInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R16_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R16_UNorm);

      }

      /// <summary>
      /// DXGI.Format.R16G16 formats
      /// </summary>
      public static class R16G16
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R16G16_Float"/>
         /// </summary>
         public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R16G16_Float);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R16G16_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16_SNorm"/>
         /// </summary>
         public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R16G16_SNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R16G16_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R16G16_UInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R16G16_UNorm);

      }

      /// <summary>
      /// DXGI.Format.R16G16B16A16 formats
      /// </summary>
      public static class R16G16B16A16
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R16G16B16A16_Float"/>
         /// </summary>
         public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R16G16B16A16_Float);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16B16A16_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R16G16B16A16_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16B16A16_SNorm"/>
         /// </summary>
         public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R16G16B16A16_SNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16B16A16_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R16G16B16A16_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16B16A16_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R16G16B16A16_UInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R16G16B16A16_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R16G16B16A16_UNorm);

      }

      /// <summary>
      /// DXGI.Format.R32 formats
      /// </summary>
      public static class R32
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R32_Float"/>
         /// </summary>
         public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R32_Float);

         /// <summary>
         /// Corresponds to <see cref="Format.R32_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R32_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R32_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R32_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R32_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R32_UInt);

      }

      /// <summary>
      /// DXGI.Format.R32G32 formats
      /// </summary>
      public static class R32G32
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R32G32_Float"/>
         /// </summary>
         public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R32G32_Float);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R32G32_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R32G32_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R32G32_UInt);

      }

      /// <summary>
      /// DXGI.Format.R32G32B32 formats
      /// </summary>
      public static class R32G32B32
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R32G32B32_Float"/>
         /// </summary>
         public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R32G32B32_Float);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32B32_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R32G32B32_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32B32_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R32G32B32_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32B32_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R32G32B32_UInt);
      }

      /// <summary>
      /// DXGI.Format.R32G32B32A32 formats
      /// </summary>
      public static class R32G32B32A32
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R32G32B32A32_Float"/>
         /// </summary>
         public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R32G32B32A32_Float);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32B32A32_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R32G32B32A32_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32B32A32_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R32G32B32A32_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R32G32B32A32_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R32G32B32A32_UInt);
      }


      /// <summary>
      /// DXGI.Format.R8 formats
      /// </summary>
      public static class R8
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R8_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R8_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R8_SNorm"/>
         /// </summary>
         public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R8_SNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.R8_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R8_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R8_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R8_UInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R8_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R8_UNorm);

      }

      /// <summary>
      /// DXGI.Format.R8G8 formats
      /// </summary>
      public static class R8G8
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R8G8_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R8G8_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8_SNorm"/>
         /// </summary>
         public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R8G8_SNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R8G8_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R8G8_UInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R8G8_UNorm);
      }

      /// <summary>
      /// DXGI.Format.R8G8B8A8 formats
      /// </summary>
      public static class R8G8B8A8
      {
         /// <summary>
         /// Corresponds to <see cref="Format.R8G8B8A8_SInt"/>
         /// </summary>
         public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R8G8B8A8_SInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8B8A8_SNorm"/>
         /// </summary>
         public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R8G8B8A8_SNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8B8A8_Typeless"/>
         /// </summary>
         public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R8G8B8A8_Typeless);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8B8A8_UInt"/>
         /// </summary>
         public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R8G8B8A8_UInt);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8B8A8_UNorm"/>
         /// </summary>
         public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R8G8B8A8_UNorm);

         /// <summary>
         /// Corresponds to <see cref="Format.R8G8B8A8_UNorm_SRgb"/>
         /// </summary>
         public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.R8G8B8A8_UNorm_SRgb);
      }


      /// <summary>
      /// Returns <see cref="Format"/> from <see cref="SurfaceFormat"/>
      /// </summary>
      /// <param name="from"></param>
      /// <returns></returns>
      public static implicit operator Format(SurfaceFormat from)
      {
         return from.Value;
      }

      /// <summary>
      /// Returns <see cref="SurfaceFormat"/> form <see cref="Format"/>
      /// </summary>
      /// <param name="from">DXGI Format</param>
      /// <returns></returns>
      public static implicit operator SurfaceFormat(Format from)
      {
         return new SurfaceFormat(from);
      }

      /// <summary>
      /// Indicates whether the current object is equal to another object of the same type.
      /// </summary>
      /// <returns>
      /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
      /// </returns>
      /// <param name="other"><see cref="SurfaceFormat"/> to compare</param>
      public bool Equals(SurfaceFormat other)
      {
         return Value == other.Value;
      }

      /// <summary>
      /// Indicates whether this instance and a specified object are equal.
      /// </summary>
      /// <returns>
      /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false. 
      /// </returns>
      /// <param name="obj">The object to compare with the current instance. </param><filterpriority>2</filterpriority>
      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is SurfaceFormat && Equals((SurfaceFormat)obj);
      }

      /// <summary>
      /// Returns the hash code for this instance.
      /// </summary>
      /// <returns>
      /// A 32-bit signed integer that is the hash code for this instance.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public override int GetHashCode()
      {
         return Value.GetHashCode();
      }

      /// <summary>
      /// Overload for comparing 2 <see cref="SurfaceFormat"/> variables
      /// </summary>
      /// <param name="left">left <see cref="SurfaceFormat"/></param>
      /// <param name="right">right <see cref="SurfaceFormat"/></param>
      /// <returns></returns>
      public static bool operator ==(SurfaceFormat left, SurfaceFormat right)
      {
         return left.Equals(right);
      }


      /// <summary>
      /// Overload for comparing 2 <see cref="SurfaceFormat"/> variables
      /// </summary>
      /// <param name="left">left <see cref="SurfaceFormat"/></param>
      /// <param name="right">right <see cref="SurfaceFormat"/></param>
      /// <returns></returns>
      public static bool operator !=(SurfaceFormat left, SurfaceFormat right)
      {
         return !left.Equals(right);
      }

      /// <summary>
      /// Returns the fully qualified type name of this instance.
      /// </summary>
      /// <returns>
      /// A <see cref="System.String"/> containing a <see cref="SurfaceFormat"/>.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public override string ToString()
      {
         return $"{Value}";
      }
   }
}
