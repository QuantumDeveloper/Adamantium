using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Extensions
{
    internal static partial class FontStreamExtensions
    {
        const uint DefaultTag = 1414284868;

        public static ValueRecord ReadValueRecord(this FontStreamReader reader, ValueFormat format)
        {
            var record = new ValueRecord();

            if (format.HasFlag(ValueFormat.XPlacement))
            {
                record.XPlacement = reader.ReadInt16();
            }

            if (format.HasFlag(ValueFormat.YPlacement))
            {
                record.YPlacement = reader.ReadInt16();
            }

            if (format.HasFlag(ValueFormat.XAdvance))
            {
                record.XAdvance = reader.ReadInt16();
            }

            if (format.HasFlag(ValueFormat.YAdvance))
            {
                record.YAdvance = reader.ReadInt16();
            }

            if (format.HasFlag(ValueFormat.XPlacementDevice))
            {
                record.XPlacementDevice = reader.ReadUInt16();
            }

            if (format.HasFlag(ValueFormat.YPlacementDevice))
            {
                record.YPlacementDevice = reader.ReadUInt16();
            }

            if (format.HasFlag(ValueFormat.XAdvanceDevice))
            {
                record.XAdvanceDevice = reader.ReadUInt16();
            }

            if (format.HasFlag(ValueFormat.YAdvanceDevice))
            {
                record.YAdvanceDevice = reader.ReadUInt16();
            }

            return record;
        }

        public static LangSysTable ReadLangSysTable(this FontStreamReader reader, uint tag, long offset)
        {
            if (offset <= 0)
            {
                return null;
            }

            reader.Position = offset;
            var langSysTable = new LangSysTable(tag)
            {
                LookupOrderOffset = reader.ReadUInt16(),
                RequiredFeatureIndex = reader.ReadUInt16()
            };
            var indexCount = reader.ReadUInt16();
            langSysTable.FeatureIndices = reader.ReadUInt16Array(indexCount);

            return langSysTable;
        }

        public static List<FeatureTable> ReadFeatureList(this FontStreamReader reader, long featureListOffset)
        {
            var features = new List<FeatureTable>();

            reader.Position = featureListOffset;
            var count = reader.ReadUInt16();
            var records = new FeatureRecord[count];
            for (int i = 0; i < count; ++i)
            {
                var record = new FeatureRecord();
                records[i] = record;
                record.Tag = reader.ReadUInt32();
                record.Offset = reader.ReadUInt16() + featureListOffset;
            }

            long[] paramsOffsets = new long[count];

            for (int i = 0; i < count; ++i)
            {
                var record = records[i];
                var feature = new FeatureTable(record.Tag);
                long offset = reader.ReadUInt16();
                if (offset != 0)
                {
                    offset += record.Offset;
                }

                paramsOffsets[i] = offset;
                var indexCount = reader.ReadUInt16();
                feature.LookupListIndices = reader.ReadUInt16Array(indexCount);
                features.Add(feature);
            }

            for (int i = 0; i < paramsOffsets.Length; ++i)
            {
                if (paramsOffsets[i] <= 0) continue;

                var paramsTable = reader.ReadFeatureParametersTable(paramsOffsets[i]);
                features[i].FeatureParameters = paramsTable;
            }

            return features;
        }

        public static List<ScriptTable> ReadScriptList(this FontStreamReader reader, long scriptListOffset)
        {
            var scriptTables = new List<ScriptTable>();

            var count = reader.ReadUInt16();
            var scriptRecordMap = new Dictionary<uint, long>();
            for (int i = 0; i < count; i++)
            {
                var tag = reader.ReadUInt32();
                long offset = reader.ReadUInt16();
                scriptRecordMap[tag] = scriptListOffset + offset;
            }

            foreach (var recordMap in scriptRecordMap)
            {
                reader.Position = recordMap.Value;
                var defaultLangSysOffset = reader.ReadUInt16() + recordMap.Value;
                var scriptTable = new ScriptTable(recordMap.Key);
                scriptTables.Add(scriptTable);
                var langSysCount = reader.ReadUInt16();
                var langSysTags = new uint[langSysCount];
                var langSysOffsets = new long[langSysCount];
                for (int i = 0; i < langSysCount; ++i)
                {
                    langSysTags[i] = reader.ReadUInt32();
                    var offset = reader.ReadUInt16() + recordMap.Value;
                    langSysOffsets[i] = offset;
                }

                scriptTable.DefaultLang = reader.ReadLangSysTable(DefaultTag, defaultLangSysOffset);
                scriptTable.LangSysTables = new LangSysTable[langSysCount];

                for (int i = 0; i < langSysCount; ++i)
                {
                    scriptTable.LangSysTables[i] = reader.ReadLangSysTable(langSysTags[i], langSysOffsets[i]);
                }
            }

            return scriptTables;
        }

        public static FeatureParametersTable ReadFeatureParametersTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var table = new FeatureParametersTable();
            table.Format = reader.ReadUInt16();
            table.FeatUiLabelNameId = reader.ReadUInt16();
            table.FeatUiTooltipTextNameId = reader.ReadUInt16();
            table.SampleTextNameId = reader.ReadUInt16();
            table.NumNamedParameters = reader.ReadUInt16();
            table.FirstParamUiLabelNameId = reader.ReadUInt16();
            table.CharCount = reader.ReadUInt16();
            table.Character = reader.ReadUInt24Array(table.CharCount);

            return table;
        }

        public static CoverageTable ReadCoverageTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var format = reader.ReadUInt16();

            switch (format)
            {
                case 1:
                    var count = reader.ReadUInt16();
                    var glyphs = reader.ReadUInt16Array(count);
                    return new CoverageTableFormat1() {GlyphIdArray = glyphs};
                    break;
                case 2:
                    var rangeCount = reader.ReadUInt16();
                    ushort[] startIndices = new ushort[rangeCount];
                    ushort[] endIndices = new ushort[rangeCount];
                    ushort[] coverageIndices = new ushort[rangeCount];

                    for (int i = 0; i < rangeCount; ++i)
                    {
                        startIndices[i] = reader.ReadUInt16();
                        endIndices[i] = reader.ReadUInt16();
                        coverageIndices[i] = reader.ReadUInt16();
                    }

                    var format2Table = new CoverageTableFormat2();
                    format2Table.RangeCount = rangeCount;
                    format2Table.StartIndices = startIndices;
                    format2Table.EndIndices = endIndices;
                    format2Table.CoverageIndices = coverageIndices;
                    return format2Table;
                default:
                    throw new NotSupportedException($"Format {format} is not supported for coverage table");
            }
        }

        public static AnchorTable ReadAnchorTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var anchorTable = new AnchorTable();
            anchorTable.Format = reader.ReadUInt16();
            anchorTable.XCoordinate = reader.ReadInt16();
            anchorTable.YCoordinate = reader.ReadInt16();
            switch (anchorTable.Format)
            {
                case 2:
                    anchorTable.AnchorPoint = reader.ReadUInt16();
                    break;
                case 3:
                    var xDeviceOffset = reader.ReadUInt16();
                    var yDeviceOffset = reader.ReadUInt16();
                    if (xDeviceOffset > 0)
                    {
                        anchorTable.XDevice = reader.ReadDeviceTable(xDeviceOffset + offset);
                    }

                    if (yDeviceOffset > 0)
                    {
                        anchorTable.XDevice = reader.ReadDeviceTable(yDeviceOffset + offset);
                    }
                    break;
            }

            return anchorTable;
        }

        public static DeviceTable ReadDeviceTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var table = new DeviceTable();
            table.StartSize = reader.ReadUInt16();
            table.EndSize = reader.ReadUInt16();
            table.DeltaFormat = (DeltaFormatValues)reader.ReadUInt16();
            table.DeltaValues = reader.ReadUInt16Array(table.EndSize - table.StartSize);

            return table;
        }

        public static List<LookupTable> ReadLookupListTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var lookupCount = reader.ReadUInt16();
            var lookupOffsets = reader.ReadUInt16Array(lookupCount);

            var lookupList = new List<LookupTable>();

            for (int i = 0; i < lookupCount; i++)
            {
                long lookupTableOffset = offset + lookupOffsets[i];
                var lookup = reader.ReadLookupTable(lookupTableOffset);
                lookupList.Add(lookup);
            }

            return lookupList;
        }

        private static PairSet ReadPairSet(this FontStreamReader reader, ValueFormat value1Format, ValueFormat value2Format)
        {
            var pairSet = new PairSet();
            pairSet.SecondGlyph = reader.ReadUInt16();
            pairSet.ValueRecord1 = reader.ReadValueRecord(value1Format);
            pairSet.ValueRecord2 = reader.ReadValueRecord(value2Format);

            return pairSet;
        }
        
        private static ClassDefTable ReadClassDefTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var classDef = new ClassDefTable();
            classDef.Format = reader.ReadUInt16();
            switch (classDef.Format)
            {
                case 1:
                    classDef.StartGlyphId = reader.ReadUInt16();
                    var glyphCount = reader.ReadUInt16();
                    classDef.ClassValueArray = reader.ReadUInt16Array(glyphCount);
                    break;
                case 2:
                    var classRangeCount = reader.ReadUInt16();
                    var records = new ClassRangeRecord[classRangeCount];
                    for (int i = 0; i < classRangeCount; i++)
                    {
                        var start = reader.ReadUInt16();
                        var end = reader.ReadUInt16();
                        var classId = reader.ReadUInt16();
                        var recordItem = new ClassRangeRecord(start, end, classId);
                        records[i] = recordItem;
                    }

                    classDef.ClassRangeRecords = records;
                    break;
            }

            return classDef;
        }

        private static MarkArrayTable ReadMarkArrayTable(this FontStreamReader reader, long subTableOffset)
        {
            reader.Position = subTableOffset;
            
            var markArrayTable = new MarkArrayTable();
            var count = reader.ReadUInt16();
            markArrayTable.MarkRecords = new MarkRecord[count];
            for (int i = 0; i < count; i++)
            {
                var markClass = reader.ReadUInt16();
                var offset = reader.ReadUInt16();
                markArrayTable.MarkRecords[i] = new MarkRecord(markClass, offset);
            }

            markArrayTable.AnchorTables = new AnchorTable[count];
            for (int i = 0; i < count; i++)
            {
                var markRecord = markArrayTable.MarkRecords[i];
                markArrayTable.AnchorTables[i] = reader.ReadAnchorTable(markRecord.Offset + subTableOffset);
            }

            return markArrayTable;
        }
        
        private static Mark2ArrayTable ReadMark2ArrayTable(this FontStreamReader reader, long subTableOffset, int markClassCount)
        {
            reader.Position = subTableOffset;
            
            var markArrayTable = new Mark2ArrayTable();
            var count = reader.ReadUInt16();
            markArrayTable.Records = new Mark2Record[count];
            for (int i = 0; i < count; i++)
            {
                var record = new Mark2Record();
                markArrayTable.Records[i] = record;
                record.Anchors = new AnchorTable[markClassCount];
                var anchorOffsets = reader.ReadUInt16Array(markClassCount);
                for (int k = 0; k < markClassCount; ++k)
                {
                    long offset = anchorOffsets[k];
                    
                    if (offset <= 0) continue;

                    offset += subTableOffset;

                    record.Anchors[k] = reader.ReadAnchorTable(offset);
                }
                
            }

            return markArrayTable;
        }
        
        private static BaseArrayTable ReadBaseArrayTable(this FontStreamReader reader, long subTableOffset, int markClassCount)
        {
            reader.Position = subTableOffset;
            
            var baseArrayTable = new BaseArrayTable();
            var count = reader.ReadUInt16();
            baseArrayTable.BaseRecords = new BaseRecord[count];
            var anchorOffsetArray = reader.ReadUInt16Array(markClassCount);
            for (int k = 0; k < count; ++k)
            {
                var baseRecord = new BaseRecord();
                baseArrayTable.BaseRecords[k] = baseRecord;
                baseRecord.Anchors = new AnchorTable[markClassCount];
                for (int i = 0; i < markClassCount; ++i)
                {
                    long offset = anchorOffsetArray[i];
                    
                    if (offset <= 0) continue;

                    offset += subTableOffset;
                    
                    baseRecord.Anchors[i] = reader.ReadAnchorTable(offset);
                }
            }

            return baseArrayTable;
        }
        
        private static LigatureArrayTable ReadLigatureArrayTable(this FontStreamReader reader, long subTableOffset, int markClassCount)
        {
            reader.Position = subTableOffset;
            
            var ligatureArrayTable = new LigatureArrayTable();
            var ligatureCount = reader.ReadUInt16();
            ligatureArrayTable.AttachTables = new LigatureAttachTable[ligatureCount];
            for (int k = 0; k < ligatureCount; ++k)
            {
                var ligatureAttachTable = new LigatureAttachTable();
                ligatureArrayTable.AttachTables[k] = ligatureAttachTable;

                var componentCount = reader.ReadUInt16();
                ligatureAttachTable.ComponentRecords = new ComponentRecord[componentCount];
                for (int i = 0; i < componentCount; ++i)
                {
                    var componentRecord = new ComponentRecord();
                    componentRecord.Anchors = new AnchorTable[markClassCount];
                    
                    var anchorOffsetArray = reader.ReadUInt16Array(markClassCount);
                    
                    for (int m = 0; m < markClassCount; ++m)
                    {
                        long offset = anchorOffsetArray[i];
                    
                        if (offset <= 0) continue;

                        offset += subTableOffset;
                    
                        componentRecord.Anchors[i] = reader.ReadAnchorTable(offset);
                    }
                }
            }

            return ligatureArrayTable;
        }

        private static SequenceRuleSetTable ReadSequenceRuleSetTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var count = reader.ReadUInt16();
            var ruleSet = new SequenceRuleSetTable();
            ruleSet.Rules = new SequenceRuleTable[count];

            for (int i = 0; i < count; ++i)
            {
                ruleSet.Rules[i] = reader.ReadSequenceRuleTable();
            }

            return ruleSet;
        }

        private static SequenceRuleTable ReadSequenceRuleTable(this FontStreamReader reader)
        {
            var table = new SequenceRuleTable();
            var glyphCount = reader.ReadUInt16();
            var lookupCount = reader.ReadUInt16();
            table.InputSequence = new ushort[glyphCount - 1];
            table.SeqLookupRecords = reader.ReadSequenceLookupRecordArray(lookupCount);
            
            return table;
        }
        
        private static ClassSequenceRuleSetTable ReadClassSequenceRuleSetTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var count = reader.ReadUInt16();
            var ruleSet = new ClassSequenceRuleSetTable();
            ruleSet.Rules = new ClassSequenceRuleTable[count];

            for (int i = 0; i < count; ++i)
            {
                ruleSet.Rules[i] = reader.ReadClassSequenceRuleTable();
            }

            return ruleSet;
        }

        private static ClassSequenceRuleTable ReadClassSequenceRuleTable(this FontStreamReader reader)
        {
            var table = new ClassSequenceRuleTable();
            var glyphCount = reader.ReadUInt16();
            var lookupCount = reader.ReadUInt16();
            table.InputSequence = new ushort[glyphCount - 1];
            table.LookupRecords = reader.ReadSequenceLookupRecordArray(lookupCount);
            return table;
        }
        
        private static ChainedSequenceRuleSetTable ReadChainedSequenceRuleSetTable(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var count = reader.ReadUInt16();
            var ruleSet = new ChainedSequenceRuleSetTable();
            ruleSet.Rules = new ChainedSequenceRuleTable[count];
            var offsets = reader.ReadUInt16Array(count);

            for (int i = 0; i < count; ++i)
            {
                var offset = offsets[i] + subtableOffset;
                ruleSet.Rules[i] = reader.ReadChainedSequenceRuleTable(offset);
            }

            return ruleSet;
        }

        private static ChainedSequenceRuleTable ReadChainedSequenceRuleTable(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var table = new ChainedSequenceRuleTable();
            var backtrackGlyphCount = reader.ReadUInt16();
            table.BacktrackSequence = reader.ReadUInt16Array(backtrackGlyphCount);
            var inputGlyphCount = reader.ReadUInt16();
            table.InputSequence = reader.ReadUInt16Array(inputGlyphCount - 1);
            var lookaheadGlyphCount = reader.ReadUInt16();
            table.LookaheadSequence = reader.ReadUInt16Array(lookaheadGlyphCount);
            var seqLookupCount = reader.ReadUInt16();
            table.SeqLookupRecords = reader.ReadSequenceLookupRecordArray(seqLookupCount);

            return table;
        }

        private static SequenceLookupRecord[] ReadSequenceLookupRecordArray(this FontStreamReader reader, int count)
        {
            var lookupArray = new SequenceLookupRecord[count];
            for (int i = 0; i < count; ++i)
            {
                var record = new SequenceLookupRecord();
                record.SequenceIndex = reader.ReadUInt16();
                record.LookupListIndex = reader.ReadUInt16();
                lookupArray[i] = record;
            }

            return lookupArray;
        }
        
        public static LookupTable ReadLookupTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var lookup = new LookupTable();
            lookup.LookupType = reader.ReadUInt16();
            lookup.LookupFlag = (LookupFlags)reader.ReadUInt16();
            var subtableCount = reader.ReadUInt16();
            var subtableOffsets = reader.ReadUInt16Array(subtableCount);
            if (lookup.LookupFlag == LookupFlags.UseMarkFilteringSet)
            {
                lookup.MarkFilteringSet = reader.ReadUInt16();
            }

            lookup.Subtables = new LookupSubtable[subtableCount];
            for (int i = 0; i < subtableCount; ++i)
            {
                var subtableOffset = subtableOffsets[i] + offset;
                lookup.Subtables[i] = reader.ReadLookupSubtable(lookup.LookupType, subtableOffset);
            }

            return lookup;
        }
        
        private static LookupSubtable ReadLookupSubTableType1(this FontStreamReader reader, long subtableOffset)
        {
            var format = reader.ReadUInt16();
            var coverageOffset = reader.ReadUInt16() + subtableOffset;
            var valueFormat = (ValueFormat) reader.ReadUInt16();

            switch (format)
            {
                case 1:
                {
                    var record = reader.ReadValueRecord(valueFormat);
                    var coverage = reader.ReadCoverageTable(coverageOffset);
                    return new LookupSubtableType1(coverage, record);
                }
                case 2:
                {
                    var count = reader.ReadUInt16();
                    var records = new ValueRecord[count];
                    for (int i = 0; i < count; ++i)
                    {
                        records[i] = reader.ReadValueRecord(valueFormat);
                    }

                    var coverage = reader.ReadCoverageTable(coverageOffset);

                    return new LookupSubtableType1(coverage, records);
                }
                default:
                    return new UnImplementedLookupSubTable(format);
            }
        }

        private static LookupSubtable ReadLookupSubTableType2(this FontStreamReader reader, long subtableOffset)
        {
            var format = reader.ReadUInt16();
            switch (format)
            {
                case 1:
                {
                    var coverageOffset = subtableOffset + reader.ReadUInt16();
                    var value1Format = (ValueFormat)reader.ReadUInt16(); // can be null
                    var value2Format = (ValueFormat)reader.ReadUInt16(); // can be null
                    var pairSetCount = reader.ReadUInt16();
                    var pairSetOffsetArray = reader.ReadUInt16Array(pairSetCount);
                    var pairSetTables = new PairSetTable[pairSetCount];
                    var subtable = new LookupSubtableType2Format1();

                    for (int i = 0; i < pairSetCount; ++i)
                    {
                        reader.Position = subtableOffset + pairSetOffsetArray[i]; 
                        var pairCount = reader.ReadUInt16();
                        var pairSets = new PairSet[pairCount];
                        for (int j = 0; j < pairCount; j++)
                        {
                            pairSets[j] = reader.ReadPairSet(value1Format, value2Format);
                        }

                        subtable.CoverageTable = reader.ReadCoverageTable(coverageOffset);
                        var pairSetTable = new PairSetTable();
                        pairSetTable.PairSets = pairSets;
                        pairSetTables[i] = pairSetTable;
                    }
                    
                    subtable.PairSetsTables = pairSetTables;
                    return subtable;
                }
                case 2:
                {
                    var coverageOffset = subtableOffset + reader.ReadUInt16();
                    var value1Format = (ValueFormat)reader.ReadUInt16();
                    var value2Format = (ValueFormat)reader.ReadUInt16();
                    var classDef1Offset = subtableOffset + reader.ReadUInt16();
                    var classDef2Offset = subtableOffset + reader.ReadUInt16();
                    var class1Count = reader.ReadUInt16();
                    var class2Count = reader.ReadUInt16();

                    var class1Records = new Class1Record[class1Count];
                    for (int i = 0; i < class1Count; ++i)
                    {
                        var class2Records = new Class2Record[class2Count];
                        for (int j = 0; j < class2Count; j++)
                        {
                            var value1 = reader.ReadValueRecord(value1Format);
                            var value2 = reader.ReadValueRecord(value1Format);
                            class2Records[j] = new Class2Record() {Value1 = value1, Value2 = value2};
                        }

                        class1Records[i] = new Class1Record() {Class2Records = class2Records};
                    }
                    
                    var subtable = new LookupSubtableType2Format2();
                    subtable.Class1Records = class1Records;
                    subtable.ClassDef1 = reader.ReadClassDefTable(classDef1Offset);
                    subtable.ClassDef1 = reader.ReadClassDefTable(classDef2Offset);
                    subtable.CoverageTable = reader.ReadCoverageTable(coverageOffset);
                    return subtable;
                }
                default:
                    return new UnImplementedLookupSubTable(format);
            }
        }

        private static LookupSubtable ReadLookupSubTableType3(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var lookup = new LookupSubTableType3();
            var format = reader.ReadUInt16();

            if (format != 1)
            {
                return new UnImplementedLookupSubTable(format);
            }

            var coverageOffset = reader.ReadUInt16() + subtableOffset;
            var entryExitCount = reader.ReadUInt16();
            var entryAnchorOffset = new ushort[entryExitCount];
            var exitAnchorOffset = new ushort[entryExitCount];

            for (int i = 0; i < entryExitCount; ++i)
            {
                entryAnchorOffset[i] = reader.ReadUInt16();
                exitAnchorOffset[i] = reader.ReadUInt16();
            }

            lookup.Coverage = reader.ReadCoverageTable(coverageOffset);

            lookup.EntryAnchors = new AnchorTable[entryExitCount];
            lookup.ExitAnchors = new AnchorTable[entryExitCount];
            for (int i = 0; i < entryExitCount; i++)
            {
                var entryOffset = entryAnchorOffset[i];
                if (entryOffset > 0)
                {
                    lookup.EntryAnchors[i] = reader.ReadAnchorTable(entryOffset + subtableOffset);
                }

                var exitOffset = exitAnchorOffset[i];
                if (exitOffset > 0)
                {
                    lookup.ExitAnchors[i] = reader.ReadAnchorTable(exitOffset + subtableOffset);
                }
            }

            return lookup;
        }
        
        private static LookupSubtable ReadLookupSubTableType4(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();

            if (format != 1)
            {
                return new UnImplementedLookupSubTable(format);
            }

            var markCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var baseCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var markClassCount = reader.ReadUInt16();
            var markArrayOffset = reader.ReadUInt16() + subtableOffset;
            var baseArrayOffset = reader.ReadUInt16() + subtableOffset;

            var lookupType4 = new LookupSubTableType4();
            lookupType4.MarkCoverage = reader.ReadCoverageTable(markCoverageOffset);
            lookupType4.BaseCoverage = reader.ReadCoverageTable(baseCoverageOffset);
            lookupType4.MarkArrayTable = reader.ReadMarkArrayTable(markArrayOffset);
            lookupType4.BaseArrayTable = reader.ReadBaseArrayTable(baseArrayOffset, markClassCount);

            return lookupType4;
        }
        
        private static LookupSubtable ReadLookupSubTableType5(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            if (format != 1)
            {
                return new UnImplementedLookupSubTable(format);
            }
            
            var markCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var markClassCount = reader.ReadUInt16();
            var markArrayOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureArrayOffset = reader.ReadUInt16() + subtableOffset;
            
            var lookupType5 = new LookupSubTableType5();
            lookupType5.MarkCoverage = reader.ReadCoverageTable(markArrayOffset);
            lookupType5.LigatureCoverage = reader.ReadCoverageTable(ligatureCoverageOffset);
            lookupType5.MarkArrayTable = reader.ReadMarkArrayTable(markArrayOffset);
            lookupType5.LigatureArrayTable = reader.ReadLigatureArrayTable(ligatureArrayOffset, markClassCount);

            return lookupType5;
        }
        
        private static LookupSubtable ReadLookupSubTableType6(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();

            if (format != 1)
            {
                return new UnImplementedLookupSubTable(format);
            }

            var markCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var classCount = reader.ReadUInt16();
            var markArrayOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureArrayOffset = reader.ReadUInt16() + subtableOffset;

            var lookupSubTable = new LookupSubTableType6();
            lookupSubTable.Mark1Coverage = reader.ReadCoverageTable(markCoverageOffset);
            lookupSubTable.Mark2Coverage = reader.ReadCoverageTable(ligatureCoverageOffset);
            lookupSubTable.Mark1ArrayTable = reader.ReadMarkArrayTable(markArrayOffset);
            lookupSubTable.Mark2ArrayTable = reader.ReadMark2ArrayTable(ligatureArrayOffset, classCount);

            return lookupSubTable;
        }
        
        private static LookupSubtable ReadLookupSubTableType7(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();

            switch (format)
            {
                case 1:
                {
                    var lookupFormat1 = new LookupSubTableType7Format1();
                    var coverageOffset = reader.ReadUInt16() + subtableOffset;
                    var ruleSetCount = reader.ReadUInt16();
                    var ruleSetOffsets = reader.ReadUInt16Array(ruleSetCount);

                    lookupFormat1.SequenceRuleSets = new SequenceRuleSetTable[ruleSetCount];
                    for (int i = 0; i < ruleSetCount; i++)
                    {
                        long offset = ruleSetOffsets[i];

                        if (offset <= 0) continue;

                        offset += subtableOffset;
                        var ruleSet = reader.ReadSequenceRuleSetTable(offset);
                        lookupFormat1.SequenceRuleSets[i] = ruleSet;
                    }

                    lookupFormat1.Coverage = reader.ReadCoverageTable(coverageOffset);

                    return lookupFormat1;
                }
                case 2:
                {
                    var lookupFormat2 = new LookupSubTableType7Format2();
                    var coverageOffset = reader.ReadUInt16() + subtableOffset;
                    var classDefOffset = reader.ReadUInt16() + subtableOffset;
                    var ruleSetCount = reader.ReadUInt16();
                    var ruleSetOffsets = reader.ReadUInt16Array(ruleSetCount);

                    lookupFormat2.Coverage = reader.ReadCoverageTable(coverageOffset);
                    lookupFormat2.ClassDef = reader.ReadClassDefTable(classDefOffset);

                    lookupFormat2.ClassSequenceRuleSets = new ClassSequenceRuleSetTable[ruleSetCount];
                    
                    for (int i = 0; i < ruleSetCount; i++)
                    {
                        long offset = ruleSetOffsets[i];

                        if (offset <= 0) continue;

                        offset += subtableOffset;
                        var ruleSet = reader.ReadClassSequenceRuleSetTable(offset);
                        lookupFormat2.ClassSequenceRuleSets[i] = ruleSet;
                    }
                    
                    return lookupFormat2;
                }
                case 3:
                {
                    var glyphCount = reader.ReadUInt16();
                    var lookupCount = reader.ReadUInt16();
                    var coverageOffsets = reader.ReadUInt16Array(glyphCount);

                    var lookupFormat3 = new LookupSubTableType7Format3();
                    lookupFormat3.LookupRecords = reader.ReadSequenceLookupRecordArray(lookupCount);
                    lookupFormat3.CoverageTables = new CoverageTable[glyphCount];
                    
                    for (int i = 0; i < glyphCount; ++i)
                    {
                        var offset = coverageOffsets[i];
                        lookupFormat3.CoverageTables[i] = reader.ReadCoverageTable(offset + subtableOffset);
                    }

                    return lookupFormat3;
                }
                default:
                    return new UnImplementedLookupSubTable(format);
            }
        }
        
        private static LookupSubtable ReadLookupSubTableType8(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            switch (format)
            {
                case 1:
                {
                    var lookupFormat1 = new LookupSubTableType8Format1();
                    var coverageOffset = reader.ReadUInt16() + subtableOffset;
                    var chainedSeqRuleSetCount = reader.ReadUInt16();
                    var chainedSeqRuleSetOffsets = reader.ReadUInt16Array(chainedSeqRuleSetCount);

                    lookupFormat1.Coverage = reader.ReadCoverageTable(coverageOffset);
                    lookupFormat1.ChainedSeqRuleSets = new ChainedSequenceRuleSetTable[chainedSeqRuleSetCount];
                    for (int i = 0; i < chainedSeqRuleSetCount; i++)
                    {
                        long offset = chainedSeqRuleSetOffsets[i];
                        if (offset <= 0) continue;

                        offset += subtableOffset;

                        lookupFormat1.ChainedSeqRuleSets[i] = reader.ReadChainedSequenceRuleSetTable(offset);
                    }

                    return lookupFormat1;
                }
                case 2:
                {
                    var lookupFormat2 = new LookupSubTableType8Format2();
                    var coverageOffset = reader.ReadUInt16() + subtableOffset;
                    var backtrackClassDefOffset = reader.ReadUInt16() + subtableOffset;
                    var inputClassDefOffset = reader.ReadUInt16() + subtableOffset;
                    var lookaheadClassDefOffset = reader.ReadUInt16() + subtableOffset;
                    var chainedSeqRuleSetCount = reader.ReadUInt16();
                    var chainedSeqRuleSetOffsets = reader.ReadUInt16Array(chainedSeqRuleSetCount);

                    lookupFormat2.Coverage = reader.ReadCoverageTable(coverageOffset);
                    lookupFormat2.ChainedSeqRuleSets = new ChainedSequenceRuleSetTable[chainedSeqRuleSetCount];
                    for (int i = 0; i < chainedSeqRuleSetCount; i++)
                    {
                        long offset = chainedSeqRuleSetOffsets[i];
                        if (offset <= 0) continue;

                        offset += subtableOffset;

                        lookupFormat2.ChainedSeqRuleSets[i] = reader.ReadChainedSequenceRuleSetTable(offset);
                    }

                    lookupFormat2.BacktrackClassDef = reader.ReadClassDefTable(backtrackClassDefOffset);
                    lookupFormat2.InputClassDef = reader.ReadClassDefTable(inputClassDefOffset);
                    lookupFormat2.LookaheadClassDef = reader.ReadClassDefTable(lookaheadClassDefOffset);

                    return lookupFormat2;
                }
                case 3:
                {
                    var lookupFormat3 = new LookupSubTableType8Format3();
                    var backtrackGlyphCount = reader.ReadUInt16();
                    var backtrackCoverageOffsets = reader.ReadUInt16Array(backtrackGlyphCount);
                    var inputGlyphCount = reader.ReadUInt16();
                    var inputCoverageOffsets = reader.ReadUInt16Array(inputGlyphCount);
                    var lookaheadGlyphCount = reader.ReadUInt16();
                    var lookaheadCoverageOffsets = reader.ReadUInt16Array(lookaheadGlyphCount);
                    var seqLookupCount = reader.ReadUInt16();
                    lookupFormat3.LookupRecords = reader.ReadSequenceLookupRecordArray(seqLookupCount);

                    lookupFormat3.BacktrackCoverages = new CoverageTable[backtrackGlyphCount];
                    for (int i = 0; i < backtrackGlyphCount; ++i)
                    {
                        var offset = backtrackCoverageOffsets[i] + subtableOffset;
                        lookupFormat3.BacktrackCoverages[i] = reader.ReadCoverageTable(offset);
                    }
                    
                    lookupFormat3.InputCoverages = new CoverageTable[inputGlyphCount];
                    for (int i = 0; i < inputGlyphCount; ++i)
                    {
                        var offset = inputCoverageOffsets[i] + subtableOffset;
                        lookupFormat3.InputCoverages[i] = reader.ReadCoverageTable(offset);
                    }
                    
                    lookupFormat3.LookaheadCoverages = new CoverageTable[lookaheadGlyphCount];
                    for (int i = 0; i < lookaheadGlyphCount; ++i)
                    {
                        var offset = lookaheadCoverageOffsets[i] + subtableOffset;
                        lookupFormat3.LookaheadCoverages[i] = reader.ReadCoverageTable(offset);
                    }
                    

                    return lookupFormat3;
                }
                default:
                    return new UnImplementedLookupSubTable(format);
            }
        }
        
        private static LookupSubtable ReadLookupSubTableType9(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var format = reader.ReadUInt16();
            var extensionLookupType = reader.ReadUInt16();
            var extensionOffset = reader.ReadUInt32();
            return reader.ReadLookupSubtable(extensionLookupType, extensionOffset);
        }
        
        public static LookupSubtable ReadLookupSubtable(this FontStreamReader reader, ushort lookupType, long subtableOffset)
        {
            switch (lookupType)
            {
                case 1:
                    return reader.ReadLookupSubTableType1(subtableOffset);
                case 2:
                    return reader.ReadLookupSubTableType2(subtableOffset);
                case 3:
                    return reader.ReadLookupSubTableType3(subtableOffset);
                case 4:
                    return reader.ReadLookupSubTableType4(subtableOffset);
                case 5:
                    return reader.ReadLookupSubTableType5(subtableOffset);
                case 6:
                    return reader.ReadLookupSubTableType6(subtableOffset);
                case 7:
                    return reader.ReadLookupSubTableType7(subtableOffset);
                case 8:
                    return reader.ReadLookupSubTableType8(subtableOffset);
                case 9:
                    return reader.ReadLookupSubTableType9(subtableOffset);
                default: throw new NotSupportedException();
            }
        }
    }
}