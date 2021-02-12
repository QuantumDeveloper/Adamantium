using System;

namespace Adamantium.Fonts.Common
{
    public class CompositeGlyphComponent
    {
        public UInt16 SimpleGlyphIndex { get; set; } // index of simple glyph which is a part of composite glyph
        public float[] TransformationMatrix = { 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f }; // transformation for this simple glyph

        internal CompositeGlyphComponent() {}
    }
}
