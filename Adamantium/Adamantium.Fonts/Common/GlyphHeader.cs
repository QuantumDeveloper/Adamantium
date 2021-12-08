using System;

namespace Adamantium.Fonts.Common
{
    internal class GlyphHeader
    {
        public Int16 NumberOfContours { get; set; }
        public Int16 XMin { get; set; }
        public Int16 YMin { get; set; }
        public Int16 XMax { get; set; }
        public Int16 YMax { get; set; }
    };
}
