using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Adamantium.Fonts.OTF
{
    internal class CFFParser : ICFFParser
    {
        private OTFStreamReader otfReader;
        private uint cffOffset;

        private CFFFontSet fontSet;
        
        // TABLES
        private CFFHeader cffHeader;
        private CFFIndex cffNameIndex;
        private CFFIndex cffTopDictIndex;
        private CFFIndex cffStringIndex;

        // Pipeline for constructing character
        PipelineAssembler pipelineAssembler;
        
        public CFFParser(uint cffOffset, OTFStreamReader reader)
        {
            this.cffOffset = cffOffset;
            otfReader = reader;
            fontSet = new CFFFontSet();
            pipelineAssembler = new PipelineAssembler(this);
        }

        public CFFIndex GlobalSubroutineIndex { get; private set; }
        public int GlobalSubrBias { get; private set; }
        CFFFont ICFFParser.Parse()
        {
            ReadCFFHeader();
            ReadNameIndex();
            ReadTopDictIndex();
            ReadStringIndex();
            ReadGlobalSubroutineIndex();
            foreach (var font in fontSet.Fonts)
            {
                ReadFDSelect(font);
                ReadFDArray(font);
                ReadLocalSubroutineIndex(font);
                ReadCharStringsIndex(font);
                ReadCharsets(font);
            }

            if (fontSet.Fonts.Count > 1)
            {
                Debug.WriteLine($"Warning! File has more than one font");
            }

            return fontSet.Fonts.FirstOrDefault();
        }

        
        private void ReadCFFHeader()
        {
            cffHeader = new CFFHeader();
            otfReader.Position = cffOffset;

            cffHeader.Major = otfReader.ReadByte();
            cffHeader.Minor = otfReader.ReadByte();
            cffHeader.HeaderSize = otfReader.ReadByte();
            cffHeader.OffsetSize = otfReader.ReadByte();

            // if there are 4 'magic' bytes - fill header size as 4 bytes
            if (cffHeader.Major == 0x5F &&
                cffHeader.Minor == 0x0F &&
                cffHeader.HeaderSize == 0x3C &&
                cffHeader.OffsetSize == 0xF5)
            {
                cffHeader.HeaderSize = 4;
            }
        }

        private void ReadNameIndex()
        {
            // set position to the start of Name Index (it is start of CFF data + size of CFF header)
            otfReader.Position = cffOffset;
            otfReader.Position += cffHeader.HeaderSize;

            cffNameIndex = otfReader.ReadCffIndex();

            for (int i = 0; i < cffNameIndex.Count; ++i)
            {
                var font = new CFFFont(fontSet);
                var name = Encoding.UTF8.GetString(cffNameIndex.DataByOffset[i]);
                font.Name = name;
                fontSet.AddFont(font);
            }
        }

        private void ReadTopDictIndex()
        {
            // set position to the start of Top Dict Index (it is start of CFF data + size of CFF header + data in Name Index)
            otfReader.Position = cffOffset;
            otfReader.Position += cffHeader.HeaderSize;
            otfReader.Position += cffNameIndex.GetLength();

            cffTopDictIndex = otfReader.ReadCffIndex();

            if (cffTopDictIndex.Count > 1)
            {
                int x = 0;
            }

            for (int i = 0; i < cffTopDictIndex.Count; ++i)
            {
                fontSet.Fonts[i].ParseTopDict(cffTopDictIndex.DataByOffset[i]);
            }
        }

        private void ReadStringIndex()
        {
            // set position to the start of String Dict Index (it is start of CFF data + size of CFF header + data in Name Index + data in Top Dict Index)
            otfReader.Position = cffOffset;
            otfReader.Position += cffHeader.HeaderSize;
            otfReader.Position += cffNameIndex.GetLength();
            otfReader.Position += cffTopDictIndex.GetLength();

            cffStringIndex = otfReader.ReadCffIndex();

            var uniqueStrings = new List<string>();

            for (int i = 0; i < cffStringIndex.DataByOffset.Count; ++i)
            {
                uniqueStrings.Add(Encoding.UTF8.GetString(cffStringIndex.DataByOffset[i]));
            }

            fontSet.SetUniqueStrings(uniqueStrings);
        }

        private void ReadGlobalSubroutineIndex()
        {
            // set position to the start of String Dict Index (it is start of CFF data + size of CFF header + data in Name Index + data in Top Dict Index + data in String Index)
            otfReader.Position = cffOffset;
            otfReader.Position += cffHeader.HeaderSize;
            otfReader.Position += cffNameIndex.GetLength();
            otfReader.Position += cffTopDictIndex.GetLength();
            otfReader.Position += cffStringIndex.GetLength();

            GlobalSubroutineIndex = otfReader.ReadCffIndex();

            GlobalSubrBias = this.CalculateSubrBias(GlobalSubroutineIndex.Count);
        }

        /// <summary>
        /// The FDSelect associates an FD(Font DICT) with a glyph by
        /// specifying an FD index for that glyph. The FD index is used to
        /// access one of the Font DICTs stored in the Font DICT INDEX.
        /// </summary>
        /// <param name="font"></param>
        private void ReadFDSelect(CFFFont font)
        {
            if (font.CIDFontInfo.FDSelect == 0) return;

            var charStringCount = ReadCharStringIndexCount(font);

            otfReader.Position = cffOffset + font.CIDFontInfo.FDSelect;
            byte format = otfReader.ReadByte();
            font.CIDFontInfo.FdSelectFormat = format;
            switch (format)
            {
                case 0:
                    font.CIDFontInfo.FdRanges0 = otfReader.ReadBytes(charStringCount);
                    break;
                case 3:
                    var rangesCount = otfReader.ReadUInt16();
                    font.CIDFontInfo.FdRanges3 = new FDRange3[rangesCount+1];
                    for (int i = 0; i < rangesCount; i++)
                    {
                        var range = new FDRange3(otfReader.ReadUInt16(), otfReader.ReadByte());
                        font.CIDFontInfo.FdRanges3[i] = range;
                    }
                    
                    // sentinel
                    font.CIDFontInfo.FdRanges3[rangesCount] = new FDRange3(otfReader.ReadUInt16(), 0);
                    break;
                default:
                    throw new NotSupportedException($"Format {format} is not supported in FDSelect");
            }
        }

        private void ReadFDArray(CFFFont font)
        {
            if (font.CIDFontInfo.FDArray == 0) return;

            otfReader.Position = cffOffset + font.CIDFontInfo.FDArray;

            font.CIDFontDicts = otfReader.ReadFDArray(cffOffset, (uint)font.CIDFontInfo.FDArray);
        }

        private void ReadLocalSubroutineIndex(CFFFont font)
        {
            // set position to the start of Private DICT data (it is start of CFF data + offset to Private DICT data from Top DICT)
            otfReader.Position = cffOffset;
            var topDictPrivateEntry = font.GetDictOperatorValue(DictOperatorsType.Private).AsNumberNumber();
            otfReader.Position += (int) topDictPrivateEntry.Number2;

            // save the beginning of Private DICT data start for later use (e.g for local Subrs offset)
            var privateDictDataStart = otfReader.Position;

            List<byte> privateDictRawData = new List<byte>();

            // read the whole Private DICT data
            privateDictRawData.AddRange(otfReader.ReadBytes((int) topDictPrivateEntry.Number1));
            privateDictRawData.Reverse();

            font.ParsePrivateDict(privateDictRawData.ToArray());

            if (!font.IsLocalSubroutineAvailable)
                return; // there is no local subroutines for this font

            // get the offset to local subroutines
            var localSubrOffset = font.GetDictOperatorValue(DictOperatorsType.Subrs).AsInt();

            // fill local subrs index structure
            otfReader.Position = privateDictDataStart + localSubrOffset;

            font.LocalSubroutineIndex = otfReader.ReadCffIndex();

            font.LocalSubrBias = this.CalculateSubrBias(font.LocalSubroutineIndex.Count);
        }

        private ushort ReadCharStringIndexCount(CFFFont font)
        {
            otfReader.Position = cffOffset;
            otfReader.Position += font.GetDictOperatorValue(DictOperatorsType.CharStrings).AsInt();
            var count = otfReader.ReadUInt16();

            return count;
        }

        private void ReadCharStringsIndex(CFFFont font)
        {
            // set position to the start of CharStrings (it is start of CFF data + offset to CharStrings from Top DICT)
            otfReader.Position = cffOffset;
            otfReader.Position += font.GetDictOperatorValue(DictOperatorsType.CharStrings).AsInt();
            
            font.CharStringsIndex = otfReader.ReadCffIndex();

            var mainStack = new Stack<byte>();
            int exceptions = 0;
            var glyphs = new List<Glyph>();

            var fdArraySelector = new FontDictArraySelector(font.CIDFontInfo);

            // STEP 0. After filling the Index struct traverse the raw data array (ALL characters are here currently)

            for (var i = 0; i < font.CharStringsIndex.DataByOffset.Count; ++i)
            {
                // STEP 1. Take offsets one by one and fill another byte array - this time it is only bytes relative to the current character

                var data = font.CharStringsIndex.DataByOffset[i];
                for (int j = data.Length; j >= 1; --j)
                {
                    mainStack.Push(data[j - 1]);
                }

                // STEP 3. Use fluent approach
                // Byte Array --> Command List --> Outlines --> Bezier descretion
                // Glyph g = CommandList(mainStack).OutlineList().BezierSampling(int sampleRate);
                // g.charcode = 0;
                // g.encoding = encode;
                // VertexBuf vb = g.Triangulate();

                //List<Glyph> ...
                try
                {
                    FontDict fontDict = null;
                    if (font.IsCIDFont)
                    {
                        var fdArrayIndex = fdArraySelector.SelectFontDictArray((uint) i);
                        fontDict = font.CIDFontDicts[fdArrayIndex];
                    }
                    
                    Glyph glyph = pipelineAssembler
                        .CreateGlyph((uint) i)
                        .FillCommandList(font, mainStack, fontDict, index: i)
                        .FillOutlines()
                        .PrepareSegments()
                        .GetGlyph();
                    glyphs.Add(glyph);
                    
                }
                catch (Exception e)
                {
                    exceptions++;
                }
            }

            font.SetGlyphs(glyphs.ToArray());
        }

        private void ReadCharsets(CFFFont font)
        {
            otfReader.Position = cffOffset;
            otfReader.Position += font.GetDictOperatorValue(DictOperatorsType.charset).AsInt();

            font.GetGlyphByIndex(0).Name = fontSet.GetStringBySid(0);
            var format = otfReader.ReadByte();
            switch (format)
            {
                case 0:
                    ReadCharsetFormat0(font);
                    break;
                case 1:
                case 2:
                    ReadCharsetFormat1Or2(font, format);
                    break;
            }
        }

        private void ReadCharsetFormat0(CFFFont font)
        {
            for (var index = 1; index < font.Glyphs.Count; index++)
            {
                var glyph = font.GetGlyphByIndex((uint)index);
                glyph.SID = otfReader.ReadUInt16();
                glyph.Name = fontSet.GetStringBySid(glyph.SID);
            }
        }
        
        private void ReadCharsetFormat1Or2(CFFFont font, byte format)
        {
            for (var index = 1; index < font.Glyphs.Count; index++)
            {
                uint sid = otfReader.ReadUInt16();
                int count = 0;
                if (format == 1)
                {
                    count = otfReader.ReadByte() + 1; // +1 because first glyph is not included
                }
                else
                {
                    count = otfReader.ReadUInt16() + 1;
                }

                do
                {
                    var glyph = font.GetGlyphByIndex((uint)index);
                    glyph.SID = sid;
                    glyph.Name = fontSet.GetStringBySid(glyph.SID);

                    sid++;
                    index++;
                    count--;

                } while (count > 0);
            }
        }
    }
}