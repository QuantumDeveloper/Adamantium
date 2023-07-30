/// Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.
/// Under the MIT License, details: License.txt.

using System;

namespace Adamantium.Imaging.Jpeg.IO
{
    internal class JpegMarkerFoundException : Exception
    {
        public JpegMarkerFoundException(byte marker) { Marker = marker; }
        public byte Marker;
    }
}
