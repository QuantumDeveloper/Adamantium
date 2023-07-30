using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class fcTL : Chunk
    {
        public fcTL()
        {
            Name = "fcTL";
        }

        /// <summary>
        /// Sequence number of the animation chunk, starting from 0
        /// </summary>
        public uint SequenceNumber { get; set; }
        /// <summary>
        /// Width of the following frame
        /// </summary>
        public uint Width  {get; set;}
        /// <summary>
        /// Height of the following frame
        /// </summary>
        public uint Height { get; set; }
        /// <summary>
        /// X position at which to render the following frame
        /// </summary>
        public uint XOffset{get; set;}
        /// <summary>
        /// Y position at which to render the following frame
        /// </summary>
        public uint YOffset { get; set; }
        /// <summary>
        /// Frame delay fraction numerator
        /// </summary>
        public ushort DelayNum{get; set;}
        /// <summary>
        /// Frame delay fraction denominator
        /// </summary>
        public ushort DelayDen { get; set; }
        /// <summary>
        /// Type of frame area disposal to be done after rendering this frame
        /// </summary>
        public DisposeOp DisposeOp { get; set; }
        /// <summary>
        /// Type of frame area rendering for this frame
        /// </summary>
        public BlendOp BlendOp { get; set; }

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(SequenceNumber));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Width));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Height));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(XOffset));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(YOffset));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(DelayNum));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(DelayDen));
            bytes.Add((byte)DisposeOp);
            bytes.Add((byte)BlendOp);

            return bytes.ToArray();
        }

        internal static fcTL FromFrame(PngFrame frame)
        {
            var fctl = new fcTL();
            fctl.SequenceNumber = frame.SequenceNumberFCTL;
            fctl.Width = frame.EncodedWidth;
            fctl.Height = frame.EncodedHeight;
            fctl.XOffset = frame.XOffset;
            fctl.YOffset = frame.YOffset;
            fctl.DelayNum = frame.DelayNumerator;
            fctl.DelayDen = frame.DelayDenominator;
            fctl.DisposeOp = DisposeOp.Background;
            fctl.BlendOp = BlendOp.Source;

            return fctl;
        }
    }
}
