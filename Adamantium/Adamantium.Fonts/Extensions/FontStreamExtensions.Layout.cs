using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.GPOS;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Extensions
{
    internal static partial class FontStreamExtensions
    {
        const uint DefaultTag = 1145457748;

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

        public static FeatureTable[] ReadFeatureList(this FontStreamReader reader, long featureListOffset)
        {
            reader.Position = featureListOffset;
            
            var features = new List<FeatureTable>();
            
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

            return features.ToArray();
        }

        public static ScriptTable[] ReadScriptList(this FontStreamReader reader, long scriptListOffset)
        {
            reader.Position = scriptListOffset;
            
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

            return scriptTables.ToArray();
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

        public static AnchorPointTable ReadAnchorTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var anchorTable = new AnchorPointTable();
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
                        anchorTable.YDevice = reader.ReadDeviceTable(yDeviceOffset + offset);
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

            markArrayTable.AnchorTables = new AnchorPointTable[count];
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
                record.Anchors = new AnchorPointTable[markClassCount];
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
                baseRecord.Anchors = new AnchorPointTable[markClassCount];
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
                    componentRecord.Anchors = new AnchorPointTable[markClassCount];
                    
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
            table.GlyphCount = reader.ReadUInt16();
            var lookupCount = reader.ReadUInt16();
            table.InputSequence = new ushort[table.GlyphCount - 1];
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

        private static AlternateSetTable ReadAlternateSetTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var alternateSetTable = new AlternateSetTable();
            var glyphCount = reader.ReadUInt16();
            alternateSetTable.AlternateGlyphIDs = reader.ReadUInt16Array(glyphCount);
            
            return alternateSetTable;
        }
        
        private static LigatureSetTable ReadLigatureSetTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var ligatureSetTable = new LigatureSetTable();
            var glyphCount = reader.ReadUInt16();
            var ligatureOffsets = reader.ReadUInt16Array(glyphCount);
            ligatureSetTable.LigatureTables = new LigatureTable[glyphCount];

            for (int i = 0; i < glyphCount; ++i)
            {
                var ligatureOffset = ligatureOffsets[i] + offset;
                ligatureSetTable.LigatureTables[i] = reader.ReadLigatureTable(ligatureOffset);
            }
            
            return ligatureSetTable;
        }
        
        private static LigatureTable ReadLigatureTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var ligatureTable = new LigatureTable();
            ligatureTable.LigatureGlyphID = reader.ReadUInt16();
            var componentCount = reader.ReadUInt16();
            ligatureTable.ComponentGlypIDs = reader.ReadUInt16Array(componentCount - 1);
            
            return ligatureTable;
        }

        private static SequenceTable ReadSequenceTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;

            var count = reader.ReadUInt16();
            var sequence = new SequenceTable();
            sequence.SubstituteGlyphIDs = reader.ReadUInt16Array(count);
            return sequence;
        }

        private static AttachmentListTable ReadAttachListTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var attachmentListTable = new AttachmentListTable();
            var coverageOffset = reader.ReadUInt16() + offset;
            var glyphCount = reader.ReadUInt16();
            var attachPointOffsets = reader.ReadUInt16Array(glyphCount);
            attachmentListTable.AttachPoints = new AttachPoint[glyphCount];

            for (int i = 0; i < glyphCount; i++)
            {
                var pointCount = reader.ReadUInt16();
                var attachPoint = new AttachPoint();
                attachPoint.PointIndices = reader.ReadUInt16Array(pointCount);
                attachmentListTable.AttachPoints[i] = attachPoint;
            }

            attachmentListTable.Coverage = reader.ReadCoverageTable(coverageOffset);

            return attachmentListTable;
        }

        private static LigatureCaretList ReadLigatureCaretList(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var ligatureCaretList = new LigatureCaretList();
            var coverageOffset = reader.ReadUInt16() + offset;
            var ligGlyphCount = reader.ReadUInt16();
            var ligGlyphOffsets = reader.ReadUInt16Array(ligGlyphCount);
            ligatureCaretList.LigatureGlyphs = new LigatureGlyph[ligGlyphCount];

            for (int i = 0; i < ligGlyphCount; i++)
            {
                ligatureCaretList.LigatureGlyphs[i] = reader.ReadLigatureGlyph(ligGlyphOffsets[i] + offset);
                
            }

            ligatureCaretList.Coverage = reader.ReadCoverageTable(coverageOffset);

            return ligatureCaretList;
        }

        private static LigatureGlyph ReadLigatureGlyph(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var ligTable = new LigatureGlyph();
            var caretCount = reader.ReadUInt16();
            ligTable.CaretValues = new CaretValue[caretCount];
            var caretOffsets = reader.ReadUInt16Array(caretCount);
            for (int j = 0; j < caretCount; ++j)
            {
                ligTable.CaretValues[j] = reader.ReadCaretValue(caretOffsets[j] + offset);
            }

            return ligTable;
        }

        private static CaretValue ReadCaretValue(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            
            var caretValue = new CaretValue();
            caretValue.Format = reader.ReadUInt16();
            switch (caretValue.Format)
            {
                case 1:
                    caretValue.Coordinate = reader.ReadInt16();
                    break;
                case 2:
                    caretValue.PointIndex = reader.ReadUInt16();
                    break;
                case 3:
                    caretValue.Coordinate = reader.ReadInt16();
                    caretValue.DeviceOffset = reader.ReadUInt16() + offset;
                    break;
            }

            return caretValue;
        }

        private static MarkGlyphSetsTable ReadMarkGlyphSetsTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;

            var mark = new MarkGlyphSetsTable();
            var format = reader.ReadUInt16();
            var count = reader.ReadUInt16();
            var coverageOffsets = reader.ReadUInt32Array(count);

            mark.CoverageTables = new CoverageTable[count];
            for (int i = 0; i < count; ++i)
            {
                mark.CoverageTables[i] = reader.ReadCoverageTable(coverageOffsets[i] + offset);
            }
            
            return mark;
        }

        public static GlyphDefinitionTable ReadGDEFTable(this FontStreamReader reader, long offset)
        {
            var gdef = new GlyphDefinitionTable();
            gdef.MajorVersion = reader.ReadUInt16();
            gdef.MinorVersion = reader.ReadUInt16();
            var glyphClassDefOffset = reader.ReadUInt16() + offset;
            var attachListOffset = reader.ReadUInt16() + offset;
            var ligCaretListOffset = reader.ReadUInt16() + offset;
            var markAttachClassDefOffset = reader.ReadUInt16() + offset;

            gdef.GlyphClassDefTable = reader.ReadClassDefTable(glyphClassDefOffset);
            gdef.AttachList = reader.ReadAttachListTable(attachListOffset);
            gdef.LigatureCaretList = reader.ReadLigatureCaretList(ligCaretListOffset);
            gdef.MarkAttachClassDefTable = reader.ReadClassDefTable(markAttachClassDefOffset);
            
            switch (gdef.MinorVersion)
            {
                case 2:
                {
                    var markGlyphSetsDefOffset = reader.ReadUInt16() + offset;
                    gdef.MarkGlyphSetsTable = reader.ReadMarkGlyphSetsTable(markGlyphSetsDefOffset);
                }
                    break;
                case 3:
                {
                    var markGlyphSetsDefOffset = reader.ReadUInt16() + offset;
                    var itemVarStoreOffset = reader.ReadUInt32() + offset;
                    gdef.MarkGlyphSetsTable = reader.ReadMarkGlyphSetsTable(markGlyphSetsDefOffset);
                }
                    break;
            }

            return gdef;
        }
    }
}