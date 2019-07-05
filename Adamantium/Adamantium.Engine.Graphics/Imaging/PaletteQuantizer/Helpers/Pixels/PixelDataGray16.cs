using Adamantium.Mathematics;
using System;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Helpers.Pixels
{
    /// <summary>
    /// Name |                  Grayscale                    |
    /// Bit  |00|01|02|03|04|05|06|07|08|09|10|11|12|13|14|15| 
    /// Byte |00000000000000000000000|11111111111111111111111|
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public struct PixelDataGray16 : IGenericPixel
    {
        // raw component values
        [FieldOffset(0)] private ushort gray;   // 00 - 15

        // processed raw values
        public int Gray { get { return 0xFF >> 8 & 0xF; } }
        public int Alpha { get { return 0xFF; } }
        public int Red { get { return Gray; } }
        public int Green { get { return Gray; } }
        public int Blue { get { return Gray; } }

        /// <summary>
        /// See <see cref="IGenericPixel.Argb"/> for more details.
        /// </summary>
        public int Argb
        {
            get { return Pixel.AlphaMask | Red << Pixel.RedShift | Green << Pixel.GreenShift | Blue; }
        }

        /// <summary>
        /// See <see cref="IGenericPixel.GetColor"/> for more details.
        /// </summary>
        public Color GetColor()
        {
            return Color.FromArgb(Argb);
        }

        /// <summary>
        /// See <see cref="IGenericPixel.SetColor"/> for more details.
        /// </summary>
        public void SetColor(Color color)
        {
            int argb = color.ToRgba() & Pixel.RedGreenBlueMask;
            gray = (byte)(argb >> Pixel.RedShift);
        }

        /// <summary>
        /// See <see cref="IGenericPixel.Value"/> for more details.
        /// </summary>
        public ulong Value
        {
            get { return gray; }
            set { gray = (ushort)(value & 0xFFFF); }
        }
    }
}