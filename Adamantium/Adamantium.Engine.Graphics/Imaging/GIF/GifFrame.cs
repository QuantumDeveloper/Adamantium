using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics.Imaging.GIF
{
    internal class GifFrame
    {
        internal GifImageDescriptor descriptor;

        public ColorRGB[] ColorTable { get; internal set; }

        public GraphicControlExtension GraphicControlExtension { get; set;}
    }
}
