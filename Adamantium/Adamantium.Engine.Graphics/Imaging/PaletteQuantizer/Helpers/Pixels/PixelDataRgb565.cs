using Adamantium.Mathematics;
using System;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Helpers.Pixels
{
    /// <summary>
    /// Name |     Blue     |      Green      |     Red      | 
    /// Bit  |00|01|02|03|04|05|06|07|08|09|10|11|12|13|14|15|
    /// Byte |00000000000000000000000|11111111111111111111111|
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public struct PixelDataRgb565 : IGenericPixel
    {
        // raw component values
        [FieldOffset(0)] private byte red;     // 00 - 04
        [FieldOffset(0)] private ushort green;  // 05 - 10
        [FieldOffset(2)] private byte blue;      // 11 - 15

        // raw high-level values
        [FieldOffset(0)] private ushort raw;    // 00 - 15

        // processed component values
        public int Alpha => 0xFF;
        public int Red => red >> 3 & 0xF;
        public int Green => green >> 5 & 0x1F;
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
            green = (byte)(color.G >> 2);
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
