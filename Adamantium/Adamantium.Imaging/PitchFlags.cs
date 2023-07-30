using System;

namespace Adamantium.Imaging;

[Flags]
internal enum PitchFlags
{
    None = 0x0,         // Normal operation
    LegacyDword = 0x1,  // Assume pitch is DWORD aligned instead of BYTE aligned
    Bpp24 = 0x10000,    // Override with a legacy 24 bits-per-pixel format size
    Bpp16 = 0x20000,    // Override with a legacy 16 bits-per-pixel format size
    Bpp8 = 0x40000,     // Override with a legacy 8 bits-per-pixel format size
};