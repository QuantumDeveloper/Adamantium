using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGDecodeException : Exception
    {
        public PNGDecodeException(string message): base(message)
        {

        }
    }
}
