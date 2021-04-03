using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.OTF
{
    internal class CFF2Parser : ICFFParser
    {
        private OTFStreamReader otfReader;
        private uint cffOffset;
        
        private CFFHeader cffHeader;
        private CFFFontSet fontSet;
        
        public CFF2Parser(uint cffOffset, OTFStreamReader reader)
        {
            this.cffOffset = cffOffset;
            otfReader = reader;
            fontSet = new CFFFontSet();
        }

        public IReadOnlyCollection<Glyph> Glyphs { get; }

        public CFFIndex GlobalSubroutineIndex { get; }
        public int GlobalSubrBias { get; }
        
        CFFFont ICFFParser.Parse()
        {
            ReadHeader();

            return fontSet.Fonts.FirstOrDefault();
        }

        private void ReadHeader()
        {
            cffHeader = new CFFHeader();
            otfReader.Position = cffOffset;

            cffHeader.Major = otfReader.ReadByte();
            cffHeader.Minor = otfReader.ReadByte();
            cffHeader.HeaderSize = otfReader.ReadByte();
            cffHeader.TopDictLength = otfReader.ReadUInt16();
        }
    }
}