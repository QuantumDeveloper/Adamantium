using System;

namespace Adamantium.Imaging.Png
{
    public class PngDecodeException : Exception
    {
        public PngDecodeException(uint errorCode): base(PngErrors.GetErrorFromCode(errorCode))
        {
        }
    }
}
