using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Mathematics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorRGB
    {
        public byte R;
        public byte G;
        public byte B;
    }
}
