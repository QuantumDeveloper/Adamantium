using Adamantium.Mathematics;
using System;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Helpers.Pixels
{
    /// <summary>
    /// Name |                     Blue                      |                     Green                     |                      Red                      | 
    /// Bit  |00|01|02|03|04|05|06|07|08|09|10|11|12|13|14|15|16|17|18|19|20|21|22|23|24|25|26|27|28|29|30|31|32|33|34|35|36|37|38|39|40|41|42|43|44|45|46|47|
    /// Byte |00000000000000000000000|11111111111111111111111|22222222222222222222222|33333333333333333333333|44444444444444444444444|55555555555555555555555|
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 6)]
    public struct PixelDataRgb48 : IGenericPixel
    {
        // raw component values
        [FieldOffset(0)] private ushort red;   // 00 - 15
        [FieldOffset(2)] private ushort green;  // 16 - 31
        [FieldOffset(4)] private ushort blue;    // 32 - 47

        // raw high-level values
        [FieldOffset(0)] private ulong raw;    // 00 - 47

        // processed component values
        public int Alpha => 0xFF;
        public int Red => red >> 8;
        public int Green => green >> 8;
        public int Blue => blue >> 8;

        /// <summary>
        /// See <see cref="IGenericPixel.Rgba"/> for more details.
        /// </summary>
        public int Rgba => Alpha << 48 | Red << 32 | Green << 16 | Blue;

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
            red = (ushort)(color.R << 8);
            green = (ushort)(color.G << 8);
            blue = (ushort)(color.B << 8);
        }

        /// <summary>
        /// See <see cref="IGenericPixel.Value"/> for more details.
        /// </summary>
        public ulong Value
        {
            get => raw;
            set => raw = value & 0xFFFFFFFFFFFF;
        }
    }
}
