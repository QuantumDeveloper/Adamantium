using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGEncoderException : Exception
    {
        public PNGEncoderException(uint errorCode)
        {

        }

        public PNGEncoderException(string message) : base(message)
        {

        }
    }
}
