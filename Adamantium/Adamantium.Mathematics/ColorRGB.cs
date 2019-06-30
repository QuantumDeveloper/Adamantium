using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Mathematics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorRGB
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
    }
}
