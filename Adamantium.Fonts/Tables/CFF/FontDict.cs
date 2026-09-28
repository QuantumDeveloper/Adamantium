using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.CFF
{
    internal class FontDict
    {
        public int FontName { get; set; }
        public int PrivateDictSize { get; set; }
        public int PrivateDictOffset { get; set; }
            
        public List<byte[]> LocalSubr { get; set; }
            
        public FontDict(int dictSize, int dictOffset)
        {
            PrivateDictSize = dictSize;
            PrivateDictOffset = dictOffset;
        }

        public override string ToString()
        {
            return $"FontName: {FontName}, Dict Size = {PrivateDictSize}, Offset = {PrivateDictOffset} LocalSubrs: {LocalSubr?.Count ?? 0}";
        }
    }
}