using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public class VariationSelector
    {
        public List<uint> DefaultStartCodes = new List<uint>();
        public List<uint> DefaultEndCodes = new List<uint>();
        public Dictionary<uint, uint> UVSMappings = new Dictionary<uint, uint>();
    }
}