using System;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    public class CompositeGlyphComponent
    {
        public UInt16 SimpleGlyphIndex { get; set; } // index of simple glyph which is a part of composite glyph
        public Matrix3x2 TransformMatrix { get; set; } = Matrix3x2.Identity;

        internal CompositeGlyphComponent() {}
    }
}
