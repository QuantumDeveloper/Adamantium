using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Fonts.Common
{
    internal class GlyphFeatureData
    {
        public GlyphPosition Position { get; set; }

        // SUB

        public GlyphFeatureData(GlyphPosition position)
        {
            Position = position;
        }
    }
}
