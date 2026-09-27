using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Imaging.Gif
{
    public class GifFrame
    {
        public GifFrame(int frameIndex)
        {
            FrameIndex = frameIndex;
            CompressedData = new List<byte>();
        }
        
        public int FrameIndex { get; }

        public GifImageDescriptor Descriptor { get; set; }

        public ColorRGB[] ColorTable { get; internal set; }

        public GraphicControlExtension GraphicControlExtension { get; set;}

        public int LzwMinimumCodeSize { get; set; }

        public List<byte> CompressedData { get; set; }

        public byte[] RawPixels { get; set; }

        public int[] IndexData { get; set; }

        public bool Interlaced { get; set; }
        
        public bool IsDecoded { get; internal set; }
    }
}
