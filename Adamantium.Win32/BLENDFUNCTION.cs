using System.Runtime.InteropServices;

namespace Adamantium.Win32;

/// <summary>GDI alpha-blend spec for <c>UpdateLayeredWindow</c>. For per-pixel alpha: SourceConstantAlpha=255,
/// AlphaFormat=AC_SRC_ALPHA (1).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BLENDFUNCTION
{
    public byte BlendOp;
    public byte BlendFlags;
    public byte SourceConstantAlpha;
    public byte AlphaFormat;
}
