using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class CFF2Parser : ICFFParser
    {
        private FontStreamReader otfTtfReader;
        private long cffOffset;
        
        private CFFHeader cffHeader;
        private CFFFontSet fontSet;

        public CFF2Parser(long cffOffset, FontStreamReader ttfReader)
        {
            this.cffOffset = cffOffset;
            otfTtfReader = ttfReader;
            fontSet = new CFFFontSet();
        }

        public IReadOnlyCollection<Glyph> Glyphs { get; }

        public CFFIndex GlobalSubroutineIndex { get; private set; }
        public int GlobalSubrBias { get; private set; }

        private UInt32 charstringOffset;
        private UInt32 fdArrayOffset;
        private UInt32 fdSelectOffset;
        private UInt32 variationStoreOffset;
        private CFFFont cffFont;

        public CFFFont Parse()
        {
            cffFont = new CFFFont(fontSet, CFFVersion.CFF2) {IsLocalSubroutineAvailable = false};
            ReadHeader();
            ReadTopDict();
            ReadGlobalSubrIndex();
            ReadVariationStore();
            ReadFDArray();
            ReadFDSelect();
            ReadCharstringIndex();

            return cffFont;
        }

        protected virtual void ReadHeader()
        {
            cffHeader = new CFFHeader();
            otfTtfReader.Position = cffOffset;

            cffHeader.Major = otfTtfReader.ReadByte();
            cffHeader.Minor = otfTtfReader.ReadByte();
            cffHeader.HeaderSize = otfTtfReader.ReadByte();
            cffHeader.TopDictLength = otfTtfReader.ReadUInt16();
        }

        protected virtual void ReadTopDict()
        {
            var data = otfTtfReader.ReadBytes(cffHeader.TopDictLength, true);
            var operandParser = new DictOperandParser(data, cffFont);
            var result = operandParser.GetAllAvailableOperands();

            foreach (var operandResult in result.Results)
            {
                switch (operandResult.Key)
                {
                    case DictOperatorsType.CharStrings:
                        charstringOffset = operandResult.Value.AsUInt();
                        break;
                    case DictOperatorsType.vstore:
                        variationStoreOffset = operandResult.Value.AsUInt();
                        break;
                    case DictOperatorsType.FDArray:
                        fdArrayOffset = operandResult.Value.AsUInt();
                        break;
                    case DictOperatorsType.FDSelect:
                        fdSelectOffset = operandResult.Value.AsUInt();
                        break;
                }
            }
        }

        private void ReadGlobalSubrIndex()
        {
            otfTtfReader.Position = cffOffset + cffHeader.HeaderSize + cffHeader.TopDictLength;
            GlobalSubroutineIndex = otfTtfReader.ReadCffIndex(CFFVersion.CFF2);
            
            if (GlobalSubroutineIndex.Count == 0) return;

            GlobalSubrBias = this.CalculateSubrBias(GlobalSubroutineIndex.Count);
        }

        private void ReadVariationStore()
        {
            if (variationStoreOffset == 0) return;

            var variationOffset = cffOffset + variationStoreOffset;
            otfTtfReader.Position = variationOffset;
            var length = otfTtfReader.ReadUInt16();
            variationOffset += 2; // add length
            var format = otfTtfReader.ReadUInt16();
            var variationRegionListOffset = otfTtfReader.ReadUInt32();
            var itemVariationDataCount = otfTtfReader.ReadUInt16();
            var itemVariationDataOffsets = new uint[itemVariationDataCount];
            for (int i = 0; i < itemVariationDataCount; ++i)
            {
                itemVariationDataOffsets[i] = otfTtfReader.ReadUInt32();
            }
            
            otfTtfReader.Position = variationOffset + variationRegionListOffset;
            var variationRegionList = new VariationRegionList();
            variationRegionList.AxisCount = otfTtfReader.ReadUInt16();
            variationRegionList.RegionCount = otfTtfReader.ReadUInt16();
            variationRegionList.VariationRegions = new VariationRegion[variationRegionList.RegionCount];
            for (var index = 0; index < variationRegionList.VariationRegions.Length; index++)
            {
                var region = new VariationRegion();
                region.RegionAxes = new RegionAxisCoordinates[variationRegionList.AxisCount];
                for (int i = 0; i < region.RegionAxes.Length; i++)
                {
                    var axes = new RegionAxisCoordinates();
                    axes.StartCoord = otfTtfReader.ReadInt16().FromF2Dot14();
                    axes.PeakCoord = otfTtfReader.ReadInt16().FromF2Dot14();
                    axes.EndCoord = otfTtfReader.ReadInt16().FromF2Dot14();
                    region.RegionAxes[i] = axes;
                }
                variationRegionList.VariationRegions[index] = region;
            }

            var variationDataList = new List<ItemVariationDataSubtable>();
            for (int i = 0; i < itemVariationDataCount; ++i)
            {
                otfTtfReader.Position = variationOffset + itemVariationDataOffsets[i];
                var variationDataSubtable = new ItemVariationDataSubtable();
                variationDataSubtable.ItemCount = otfTtfReader.ReadUInt16();
                variationDataSubtable.ShortDeltaCount = otfTtfReader.ReadUInt16();
                variationDataSubtable.RegionIndexCount = otfTtfReader.ReadUInt16();
                variationDataSubtable.RegionIndices = otfTtfReader.ReadUInt16Array(variationDataSubtable.RegionIndexCount);
                variationDataSubtable.DeltaSets = new DeltaSet[variationDataSubtable.ItemCount];
                if (variationDataSubtable.ItemCount > 0)
                {
                    for (int k = 0; k < variationDataSubtable.ItemCount; ++k)
                    {
                        var deltaSet = new DeltaSet();
                        deltaSet.ShortDeltaData = otfTtfReader.ReadInt16Array(variationDataSubtable.ShortDeltaCount);
                        var deltaDataCount = variationDataSubtable.RegionIndexCount -
                                             variationDataSubtable.ShortDeltaCount;
                        deltaSet.DeltaData = otfTtfReader.ReadSignedBytes(deltaDataCount);

                        variationDataSubtable.DeltaSets[k] = deltaSet;
                    }
                }
                
                variationDataList.Add(variationDataSubtable);
            }

            cffFont.VariationStore = new VariationStore(variationRegionList, variationDataList.ToArray());
        }

        private void ReadFDArray()
        {
            if (fdArrayOffset == 0) return;
            
            otfTtfReader.Position = cffOffset + fdArrayOffset;
            cffFont.CIDFontDicts = otfTtfReader.ReadFDArray(cffOffset, fdArrayOffset, cffFont);
        }

        private void ReadFDSelect()
        {
            if (cffFont.CIDFontDicts.Count <= 1 && fdSelectOffset == 0) return;

            var charStringCount = ReadCharStringIndexCount();
            otfTtfReader.Position = cffOffset + fdSelectOffset;
            otfTtfReader.ReadFDSelect(cffFont, (int)charStringCount);
        }

        private UInt32 ReadCharStringIndexCount()
        {
            otfTtfReader.Position = cffOffset + charstringOffset;
            
            var count = otfTtfReader.ReadUInt32();
            return count;
        }

        private void ReadCharstringIndex()
        {
            otfTtfReader.Position = cffOffset + charstringOffset;

            var charstringIndex = otfTtfReader.ReadCffIndex(CFFVersion.CFF2);
            cffFont.CharStringsIndex = charstringIndex;
            
            var count = cffFont.CharStringsIndex.DataByOffset.Count;
            var glyphs = new Glyph[count];
            var fontDicts = new FontDict[count];
            var source = new CFFGlyphOutlineSource(this, cffFont, fontDicts);
            var fdArraySelector = new FontDictArraySelector(cffFont.CIDFontInfo);

            for (var i = 0; i < count; ++i)
            {
                var glyph = Glyph.Create((uint)i, OutlineType.CompactFontFormat);
                glyphs[i] = glyph;
                try
                {
                    if (cffFont.IsCIDFont)
                    {
                        fontDicts[i] = cffFont.CIDFontDicts[fdArraySelector.SelectFontDictArray((uint)i)];
                    }
                    else if (cffFont.CIDFontDicts.Count == 1)
                    {
                        fontDicts[i] = cffFont.CIDFontDicts[0];
                    }

                    glyph.SetOutlineSource(source);
                }
                catch (Exception)
                {
                    glyph.IsInvalid = true;
                }
            }

            cffFont.SetGlyphs(glyphs);
        }
    }
}