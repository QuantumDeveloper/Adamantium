using System;
using Adamantium.Mathematics;
using MessagePack;

namespace Adamantium.Fonts.Common
{
    [MessagePackObject]
    public class CompositeGlyphComponent
    {
        [Key(0)]
        public UInt16 SimpleGlyphIndex { get; set; } // index of simple glyph which is a part of composite glyph
        [Key(1)]
        public Matrix3x2 TransformMatrix { get; set; } = Matrix3x2.Identity;

        internal CompositeGlyphComponent() {}
    }
}
