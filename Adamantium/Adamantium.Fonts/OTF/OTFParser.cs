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
    internal class OTFParser
    {
        // OTF font file path (all data in Big-Endian)
        private readonly string filePath;
        private readonly uint resolution;

        private TTCHeader ttcHeader;
        private List<TableDirectory> tableDirectories;

        // is font collection
        private bool IsFontCollection;

        // OTF byte reader
        private OTFStreamReader otfReader;

        // list of common mandatory tables
        private static ReadOnlyCollection<string> commonMandatoryTables;

        // list of TrueType outlines mandatory tables
        private static ReadOnlyCollection<string> trueTypeMandatoryTables;

        // list of CFF outlines mandatory tables
        private static Dictionary<string, string> cffMandatoryTables;

        // CFF table version (null if not OTF outlines format)
        private CFFVersion? CFFVersion;

        private ICFFParser cffParser;

        private CFFFontSet fontSet;
        
        public TypeFace TypeFace { get; }


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
                {"CFF", "CFF "},
                {"CFF2", "CFF2"}
            };
        }

        public OTFParser(string filePath, UInt32 resolution = 0)
        {
            tableDirectories = new List<TableDirectory>();
            this.filePath = filePath;
            this.resolution = resolution;

            TypeFace = new TypeFace();

            //bezierResolution = resolution > 0 ? resolution : 1;

            Parse();
        }

        private unsafe void Parse()
        {
            var bytes = File.ReadAllBytes(filePath);
            var memoryPtr = Marshal.AllocHGlobal(bytes.Length);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Buffer.MemoryCopy(handle.AddrOfPinnedObject().ToPointer(), memoryPtr.ToPointer(), bytes.Length,
                bytes.Length);
            handle.Free();
            otfReader = new OTFStreamReader((byte*) memoryPtr, bytes.Length);

            // 1st step - check if this is a single font or collection
            IsOTFCollection();

            // reset stream position
            otfReader.Position = 0;

            // 2nd step - read offset table (if single font)
            if (!IsFontCollection)
            {
                ReadTableDirectory();
            }
            else // read TTC Header
            {
                ReadTTCHeader();
                ReadTableDirectories();
            }

            foreach (var tableDirectory in tableDirectories)
            {
                if (tableDirectory.OutlineType == OutlineType.CompactFontFormat)
                {
                    ParseCFF(tableDirectory);
                }
                else
                {
                    // call TTF parser here
                }
                ReadCmapTable(tableDirectory);
            }
        }

        private void IsOTFCollection()
        {
            otfReader.Position = 0;
            IsFontCollection = (otfReader.ReadString(4) == "ttcf");
        }

        private void ReadTTCHeader()
        {
            otfReader.Position = 0;
            ttcHeader = new TTCHeader();
            ttcHeader.Tag = otfReader.ReadString(4);
            ttcHeader.MajorVersion = otfReader.ReadUInt16();
            ttcHeader.MinorVersion = otfReader.ReadUInt16();
            ttcHeader.NumFonts = otfReader.ReadUInt32();
            ttcHeader.TableDirectoryOffsets = new UInt32[ttcHeader.NumFonts];
            for (int i = 0; i < ttcHeader.NumFonts; ++i)
            {
                ttcHeader.TableDirectoryOffsets[i] = otfReader.ReadUInt32();
            }
        }

        private void ReadTableDirectories()
        {
            foreach (var offset in  ttcHeader.TableDirectoryOffsets)
            {
                otfReader.Position = offset;
                ReadTableDirectory();
            }
        }

        private void ReadTableDirectory()
        {
            var tableDirectory = new TableDirectory();
            tableDirectories.Add(tableDirectory);

            tableDirectory.SfntVersion = otfReader.ReadUInt32();
            tableDirectory.numTables = otfReader.ReadUInt16();

            tableDirectory.OutlineType = (tableDirectory.SfntVersion == 0x00010000
                ? OutlineType.TrueType
                : OutlineType.CompactFontFormat);

            // skip other fields
            otfReader.Position += 6;
            
            // 3rd step - read all table records for current table directory
            ReadTableRecords(tableDirectory);
        }

        private void ReadTableRecords(TableDirectory tableDirectory)
        {
            tableDirectory.TablesOffsets = new Dictionary<string, uint>();

            for (int i = 0; i < tableDirectory.numTables; ++i)
            {
                string tag = otfReader.ReadString(4);

                // skip the checkSum
                otfReader.Position += 4;

                UInt32 offset = otfReader.ReadUInt32();

                // skip length
                otfReader.Position += 4;

                tableDirectory.TablesOffsets[tag] = offset;
            }

            CheckMandatoryTables(tableDirectory);
        }

        private void CheckMandatoryTables(TableDirectory tableDirectory)
        {
            foreach (var table in commonMandatoryTables)
            {
                if (!tableDirectory.TablesOffsets.ContainsKey(table))
                {
                    throw new ParserException($"Table {table} is not present in {filePath}");
                }
            }

            switch (tableDirectory.OutlineType)
            {
                case OutlineType.TrueType:
                    foreach (var table in trueTypeMandatoryTables)
                    {
                        if (!tableDirectory.TablesOffsets.ContainsKey(table))
                        {
                            throw new ParserException($"Table {table} is not present in {filePath}");
                        }
                    }

                    break;
                case OutlineType.CompactFontFormat:
                    if (!tableDirectory.TablesOffsets.ContainsKey(cffMandatoryTables["CFF"]) &&
                        !tableDirectory.TablesOffsets.ContainsKey(cffMandatoryTables["CFF2"]))
                    {
                        throw new ParserException(
                            $"Table either {cffMandatoryTables["CFF"]} or {cffMandatoryTables["CFF2"]} is not present in {filePath}");
                    }

                    break;
            }
        }

        private void DetermineCFFVersion(TableDirectory tableDirectory)
        {
            if (tableDirectory.OutlineType == OutlineType.CompactFontFormat)
            {
                if (tableDirectory.TablesOffsets.ContainsKey(cffMandatoryTables["CFF"]))
                {
                    CFFVersion = OTF.CFFVersion.CFF;
                }
                else
                {
                    CFFVersion = OTF.CFFVersion.CFF2;
                }
            }
        }

        private void ParseCFF(TableDirectory tableDirectory)
        {
            DetermineCFFVersion(tableDirectory);

            if (CFFVersion == OTF.CFFVersion.CFF)
            {
                cffParser = new CFFParser(tableDirectory.TablesOffsets[cffMandatoryTables["CFF"]], otfReader);
            }
            else if (CFFVersion == OTF.CFFVersion.CFF2)
            {
                cffParser = new CFFParser(tableDirectory.TablesOffsets[cffMandatoryTables["CFF2"]], otfReader);
            }
            
            fontSet = cffParser.Parse();
        }

        private void ReadCmapTable(TableDirectory tableDirectory)
        {
            CMapTable cmap = new CMapTable();
            otfReader.Position = tableDirectory.TablesOffsets["cmap"];
            cmap.Version = otfReader.ReadUInt16();
            var numTables = otfReader.ReadUInt16();
            var encodings = new EncodingRecord[numTables];
            for (var index = 0; index < encodings.Length; index++)
            {
                encodings[index] = ReadEncodingRecord();
            }

            cmap.CharacterMaps = new CharacterMap[numTables];

            for (var index = 0; index < encodings.Length; index++)
            {
                var encoding = encodings[index];
                otfReader.Position = tableDirectory.TablesOffsets["cmap"] + encoding.SubtableOffset;
                var format = otfReader.ReadUInt16();
                CharacterMap map = ReadCharacterMap(format);
                map.PlatformId = encoding.PlatformId;
                map.EncodingId = encoding.EncodingId;

                cmap.CharacterMaps[index] = map;
            }
            
            cmap.CollectUnicodeToGlyphMappings();


            foreach (var font in fontSet.Fonts)
            {
                foreach (var glyph in font.Glyphs)
                {
                    glyph.Unicodes.AddRange(cmap.GetUnicodesByGlyphId(glyph.Index));
                }
            }
            
        }

        private CharacterMap ReadCharacterMap(ushort format)
        {
            switch (format)
            {
                case 0:
                    return otfReader.ReadCharacterMapFormat0();
                case 2:
                    return otfReader.ReadCharacterMapFormat2();
                case 4:
                    return otfReader.ReadCharacterMapFormat4();
                case 6:
                    return otfReader.ReadCharacterMapFormat6();
                case 8:
                    return otfReader.ReadCharacterMapFormat8();
                case 10:
                    return otfReader.ReadCharacterMapFormat10();
                case 12:
                    return otfReader.ReadCharacterMapFormat12();
                case 13:
                    return otfReader.ReadCharacterMapFormat13();
                case 14:
                    return otfReader.ReadCharacterMapFormat14();
                default:
                    throw new NotSupportedException($"Format {format} is not supported");
            }
        }

        private EncodingRecord ReadEncodingRecord()
        {
            var record = new EncodingRecord();
            record.PlatformId = otfReader.ReadUInt16();
            record.EncodingId = otfReader.ReadUInt16();
            record.SubtableOffset = otfReader.ReadUInt32();
            return record;
        }

    }
}
