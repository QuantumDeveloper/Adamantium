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
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
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
                    var subtable = new LookupSubtableType2Format2();
                    return subtable;
                }
                default:
                    throw new NotSupportedException($"Format {format} is not supported");
            }
        }

        private static PairSet ReadPairSet(this FontStreamReader reader, ValueFormat value1Format, ValueFormat value2Format)
        {
            var pairSet = new PairSet();
            pairSet.SecondGlyph = reader.ReadUInt16();
            pairSet.ValueRecord1 = reader.ReadValueRecord(value1Format);
            pairSet.ValueRecord2 = reader.ReadValueRecord(value2Format);

            return pairSet;
        }
    }
}