using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGDecoder
    {
        private UnmanagedMemoryStream stream;
        public PNGDecoder(UnmanagedMemoryStream stream)
        {
            this.stream = stream;
        }

        public void Decode()
        {
            if (!ReadHeader())
            {
                throw new ArgumentException("this file does not have a proper PNG header");
            }
        }

        private bool ReadHeader()
        {
            var bytes = stream.ReadBytes(8);
            for(int i = 0; i<bytes.Length; ++i)
            {
                if (bytes[i] != PNGHelper.PngHeader[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
