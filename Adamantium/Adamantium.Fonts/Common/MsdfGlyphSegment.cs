﻿using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    public struct MsdfGlyphSegment
    {
        public Color MsdfColor;
        public LineSegment2D Segment;
        
        public MsdfGlyphSegment(Vector2D start, Vector2D end)
        {
            Segment = new LineSegment2D(start, end);
            MsdfColor = Colors.White;
        }
        
        public MsdfGlyphSegment(Vector2D start, Vector2D end, Color msdfColor)
        {
            Segment = new LineSegment2D(start, end);
            MsdfColor = msdfColor;
        }
    }
}