/// Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.
/// Under the MIT License, details: License.txt.

using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.JPEG
{
    public class JpegHeader
    {
        public byte Marker;
        public byte[] Data;
        internal bool IsJFIF = false;
        public new string ToString { get { return Encoding.UTF8.GetString(Data, 0, Data.Length); } }
    }

}