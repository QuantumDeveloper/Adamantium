using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.WOFF
{
    internal class WoffTableDirectory
    {
        public Dictionary<string, WoffTable> Tables { get; }

        public WoffTableDirectory()
        {
            Tables = new Dictionary<string, WoffTable>();
        }
    }
}