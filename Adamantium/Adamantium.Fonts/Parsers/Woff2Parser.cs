using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Exceptions;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.WOFF;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Parsers
{
    internal class Woff2Parser : OTFParser
    {
        private readonly FontStreamReader reader;
        
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
            InitializeBase(filePath, resolution);
            fontEntries = new List<FontCollectionEntry>();
            reader = filePath.LoadIntoStream();
            Parse();
        }

        internal static TypeFace Parse(string filePath, byte resolution)
        {
            var parser = new Woff2Parser(filePath, resolution);
            return parser.TypeFace;
        }

        protected override void Parse()
        {
            ReadHeader();
            ReadTableDirectories();
            FontReader = DecompressStream();
            TableDirectories = new List<TableDirectory>(ConvertFontCollection());
            ReadFontCollection();
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
                    var collectionEntry = new FontCollectionEntry();
                    collectionEntry.NumTables = reader.Read255UInt16();
                    collectionEntry.Flavor = reader.ReadUInt32();
                    var indices = new ushort[collectionEntry.NumTables];
                    for (int x = 0; x < collectionEntry.NumTables; ++x)
                    {
                        indices[x] = reader.Read255UInt16();
                    }
                    
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
        
        
        protected override void ReadGlyphIndexToLocationTable(TableEntry entry)
        {
            // Null transform. Use default implementation
            if (entry.PreprocessingTransform == 3)
            {
                base.ReadGlyphIndexToLocationTable(entry);
            }
            else
            {
                // Nothing to do here as far as the table length should be zero
            }
        }
        
        protected override void ReadTTFGlyphs(TableEntry entry)
        {
            // Null transform. Use default implementation
            if (entry.PreprocessingTransform == 3)
            {
                base.ReadTTFGlyphs(entry);
            }
            else
            {
                ReadTransformedGlyfTable(entry);
            }
        }

        private void ReadTransformedGlyfTable(TableEntry entry)
        {
            /*
             *  Fixed	version	= 0x00000000
             *  UInt16	numGlyphs	Number of glyphs
             *  UInt16	indexFormat	Offset format for loca table, should be consistent with indexToLocFormat of the original head table (see [OFF] specification)
             *  UInt32	nContourStreamSize	Size of nContour stream in bytes
             *  UInt32	nPointsStreamSize	Size of nPoints stream in bytes
             *  UInt32	flagStreamSize	Size of flag stream in bytes
             *  UInt32	glyphStreamSize	Size of glyph stream in bytes (a stream of variable-length encoded values, see description below)
             *  UInt32	compositeStreamSize	Size of composite stream in bytes (a stream of variable-length encoded values, see description below)
             *  UInt32	bboxStreamSize	Size of bbox data in bytes representing combined length of bboxBitmap (a packed bit array) and bboxStream (a stream of Int16 values)
             *  UInt32	instructionStreamSize	Size of instruction stream (a stream of UInt8 values)
             *  Int16	nContourStream[]	Stream of Int16 values representing number of contours for each glyph record
             *  255UInt16	nPointsStream[]	Stream of values representing number of outline points for each contour in glyph records
             *  UInt8	flagStream[]	Stream of UInt8 values representing flag values for each outline point.
             *  Vary	glyphStream[]	Stream of bytes representing point coordinate values using variable length encoding format (defined in subclause 5.2)
             *  Vary	compositeStream[]	Stream of bytes representing component flag values and associated composite glyph data
             *  UInt8	bboxBitmap[]	Bitmap (a numGlyphs-long bit array) indicating explicit bounding boxes
             *  Int16	bboxStream[]	Stream of Int16 values representing glyph bounding box data
             *  UInt8	instructionStream[]	Stream of UInt8 values representing a set of instructions for each corresponding glyph
             */
            
            FontReader.Position = entry.Offset;
            var version = FontReader.ReadUInt32();
            var numGlyphs = FontReader.ReadUInt16();
            var indexFormat = FontReader.ReadUInt16();
            
            var nContourStreamSize = FontReader.ReadUInt32();
            var nPointsStreamSize = FontReader.ReadUInt32();
            var flagStreamSize = FontReader.ReadUInt32();
            var glyphStreamSize = FontReader.ReadUInt32();
            var compositeStreamSize = FontReader.ReadUInt32();
            var bboxStreamSize = FontReader.ReadUInt32();
            var instructionStreamSize = FontReader.ReadUInt32();
            
            var nContourStartAt = FontReader.Position;
            var nPointStartAt = nContourStartAt + nContourStreamSize;
            var flagStreamStartAt = nPointStartAt + nPointsStreamSize;
            var glyphStreamStartAt = flagStreamStartAt + flagStreamSize;
            var compositeStreamStartAt = glyphStreamStartAt + glyphStreamSize;

            var bboxStreamStartAt = compositeStreamStartAt + compositeStreamSize;
            var instructionStreamStartAt = bboxStreamStartAt + bboxStreamSize;
            var endAt = instructionStreamStartAt + instructionStreamSize;

            var tempGlyphs = new TempGlyph[numGlyphs];
            int totalContourCount = 0;
            for (uint i = 0; i < numGlyphs; ++i)
            {
                var numContour = FontReader.ReadInt16();
                tempGlyphs[i] = new TempGlyph(i, numContour);
                if (numContour > 0)
                {
                    totalContourCount += numContour;
                }
            }

            var pointsPerContour = new ushort[totalContourCount];
            for (var i = 0; i < totalContourCount; ++i)
            {
                pointsPerContour[i] = FontReader.Read255UInt16();
            }

            if (FontReader.Position != flagStreamStartAt)
            {
                throw new ParserException($"Reader position after points stream is not equals to calculated one");
            }

            byte[] flagStream = FontReader.ReadBytes(flagStreamSize, true);
            
            if (FontReader.Position != glyphStreamStartAt)
            {
                throw new ParserException($"Reader position after flags stream is not equals to calculated one");
            }

            var compositeGlyphs = tempGlyphs.Where(x => x.NumContours < 0).ToArray();

            var glyphs = new Glyph[numGlyphs];

            FontReader.Position = compositeStreamStartAt;
            foreach (var compositeGlyph in compositeGlyphs)
            {
                var glyph = new Glyph(compositeGlyph.Index);
                compositeGlyph.HasInstructions = ReadTTFCompositeGlyphComponentData(glyph, false);
                glyphs[compositeGlyph.Index] = glyph;
            }

            FontReader.Position = glyphStreamStartAt;
            var currentFlagIndex = 0;
            var pointContourIndex = 0;
            for (int i = 0; i < numGlyphs; ++i)
            {
                BuildSimpleGlyph(
                    ref glyphs[i],
                    tempGlyphs[i], 
                    pointsPerContour, 
                    ref pointContourIndex, 
                    flagStream,
                    ref currentFlagIndex);
            }

            FontReader.Position = bboxStreamStartAt;
            int bitmapCount = (numGlyphs + 7) / 8;
            byte[] bboxBitmap = FontReader.ReadBytes((long) bitmapCount, true).ExpandBitmap();
            for (int i = 0; i < numGlyphs; ++i)
            {
                var glyph = glyphs[i];

                var hasBbox = bboxBitmap[i];
                if (hasBbox == 1)
                {
                    var minX = FontReader.ReadInt16();
                    var minY = FontReader.ReadInt16();
                    var maxX = FontReader.ReadInt16();
                    var maxY = FontReader.ReadInt16();
                    glyph.BoundingRectangle = Rectangle.FromCorners(minX, minY, maxX, maxY);
                }
                else
                {
                    if (glyph.IsComposite)
                    {
                        throw new NotSupportedException($"There is no bounding box for composite glyph: {glyph.Index}");
                    }

                    glyph.RecalculateBounds();
                }
            }

            FontReader.Position = instructionStreamStartAt;
            
            for (int i = 0; i < numGlyphs; ++i)
            {
                var tempGlyph = tempGlyphs[i];
                glyphs[i].SetInstructions(FontReader.ReadBytes(tempGlyph.InstructionsLength, true));
            }
            
            TypeFace.SetGlyphs(glyphs);
        }

        private void BuildSimpleGlyph(
            ref Glyph glyph,
            TempGlyph tempGlyph, 
            ushort[] pointPerContours, 
            ref int pointContourIndex,
            byte[] flagsStream,
            ref int flagStreamIndex)
        {
            if (tempGlyph.NumContours == 0)
            {
                glyph = Glyph.EmptyGlyph(tempGlyph.Index);
                return;
            }
            
            if (tempGlyph.NumContours < 0) // composite glyph
            {
                if (tempGlyph.HasInstructions)
                {
                    tempGlyph.InstructionsLength = FontReader.Read255UInt16();
                }
                
                return;
            }

            int currentX = 0;
            int currentY = 0;

            int numContours = tempGlyph.NumContours;
            var endContours = new ushort[numContours];

            for (int i = 0; i < numContours; ++i)
            {
                var numPoints = pointPerContours[pointContourIndex++];
                endContours[i] = numPoints;
            }

            if (glyph == null)
            {
                glyph = new Glyph(tempGlyph.Index);
            }

            for (int i = 0; i < numContours; ++i)
            {
                var outline = new Outline();
                glyph.AddOutline(outline);

                var endContour = endContours[i];

                for (int k = 0; k < endContour; ++k)
                {
                    byte flag = flagsStream[flagStreamIndex++];

                    bool isOnCurve = Convert.ToBoolean(flag & 0x80);
                    //bool isOnCurve = flag >> 7 == 0;

                    int xyFormat = flag & 0x7F; // remaining 7 bits defines x,y format

                    var entry = TripletEncodingTable.GetEntry(xyFormat);

                    // According to docs, ByteCount also include 1 byte for flags, so real ByteCount is ByteCount - 1
                    var packedXY = FontReader.ReadBytes(entry.ByteCount - 1, true);

                    int x = 0;
                    int y = 0;

                    // Pack starts from x coordinate, so we first switch on XBits
                    switch (entry.XBits)
                    {
                        // xBits = 0, yBits = 8
                        case 0:
                            x = 0;
                            y = entry.TranslateY(packedXY[0]);
                            break;
                        // xBits = 4, yBits = 4
                        case 4:
                            x = entry.TranslateX(packedXY[0] >> 4);
                            y = entry.TranslateY(packedXY[0] & 0xF);
                            break;
                        // xBits = 8, yBits = 0 or // xBits = 8, yBits = 8
                        case 8:
                            x = entry.TranslateX(packedXY[0]);
                            y = entry.YBits == 8 ? entry.TranslateY(packedXY[1]) : 0;
                            break;
                        // xBits = 12, yBits = 12
                        case 12:
                            x = entry.TranslateX((packedXY[0] << 4) | (packedXY[1] >> 4));
                            y = entry.TranslateY(((packedXY[1] & 0xF) << 8) | (packedXY[2]));
                            break;
                        // xBits = 16, yBits = 16
                        case 16:
                            x = entry.TranslateX((packedXY[0] << 8) | packedXY[1]);
                            y = entry.TranslateY((packedXY[2] << 8) | packedXY[3]);
                            break;
                    }

                    var point = new OutlinePoint(currentX += x, currentY += y, isOnCurve);
                    outline.Points.Add(point);
                }
            }

            tempGlyph.InstructionsLength = FontReader.Read255UInt16();
        }

        private class TempGlyph
        {
            public readonly uint Index;
            public readonly int NumContours;

            public bool HasInstructions;
            public int InstructionsLength;

            public TempGlyph(uint index, int numContour)
            {
                Index = index;
                NumContours = numContour;
            }
        }
        
    }
}