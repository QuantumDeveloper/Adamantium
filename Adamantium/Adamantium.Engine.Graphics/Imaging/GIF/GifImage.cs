using Adamantium.Mathematics;
using System.Collections.Generic;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    internal class GifImage
    {
        public List<ColorRGB> GlobalColorTable { get; internal set; }

        public List<GifFrame> Frames { get; }

        public GifFrame CurrentFrame { get; set; }

        public byte ColorDepth { get; internal set; }

        public GifImage()
        {
            Frames = new List<GifFrame>();
            GlobalColorTable = new List<ColorRGB>();
        }
    }
}
