using System;

namespace Adamantium.Imaging.Png
{
    public class PNGDecodeException : Exception
    {
        public PNGDecodeException(uint errorCode): base(PNGErrors.GetErrorFromCode(errorCode))
        {
        }
    }
}
