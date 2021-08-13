using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.GPOS;
using Adamantium.Fonts.Tables.GSUB;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Extensions
{
    internal static partial class FontStreamExtensions
    {
        public static ILookupTable[] ReadGSUBLookupListTable(this FontStreamReader reader, long offset)
        {
            reader.Position = offset;
            var lookupCount = reader.ReadUInt16();
            var lookupOffsets = reader.ReadUInt16Array(lookupCount);

            var lookupList = new List<ILookupTable>();

            for (int i = 0; i < lookupCount; i++)
            {
                long lookupTableOffset = offset + lookupOffsets[i];
                var lookup = reader.ReadGSUBLookupTable(lookupTableOffset);
                lookupList.Add(lookup);
            }

            return lookupList.ToArray();
        }
        
        public static ILookupTable ReadGSUBLookupTable(this FontStreamReader reader, long fffset)
        {
            reader.Position = fffset;
            var lookup = new GSUBLookupTable();
            lookup.LookupType = reader.ReadUInt16();
            lookup.LookupFlag = (LookupFlags)reader.ReadUInt16();
            var subtableCount = reader.ReadUInt16();
            var subtableOffsets = reader.ReadUInt16Array(subtableCount);
            if (lookup.LookupFlag == LookupFlags.UseMarkFilteringSet)
            {
                lookup.MarkFilteringSet = reader.ReadUInt16();
            }

            lookup.SubTables = new GSUBLookupSubTable[subtableCount];
            for (int i = 0; i < subtableCount; ++i)
            {
                var subtableOffset = subtableOffsets[i] + fffset;
                lookup.SubTables[i] = reader.ReadGSUBLookupSubtable((GSUBLookupType)lookup.LookupType, subtableOffset);
            }

            return lookup;
        }

        private static ILookupSubTable ReadSingleSubstitutionSubTable(this FontStreamReader reader, GSUBLookupType type,
            long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();

            switch (format)
            {
                case 1:
                {
                    var lookupFormat1 = new SingleSubstitutionSubTableFormat1();
                    var coverageOffset = reader.ReadUInt16() + subtableOffset;
                    lookupFormat1.DeltaGlyphId = reader.ReadInt16();
                    lookupFormat1.Coverage = reader.ReadCoverageTable(coverageOffset);
                    return lookupFormat1;
                }
                case 2:
                {
                    var lookupFormat2 = new SingleSubstitutionSubTableFormat2();
                    var coverageOffset = reader.ReadUInt16() + subtableOffset;
                    var glyphCount = reader.ReadUInt16();
                    lookupFormat2.SubstituteGlyphIds = reader.ReadUInt16Array(glyphCount);
                    lookupFormat2.Coverage = reader.ReadCoverageTable(coverageOffset);
                    return lookupFormat2;
                }
                default: return new UnImplementedGSUBLookupSubTable(type, format);
            }
        }
        
        private static ILookupSubTable ReadMultipleSubstitutionSubTable(this FontStreamReader reader, GSUBLookupType type,
            long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            if (format != 1)
            {
                return new UnImplementedGSUBLookupSubTable(type, format);
            }

            var lookupType2 = new MultipleSubstitutionSubTable();
            var coverageOffset = reader.ReadUInt16() + subtableOffset;
            var sequenceCount = reader.ReadUInt16();
            var sequenceOffsets = reader.ReadUInt16Array(sequenceCount);
            lookupType2.SequenceTables = new SequenceTable[sequenceCount];

            for (int i = 0; i < sequenceCount; ++i)
            {
                var offset = sequenceOffsets[i] + subtableOffset;
                lookupType2.SequenceTables[i] = reader.ReadSequenceTable(offset);
            }

            lookupType2.Coverage = reader.ReadCoverageTable(coverageOffset);

            return lookupType2;
        }
        
        private static ILookupSubTable ReadAlternateSubstitutionSubTable(this FontStreamReader reader, GSUBLookupType type,
            long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            if (format != 1)
            {
                return new UnImplementedGSUBLookupSubTable(type, format);
            }

            var lookupType2 = new AlternateSubstitutionSubTable();
            var coverageOffset = reader.ReadUInt16() + subtableOffset;
            var alternateSetCount = reader.ReadUInt16();
            var alternateSetOffsets = reader.ReadUInt16Array(alternateSetCount);
            lookupType2.AlternateSetTables = new AlternateSetTable[alternateSetCount];

            for (int i = 0; i < alternateSetCount; ++i)
            {
                var offset = alternateSetOffsets[i] + subtableOffset;
                lookupType2.AlternateSetTables[i] = reader.ReadAlternateSetTable(offset);
            }

            lookupType2.Coverage = reader.ReadCoverageTable(coverageOffset);

            return lookupType2;
        }
        
        private static ILookupSubTable ReadLigatureSubstitutionSubTable(this FontStreamReader reader, GSUBLookupType type,
            long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            if (format != 1)
            {
                return new UnImplementedGSUBLookupSubTable(type, format);
            }

            var lookupType2 = new LigatureSubstitutionSubTable();
            var coverageOffset = reader.ReadUInt16() + subtableOffset;
            var ligatureSetCount = reader.ReadUInt16();
            var ligatureSetOffsets = reader.ReadUInt16Array(ligatureSetCount);
            lookupType2.LigatureSetTables = new LigatureSetTable[ligatureSetCount];

            for (int i = 0; i < ligatureSetCount; ++i)
            {
                var offset = ligatureSetOffsets[i] + subtableOffset;
                lookupType2.LigatureSetTables[i] = reader.ReadLigatureSetTable(offset);
            }

            lookupType2.Coverage = reader.ReadCoverageTable(coverageOffset);

            return lookupType2;
        }

        private static ILookupSubTable ReadContextualSubstitutionSubTable(this FontStreamReader reader,
            GSUBLookupType type,
            long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            switch (format)
            {
                case 1:
                {
                    var lookupFormat1 = new ContextualSubstitutionSubTableFormat1();
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
                    var lookupFormat2 = new ContextualSubstitutionSubTableFormat2();
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

                    var lookupFormat3 = new ContextualSubstitutionSubTableFormat3();
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
                    return new UnImplementedGSUBLookupSubTable(type, format);
            }
        }

        private static ILookupSubTable ReadChainedContextsSubstitutionSubTable(this FontStreamReader reader,
            GSUBLookupType type,
            long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            switch (format)
            {
                case 1:
                {
                    var lookupFormat1 = new ChainedContextsSubstitutionSubTableFormat1();
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
                    var lookupFormat2 = new ChainedContextsSubstitutionSubTableFormat2();
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
                    var lookupFormat3 = new ChainedContextsSubstitutionSubTableFormat3();
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
                    return new UnImplementedGSUBLookupSubTable(type, format);
            }
        }

        private static ILookupSubTable ReadReverseChainingContextualSubstitutionSubTable(this FontStreamReader reader,
            GSUBLookupType type,
            long subtableOffset)
        {
            reader.Position = subtableOffset;

            var format = reader.ReadUInt16();
            if (format != 1)
            {
                return new UnImplementedGSUBLookupSubTable(type, format);
            }

            var reverseContext = new ReverseChainingContextualSubstitutionFormat1();
            
            var coverageOffset = reader.ReadUInt16() + subtableOffset;
            var backtrackGlyphCount = reader.ReadUInt16();
            var backtrackCoverageOffsets = reader.ReadUInt16Array(backtrackGlyphCount);
            var lookaheadGlyphCount = reader.ReadUInt16();
            var lookaheadCoverageOffsets = reader.ReadUInt16Array(lookaheadGlyphCount);
            var glyphCount = reader.ReadUInt16();
            reverseContext.SubstituteGlyphIDs = reader.ReadUInt16Array(glyphCount);

            reverseContext.Coverage = reader.ReadCoverageTable(coverageOffset);
            
            reverseContext.BacktrackCoverages = new CoverageTable[backtrackGlyphCount];
            for (int i = 0; i < backtrackGlyphCount; ++i)
            {
                var offset = backtrackCoverageOffsets[i] + subtableOffset;
                reverseContext.BacktrackCoverages[i] = reader.ReadCoverageTable(offset);
            }
            
            reverseContext.LookaheadCoverages = new CoverageTable[lookaheadGlyphCount];
            for (int i = 0; i < lookaheadGlyphCount; ++i)
            {
                var offset = lookaheadCoverageOffsets[i] + subtableOffset;
                reverseContext.LookaheadCoverages[i] = reader.ReadCoverageTable(offset);
            }

            return reverseContext;
        }
        
        private static ILookupSubTable ReadExtensionSubstitutionSubTable(this FontStreamReader reader, GSUBLookupType type, long subtableOffset)
        {
            reader.Position = subtableOffset;
            
            var format = reader.ReadUInt16();
            var extensionLookupType = (GSUBLookupType)reader.ReadUInt16();
            var extensionOffset = reader.ReadUInt32();
            return reader.ReadGSUBLookupSubtable(extensionLookupType, extensionOffset + subtableOffset);
        }

        private static ILookupSubTable ReadGSUBLookupSubtable(this FontStreamReader reader, GSUBLookupType type, long subtableOffset)
        {
            switch (type)
            {
                case GSUBLookupType.Single: return reader.ReadSingleSubstitutionSubTable(type, subtableOffset);
                case GSUBLookupType.Multiple: return reader.ReadMultipleSubstitutionSubTable(type, subtableOffset);
                case GSUBLookupType.Alternate: return reader.ReadAlternateSubstitutionSubTable(type, subtableOffset);
                case GSUBLookupType.Ligature: return reader.ReadLigatureSubstitutionSubTable(type, subtableOffset);
                case GSUBLookupType.Context: return reader.ReadContextualSubstitutionSubTable(type, subtableOffset);
                case GSUBLookupType.ChainingContext: return reader.ReadChainedContextsSubstitutionSubTable(type, subtableOffset);
                case GSUBLookupType.ReverseChainingContextSingle: return reader.ReadReverseChainingContextualSubstitutionSubTable(type, subtableOffset);
                case GSUBLookupType.ExtensionSubstitution: return reader.ReadExtensionSubstitutionSubTable(type, subtableOffset);
                default: throw new NotSupportedException();
            }
        }
    }
}