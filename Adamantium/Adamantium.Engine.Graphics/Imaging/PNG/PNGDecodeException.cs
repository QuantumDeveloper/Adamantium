using System;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGDecodeException : Exception
    {
        public PNGDecodeException(uint errorCode): base(PNGErrors.GetErrorFromCode(errorCode))
        {
        }
    }
}
