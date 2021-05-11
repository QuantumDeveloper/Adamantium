using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Extensions
{
    internal static class FontStreamExtensions
    {
        const byte OneMoreByteCode1    = 255;
        const byte OneMoreByteCode2    = 254;
        const byte WordCode            = 253;
        const byte LowestUCode         = 253;
        
        public static FontStreamReader LoadIntoStream(this string file)
        {
            var bytes = File.ReadAllBytes(file);
            return new FontStreamReader(bytes, file);
        }
        
        public static FontStreamReader LoadIntoStream(this byte[] buffer)
        {
            return new(buffer);
        }

        public static CharacterMapFormat0 ReadCharacterMapFormat0(this FontStreamReader reader)
        {
            var format0 = new CharacterMapFormat0
            {
                Length = reader.ReadUInt16(), 
                Language = reader.ReadUInt16(), 
                GlyphIdArray = reader.ReadBytes(256)
            };
            return format0;
        }

        public static CharacterMapFormat2 ReadCharacterMapFormat2(this FontStreamReader reader)
        {
            var format2 = new CharacterMapFormat2();
            format2.Length = reader.ReadUInt16();
            format2.Language = reader.ReadUInt16();
            for (int i = 0; i < format2.Length; i++)
            {
                format2.GlyphIdArray[i] = reader.ReadUInt16();
            }
            return format2;
        }
        
        public static CharacterMapFormat4 ReadCharacterMapFormat4(this FontStreamReader reader)
        {
            var format4 = new CharacterMapFormat4();
            format4.Length = reader.ReadUInt16();

            var tableEndAt = reader.Position + format4.Length;
            
            format4.Language = reader.ReadUInt16();
            format4.SegCountX2 = reader.ReadUInt16();
            format4.SegCount = (UInt16)(format4.SegCountX2 / 2);
            format4.SearchRange = reader.ReadUInt16();
            format4.EntrySelector = reader.ReadUInt16();
            format4.RangeShift = reader.ReadUInt16();

            format4.EndCode = reader.ReadUInt16Array(format4.SegCount);
            format4.ReservedPad = reader.ReadUInt16();
            format4.StartCode = reader.ReadUInt16Array(format4.SegCount);
            format4.IdDelta = reader.ReadInt16Array(format4.SegCount);
            format4.IdRangeOffsets = reader.ReadUInt16Array(format4.SegCount);
            
            var remainingLength = tableEndAt - reader.Position;
            var remainingEntries = remainingLength / 2;

            format4.GlyphIdArray = reader.ReadUInt16Array((int)remainingEntries);

            return format4;
        }
        
        public static CharacterMapFormat6 ReadCharacterMapFormat6(this FontStreamReader reader)
        {
            var format6 = new CharacterMapFormat6
            {
                Length = reader.ReadUInt16(),
                Language = reader.ReadUInt16(),
                FirstCode = reader.ReadUInt16(),
                EntryCount = reader.ReadUInt16()
            };
            format6.GlyphIdArray = reader.ReadUInt16Array(format6.EntryCount);
            
            return format6;
        }
        
        public static CharacterMapFormat8 ReadCharacterMapFormat8(this FontStreamReader reader)
        {
            throw new NotSupportedException("Parsing of CMAP format 8 is not supported");
        }
        
        public static CharacterMapFormat10 ReadCharacterMapFormat10(this FontStreamReader reader)
        {
            throw new NotSupportedException("Parsing of CMAP format 10 is not supported");
        }
        
        public static CharacterMapFormat12 ReadCharacterMapFormat12(this FontStreamReader reader)
        {
            var format12 = new CharacterMapFormat12();
            format12.Reserved = reader.ReadUInt16();
            format12.Length = reader.ReadUInt32();
            format12.Language = reader.ReadUInt32();
            format12.NumGroups = reader.ReadUInt32();
            format12.Groups = new SequentialMapGroup[format12.NumGroups];

            for (int i = 0; i < format12.Groups.Length; ++i)
            {
                format12.Groups[i].StartCharCode = reader.ReadUInt32();
                format12.Groups[i].EndCharCode = reader.ReadUInt32();
                format12.Groups[i].StartGlyphId = reader.ReadUInt32();
            }

            format12.StartCharCodes = format12.Groups.Select(x => x.StartCharCode).ToArray();
            format12.EndCharCodes = format12.Groups.Select(x => x.EndCharCode).ToArray();
            format12.StartGlyphIds = format12.Groups.Select(x => x.StartGlyphId).ToArray();
            
            return format12;
        }
        
        public static CharacterMapFormat13 ReadCharacterMapFormat13(this FontStreamReader reader)
        {
            var format13 = new CharacterMapFormat13();
            format13.Reserved = reader.ReadUInt16();
            format13.Length = reader.ReadUInt32();
            format13.Language = reader.ReadUInt32();
            format13.NumGroups = reader.ReadUInt32();
            format13.Groups = new ConstantMapGroup[format13.NumGroups];

            for (int i = 0; i < format13.Groups.Length; ++i)
            {
                format13.Groups[i].StartCharCode = reader.ReadUInt32();
                format13.Groups[i].EndCharCode = reader.ReadUInt32();
                format13.Groups[i].GlyphId = reader.ReadUInt32();
            }
            
            return format13;
        }

        public static CharacterMapFormat14 ReadCharacterMapFormat14(this FontStreamReader reader)
        {
            var startPos = reader.Position - 2;
            var format14 = new CharacterMapFormat14();
            format14.Length = reader.ReadUInt32();
            format14.NumVarSelectorRecords = reader.ReadUInt32();
            var records = new VariationSelectorRecord[format14.NumVarSelectorRecords];

            for (int i = 0; i < format14.NumVarSelectorRecords; ++i)
            {
                records[i].VarSelector = reader.ReadUint24();
                records[i].DefaultUVSOffset = reader.ReadUInt32();
                records[i].NonDefaultUVSOffset = reader.ReadUInt32();
            }

            for (int i = 0; i < format14.NumVarSelectorRecords; ++i)
            {
                var selector = new VariationSelector();
                var record = records[i];
                if (record.DefaultUVSOffset != 0)
                {
                    reader.Position = startPos + record.DefaultUVSOffset;
                    uint numUnicodeValueRanges = reader.ReadUInt32();
                    for (int x = 0; x < numUnicodeValueRanges; ++x)
                    {
                        var startCode = reader.ReadUint24();
                        selector.DefaultStartCodes.Add(startCode);
                        selector.DefaultEndCodes.Add(startCode + reader.ReadByte());
                    }
                }

                if (record.NonDefaultUVSOffset != 0)
                {
                    reader.Position = startPos + record.NonDefaultUVSOffset;
                    uint numUVSMappings = reader.ReadUInt32();
                    for (int x = 0; x < numUVSMappings; ++x)
                    {
                        var unicodeValue = reader.ReadUint24();
                        var glyphId = reader.ReadUInt16();
                        selector.UVSMappings.Add(unicodeValue, glyphId);
                    }
                }

                format14.VarSelectors.Add(record.VarSelector, selector);
            }

            return format14;
        }

        public static UInt16[] ReadUInt16Array(this FontStreamReader reader, int count)
        {
            var arr = new UInt16[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = reader.ReadUInt16();
            }

            return arr;
        }
        
        public static Int16[] ReadInt16Array(this FontStreamReader reader, int count)
        {
            var arr = new Int16[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = reader.ReadInt16();
            }

            return arr;
        }

        public static bool ReadUIntBase128(this FontStreamReader reader, out UInt32 result)
        {
            // bool ReadUIntBase128( data, *result )
            // {
            //     UInt32 accum = 0;
            //
            //     for (i = 0; i < 5; i++) {
            //         UInt8 data_byte = data.getNextUInt8();
            //
            //         // No leading 0's
            //         if (i == 0 && data_byte = 0x80) return false;
            //
            //         // If any of top 7 bits are set then << 7 would overflow
            //         if (accum & 0xFE000000) return false;
            //
            //         *accum = (accum << 7) | (data_byte & 0x7F);
            //
            //         // Spin until most significant bit of data byte is false
            //         if ((data_byte & 0x80) == 0) {
            //             *result = accum;
            //             return true;
            //         }
            //     }
            //     // UIntBase128 sequence exceeds 5 bytes
            //     return false;
            // }

            UInt32 accum = 0;
            result = 0;

            for (int i = 0; i < 5; ++i)
            {
                byte dataByte = reader.ReadByte();
                
                // No leading 0's
                if (i == 0 && dataByte == 0x80) return false;
                
                // If any of top 7 bits are set then << 7 would overflow
                if ((accum & 0xFE000000) != 0) return false;

                accum = (accum << 7) | (uint)(dataByte & 0x7F);
                
                // Spin until most significant bit of data byte is false
                if ((dataByte & 0x80) == 0)
                {
                    result = accum;
                    return true;
                }
            }
            
            // UIntBase128 sequence exceeds 5 bytes
            return false;
        }

        public static ushort Read255UInt16(this FontStreamReader reader)
        {
            // Read255UShort( data )
            // {
            //     UInt8 code;
            //     UInt16 value, value2;
            //
            //     const oneMoreByteCode1    = 255;
            //     const oneMoreByteCode2    = 254;
            //     const wordCode            = 253;
            //     const lowestUCode         = 253;
            //
            //     code = data.getNextUInt8();
            //     if ( code == wordCode ) {
            //         /* Read two more bytes and concatenate them to form UInt16 value*/
            //         value = data.getNextUInt8();
            //         value <<= 8;
            //         value &= 0xff00;
            //         value2 = data.getNextUInt8();
            //         value |= value2 & 0x00ff;
            //     }
            //     else if ( code == oneMoreByteCode1 ) {
            //         value = data.getNextUInt8();
            //         value = (value + lowestUCode);
            //     }
            //     else if ( code == oneMoreByteCode2 ) {
            //         value = data.getNextUInt8();
            //         value = (value + lowestUCode*2);
            //     }
            //     else {
            //         value = code;
            //     }
            //     return value;
            // }

            byte code = 0;
            UInt16 value;

            code = reader.ReadByte();
            if (code == WordCode)
            {
                /* Read two more bytes and concatenate them to form UInt16 value */
                value = reader.ReadByte();
                value <<= 8;
                value &= 0xff00;
                UInt16 value2 = reader.ReadByte();
                value |= (ushort)(value2 & 0x00ff);
            }
            else if (code == OneMoreByteCode1)
            {
                value = reader.ReadByte();
                value = (ushort)(value + LowestUCode);
            }
            else if (code == OneMoreByteCode2)
            {
                value = reader.ReadByte();
                value = (ushort)(value + LowestUCode * 2);
            }
            else
            {
                value = code;
            }

            return value;
        }
    }
}