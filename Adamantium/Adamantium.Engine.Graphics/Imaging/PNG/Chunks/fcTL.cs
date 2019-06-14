﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
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

        internal override byte[] GetChunkBytes(PNGState state)
        {
            throw new NotImplementedException();
        }
    }
}
