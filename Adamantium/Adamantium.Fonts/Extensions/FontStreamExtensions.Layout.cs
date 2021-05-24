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

        public static LookupSubtable ReadLookupSubtable(this FontStreamReader reader, ushort lookupType, long subtableOffset)
        {
            switch (lookupType)
            {
                case 1:
                    return reader.ReadLookupSubtableType1(subtableOffset);
                case 2:
                    return reader.ReadLookupSubtableType2(subtableOffset);
                case 3:
                    return reader.ReadLookupSubtableType3(subtableOffset);
                case 4:
                    return reader.ReadLookupSubtableType4(subtableOffset);
                case 5:
                    return reader.ReadLookupSubtableType5(subtableOffset);
                case 6:
                    return reader.ReadLookupSubtableType6(subtableOffset);
                case 7:
                    return reader.ReadLookupSubtableType7(subtableOffset);
                case 8:
                    return reader.ReadLookupSubtableType8(subtableOffset);
                case 9:
                    return reader.ReadLookupSubtableType9(subtableOffset);
                default: throw new NotSupportedException();
            }
        }

        
        private static LookupSubtable ReadLookupSubtableType1(this FontStreamReader reader, long subtableOffset)
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
                default: throw new NotSupportedException();
            }
        }

        private static LookupSubtable ReadLookupSubtableType2(this FontStreamReader reader, long subtableOffset)
        {
            var format = reader.ReadUInt16();
            switch (format)
            {
                case 1:
                {
                    var coverageOffset = subtableOffset + reader.ReadUInt16();
                    var value1Format = (ValueFormat)reader.ReadUInt16();
                    var value2Format = (ValueFormat)reader.ReadUInt16();
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
                        subtable.PairSetsTables = pairSetTables;
                    }
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
                    throw new NotSupportedException($"Format {format} is not supported");
            }
        }

        private static LookupSubtable ReadLookupSubtableType3(this FontStreamReader reader, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var lookup = new LookupSubtableType3();
            lookup.Format = reader.ReadUInt16();
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
        
        private static LookupSubtable ReadLookupSubtableType4(this FontStreamReader reader, long subtableOffset)
        {
            return null;
        }
        
        private static LookupSubtable ReadLookupSubtableType5(this FontStreamReader reader, long subtableOffset)
        {
            return null;
        }
        
        private static LookupSubtable ReadLookupSubtableType6(this FontStreamReader reader, long subtableOffset)
        {
            return null;
        }
        
        private static LookupSubtable ReadLookupSubtableType7(this FontStreamReader reader, long subtableOffset)
        {
            return null;
        }
        
        private static LookupSubtable ReadLookupSubtableType8(this FontStreamReader reader, long subtableOffset)
        {
            return null;
        }
        
        private static LookupSubtable ReadLookupSubtableType9(this FontStreamReader reader, long subtableOffset)
        {
            return null;
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
    }
}