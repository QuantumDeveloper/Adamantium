using System;
using System.Collections.Generic;
using System.Linq;
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
        private PipelineAssembler pipelineAssembler;
        
        public CFF2Parser(long cffOffset, FontStreamReader ttfReader)
        {
            this.cffOffset = cffOffset;
            otfTtfReader = ttfReader;
            fontSet = new CFFFontSet();
            pipelineAssembler = new PipelineAssembler(this, CFFVersion.CFF2);
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
            cffFont = new CFFFont(fontSet) {IsLocalSubroutineAvailable = false};
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
            var operandParser = new DictOperandParser(data);
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
                    axes.StartCoord = otfTtfReader.ReadInt16().ToF2Dot14();
                    axes.PeakCoord = otfTtfReader.ReadInt16().ToF2Dot14();
                    axes.EndCoord = otfTtfReader.ReadInt16().ToF2Dot14();
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
            cffFont.CIDFontDicts = otfTtfReader.ReadFDArray(cffOffset, fdArrayOffset, CFFVersion.CFF2);
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
            
            var mainStack = new Stack<byte>();
            int exceptions = 0;
            var glyphs = new List<Glyph>();

            var fdArraySelector = new FontDictArraySelector(cffFont.CIDFontInfo);

            // STEP 0. After filling the Index struct traverse the raw data array (ALL characters are here currently)

            for (var i = 0; i < cffFont.CharStringsIndex.DataByOffset.Count; ++i)
            {
                // STEP 1. Take offsets one by one and fill another byte array - this time it is only bytes relative to the current character

                var data = cffFont.CharStringsIndex.DataByOffset[i];
                for (int j = data.Length; j >= 1; --j)
                {
                    mainStack.Push(data[j - 1]);
                }

                // STEP 3. Use fluent approach
                // Byte Array --> Command List --> Outlines --> Bezier descretion
                // Glyph g = CommandList(mainStack).OutlineList().BezierSampling(int sampleRate);
                // g.charcode = 0;
                // g.encoding = encode;
                // VertexBuf vb = g.Triangulate();

                //List<Glyph> ...
                try
                {
                    FontDict fontDict = null;
                    if (cffFont.IsCIDFont)
                    {
                        var fdArrayIndex = fdArraySelector.SelectFontDictArray((uint) i);
                        fontDict = cffFont.CIDFontDicts[fdArrayIndex];
                    }
                    
                    Glyph glyph = pipelineAssembler
                        .CreateGlyph((uint) i)
                        .FillCommandList(cffFont, mainStack, fontDict, index: i)
                        .FillOutlines()
                        .GetGlyph();
                    glyphs.Add(glyph);
                    
                }
                catch (Exception e)
                {
                    glyphs.Add(new Glyph((uint)i){ OutlineType = OutlineType.CompactFontFormat, IsInvalid = true });
                    exceptions++;
                }
            }

            cffFont.SetGlyphs(glyphs.ToArray());
        }
    }
}