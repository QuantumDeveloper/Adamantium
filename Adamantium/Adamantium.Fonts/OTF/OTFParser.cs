using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Adamantium.Fonts.Common;
using StreamReader = Adamantium.Fonts.Common.StreamReader;

namespace Adamantium.Fonts.OTF
{
    public enum OutlinesType
    {
        TrueType,
        CompactFontFormat // CFF
    }

    public enum CFFVersion
    {
        CFF,
        CFF2
    }

    public class OTFParser
    {
        // OTF font file path (all data in Big-Endian)
        private readonly string filePath;

        // is font collection
        private bool IsFontCollection;

        // OTF byte reader
        private StreamReader otfReader;

        // "tag-offset" mapping
        private Dictionary<string, UInt32> tablesOffsets;

        // list of common mandatory tables
        private static ReadOnlyCollection<string> commonMandatoryTables;

        // list of TrueType outlines mandatory tables
        private static ReadOnlyCollection<string> trueTypeMandatoryTables;

        // list of CFF outlines mandatory tables
        private static Dictionary<string, string> cffMandatoryTables;

        // CFF table version (null if not OTF outlines format)
        private CFFVersion? CFFVersion;

        // Top DICT Parser
        private TopDictParser topDictParser;
        private PrivateDictParser privateDictParser;

        // TABLES
        private OffsetTable offsetTable;
        private CFFHeader cffHeader;
        private CFFIndex cffNameIndex;
        private CFFIndex cffTopDictIndex;
        private CFFIndex cffStringIndex;
        private CFFIndex cffGlobalSubroutineIndex;
        private CFFIndex cffLocalSubroutineIndex;
        private CFFIndex cffCharStringsIndex;

        // Biases for subr indices
        private int globalSubrBias;
        private int localSubrBias;
        private int charstringType;

        // Pipeline for constructing character
        PipelineAssembler pipelineAssembler;

        static OTFParser()
        {
            commonMandatoryTables = new ReadOnlyCollection<string>(new List<string>
            {
                "cmap",
                "head",
                "hhea",
                "hmtx",
                "maxp",
                "name",
                "OS/2",
                "post"
            });

            trueTypeMandatoryTables = new ReadOnlyCollection<string>(new List<string>
            {
                "glyf",
                "loca"
            });

            cffMandatoryTables = new Dictionary<string, string>()
            {
                { "CFF", "CFF " },
                { "CFF2", "CFF2" }
            };
        }

        public OTFParser(string filePath, UInt32 resolution = 0)
        {
            this.filePath = filePath;

            pipelineAssembler = new PipelineAssembler(this);

            //FontData = new Font();

            //bezierResolution = resolution > 0 ? resolution : 1;

            OTFParseFont();
        }

        private unsafe void OTFParseFont()
        {
            var bytes = File.ReadAllBytes(filePath);
            var memoryPtr = Marshal.AllocHGlobal(bytes.Length);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Buffer.MemoryCopy(handle.AddrOfPinnedObject().ToPointer(), memoryPtr.ToPointer(), bytes.Length, bytes.Length);
            handle.Free();
            otfReader = new StreamReader((byte*)memoryPtr, bytes.Length);

            // 1st step - check if this is a single font or collection
            IsOTFCollection();

            // reset stream position
            otfReader.Position = 0;

            // 2nd step - read offset table (if single font)
            ReadOffsetTable();

            // 3rd step - read all table records
            ReadTableRecords();

            if (offsetTable.outlinesType == OutlinesType.CompactFontFormat)
            {
                ParseCFFCommon();
            }
            else
            {
                // call TTF parser here
            }
        }

        private void IsOTFCollection()
        {
            otfReader.Position = 0;
            IsFontCollection = (otfReader.ReadString(4) == "ttcf");
        }

        private void ReadOffsetTable()
        {
            offsetTable = new OffsetTable();

            offsetTable.SfntVersion = otfReader.ReadUInt32();
            offsetTable.numTables = otfReader.ReadUInt16();

            offsetTable.outlinesType = (offsetTable.SfntVersion == 0x00010000 ? OutlinesType.TrueType : OutlinesType.CompactFontFormat);

            // skip other fields
            otfReader.Position += 6;
        }

        private void ReadTableRecords()
        {
            tablesOffsets = new Dictionary<string, uint>();

            for (int i = 0; i < offsetTable.numTables; ++i)
            {
                string tag = otfReader.ReadString(4);

                // skip the checkSum
                otfReader.Position += 4;

                UInt32 offset = otfReader.ReadUInt32();

                // skip length
                otfReader.Position += 4;

                tablesOffsets[tag] = offset;
            }

            CheckMandatoryTables();
        }

        private void CheckMandatoryTables()
        {
            foreach (var table in commonMandatoryTables)
            {
                if (!tablesOffsets.ContainsKey(table))
                {
                    throw new ParserException($"Table {table} is not present in {filePath}");
                }
            }

            switch (offsetTable.outlinesType)
            {
                case OutlinesType.TrueType:
                    foreach (var table in trueTypeMandatoryTables)
                    {
                        if (!tablesOffsets.ContainsKey(table))
                        {
                            throw new ParserException($"Table {table} is not present in {filePath}");
                        }
                    }
                    break;
                case OutlinesType.CompactFontFormat:
                    if (!tablesOffsets.ContainsKey(cffMandatoryTables["CFF"]) && !tablesOffsets.ContainsKey(cffMandatoryTables["CFF2"]))
                    {
                        throw new ParserException($"Table either {cffMandatoryTables["CFF"]} or {cffMandatoryTables["CFF2"]} is not present in {filePath}");
                    }
                    break;
            }
        }

        private void DetermineCFFVersion()
        {
            if (offsetTable.outlinesType == OutlinesType.CompactFontFormat)
            {
                if (tablesOffsets.ContainsKey(cffMandatoryTables["CFF"]))
                {
                    CFFVersion = OTF.CFFVersion.CFF;
                }
                else
                {
                    CFFVersion = OTF.CFFVersion.CFF2;
                }
            }
        }

        private void ParseCFFCommon()
        {
            DetermineCFFVersion();

            if (CFFVersion == OTF.CFFVersion.CFF)
            {
                ParseCFF();
            }
            else if (CFFVersion == OTF.CFFVersion.CFF2)
            {
                ParseCFF2();
            }

        }

        private void ParseCFF()
        {
            ReadCFFHeader();
            ReadNameIndex();
            ReadTopDictIndex();
            ReadStringIndex();
            ReadGlobalSubroutineIndex();
            ReadLocalSubroutineIndex();
            ReadCharStringsIndex();
        }

        private void ParseCFF2()
        {
            ReadCFF2Header();
        }

        private void ReadCFFHeader()
        {
            cffHeader = new CFFHeader();
            otfReader.Position = tablesOffsets[cffMandatoryTables["CFF"]];

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
            otfReader.Position = tablesOffsets[cffMandatoryTables["CFF"]];
            otfReader.Position += cffHeader.HeaderSize;

            FillCffIndex(ref cffNameIndex);

            // for debug
            string Name = Encoding.ASCII.GetString(cffNameIndex.Data);
        }

        private void ReadTopDictIndex()
        {
            // set position to the start of Top Dict Index (it is start of CFF data + size of CFF header + data in Name Index)
            otfReader.Position = tablesOffsets[cffMandatoryTables["CFF"]];
            otfReader.Position += cffHeader.HeaderSize;
            otfReader.Position += GetIndexLength(cffNameIndex);

            FillCffIndex(ref cffTopDictIndex);

            topDictParser = new TopDictParser(cffTopDictIndex.Data);
            
            charstringType = topDictParser.GetOperatorValue(TopDictOperatorsType.CharStringType).AsInt();
        }

        private void ReadStringIndex()
        {
            // set position to the start of String Dict Index (it is start of CFF data + size of CFF header + data in Name Index + data in Top Dict Index)
            otfReader.Position = tablesOffsets[cffMandatoryTables["CFF"]];
            otfReader.Position += cffHeader.HeaderSize;
            otfReader.Position += GetIndexLength(cffNameIndex);
            otfReader.Position += GetIndexLength(cffTopDictIndex);

            FillCffIndex(ref cffStringIndex);

            List<string> parsed = new List<string>();
            List<byte> raw = new List<byte>();

            for (int i = 0; i < cffStringIndex.Offsets.Count - 1; ++i)
            {
                raw.Clear();

                for (uint j = cffStringIndex.Offsets[i]; j < cffStringIndex.Offsets[i + 1]; ++j)
                {
                    raw.Add(cffStringIndex.Data[j - 1]);
                }

                parsed.Add(Encoding.ASCII.GetString(raw.ToArray()));
            }
        }

        private void ReadGlobalSubroutineIndex()
        {
            // set position to the start of String Dict Index (it is start of CFF data + size of CFF header + data in Name Index + data in Top Dict Index + data in String Index)
            otfReader.Position = tablesOffsets[cffMandatoryTables["CFF"]];
            otfReader.Position += cffHeader.HeaderSize;
            otfReader.Position += GetIndexLength(cffNameIndex);
            otfReader.Position += GetIndexLength(cffTopDictIndex);
            otfReader.Position += GetIndexLength(cffStringIndex);

            FillCffIndex(ref cffGlobalSubroutineIndex);

            globalSubrBias = CalculateSubrBias(cffGlobalSubroutineIndex.Count);
        }

        private void ReadLocalSubroutineIndex()
        {
            // set position to the start of Private DICT data (it is start of CFF data + offset to Private DICT data from Top DICT)
            otfReader.Position = tablesOffsets[cffMandatoryTables["CFF"]];
            var topDictPrivateEntry = topDictParser.GetOperatorValue(TopDictOperatorsType.Private).AsNumberNumber();
            otfReader.Position += (int)topDictPrivateEntry.Number2;

            // save the beginning of Private DICT data start for later use (e.g for local Subrs offset)
            var privateDictDataStart = otfReader.Position;

            List<byte> privateDictRawData = new List<byte>();

            // read the whole Private DICT data
            privateDictRawData.AddRange(otfReader.ReadBytes((int)topDictPrivateEntry.Number1));
            privateDictRawData.Reverse();

            privateDictParser = new PrivateDictParser(privateDictRawData.ToArray());

            // get the offset to local subroutines
            var localSubrOffset = privateDictParser.GetOperatorValue(PrivateDictOperatorsType.Subrs).AsInt();

            // fill local subrs index structure
            otfReader.Position = privateDictDataStart + localSubrOffset;

            FillCffIndex(ref cffLocalSubroutineIndex);

            localSubrBias = CalculateSubrBias(cffLocalSubroutineIndex.Count);
        }

        private void ReadCharStringsIndex()
        {
            // set position to the start of CharStrings (it is start of CFF data + offset to CharStrings from Top DICT)
            otfReader.Position = tablesOffsets[cffMandatoryTables["CFF"]];
            otfReader.Position += topDictParser.GetOperatorValue(TopDictOperatorsType.CharStrings).AsInt();
            
            /* var charstringType = topDictParser.GetOperatorValue(TopDictOperatorsType.CharStringType).AsInt();
            System.Diagnostics.Debug.WriteLine($"Char string Type = {charstringType}"); */

            FillCffIndex(ref cffCharStringsIndex);

            var mainStack = new Stack<byte>();
            var glyphs = new List<Glyph>();
            int exceptions = 0;

            // STEP 0. After filling the Index struct traverse the raw data array (ALL characters are here currently)

            for (var i = 1; i < cffCharStringsIndex.Offsets.Count; ++i)
            {
                // STEP 1. Take offsets one by one and fill another byte array - this time it is only bytes relative to the current character
                for (var j = cffCharStringsIndex.Offsets[i] - 1; j >= cffCharStringsIndex.Offsets[i - 1]; --j)
                {
                    mainStack.Push(cffCharStringsIndex.Data[j - 1]);
                }

                /*if (rawBytes[rawBytes.Count - 1] != (ushort)OutlinesOperatorsType.endchar  &&
                    rawBytes[rawBytes.Count - 1] != (ushort)OutlinesOperatorsType.callsubr &&
                    rawBytes[rawBytes.Count - 1] != (ushort)OutlinesOperatorsType.callgsubr)
                {
                    throw new ArgumentException("FINAL COMMAND NOT ENDCHAR || CALLSUBR || CALLGSUBR");
                }*/

                // STEP 3. Use fluent approach
                // Byte Array --> Command List --> Outlines --> Bezier descretion
                // Glyph g = CommandList(mainStack).OutlineList().BezierSampling(int sampleRate);
                // g.charcode = 0;
                // g.encoding = encode;
                // VertexBuf vb = g.Triangulate();

                //List<Glyph> ...
                try
                {
                    if (i == 12)
                    {
                        int x = 0;
                        // foreach (var b in mainStack)
                        // {
                        //     //Debug.WriteLine(b);
                        // }
                        
                    }
                    Glyph glyph = pipelineAssembler.GetCommandList(mainStack, index: i).GetOutlines().Sample(1).Build();
                    glyphs.Add(glyph);
                    Debug.WriteLine($"Glyph {i} added");
                }
                catch (Exception e)
                {
                    exceptions++;
                }
            }
        }

        private void ReadCFF2Header()
        {
            cffHeader = new CFFHeader();
            otfReader.Position = tablesOffsets[cffMandatoryTables["CFF2"]];

            cffHeader.Major = otfReader.ReadByte();
            cffHeader.Minor = otfReader.ReadByte();
            cffHeader.HeaderSize = otfReader.ReadByte();
            cffHeader.TopDictLength = otfReader.ReadUInt16();
        }

        private void FillCffIndex(ref CFFIndex cffIndex)
        {
            cffIndex = new CFFIndex();

            cffIndex.Count = otfReader.ReadUInt16();

            if (cffIndex.Count == 0)
            {
                return;
            }

            cffIndex.OffsetSize = otfReader.ReadByte();

            cffIndex.Offsets = new List<uint>();

            for (int i = 0; i <= cffIndex.Count; ++i)
            {
                switch (cffIndex.OffsetSize)
                {
                    case 1:
                        cffIndex.Offsets.Add(otfReader.ReadByte());
                        break;
                    case 2:
                        cffIndex.Offsets.Add(otfReader.ReadUInt16());
                        break;
                    case 3:
                        var rawOffset = otfReader.ReadBytes(3).ToList();
                        rawOffset.Insert(0, 0);
                        cffIndex.Offsets.Add(BitConverter.ToUInt32(rawOffset.ToArray(), 0));
                        break;
                    case 4:
                        cffIndex.Offsets.Add(otfReader.ReadUInt32());
                        break;
                }
            }

            cffIndex.Data = otfReader.ReadBytes((int)cffIndex.Offsets.Last() - 1).Reverse().ToArray();
        }

        private long GetIndexLength(CFFIndex cffIndex)
        {
            if (cffIndex.Count != 0)
            {
                return 3 + (cffIndex.Count + 1) * cffIndex.OffsetSize + (cffIndex.Offsets[cffIndex.Count] - 1);
            }
            else
            {
                return 2; // according to documents
            }
        }

        private int CalculateSubrBias(uint subrCount)
        {
            if (charstringType == 1)
            {
                return 0;
            }

            return subrCount switch
            {
                < 1240 => 107,
                < 33900 => 1131,
                _ => 32768
            };
        }

        public void UnpackSubrToStack(bool global, int subrNumber, Stack<byte> mainStack)
        {
            try
            {
                CFFIndex subrIndex = (global ? cffGlobalSubroutineIndex : cffLocalSubroutineIndex);

                subrNumber += (global ? globalSubrBias : localSubrBias);

                if (subrNumber < 0)
                {
                    throw new ArgumentException($"subr number < 0 (subrNumber == {subrNumber})!");
                }

                for (var i = subrIndex.Offsets[subrNumber + 1] - 1; i >= subrIndex.Offsets[subrNumber]; --i)
                {
                    mainStack.Push(subrIndex.Data[i - 1]);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
