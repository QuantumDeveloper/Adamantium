using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Imaging.PaletteQuantizer.Helpers.Pixels
{
    /// <summary>
    /// Name |     Blue     |    Green     |     Red      | Unused
    /// Bit  |00|01|02|03|04|05|06|07|08|09|10|11|12|13|14|15|
    /// Byte |00000000000000000000000|11111111111111111111111|
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public struct PixelDataRgb555 : IGenericPixel
    {
        // raw component values
        [FieldOffset(0)] private byte red;     // 00 - 04
        [FieldOffset(0)] private ushort green;  // 05 - 09
        [FieldOffset(1)] private byte blue;      // 10 - 14

        // raw high-level values
        [FieldOffset(0)] private ushort raw;    // 00 - 15

        // processed component values
        public int Alpha => 0xFF;
        public int Red => red >> 2 & 0xF;
        public int Green => green >> 5 & 0xF;
        public int Blue => blue & 0xF;

        /// <summary>
        /// See <see cref="IGenericPixel.Rgba"/> for more details.
        /// </summary>
        public int Rgba => Pixel.AlphaMask | raw;

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
            red = (byte)(color.R >> 3);
            green = (byte)(color.G >> 3);
            blue = (byte)(color.B >> 3);
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
