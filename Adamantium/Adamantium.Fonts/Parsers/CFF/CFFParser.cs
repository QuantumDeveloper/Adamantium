using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class CFFParser : ICFFParser
    {
        private FontStreamReader otfTtfReader;
        private long cffOffset;

        private CFFFontSet fontSet;
        
        // TABLES
        private CFFHeader cffHeader;
        private CFFIndex cffNameIndex;
        private CFFIndex cffTopDictIndex;
        private CFFIndex cffStringIndex;

        public CFFParser(long cffOffset, FontStreamReader ttfReader)
        {
            this.cffOffset = cffOffset;
            otfTtfReader = ttfReader;
            fontSet = new CFFFontSet();
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
            otfTtfReader.Position = cffOffset;

            cffHeader.Major = otfTtfReader.ReadByte();
            cffHeader.Minor = otfTtfReader.ReadByte();
            cffHeader.HeaderSize = otfTtfReader.ReadByte();
            cffHeader.OffsetSize = otfTtfReader.ReadByte();

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
            otfTtfReader.Position = cffOffset;
            otfTtfReader.Position += cffHeader.HeaderSize;

            cffNameIndex = otfTtfReader.ReadCffIndex();

            for (int i = 0; i < cffNameIndex.Count; ++i)
            {
                var font = new CFFFont(fontSet, CFFVersion.CFF);
                var name = Encoding.UTF8.GetString(cffNameIndex.DataByOffset[i]);
                font.Name = name;
                fontSet.AddFont(font);
            }
        }

        private void ReadTopDictIndex()
        {
            // set position to the start of Top Dict Index (it is start of CFF data + size of CFF header + data in Name Index)
            otfTtfReader.Position = cffOffset;
            otfTtfReader.Position += cffHeader.HeaderSize;
            otfTtfReader.Position += cffNameIndex.GetLength();

            cffTopDictIndex = otfTtfReader.ReadCffIndex();

            for (int i = 0; i < cffTopDictIndex.Count; ++i)
            {
                fontSet.Fonts[i].ParseTopDict(cffTopDictIndex.DataByOffset[i]);
            }
        }

        private void ReadStringIndex()
        {
            // set position to the start of String Dict Index (it is start of CFF data + size of CFF header + data in Name Index + data in Top Dict Index)
            otfTtfReader.Position = cffOffset;
            otfTtfReader.Position += cffHeader.HeaderSize;
            otfTtfReader.Position += cffNameIndex.GetLength();
            otfTtfReader.Position += cffTopDictIndex.GetLength();

            cffStringIndex = otfTtfReader.ReadCffIndex();

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
            otfTtfReader.Position = cffOffset;
            otfTtfReader.Position += cffHeader.HeaderSize;
            otfTtfReader.Position += cffNameIndex.GetLength();
            otfTtfReader.Position += cffTopDictIndex.GetLength();
            otfTtfReader.Position += cffStringIndex.GetLength();

            GlobalSubroutineIndex = otfTtfReader.ReadCffIndex();

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

            otfTtfReader.Position = cffOffset + font.CIDFontInfo.FDSelect;
            otfTtfReader.ReadFDSelect(font, charStringCount);
        }

        private void ReadFDArray(CFFFont font)
        {
            if (font.CIDFontInfo.FDArray == 0) return;

            otfTtfReader.Position = cffOffset + font.CIDFontInfo.FDArray;

            font.CIDFontDicts = otfTtfReader.ReadFDArray(cffOffset, (uint)font.CIDFontInfo.FDArray, font);
        }

        private void ReadLocalSubroutineIndex(CFFFont font)
        {
            // set position to the start of Private DICT data (it is start of CFF data + offset to Private DICT data from Top DICT)
            otfTtfReader.Position = cffOffset;
            var topDictPrivateEntry = font.GetTopDictOperatorValue(DictOperatorsType.Private).AsNumberNumber();
            otfTtfReader.Position += (int) topDictPrivateEntry.Number2;

            // save the beginning of Private DICT data start for later use (e.g for local Subrs offset)
            var privateDictDataStart = otfTtfReader.Position;

            List<byte> privateDictRawData = new List<byte>();

            // read the whole Private DICT data
            privateDictRawData.AddRange(otfTtfReader.ReadBytes((int) topDictPrivateEntry.Number1));
            privateDictRawData.Reverse();

            font.ParsePrivateDict(privateDictRawData.ToArray());

            if (!font.IsLocalSubroutineAvailable)
                return; // there is no local subroutines for this font

            // get the offset to local subroutines
            var localSubrOffset = font.GetPrivateDictOperatorValue(DictOperatorsType.Subrs).AsInt();

            // fill local subrs index structure
            otfTtfReader.Position = privateDictDataStart + localSubrOffset;

            font.LocalSubroutineIndex = otfTtfReader.ReadCffIndex();

            font.LocalSubrBias = this.CalculateSubrBias(font.LocalSubroutineIndex.Count);
        }

        private ushort ReadCharStringIndexCount(CFFFont font)
        {
            otfTtfReader.Position = cffOffset;
            otfTtfReader.Position += font.GetTopDictOperatorValue(DictOperatorsType.CharStrings).AsInt();
            var count = otfTtfReader.ReadUInt16();

            return count;
        }

        private void ReadCharStringsIndex(CFFFont font)
        {
            // set position to the start of CharStrings (it is start of CFF data + offset to CharStrings from Top DICT)
            otfTtfReader.Position = cffOffset;
            otfTtfReader.Position += font.GetTopDictOperatorValue(DictOperatorsType.CharStrings).AsInt();
            
            font.CharStringsIndex = otfTtfReader.ReadCffIndex();

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
                    
                    var commandList = new CommandParser(this).Parse(font, mainStack, fontDict, index: i);

                    var glyph = Glyph.Create((uint) i).SetCommands(commandList).FillOutlines().RecalculateBounds();

                    glyphs.Add(glyph);
                    
                }
                catch (Exception e)
                {
                    glyphs.Add(new Glyph((uint)i){ OutlineType = OutlineType.CompactFontFormat, IsInvalid = true });
                    exceptions++;
                }
            }

            font.SetGlyphs(glyphs.ToArray());
        }

        private void ReadCharsets(CFFFont font)
        {
            otfTtfReader.Position = cffOffset;
            otfTtfReader.Position += font.GetTopDictOperatorValue(DictOperatorsType.charset).AsInt();

            font.GetGlyphByIndex(0).Name = fontSet.GetStringBySid(0);
            var format = otfTtfReader.ReadByte();
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
                glyph.SID = otfTtfReader.ReadUInt16();
                glyph.Name = fontSet.GetStringBySid(glyph.SID);
            }
        }
        
        private void ReadCharsetFormat1Or2(CFFFont font, byte format)
        {
            for (var index = 1; index < font.Glyphs.Count; index++)
            {
                uint sid = otfTtfReader.ReadUInt16();
                int count = 0;
                if (format == 1)
                {
                    count = otfTtfReader.ReadByte() + 1; // +1 because first glyph is not included
                }
                else
                {
                    count = otfTtfReader.ReadUInt16() + 1;
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