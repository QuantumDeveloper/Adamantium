using System;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.GPOS;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Extensions
{
    internal static partial class FontStreamExtensions
    {
        public static GPOSLookupTable ReadGPOSLookupTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var lookup = new GPOSLookupTable();
            lookup.LookupType = reader.ReadUInt16();
            lookup.LookupFlag = (LookupFlags)reader.ReadUInt16();
            var subtableCount = reader.ReadUInt16();
            var subtableOffsets = reader.ReadUInt16Array(subtableCount);
            if (lookup.LookupFlag == LookupFlags.UseMarkFilteringSet)
            {
                lookup.MarkFilteringSet = reader.ReadUInt16();
            }

            lookup.SubTables = new GPOSLookupSubTable[subtableCount];
            for (int i = 0; i < subtableCount; ++i)
            {
                var subtableOffset = subtableOffsets[i] + offset;
                lookup.SubTables[i] = reader.ReadGPOSLookupSubtable((GPOSLookupType)lookup.LookupType, subtableOffset);
            }

            return lookup;
        }
        
        private static GPOSLookupSubTable ReadSingleAdjustmentPositioningSubTable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var format = reader.ReadUInt16();
            var coverageOffset = reader.ReadUInt16() + subtableOffset;
            var valueFormat = (ValueFormat) reader.ReadUInt16();

            switch (format)
            {
                case 1:
                {
                    var record = reader.ReadValueRecord(valueFormat);
                    var coverage = reader.ReadCoverageTable(coverageOffset);
                    return new SingleAdjustmentPositioningSubTable(coverage, record);
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

                    return new SingleAdjustmentPositioningSubTable(coverage, records);
                }
                default:
                    return new UnImplementedGposLookupSubTable(type, format);
            }
        }

        private static GPOSLookupSubTable ReadPairAdjustmentPositioningSubTable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var format = reader.ReadUInt16();
            switch (format)
            {
                case 1:
                {
                    var coverageOffset = reader.ReadUInt16();
                    var value1Format = (ValueFormat)reader.ReadUInt16(); // can be null
                    var value2Format = (ValueFormat)reader.ReadUInt16(); // can be null
                    var pairSetCount = reader.ReadUInt16();
                    var pairSetOffsetArray = reader.ReadUInt16Array(pairSetCount);
                    var pairSetTables = new PairSetTable[pairSetCount];
                    var subtable = new PairAdjustmentPositioningSubTableFormat1();

                    for (int i = 0; i < pairSetCount; ++i)
                    {
                        reader.Position = subtableOffset + pairSetOffsetArray[i]; 
                        var pairCount = reader.ReadUInt16();
                        var pairSets = new PairSet[pairCount];
                        for (int j = 0; j < pairCount; j++)
                        {
                            pairSets[j] = reader.ReadPairSet(value1Format, value2Format);
                        }

                        subtable.CoverageTable = reader.ReadCoverageTable(coverageOffset + subtableOffset);
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
                            var value2 = reader.ReadValueRecord(value2Format);
                            class2Records[j] = new Class2Record() {Value1 = value1, Value2 = value2};
                        }

                        class1Records[i] = new Class1Record() {Class2Records = class2Records};
                    }
                    
                    var subtable = new PairAdjustmentPositioningSubTableFormat2();
                    subtable.Class1Records = class1Records;
                    subtable.ClassDef1 = reader.ReadClassDefTable(classDef1Offset);
                    subtable.ClassDef2 = reader.ReadClassDefTable(classDef2Offset);
                    subtable.CoverageTable = reader.ReadCoverageTable(coverageOffset);
                    return subtable;
                }
                default:
                    return new UnImplementedGposLookupSubTable(type, format);
            }
        }

        private static GPOSLookupSubTable ReadCursiveAttachmentPositioningSubTable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var lookup = new CursiveAttachmentPositioningSubTable();
            var format = reader.ReadUInt16();

            if (format != 1)
            {
                return new UnImplementedGposLookupSubTable(type, format);
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
        
        private static GPOSLookupSubTable ReadMarkToBaseAttachmentPositioningSubTable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();

            if (format != 1)
            {
                return new UnImplementedGposLookupSubTable(type, format);
            }

            var markCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var baseCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var markClassCount = reader.ReadUInt16();
            var markArrayOffset = reader.ReadUInt16() + subtableOffset;
            var baseArrayOffset = reader.ReadUInt16() + subtableOffset;

            var lookupType4 = new MarkToBaseAttachmentPositioningSubTable();
            lookupType4.MarkCoverage = reader.ReadCoverageTable(markCoverageOffset);
            lookupType4.BaseCoverage = reader.ReadCoverageTable(baseCoverageOffset);
            lookupType4.MarkArrayTable = reader.ReadMarkArrayTable(markArrayOffset);
            lookupType4.BaseArrayTable = reader.ReadBaseArrayTable(baseArrayOffset, markClassCount);

            return lookupType4;
        }
        
        private static GPOSLookupSubTable ReadMarkToLigatureAttachmentPositioningSubTable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            if (format != 1)
            {
                return new UnImplementedGposLookupSubTable(type, format);
            }
            
            var markCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var markClassCount = reader.ReadUInt16();
            var markArrayOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureArrayOffset = reader.ReadUInt16() + subtableOffset;
            
            var lookupType5 = new MarkToLigatureAttachmentPositioningSubTable();
            lookupType5.MarkCoverage = reader.ReadCoverageTable(markArrayOffset);
            lookupType5.LigatureCoverage = reader.ReadCoverageTable(ligatureCoverageOffset);
            lookupType5.MarkArrayTable = reader.ReadMarkArrayTable(markArrayOffset);
            lookupType5.LigatureArrayTable = reader.ReadLigatureArrayTable(ligatureArrayOffset, markClassCount);

            return lookupType5;
        }
        
        private static GPOSLookupSubTable ReadMarkToMarkAttachmentPositioningSubtable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();

            if (format != 1)
            {
                return new UnImplementedGposLookupSubTable(type, format);
            }

            var markCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureCoverageOffset = reader.ReadUInt16() + subtableOffset;
            var classCount = reader.ReadUInt16();
            var markArrayOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureArrayOffset = reader.ReadUInt16() + subtableOffset;

            var lookupSubTable = new MarkToMarkAttachmentPositioningSubtable();
            lookupSubTable.Mark1Coverage = reader.ReadCoverageTable(markCoverageOffset);
            lookupSubTable.Mark2Coverage = reader.ReadCoverageTable(ligatureCoverageOffset);
            lookupSubTable.Mark1ArrayTable = reader.ReadMarkArrayTable(markArrayOffset);
            lookupSubTable.Mark2ArrayTable = reader.ReadMark2ArrayTable(ligatureArrayOffset, classCount);

            return lookupSubTable;
        }
        
        private static GPOSLookupSubTable ReadContextualPositioningSubtable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();

            switch (format)
            {
                case 1:
                {
                    var lookupFormat1 = new ContextualPositioningSubtableFormat1();
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
                    var lookupFormat2 = new ContextualPositioningSubtableFormat2();
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

                    var lookupFormat3 = new ContextualPositioningSubtableFormat3();
                    lookupFormat3.GlyphCount = glyphCount;
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
                    return new UnImplementedGposLookupSubTable(type, format);
            }
        }
        
        private static GPOSLookupSubTable ReadChainedContextsPositioningSubtable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            switch (format)
            {
                case 1:
                {
                    var lookupFormat1 = new ChainedContextsPositioningSubtableFormat1();
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
                    var lookupFormat2 = new ChainedContextsPositioningSubtableFormat2();
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
                    var lookupFormat3 = new ChainedContextsPositioningSubtableFormat3();
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
                    return new UnImplementedGposLookupSubTable(type, format);
            }
        }
        
        private static GPOSLookupSubTable ReadExtensionPositioningSubTable(this FontStreamReader reader, GPOSLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var format = reader.ReadUInt16();
            var extensionLookupType = (GPOSLookupType)reader.ReadUInt16();
            var extensionOffset = reader.ReadUInt32();
            return reader.ReadGPOSLookupSubtable(extensionLookupType, extensionOffset + subtableOffset);
        }

        private static GPOSLookupSubTable ReadGPOSLookupSubtable(this FontStreamReader reader, GPOSLookupType lookupType, long subtableOffset)
        {
            switch (lookupType)
            {
                case GPOSLookupType.SingleAdjustment: return reader.ReadSingleAdjustmentPositioningSubTable(lookupType, subtableOffset);
                case GPOSLookupType.PairAdjustment: return reader.ReadPairAdjustmentPositioningSubTable(lookupType, subtableOffset);
                case GPOSLookupType.CursiveAttachment: return reader.ReadCursiveAttachmentPositioningSubTable(lookupType, subtableOffset);
                case GPOSLookupType.MarkToBaseAttachment: return reader.ReadMarkToBaseAttachmentPositioningSubTable(lookupType, subtableOffset);
                case GPOSLookupType.MarkToLigatureAttachment: return reader.ReadMarkToLigatureAttachmentPositioningSubTable(lookupType, subtableOffset);
                case GPOSLookupType.MarkToMarkAttachment: return reader.ReadMarkToMarkAttachmentPositioningSubtable(lookupType, subtableOffset);
                case GPOSLookupType.ContextPositioning: return reader.ReadContextualPositioningSubtable(lookupType, subtableOffset);
                case GPOSLookupType.ChainedContextPositioning: return reader.ReadChainedContextsPositioningSubtable(lookupType, subtableOffset);
                case GPOSLookupType.ExtensionPositioning: return reader.ReadExtensionPositioningSubTable(lookupType, subtableOffset);
                default: throw new NotSupportedException();
            }
        }
    }
}