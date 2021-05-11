using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.WOFF;

namespace Adamantium.Fonts.Parsers
{
    internal class Woff2Parser : IFontParser
    {
        private string filePath;
        private byte resolution;

        private FontStreamReader reader;
        
        public TypeFace TypeFace { get; private set; }

        private Woff2Header header;
        private List<FontCollectionEntry> fontEntries;

        private static Dictionary<byte, string> knownTableTags;

        static Woff2Parser()
        {
            knownTableTags = new Dictionary<byte, string>()
            {
                {0,	    "cmap"},
                {1,	    "head"},
                {2,	    "hhea"},
                {3,	    "hmtx"},
                {4,	    "maxp"},
                {5,	    "name"},
                {6,	    "OS/2"},
                {7,	    "post"},
                {8,	    "cvt "},    
                {9,	    "fpgm"},
                {10,	"glyf"},
                {11,	"loca"},
                {12,	"prep"},
                {13,	"CFF "},   
                {14,	"VORG"},
                {15,	"EBDT"},
                {16,	"EBLC"},
                {17,	"gasp"},
                {18,	"hdmx"},
                {19,	"kern"},
                {20,	"LTSH"},
                {21,	"PCLT"},
                {22,	"VDMX"},
                {23,	"vhea"},
                {24,	"vmtx"},
                {25,	"BASE"},
                {26,	"GDEF"},
                {27,	"GPOS"},
                {28,	"GSUB"},
                {29,	"EBSC"},
                {30,	"JSTF"},
                {31,	"MATH"},
                {32,	"CBDT"},
                {33,	"CBLC"},
                {34,	"COLR"},
                {35,	"CPAL"},
                {36,	"SVG "},
                {37,	"sbix"},
                {38,	"acnt"},
                {39,	"avar"},
                {40,	"bdat"},
                {41,	"bloc"},
                {42,	"bsln"},
                {43,	"cvar"},
                {44,	"fdsc"},
                {45,	"feat"},
                {46,	"fmtx"},
                {47,	"fvar"},
                {48,	"gvar"},
                {49,	"hsty"},
                {50,	"just"},
                {51,	"lcar"},
                {52,	"mort"},
                {53,	"morx"},
                {54,	"opbd"},
                {55,	"prop"},
                {56,	"trak"},
                {57,	"Zapf"},
                {58,	"Silf"},
                {59,	"Glat"},
                {60,	"Gloc"},
                {61,	"Feat"},
                {62,	"Sill"},
                {63,	"unknown"},
            };
        }

        private Woff2Parser(string filePath, byte resolution = 1)
        {
            this.filePath = filePath;
            this.resolution = resolution;
            fontEntries = new List<FontCollectionEntry>();
            reader = filePath.LoadIntoStream();

            Parse();
        }

        internal static TypeFace Parse(string filePath, byte resolution)
        {
            var parser = new Woff2Parser(filePath, resolution);
            return parser.TypeFace;
        }

        private void Parse()
        {
            ReadHeader();
            ReadTableDirectories();
            var decompressedStream = DecompressStream();
            var directories = ConvertFontCollection();
            TypeFace = OTFParser.Parse(decompressedStream, resolution, directories);
        }

        private void ReadHeader()
        {
            header = new Woff2Header();
            header.Signature = reader.ReadUInt32();
            header.Flavor = reader.ReadUInt32();
            header.Length = reader.ReadUInt32();
            header.NumTables = reader.ReadUInt16();
            header.Reserved = reader.ReadUInt16();
            header.TotalSfntSize = reader.ReadUInt32();
            header.TotalCompressedSize = reader.ReadUInt32();
            header.MajorVersion = reader.ReadUInt16();
            header.MinorVersion = reader.ReadUInt16();
            header.MetaOffset = reader.ReadUInt32();
            header.MetaLength = reader.ReadUInt32();
            header.MetaOrigLength = reader.ReadUInt32();
            header.PrivOffset = reader.ReadUInt32();
            header.PrivLength = reader.ReadUInt32();
            header.InnerFontType = (InnerFontType) header.Flavor;
        }

        private void ReadTableDirectories()
        {
            var fontEntry = new FontCollectionEntry();
            fontEntry.NumTables = header.NumTables;
            fontEntry.Tables = new Woff2Table[header.NumTables];
            ReadTableDirectory(fontEntry);
            fontEntries.Add(fontEntry);
            
            if (header.InnerFontType == InnerFontType.OTFFontCollection)
            {
                // CollectionHeader
                var version = reader.ReadUInt32();
                var numFonts = reader.Read255UInt16();
                // ----
                
                for (int i = 0; i < numFonts; ++i)
                {
                    fontEntry = new FontCollectionEntry();
                    fontEntry.NumTables = reader.Read255UInt16();
                    fontEntry.Flavor = reader.ReadUInt32();
                    var indices = new ushort[fontEntry.NumTables];
                    for (int x = 0; x < fontEntry.NumTables; ++x)
                    {
                        
                    }
                    
                    //fontEntry.Tables = 
                }
            }
        }

        private void ReadTableDirectory(FontCollectionEntry fontEntry)
        {
            long expectedOffset = 0;
            for (int i = 0; i < header.NumTables; ++i)
            {
                var table = new Woff2Table();

                var flags = reader.ReadByte();

                var tableTagIndex = flags & 0x1F;
                table.Name = tableTagIndex < 63
                    ? knownTableTags[(byte) tableTagIndex]
                    : reader.ReadString(4, Encoding.UTF8);

                table.PreprocessingTransform = (byte)((flags >> 5) & 0x3);

                if (reader.ReadUIntBase128(out var origLength))
                {
                    table.OrigLength = origLength;
                }

                table.ExpectedOffset = expectedOffset;

                switch (table.PreprocessingTransform)
                {
                    case 0:
                        switch (table.Name)
                        {
                            case "glyf":
                            case "loca":
                                if (reader.ReadUIntBase128(out var length))
                                {
                                    table.TransformLength = length;
                                    expectedOffset += table.TransformLength;
                                }
                                break;
                            default:
                                expectedOffset += table.OrigLength;
                                break;
                        }
                        break;
                    case 1:
                    case 2:
                    case 3:
                        expectedOffset += table.OrigLength;
                        break;
                }

                fontEntry.Tables[i] = table;
            }
        }

        private FontStreamReader DecompressStream()
        {
            using (var compressedStream = new MemoryStream(reader.ReadBytes(header.TotalCompressedSize, true)))
            {
                var decompressedStream = new FontStreamReader();
                try
                {
                    using (var brotli = new BrotliStream(compressedStream, CompressionMode.Decompress))
                    {
                        brotli.CopyTo(decompressedStream);
                        decompressedStream.Position = 0;
                    }
                }
                catch (Exception e)
                {
                    TypeFace.AddErrorMessage(e.Message);
                }

                return decompressedStream;
            }
        }

        private TableDirectory[] ConvertFontCollection()
        {
            var tableDirectories = new List<TableDirectory>();
            foreach (var fontEntry in fontEntries)
            {
                var otfTableDirectory = new TableDirectory();
                tableDirectories.Add(otfTableDirectory);

                otfTableDirectory.NumTables = fontEntry.NumTables;
                otfTableDirectory.SfntVersion = fontEntry.Flavor;
                if (header.Flavor == 0x00010000)
                {
                    otfTableDirectory.OutlineType = OutlineType.TrueType;
                }
                else if (header.Flavor == 0x4F54544F)
                {
                    otfTableDirectory.OutlineType = OutlineType.CompactFontFormat;
                }
                
                otfTableDirectory.NumTables = fontEntry.NumTables;
                otfTableDirectory.Tables = new TableEntry[fontEntry.NumTables];
                for (int i = 0; i < fontEntry.NumTables; ++i)
                {
                    var table = new TableEntry();
                    table.Name = fontEntry.Tables[i].Name;
                    table.Offset = fontEntry.Tables[i].ExpectedOffset;
                    table.PreprocessingTransform = fontEntry.Tables[i].PreprocessingTransform;
                    
                    otfTableDirectory.Tables[i] = table;
                }

                otfTableDirectory.CreateTableEntriesMap();
            }

            return tableDirectories.ToArray();
        }
        
    }
}