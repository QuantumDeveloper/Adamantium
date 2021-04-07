using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.OTF
{
    internal static class CFFParserExtensions
    {
        public static int CalculateSubrBias(this ICFFParser parser,  uint subrCount)
        {
            return subrCount switch
            {
                < 1240 => 107,
                < 33900 => 1131,
                _ => 32768
            };
        }
        
        public static void UnpackSubrToStack(
            this ICFFParser parser,
            byte[] data,
            Stack<byte> mainStack)
        {
            for (int j = data.Length; j >= 1; --j)
            {
                mainStack.Push(data[j - 1]);
            }
        }
        
        public static CFFIndex ReadCffIndex(this OTFStreamReader reader, CFFVersion version = CFFVersion.CFF)
        {
            var cffIndex = new CFFIndex();

            if (version == CFFVersion.CFF)
            {
                cffIndex.Count = reader.ReadUInt16();
            }
            else
            {
                cffIndex.Count = reader.ReadUInt32();
            }

            if (cffIndex.Count == 0)
            {
                return default;
            }

            cffIndex.OffsetSize = reader.ReadByte();

            cffIndex.Offsets = new List<uint>();

            for (int i = 0; i <= cffIndex.Count; ++i)
            {
                switch (cffIndex.OffsetSize)
                {
                    case 1:
                        cffIndex.Offsets.Add(reader.ReadByte());
                        break;
                    case 2:
                        cffIndex.Offsets.Add(reader.ReadUInt16());
                        break;
                    case 3:
                        var rawOffset = reader.ReadBytes(3).ToList();
                        rawOffset.Add(0);
                        cffIndex.Offsets.Add(BitConverter.ToUInt32(rawOffset.ToArray(), 0));
                        break;
                    case 4:
                        cffIndex.Offsets.Add(reader.ReadUInt32());
                        break;
                }
            }

            var data = reader.ReadBytes((int)cffIndex.Offsets.Last() - 1).Reverse().ToArray();
            var dataByOffset = new List<byte[]>();

            for (int i = 1; i < cffIndex.Offsets.Count; ++i)
            {
                var startIndex = cffIndex.Offsets[i - 1] - 1;
                var endIndex = cffIndex.Offsets[i] - 1;
                var bytes = data[(int)startIndex ..(int)endIndex];
                dataByOffset.Add(bytes);
            }

            cffIndex.DataByOffset = dataByOffset;

            return cffIndex;
        }

        public static List<FontDict> ReadFDArray(this OTFStreamReader otfReader, uint cffOffset, uint fdArrayOffset, CFFVersion version = CFFVersion.CFF)
        {
            otfReader.Position = cffOffset + fdArrayOffset;

            var fontDictIndex = otfReader.ReadCffIndex(version);
            var fontDicts = new List<FontDict>();
            
            for (int i = 0; i < fontDictIndex.Count; i++)
            {
                var dictParser = new DictOperandParser(fontDictIndex.DataByOffset[i]);
                var result = dictParser.GetAllAvailableOperands();

                int name = 0;
                int offset = 0;
                int size = 0;

                foreach (var operandResult in result.Results)
                {
                    switch (operandResult.Key)
                    {
                        case DictOperatorsType.FontName:
                            name = operandResult.Value.AsInt();
                            break;
                        case DictOperatorsType.Private: // private DICT
                            var lst = operandResult.Value.AsList();
                            size = (int) lst[0];
                            offset = (int) lst[1];
                            break;
                    }
                }

                var fontDict = new FontDict(size, offset);
                fontDict.FontName = name;
                fontDicts.Add(fontDict);
            }

            foreach (var fontDict in fontDicts)
            {
                otfReader.Position = cffOffset + fontDict.PrivateDictOffset;
                var bytes = otfReader.ReadBytes(fontDict.PrivateDictSize).Reverse().ToArray();
                var dictParser = new DictOperandParser(bytes);
                var result = dictParser.GetAllAvailableOperands();

                foreach (var operandResult in result.Results)
                {
                    switch (operandResult.Key)
                    {
                        case DictOperatorsType.Subrs:
                            var localSubrsOffset = operandResult.Value.AsInt();
                            otfReader.Position = cffOffset + fontDict.PrivateDictOffset + localSubrsOffset;
                            var offsets = otfReader.ReadCffIndex(version);
                            fontDict.LocalSubr = offsets.DataByOffset;
                            break;
                    }
                }
            }

            return fontDicts;
        }
        
        public static void ReadFDSelect(this OTFStreamReader otfReader, CFFFont font, int charStringCount)
        {
            byte format = otfReader.ReadByte();
            font.CIDFontInfo.FdSelectFormat = format;
            switch (format)
            {
                case 0:
                    font.CIDFontInfo.FdRanges0 = otfReader.ReadBytes(charStringCount);
                    break;
                case 3:
                case 4:
                    uint rangesCount = 0;
                    rangesCount = format == 3 ? otfReader.ReadUInt16() : otfReader.ReadUInt32();
                    font.CIDFontInfo.FdRanges = new FDRange[rangesCount+1];
                    for (int i = 0; i < rangesCount; i++)
                    {
                        var range = new FDRange(otfReader.ReadUInt16(), otfReader.ReadByte());
                        font.CIDFontInfo.FdRanges[i] = range;
                    }

                    uint first = format == 3 ? otfReader.ReadUInt16() : otfReader.ReadUInt32();
                    // sentinel
                    font.CIDFontInfo.FdRanges[rangesCount] = new FDRange(first, 0);
                    break;
                default:
                    throw new NotSupportedException($"Format {format} is not supported in FDSelect");
            }
        }
    }
}