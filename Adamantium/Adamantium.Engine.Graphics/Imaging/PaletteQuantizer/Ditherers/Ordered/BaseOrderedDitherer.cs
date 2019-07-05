﻿using System;
using Adamantium.Mathematics;
using Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Helpers;

namespace Adamantium.Engine.Graphics.Imaging.PaletteQuantizer.Ditherers.Ordered
{
    public abstract class BaseOrderedDitherer : BaseColorDitherer
    {
        #region | Properties |

        /// <summary>
        /// Gets the width of the matrix.
        /// </summary>
        protected abstract byte MatrixWidth { get; }

        /// <summary>
        /// Gets the height of the matrix.
        /// </summary>
        protected abstract byte MatrixHeight { get; }

        #endregion

        #region << BaseColorDitherer >>

        /// <summary>
        /// See <see cref="BaseColorDitherer.OnProcessPixel"/> for more details.
        /// </summary>
        protected override bool OnProcessPixel(Pixel sourcePixel, Pixel targetPixel)
        {
            // reads the source pixel
            Color oldColor = SourceBuffer.GetColorFromPixel(sourcePixel);

            // converts alpha to solid color
            oldColor = QuantizationHelper.ConvertAlpha(oldColor);

            // retrieves matrix coordinates
            int x = targetPixel.X % MatrixWidth;
            int y = targetPixel.Y % MatrixHeight;

            // determines the threshold
            int threshold = Convert.ToInt32(CachedMatrix[x, y]);

            // only process dithering if threshold is substantial
            if (threshold > 0)
            {
                int red = GetClampedColorElement(oldColor.R + threshold);
                int green = GetClampedColorElement(oldColor.G + threshold);
                int blue = GetClampedColorElement(oldColor.B + threshold);

                Color newColor = Color.FromRgba((byte)red, (byte)green, (byte)blue, 255);

                if (TargetBuffer.UsePalette)
                {
                    byte newPixelIndex = (byte)Quantizer.GetPaletteIndex(newColor, targetPixel.X, targetPixel.Y);
                    targetPixel.Index = newPixelIndex;
                }
                else
                {
                    targetPixel.Color = newColor;
                }
            }

            // writes the process pixel
            return true;
        }

        #endregion

        #region << IColorDitherer >>

        /// <summary>
        /// See <see cref="IColorDitherer.IsInplace"/> for more details.
        /// </summary>
        public override bool IsInplace
        {
            get { return true; }
        }

        #endregion
    }
}
