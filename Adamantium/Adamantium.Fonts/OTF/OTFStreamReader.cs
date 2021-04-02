using System;
using System.Linq;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.OTF
{
    internal unsafe class OTFStreamReader: StreamReader
    {
        public OTFStreamReader(byte* ptr, long length) : base(ptr, length)
        {
        }

        public CharMapFormat0 ReadCharacterMapFormat0()
        {
            var format0 = new CharMapFormat0();
            format0.Length = ReadUInt16();
            format0.Language = ReadUInt16();
            format0.GlyphIdArray = ReadBytes(256);
            return format0;
        }
        
        public CharMapFormat2 ReadCharacterMapFormat2()
        {
            var format2 = new CharMapFormat2();
            format2.Length = ReadUInt16();
            format2.Language = ReadUInt16();
            for (int i = 0; i < format2.Length; i++)
            {
                format2.GlyphIdArray[i] = ReadUInt16();
            }
            return format2;
        }
        
        public CharMapFormat4 ReadCharacterMapFormat4()
        {
            var format4 = new CharMapFormat4();
            format4.Length = ReadUInt16();

            var tableEndAt = Position + format4.Length;
            
            format4.Language = ReadUInt16();
            format4.SegCountX2 = ReadUInt16();
            format4.SegCount = (UInt16)(format4.SegCountX2 / 2);
            format4.SearchRange = ReadUInt16();
            format4.EntrySelector = ReadUInt16();
            format4.RangeShift = ReadUInt16();

            format4.EndCode = ReadUInt16Array(format4.SegCount);
            format4.ReservedPad = ReadUInt16();
            format4.StartCode = ReadUInt16Array(format4.SegCount);
            format4.IdDelta = ReadInt16Array(format4.SegCount);
            format4.IdRangeOffsets = ReadUInt16Array(format4.SegCount);
            
            var remainingLength = tableEndAt - Position;
            var remainingEntries = remainingLength / 2;

            format4.GlyphIdArray = ReadUInt16Array((int)remainingEntries);

            return format4;
        }
        
        public CharMapFormat6 ReadCharacterMapFormat6()
        {
            var format6 = new CharMapFormat6();
            format6.Length = ReadUInt16();
            format6.Language = ReadUInt16();
            format6.FirstCode = ReadUInt16();
            format6.EntryCount = ReadUInt16();
            format6.GlyphIdArray = ReadUInt16Array(format6.EntryCount);
            
            return format6;
        }
        
        public CharMapFormat8 ReadCharacterMapFormat8()
        {
            throw new NotSupportedException("Parsing of CMAP format 8 is not supported");
        }
        
        public CharMapFormat10 ReadCharacterMapFormat10()
        {
            throw new NotSupportedException("Parsing of CMAP format 10 is not supported");
        }
        
        public CharMapFormat12 ReadCharacterMapFormat12()
        {
            var format12 = new CharMapFormat12();
            format12.Reserved = ReadUInt16();
            format12.Length = ReadUInt32();
            format12.Language = ReadUInt32();
            format12.NumGroups = ReadUInt32();
            format12.Groups = new SequentialMapGroup[format12.NumGroups];

            for (int i = 0; i < format12.Groups.Length; ++i)
            {
                format12.Groups[i].StartCharCode = ReadUInt32();
                format12.Groups[i].EndCharCode = ReadUInt32();
                format12.Groups[i].StartGlyphId = ReadUInt32();
            }

            format12.StartCharCodes = format12.Groups.Select(x => x.StartCharCode).ToArray();
            format12.EndCharCodes = format12.Groups.Select(x => x.EndCharCode).ToArray();
            format12.StartGlyphIds = format12.Groups.Select(x => x.StartGlyphId).ToArray();
            
            return format12;
        }
        
        public CharMapFormat13 ReadCharacterMapFormat13()
        {
            var format13 = new CharMapFormat13();
            format13.Reserved = ReadUInt16();
            format13.Length = ReadUInt32();
            format13.Language = ReadUInt32();
            format13.NumGroups = ReadUInt32();
            format13.Groups = new ConstantMapGroup[format13.NumGroups];

            for (int i = 0; i < format13.Groups.Length; ++i)
            {
                format13.Groups[i].StartCharCode = ReadUInt32();
                format13.Groups[i].EndCharCode = ReadUInt32();
                format13.Groups[i].GlyphId = ReadUInt32();
            }
            
            return format13;
        }

        public CharMapFormat14 ReadCharacterMapFormat14()
        {
            var startPos = Position - 2;
            var format14 = new CharMapFormat14();
            format14.Length = ReadUInt32();
            format14.NumVarSelectorRecords = ReadUInt32();
            var records = new VariationSelectorRecord[format14.NumVarSelectorRecords];

            for (int i = 0; i < format14.NumVarSelectorRecords; ++i)
            {
                records[i].VarSelector = ReadUint24();
                records[i].DefaultUVSOffset = ReadUInt32();
                records[i].NonDefaultUVSOffset = ReadUInt32();
            }

            for (int i = 0; i < format14.NumVarSelectorRecords; ++i)
            {
                var selector = new VariationSelector();
                var record = records[i];
                if (record.DefaultUVSOffset != 0)
                {
                    Position = startPos + record.DefaultUVSOffset;
                    uint numUnicodeValueRanges = ReadUInt32();
                    for (int x = 0; x < numUnicodeValueRanges; ++x)
                    {
                        var startCode = ReadUint24();
                        selector.DefaultStartCodes.Add(startCode);
                        selector.DefaultEndCodes.Add(startCode + ReadByte());
                    }
                }

                if (record.NonDefaultUVSOffset != 0)
                {
                    Position = startPos + record.NonDefaultUVSOffset;
                    uint numUVSMappings = ReadUInt32();
                    for (int x = 0; x < numUVSMappings; ++x)
                    {
                        var unicodeValue = ReadUint24();
                        var glyphId = ReadUInt16();
                        selector.UVSMappings.Add(unicodeValue, glyphId);
                    }
                }

                format14.VarSelectors.Add(record.VarSelector, selector);
            }

            return format14;
        }

        public UInt16[] ReadUInt16Array(int count)
        {
            var arr = new UInt16[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = ReadUInt16();
            }

            return arr;
        }
        
        public Int16[] ReadInt16Array(int count)
        {
            var arr = new Int16[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = ReadInt16();
            }

            return arr;
        }
    }
}