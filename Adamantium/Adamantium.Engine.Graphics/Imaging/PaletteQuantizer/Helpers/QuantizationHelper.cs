﻿using System;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Helpers
{
    public class QuantizationHelper
    {
        private const int Alpha = 255 << 24;
        private static readonly Adamantium.Mathematics.Color BackgroundColor;
        private static readonly Double[] Factors;

        static QuantizationHelper()
        {
            BackgroundColor = new Adamantium.Mathematics.Color(240, 240, 240, 255);
            Factors = PrecalculateFactors();
        }

        /// <summary>
        /// Precalculates the alpha-fix values for all the possible alpha values (0-255).
        /// </summary>
        private static Double[] PrecalculateFactors()
        {
            Double[] result = new Double[256];

            for (Int32 value = 0; value < 256; value++)
            {
                result[value] = value / 255.0;
            }

            return result;
        }

        /// <summary>
        /// Converts the alpha blended color to a non-alpha blended color.
        /// </summary>
        /// <param name="color">The alpha blended color (ARGB).</param>
        /// <returns>The non-alpha blended color (RGB).</returns>
        internal static Color ConvertAlpha(Adamantium.Mathematics.Color color)
        {
            Int32 argb;
            return ConvertAlpha(color, out argb);
        }

        /// <summary>
        /// Converts the alpha blended color to a non-alpha blended color.
        /// </summary>
        internal static Color ConvertAlpha(Adamantium.Mathematics.Color color, out Int32 argb)
        {
            Adamantium.Mathematics.Color result = color;

            if (color.A < 255)
            {
                // performs a alpha blending (second color is BackgroundColor, by default a Control color)
                Double colorFactor = Factors[color.A];
                Double backgroundFactor = Factors[255 - color.A];
                Int32 red = (Int32) (color.R*colorFactor + BackgroundColor.R*backgroundFactor);
                Int32 green = (Int32) (color.G*colorFactor + BackgroundColor.G*backgroundFactor);
                Int32 blue = (Int32) (color.B*colorFactor + BackgroundColor.B*backgroundFactor);
                argb = red << 16 | green << 8 | blue;
                Adamantium.Mathematics.Color.FromRgba((byte)red, (byte)green, (byte)blue);
                result = Adamantium.Mathematics.Color.FromArgb(Alpha | argb);
            }
            else
            {
                argb = color.R << 16 | color.G << 8 | color.B;
            }

            return result;
        }
    }
}
