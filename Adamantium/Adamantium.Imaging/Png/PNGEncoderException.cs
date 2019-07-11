using System;

namespace Adamantium.Imaging.Png
{
    public class PNGEncoderException : Exception
    {
        public PNGEncoderException(uint errorCode) : base(PNGErrors.GetErrorFromCode(errorCode))
        {

        }
    }
}
