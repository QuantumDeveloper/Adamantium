using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Mathematics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorRGB : IEquatable<ColorRGB>
    {
        public ColorRGB(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public byte R;
        public byte G;
        public byte B;

        public override string ToString()
        {
            return $"R: {R} G: {G} B: {B}";
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = R.GetHashCode();
                hashCode = (hashCode * 397) ^ G.GetHashCode();
                hashCode = (hashCode * 397) ^ B.GetHashCode();
                return hashCode;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            return Equals((ColorRGB)obj);
        }

        public bool Equals(ColorRGB other)
        {
            return R == other.R && G == other.G && B == other.B;
        }
    }
}
