using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class Glyph
    {
        public GlyphHeader Header { get; set; }
        public int Index { get; set; }

        public bool IsSimple { get; set; } // used for separate simple / complex glyphs processing

        // for simple glyphs
        public Contour[] SegmentedContours { get; set; } // initial segmented contours
        public UInt16 NumberOfPoints { get; set; } // number of points for simple glyph

        // for composite glyphs
        public List<CompositeGlyphComponent> CompositeGlyphComponents { get; set; } // simple glyphs indeces and transformations from which the composite ones consists       

        public Glyph()
        {
            Header = new GlyphHeader();
            CompositeGlyphComponents = new List<CompositeGlyphComponent>();
        }
    };
}
