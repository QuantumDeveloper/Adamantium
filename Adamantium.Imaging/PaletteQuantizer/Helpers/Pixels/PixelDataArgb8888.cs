using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Imaging.PaletteQuantizer.Helpers.Pixels
{
    /// <summary>
    /// Name |          Blue         |        Green          |           Red         |         Alpha         |
    /// Bit  |00|01|02|03|04|05|06|07|08|09|10|11|12|13|14|15|16|17|18|19|20|21|22|23|24|25|26|27|28|29|30|31|
    /// Byte |00000000000000000000000|11111111111111111111111|22222222222222222222222|33333333333333333333333|
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct PixelDataArgb8888 : IGenericPixel
    {
        // raw component values
        [FieldOffset(0)] private readonly byte red;    // 00 - 07
        [FieldOffset(1)] private readonly byte green;   // 08 - 15
        [FieldOffset(2)] private readonly byte blue;     // 16 - 23
        [FieldOffset(3)] private readonly byte alpha;   // 24 - 31

        // raw high-level values
        [FieldOffset(0)] private int raw;             // 00 - 31

        // processed component values
        public int Red => red;
        public int Green => green;
        public int Blue => blue;
        public int Alpha => alpha;

        /// <summary>
        /// See <see cref="IGenericPixel.Rgba"/> for more details.
        /// </summary>
        public int Rgba => raw;

        /// <summary>
        /// See <see cref="IGenericPixel.GetColor"/> for more details.
        /// </summary>
        public Color GetColor()
        {
            return Color.FromRgba(Rgba);
        }

        /// <summary>
        /// See <see cref="IGenericPixel.SetColor"/> for more details.
        /// </summary>
        public void SetColor(Color color)
        {
            raw = color.ToRgba();
        }

        /// <summary>
        /// See <see cref="IGenericPixel.Value"/> for more details.
        /// </summary>
        public ulong Value
        {
            get => (uint)raw;
            set => raw = (int)(value & 0xFFFFFFFF);
        }
    }
}
