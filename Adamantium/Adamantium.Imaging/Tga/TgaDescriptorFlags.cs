using System;

namespace Adamantium.Imaging.Tga
{
    [Flags]
    internal enum TgaDescriptorFlags : byte
    {
        InvertX = 0x10,
        InvertY = 0x20,
        Interleaved2Way = 0x40, //Deprecated
        Interleaved4Way = 0x80, // Deprecated
    }
}