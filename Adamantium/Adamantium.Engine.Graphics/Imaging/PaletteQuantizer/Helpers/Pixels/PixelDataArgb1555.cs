using Adamantium.Mathematics;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Helpers.Pixels
{
    /// <summary>
    /// Name |     Blue     |    Green     |     Red      | Alpha (bit)
    /// Bit  |00|01|02|03|04|05|06|07|08|09|10|11|12|13|14|15|
    /// Byte |00000000000000000000000|11111111111111111111111|
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public struct PixelDataArgb1555 : IGenericPixel
    {
        // raw component values
        [FieldOffset(0)] private byte red;     // 00 - 04
        [FieldOffset(0)] private ushort green;  // 05 - 09
        [FieldOffset(1)] private byte blue;      // 10 - 14
        [FieldOffset(1)] private byte alpha;    // 15

        // raw high-level values
        [FieldOffset(0)] private ushort raw;    // 00 - 15

        // processed raw values
        public int Alpha => alpha >> 7 & 0x1;
        public int Red => red >> 2 & 0xF;
        public int Green => green >> 5 & 0xF;
        public int Blue => blue & 0xF;

        /// <summary>
        /// See <see cref="IGenericPixel.Rgba"/> for more details.
        /// </summary>
        public int Rgba => (Alpha == 0 ? 0 : Pixel.AlphaMask) | Red << Pixel.RedShift | Green << Pixel.GreenShift | Blue;

        /// <summary>
        /// See <see cref="IGenericPixel.GetColor"/> for more details.
        /// </summary>
        public Color GetColor()
        {
            return Color.FromArgb(Rgba);
        }

        /// <summary>
        /// See <see cref="IGenericPixel.SetColor"/> for more details.
        /// </summary>
        public void SetColor(Color color)
        {
            int argb = color.ToArgb();
            alpha = argb >> Pixel.AlphaShift > Pixel.ByteMask ? Pixel.Zero : Pixel.One;
            red = (byte)(argb >> Pixel.RedShift);
            green = (byte)(argb >> Pixel.GreenShift);
            blue = (byte)(argb >> Pixel.BlueShift);
        }

        /// <summary>
        /// See <see cref="IGenericPixel.Value"/> for more details.
        /// </summary>
        public ulong Value
        {
            get => raw;
            set => raw = (ushort)(value & 0xFFFF);
        }
    }
}
