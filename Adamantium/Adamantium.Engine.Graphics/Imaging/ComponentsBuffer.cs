using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging
{
    public class ComponentsBuffer
    {
        public byte[][,] Buffer { get; }

        public int ComponentsCount => Buffer.Length;

        public int Width { get; private set; }

        public int Height { get; private set; }

        public ComponentsBuffer(PixelBuffer pixelBuffer): this(pixelBuffer.ToComponentsBuffer(), pixelBuffer.Width, pixelBuffer.Height)
        {
        }

        public ComponentsBuffer(byte[][,] buffer, int width, int height)
        {
            Width = width;
            Height = height;
            Buffer = buffer;
        }
    }
}
