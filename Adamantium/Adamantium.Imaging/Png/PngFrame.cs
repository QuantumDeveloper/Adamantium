using System.Collections.Generic;

namespace Adamantium.Imaging.Png
{
    internal class PngFrame
    {
        public PngFrame()
        {
            frameData = new List<byte>();
        }

        public PngFrame(byte[] pixels, uint encodedWidth, uint encodedHeight, int bitDepth)
        {
            RawPixelBuffer = pixels;
            EncodedWidth = encodedWidth;
            EncodedHeight = encodedHeight;
            BitDepth = bitDepth;
            frameData = new List<byte>();
        }

        private List<byte> frameData;

        public int BitDepth { get; set; }
        
        public bool IsDecoded { get; internal set; }

        /// <summary>
        /// Sequence number of the animation chunk, starting from 0
        /// </summary>
        public uint SequenceNumberFCTL { get; set; }

        public uint SequenceNumberFDAT { get; set; }
        /// <summary>
        /// Width of the following frame
        /// </summary>
        public uint EncodedWidth { get; set; }
        /// <summary>
        /// Height of the following frame
        /// </summary>
        public uint EncodedHeight { get; set; }
        
        /// <summary>
        /// Width of the final decoded frame
        /// </summary>
        public uint Width { get; set; }
        /// <summary>
        /// Height of the final decoded frame
        /// </summary>
        public uint Height { get; set; }
        /// <summary>
        /// X position at which to render the following frame
        /// </summary>
        public uint XOffset { get; set; }
        /// <summary>
        /// Y position at which to render the following frame
        /// </summary>
        public uint YOffset { get; set; }
        /// <summary>
        /// Frame delay fraction numerator
        /// </summary>
        public ushort DelayNumerator { get; set; }
        /// <summary>
        /// Frame delay fraction denominator
        /// </summary>
        public ushort DelayDenominator { get; set; }
        /// <summary>
        /// Type of frame area disposal to be done after rendering this frame
        /// </summary>
        public DisposeOp DisposeOp { get; set; }
        /// <summary>
        /// Type of frame area rendering for this frame
        /// </summary>
        public BlendOp BlendOp { get; set; }

        public byte[] RawPixelBuffer { get; set; }

        public byte[] FrameData
        {
            get => frameData.ToArray();
            set
            {
                frameData.Clear();
                frameData.AddRange(value);
            }
        }

        public void AddBytes(params byte[] bytes)
        {
            if (bytes == null)
                return;

            frameData.AddRange(bytes);
        }
    }
}
