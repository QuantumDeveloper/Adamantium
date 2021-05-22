using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.Layout
{
    internal abstract class CoverageTable
    {
        public abstract UInt32 Format { get; }

        public abstract int FindPosition(ushort glyphIndex);

        public abstract IEnumerable<ushort> GetExpandedValues();
    }
}