using System;

namespace Adamantium.Imaging.Png
{
    public class PngEncoderException : Exception
    {
        public PngEncoderException(uint errorCode) : base(PngErrors.GetErrorFromCode(errorCode))
        {

        }
    }
}
