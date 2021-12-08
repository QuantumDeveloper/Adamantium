using System;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Extensions
{
    internal static partial class FontStreamExtensions
    {
        const byte OneMoreByteCode1    = 255;
        const byte OneMoreByteCode2    = 254;
        const byte WordCode            = 253;
        const byte LowestUCode         = 253;
        
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