using System;
using System.Runtime.InteropServices;

namespace Adamantium.Imaging
{
   /// <summary>
   /// A simple wrapper to specify number of mipmaps.
   ///  Set to true to specify all mipmaps or sets an integer value >= 1
   /// to specify the exact number of mipmaps.
   /// </summary>
   /// <remarks>
   /// This structure use implicit conversion:
   /// <ul>
   /// <li>Set to <c>true</c> to specify all mipmaps.</li>
   /// <li>Set to <c>false</c> to specify a single mipmap.</li>
   /// <li>Set to an integer value >=1 to specify an exact count of mipmaps.</li>
   /// </ul>
   /// </remarks>
   [StructLayout(LayoutKind.Sequential, Size = 4)]
   public struct MipMapCount : IEquatable<MipMapCount>
   {
      /// <summary>
      /// Automatic mipmap level based on texture size.
      /// </summary>
      public static readonly MipMapCount Auto = new MipMapCount(true);

      /// <summary>
      /// Initializes a new instance of the <see cref="MipMapCount" /> struct.
      /// </summary>
      /// <param name="allMipMaps">if set to <c>true</c> generates all mip maps.</param>
      public MipMapCount(bool allMipMaps)
      {
         this.Count = allMipMaps ? 0u : 1;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="MipMapCount" /> struct.
      /// </summary>
      /// <param name="count">The count.</param>
      public MipMapCount(uint count)
      {
         this.Count = count;
      }

      /// <summary>
      /// Number of mipmaps.
      /// </summary>
      /// <remarks>
      /// Zero(0) means generate all mipmaps. One(1) generates a single mipmap... etc.
      /// </remarks>
      public readonly uint Count;

      public bool Equals(MipMapCount other)
      {
         return this.Count == other.Count;
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj))
            return false;
         return obj is MipMapCount && Equals((MipMapCount)obj);
      }

      public override int GetHashCode()
      {
         return this.Count.GetHashCode();
      }

      public static bool operator ==(MipMapCount left, MipMapCount right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(MipMapCount left, MipMapCount right)
      {
         return !left.Equals(right);
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="MipMapCount"/> to <see cref="bool"/>.
      /// </summary>
      /// <param name="mipMap">The value.</param>
      /// <returns>The result of the conversion.</returns>
      public static implicit operator bool (MipMapCount mipMap)
      {
         return mipMap.Count == 0;
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="bool"/> to <see cref="MipMapCount"/>.
      /// </summary>
      /// <param name="mipMapAll">True to generate all mipmaps, false to use a single mipmap.</param>
      /// <returns>The result of the conversion.</returns>
      public static implicit operator MipMapCount(bool mipMapAll)
      {
         return new MipMapCount(mipMapAll);
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="MipMapCount"/> to <see cref="int"/>.
      /// </summary>
      /// <param name="mipMap">The value.</param>
      /// <returns>The count of mipmap (0 means all mipmaps).</returns>
      public static implicit operator uint (MipMapCount mipMap)
      {
         return mipMap.Count;
      }

      /// <summary>
      /// Performs an explicit conversion from <see cref="int"/> to <see cref="MipMapCount"/>.
      /// </summary>
      /// <param name="mipMapCount">True to generate all mipmaps, false to use a single mipmap.</param>
      /// <returns>The result of the conversion.</returns>
      public static implicit operator MipMapCount(uint mipMapCount)
      {
         return new MipMapCount(mipMapCount);
      }
   }
}
