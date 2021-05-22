using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Parsers.CFF;

namespace Adamantium.Fonts.Tables.CFF
{
    internal class CFFFont
    {
        private TopDictParser topDictParser;
        private PrivateDictParser privateDictParser;
        private CFFFontSet fontSet;

        private List<Glyph> glyphs;

        private Dictionary<UInt32, Glyph> indexToGlyph;
        private Dictionary<UInt32, Glyph> unicodeToGlyph;
        private Dictionary<string, Glyph> nameToGlyph;
        
        public string Name { get; set; }
        
        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        
        public UInt32 CharstringType { get; set; }
        
        internal List<FontDict> CidFontDicts { get; set; } 
        
        public CFFIndex LocalSubroutineIndex { get; set; }
        public CFFIndex CharStringsIndex { get; set; }
        public Int32 LocalSubrBias { get; internal set; }
        
        public string Version { get; set; }
        public string Notice { get; set; }
        public string CopyRight { get; set; }
        public string FullName { get; set; }        
        public string FamilyName { get; set; }
        public string Weight { get; set; } 
        public double UnderlinePosition { get; set; }
        public double UnderlineThickness { get; set; }
        public double[] FontBBox { get; set; }
        
        public CIDFontInfo CIDFontInfo { get; }
        
        public VariationStore VariationStore { get; set; } 
        
        internal List<FontDict> CIDFontDicts { get; set; }

        public bool IsCIDFont => IsCurrentFontCIDFont();
        
        public bool IsLocalSubroutineAvailable { get; internal set; }
        
        public CFFFont(CFFFontSet fontSet)
        {
            glyphs = new List<Glyph>();
            CIDFontInfo = new CIDFontInfo();
            this.fontSet = fontSet;
        }
        
        public Glyph GetGlyphByName(string name)
        {
            return nameToGlyph.TryGetValue(name, out var glyph) ? glyph : glyphs.FirstOrDefault();
        }

        public Glyph GetGlyphByUnicode(uint unicode)
        {
            return unicodeToGlyph.TryGetValue(unicode, out var glyph) ? glyph : glyphs.FirstOrDefault();
        }

        public Glyph GetGlyphByIndex(uint index)
        {
            return indexToGlyph.TryGetValue(index, out var glyph) ? glyph : glyphs.FirstOrDefault();
        }

        internal void SetGlyphs(Glyph[] glyphs)
        {
            this.glyphs.Clear();
            this.glyphs.AddRange(glyphs);
            indexToGlyph = this.glyphs.ToDictionary(x => x.Index);
            
            //nameToGlyph = this.glyphs.ToDictionary(x => x.Name);
            //unicodeToGlyph.Clear();
            // foreach (var glyph in glyphs)
            // {
            //     foreach (var unicode in glyph.Unicodes)
            //     {
            //         unicodeToGlyph[unicode] = glyph;
            //     }
            // }
        }

        public void ParseTopDict(byte[] data)
        {
            topDictParser = new TopDictParser(data);

            var result = topDictParser.GetAllAvailableOperands();
            foreach (var operandResult in result.Results)
            {
                switch (operandResult.Key)
                {
                    case DictOperatorsType.CharStringType:
                        CharstringType = operandResult.Value.AsUInt();
                        break;
                    case DictOperatorsType.version:
                        Version = fontSet.GetStringBySid(operandResult.Value.AsUInt());
                        break;
                    case DictOperatorsType.Notice:
                        Notice = fontSet.GetStringBySid(operandResult.Value.AsUInt());
                        break;
                    case DictOperatorsType.Copyright:
                        CopyRight = fontSet.GetStringBySid(operandResult.Value.AsUInt());
                        break;
                    case DictOperatorsType.FullName:
                        FullName = fontSet.GetStringBySid(operandResult.Value.AsUInt());
                        break;
                    case DictOperatorsType.FamilyName:
                        FamilyName = fontSet.GetStringBySid(operandResult.Value.AsUInt());
                        break;
                    case DictOperatorsType.Weight:
                        Weight = fontSet.GetStringBySid(operandResult.Value.AsUInt());
                        break;
                    case DictOperatorsType.UnderlinePosition:
                        UnderlinePosition = operandResult.Value.AsDouble();
                        break;
                    case DictOperatorsType.UnderlineThickness:
                        UnderlineThickness = operandResult.Value.AsDouble();
                        break;
                    case DictOperatorsType.FontBBox:
                        FontBBox = operandResult.Value.AsList().ToArray();
                        break;
                    case DictOperatorsType.ROS:
                        CIDFontInfo.ROS_Register = fontSet.GetStringBySid((uint) operandResult.Value.AsList()[0]);
                        CIDFontInfo.ROS_Ordering = fontSet.GetStringBySid((uint) operandResult.Value.AsList()[1]);
                        CIDFontInfo.ROS_Supplement = fontSet.GetStringBySid((uint) operandResult.Value.AsList()[2]);
                        break;
                    case DictOperatorsType.CIDCount:
                        CIDFontInfo.CIDFountCount = operandResult.Value.AsInt();
                        break;
                    case DictOperatorsType.CIDFontVersion:
                        CIDFontInfo.CIDFontVersion = operandResult.Value.AsInt();
                        break;
                    case DictOperatorsType.FDSelect:
                        CIDFontInfo.FDSelect = operandResult.Value.AsInt();
                        break;
                    case DictOperatorsType.FDArray:
                        CIDFontInfo.FDArray = operandResult.Value.AsInt();
                        break;
                }
            }
        }

        public void ParsePrivateDict(byte[] data)
        {
            privateDictParser = new PrivateDictParser(data);

            IsLocalSubroutineAvailable = privateDictParser.IsOperatorAvailable(DictOperatorsType.Subrs);
        }

        public GenericOperandResult GetTopDictOperatorValue(DictOperatorsType opType)
        {
            return topDictParser[opType];
        }
        
        public GenericOperandResult GetPrivateDictOperatorValue(DictOperatorsType opType)
        {
            return privateDictParser[opType];
        }

        private bool IsCurrentFontCIDFont()
        {
            switch (CIDFontInfo.FdSelectFormat)
            {
                case 0 when CIDFontInfo.FdRanges0 != null:
                case 3 when CIDFontInfo.FdRanges != null:
                case 4 when CIDFontInfo.FdRanges != null:
                    return true;
                default:
                    return false;
            }
        }
    }
}