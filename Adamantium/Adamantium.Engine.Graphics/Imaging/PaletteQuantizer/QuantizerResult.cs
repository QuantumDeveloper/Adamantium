using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics.Imaging.PaletteQuantizer
{
    public class QuantizerResult
    {
        public PixelBuffer Image { get; set; }

        public Color[] ColorTable { get; set; }

        public QuantizerResult()
        {
        }

        public QuantizerResult(PixelBuffer image, Color[] colorTable)
        {
            Image = image;
            ColorTable = colorTable;
        }
    }
}
