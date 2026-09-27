using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class BlendData
    {
        // Outer list is operand list, inner list - deltas for current operand for each region
        /*
            Regular: 100 200 rmoveto
            Light: 100 150 rmoveto
            Bold: 100 300 rmoveto
            Condensed: 50 100 rmoveto
            
            (100 200) (0 0 -50) (-50 100 -100) 2 blend rmoveto
            
            https://docs.microsoft.com/en-us/typography/opentype/spec/cff2charstr#section4.5
         */
        public List<RegionData> Deltas;
    }
}