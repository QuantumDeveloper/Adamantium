using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Exceptions;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Parsers.CFF;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;
using Adamantium.Fonts.Tables.WOFF;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Parsers
{
    internal class OTFParser : TTFParser
    {
        private TTCHeader ttcHeader;

        // is font collection
        private bool IsFontCollection;

        // list of common mandatory tables
        private static ReadOnlyCollection<string> commonMandatoryTables;

        // list of TrueType outlines mandatory tables
        private static ReadOnlyCollection<string> trueTypeMandatoryTables;

        // list of CFF outlines mandatory tables
        private static Dictionary<string, string> cffMandatoryTables;

        // CFF table version (null if not OTF outlines format)
        private CFFVersion? CFFVersion;

        private ICFFParser cffParser;

        private CFFFont cffFont;

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
        
        internal static TypeFace Parse(string path, byte resolution)
        {
            var parser = new OTFParser(path, resolution);
            return parser.TypeFace;
        }

        internal static TypeFace Parse(FontStreamReader FontReader, byte sample, params TableDirectory[] tableDirectories)
        {
            var parser = new OTFParser(FontReader, sample, tableDirectories);
            return parser.TypeFace;
        }

        private OTFParser(string filePath, byte resolution = 1) : base(filePath, resolution)
        {
        }

        private OTFParser(FontStreamReader fontStreamReader, byte resolution = 0, params TableDirectory[] tableDirectories) 
            : base(fontStreamReader, resolution, tableDirectories)
        {
        }

        protected override void Parse()
        {
            // 1st step - check if this is a single font or collection
            IsOTFCollection();

            // reset stream position
            FontReader.Position = 0;

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

            ReadFontCollection();
        }

        protected override void ReadTable(TableEntry entry)
        {
            switch (entry.Name)
            {
                case "CFF ":
                case "CFF2":
                    DetermineCFFVersion(CurrentTableDirectory);
                    ParseCFF(entry);
                    break;
            }
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

        internal class TempGlyph
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

        private void IsOTFCollection()
        {
            FontReader.Position = 0;
            IsFontCollection = (FontReader.ReadString(4) == "ttcf");
        }

        private void ReadTTCHeader()
        {
            FontReader.Position = 0;
            ttcHeader = new TTCHeader();
            ttcHeader.Tag = FontReader.ReadString(4);
            ttcHeader.MajorVersion = FontReader.ReadUInt16();
            ttcHeader.MinorVersion = FontReader.ReadUInt16();
            ttcHeader.NumFonts = FontReader.ReadUInt32();
            ttcHeader.TableDirectoryOffsets = new UInt32[ttcHeader.NumFonts];
            for (int i = 0; i < ttcHeader.NumFonts; ++i)
            {
                ttcHeader.TableDirectoryOffsets[i] = FontReader.ReadUInt32();
            }
        }

        private void ReadTableDirectories()
        {
            foreach (var offset in  ttcHeader.TableDirectoryOffsets)
            {
                FontReader.Position = offset;
                ReadTableDirectory();
            }
        }

        private void ReadTableDirectory()
        {
            var tableDirectory = new TableDirectory();
            TableDirectories.Add(tableDirectory);

            tableDirectory.SfntVersion = FontReader.ReadUInt32();
            tableDirectory.NumTables = FontReader.ReadUInt16();

            tableDirectory.OutlineType = (tableDirectory.SfntVersion == 0x00010000
                ? OutlineType.TrueType
                : OutlineType.CompactFontFormat);

            // skip other fields
            FontReader.Position += 6;
            
            // 3rd step - read all table records for current table directory
            ReadTableRecords(tableDirectory);
        }

        private void ReadTableRecords(TableDirectory tableDirectory)
        {
            tableDirectory.Tables = new TableEntry[tableDirectory.NumTables];
            
            for (int i = 0; i < tableDirectory.NumTables; ++i)
            {
                var table = new TableEntry
                {
                    Name = FontReader.ReadString(4),
                    CheckSum = FontReader.ReadUInt32(),
                    Offset = FontReader.ReadUInt32(),
                    Length = FontReader.ReadUInt32()
                };

                tableDirectory.TablesOffsets[table.Name] = table.Offset;
                tableDirectory.Tables[i] = table;
            }

            CheckMandatoryTables(tableDirectory);
        }

        private void CheckMandatoryTables(TableDirectory tableDirectory)
        {
            foreach (var table in commonMandatoryTables)
            {
                if (!tableDirectory.TablesOffsets.ContainsKey(table))
                {
                    throw new ParserException($"Table {table} is not present in {FilePath}");
                }
            }

            switch (tableDirectory.OutlineType)
            {
                case OutlineType.TrueType:
                    foreach (var table in trueTypeMandatoryTables)
                    {
                        if (!tableDirectory.TablesOffsets.ContainsKey(table))
                        {
                            throw new ParserException($"Table {table} is not present in {FilePath}");
                        }
                    }

                    break;
                case OutlineType.CompactFontFormat:
                    if (!tableDirectory.TablesOffsets.ContainsKey(cffMandatoryTables["CFF"]) &&
                        !tableDirectory.TablesOffsets.ContainsKey(cffMandatoryTables["CFF2"]))
                    {
                        throw new ParserException(
                            $"Table either {cffMandatoryTables["CFF"]} or {cffMandatoryTables["CFF2"]} is not present in {FilePath}");
                    }

                    break;
            }
        }

        private void DetermineCFFVersion(TableDirectory tableDirectory)
        {
            if (tableDirectory.OutlineType != OutlineType.CompactFontFormat) return;
            
            if (tableDirectory.TablesOffsets.ContainsKey(cffMandatoryTables["CFF"]))
            {
                CFFVersion = Tables.CFF.CFFVersion.CFF;
            }
            else
            {
                CFFVersion = Tables.CFF.CFFVersion.CFF2;
            }
        }

        private void ParseCFF(TableEntry entry)
        {
            var offset = entry.Offset;
            
            cffParser = CFFVersion switch
            {
                Tables.CFF.CFFVersion.CFF => new CFFParser(offset, FontReader),
                Tables.CFF.CFFVersion.CFF2 => new CFF2Parser(offset, FontReader),
                _ => cffParser
            };

            cffFont = cffParser.Parse();
            TypeFace.SetGlyphs(cffFont.Glyphs);
        }

        

    }
}
