using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Margins
    {
        public int Left;

        public int Right;

        public int Top;

        public int Bottom;
    }
}
