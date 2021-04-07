using System;
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
        private PipelineAssembler pipelineAssembler;
        private VariationStore variationStore;
        
        public CFF2Parser(uint cffOffset, OTFStreamReader reader)
        {
            this.cffOffset = cffOffset;
            otfReader = reader;
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
            otfReader.Position = cffOffset;

            cffHeader.Major = otfReader.ReadByte();
            cffHeader.Minor = otfReader.ReadByte();
            cffHeader.HeaderSize = otfReader.ReadByte();
            cffHeader.TopDictLength = otfReader.ReadUInt16();
        }

        protected virtual void ReadTopDict()
        {
            var data = otfReader.ReadBytes(cffHeader.TopDictLength).Reverse().ToArray();
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
            otfReader.Position = cffOffset + cffHeader.HeaderSize + cffHeader.TopDictLength;
            GlobalSubroutineIndex = otfReader.ReadCffIndex(CFFVersion.CFF2);
            
            if (GlobalSubroutineIndex.Count == 0) return;

            GlobalSubrBias = this.CalculateSubrBias(GlobalSubroutineIndex.Count);
        }

        private void ReadVariationStore()
        {
            if (variationStoreOffset == 0) return;

            var variationOffset = cffOffset + variationStoreOffset;
            otfReader.Position = variationOffset;
            var length = otfReader.ReadUInt16();
            variationOffset += 2; // add length
            var format = otfReader.ReadUInt16();
            var variationRegionListOffset = otfReader.ReadUInt32();
            var itemVariationDataCount = otfReader.ReadUInt16();
            var itemVariationDataOffsets = new uint[itemVariationDataCount];
            for (int i = 0; i < itemVariationDataCount; ++i)
            {
                itemVariationDataOffsets[i] = otfReader.ReadUInt32();
            }
            
            otfReader.Position = variationOffset + variationRegionListOffset;
            var variationRegionList = new VariationRegionList();
            variationRegionList.AxisCount = otfReader.ReadUInt16();
            variationRegionList.RegionCount = otfReader.ReadUInt16();
            variationRegionList.VariationRegions = new VariationRegion[variationRegionList.RegionCount];
            for (var index = 0; index < variationRegionList.VariationRegions.Length; index++)
            {
                var region = new VariationRegion();
                region.RegionAxes = new RegionAxisCoordinates[variationRegionList.AxisCount];
                for (int i = 0; i < region.RegionAxes.Length; i++)
                {
                    var axes = new RegionAxisCoordinates();
                    axes.StartCoord = otfReader.ReadInt16().ToF2Dot14();
                    axes.PeakCoord = otfReader.ReadInt16().ToF2Dot14();
                    axes.EndCoord = otfReader.ReadInt16().ToF2Dot14();
                    region.RegionAxes[i] = axes;
                }
                variationRegionList.VariationRegions[index] = region;
            }

            var variationDataList = new List<ItemVariationDataSubtable>();
            for (int i = 0; i < itemVariationDataCount; ++i)
            {
                otfReader.Position = variationOffset + itemVariationDataOffsets[i];
                var variationDataSubtable = new ItemVariationDataSubtable();
                variationDataSubtable.ItemCount = otfReader.ReadUInt16();
                variationDataSubtable.ShortDeltaCount = otfReader.ReadUInt16();
                variationDataSubtable.RegionIndexCount = otfReader.ReadUInt16();
                variationDataSubtable.RegionIndices = otfReader.ReadUInt16Array(variationDataSubtable.RegionIndexCount);
                variationDataSubtable.DeltaSets = new DeltaSet[variationDataSubtable.ItemCount];
                if (variationDataSubtable.ItemCount > 0)
                {
                    for (int k = 0; k < variationDataSubtable.ItemCount; ++k)
                    {
                        var deltaSet = new DeltaSet();
                        deltaSet.ShortDeltaData = otfReader.ReadInt16Array(variationDataSubtable.ShortDeltaCount);
                        var deltaDataCount = variationDataSubtable.RegionIndexCount -
                                             variationDataSubtable.ShortDeltaCount;
                        deltaSet.DeltaData = otfReader.ReadSignedBytes(deltaDataCount);

                        variationDataSubtable.DeltaSets[k] = deltaSet;
                    }
                }
                
                variationDataList.Add(variationDataSubtable);
            }

            variationStore = new VariationStore(variationRegionList, variationDataList.ToArray());
        }

        private void ReadFDArray()
        {
            if (fdArrayOffset == 0) return;
            
            otfReader.Position = cffOffset + fdArrayOffset;
            cffFont.CIDFontDicts = otfReader.ReadFDArray(cffOffset, fdArrayOffset, CFFVersion.CFF2);
        }

        private void ReadFDSelect()
        {
            if (cffFont.CIDFontDicts.Count <= 1 && fdSelectOffset == 0) return;

            var charStringCount = ReadCharStringIndexCount();
            otfReader.Position = cffOffset + fdSelectOffset;
            otfReader.ReadFDSelect(cffFont, (int)charStringCount);
        }

        private UInt32 ReadCharStringIndexCount()
        {
            otfReader.Position = cffOffset + charstringOffset;
            
            var count = otfReader.ReadUInt32();
            return count;
        }

        private void ReadCharstringIndex()
        {
            otfReader.Position = cffOffset + charstringOffset;

            var charstringIndex = otfReader.ReadCffIndex(CFFVersion.CFF2);
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
                        .PrepareSegments()
                        .GetGlyph();
                    glyphs.Add(glyph);
                    
                }
                catch (Exception e)
                {
                    exceptions++;
                }
            }

            cffFont.SetGlyphs(glyphs.ToArray());
        }
    }
}