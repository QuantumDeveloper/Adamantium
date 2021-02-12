using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.DataOut;
using Adamantium.Mathematics;
using StreamReader = Adamantium.Fonts.Common.StreamReader;
using Point = Adamantium.Fonts.Common.Point;

namespace Adamantium.Fonts.TTF
{
    public class TTFFontParser
    {
        // TTF font data - main output of parser class
        public Font FontData { get; set; }

        // mandatory tables
        private static List<string> mandatoryTables;

        // "table name-table entry" mapping
        private Dictionary<string, TTFTableEntry> tableMap;

        // tables
        private TTFFileHeader header;
        private TTFHeadTable head;
        private TTFMaximumProfileTable maxp;
        private TTFNameTable name;
        private TTFIndexToLocationTable loca;
        private TTFCharToGlyphTable cmap;
        private TTFHorizontalHeaderTable hhea;
        private TTFHorizontalMetricsTable hmtx;
        private TTFKerningTable kern;

        // resolution of bezier curves
        private UInt32 bezierResolution;

        /*@TODO: divide into 2 arrays - simple and composite
         * first: triangulate simple
         * second: transform simple into composite - no additional triangulation needed
         */

        // glyphs geometry
        private Glyph[] glyphs;

        // TTF font file path (all data in Big-Endian)
        private readonly string filePath;

        // TTF byte reader
        private StreamReader ttfReader;

        static TTFFontParser()
        {
            mandatoryTables = new List<string>();
            mandatoryTables.Add("head");
            mandatoryTables.Add("maxp");
            mandatoryTables.Add("loca");
            mandatoryTables.Add("cmap");
            mandatoryTables.Add("glyf");
        }

        public TTFFontParser(string filePath, UInt32 resolution)
        {
            this.filePath = filePath;

            FontData = new Font();

            bezierResolution = resolution > 0 ? resolution : 1;

            TTFParseFont();
        }

        private unsafe void TTFParseFont()
        {
            var bytes = File.ReadAllBytes(filePath);
            var memoryPtr = Marshal.AllocHGlobal(bytes.Length);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Buffer.MemoryCopy(handle.AddrOfPinnedObject().ToPointer(), memoryPtr.ToPointer(), bytes.Length, bytes.Length);
            handle.Free();
            ttfReader = new StreamReader((byte*)memoryPtr, bytes.Length);

            // 1st step: read ttf file header, we need number of tables from here
            ReadTTFHeader();

            // 2nd step: make "name - table" mapping for all tables, we need name, offset and length from here
            MapTTFTables();

            // 3rd step: read "head" table
            ReadTTFHeadTable();

            // 4th step: read "maxp" table
            ReadTTFMaximumProfileTable();

            // 5th step: read "name" table
            ReadTTFNameTable();            

            // 6th step: read "loca" table
            ReadTTFGlyphIndexToLocationTable();

            // 7th step: read "cmap" table (whole path character -> ("cmap") -> glyph index -> ("loca") -> location offset)
            ReadTTFCharToGlyphIndexTable();

            // 8th step: read "hhea" table
            ReadTTFHorizontalHeaderTable();

            // 9th step: read "hmtx" table
            ReadTTFHorizontalMetricsTable();

            // 10th step: read glyph data from "glyf" table
            ReadTTFAllGlyphs();

            // 11th step: generate all bezier contours and triangles
            GenerateAllGeometry();

            // 12th step: read "kern" table
            ReadTTFKerningTable();
        }

        private TTFTableEntry GetTTFTableEntry(string key)
        {
            if (!tableMap.TryGetValue(key, out var entry))
            {
                if (mandatoryTables.Contains(key))
                {
                    throw new ParserException($"'{key}' table not found");
                }
            }

            return entry;
        }

        private void ReadTTFHeader()
        {
            header = new TTFFileHeader();

            header.MajorVersion = ttfReader.ReadUInt16();
            header.MinorVersion = ttfReader.ReadUInt16();
            header.NumTables = ttfReader.ReadUInt16();
            header.SearchRange = ttfReader.ReadUInt16();
            header.EntrySelector = ttfReader.ReadUInt16();
            header.RangeShift = ttfReader.ReadUInt16();
        }

        private void MapTTFTables()
        {
            tableMap = new Dictionary<string, TTFTableEntry>();

            // make "name - table" mapping for all tables, we need name, offset and length from here
            for (var i = 0; i < header.NumTables; ++i)
            {
                var entry = ReadTTFTableEntry();
                tableMap.Add(entry.Name, entry);
            }
        }

        private TTFTableEntry ReadTTFTableEntry()
        {
            TTFTableEntry entry = new TTFTableEntry();

            entry.Name = ttfReader.ReadString(4);
            ttfReader.Position -= 4;
            entry.Tag = ttfReader.ReadUInt32();
            entry.CheckSum = ttfReader.ReadUInt32();
            entry.OffsetPos = ttfReader.ReadUInt32();
            entry.Length = ttfReader.ReadUInt32();

            return entry;
        }

        private void ReadTTFHeadTable()
        {
            head = new TTFHeadTable();
            var headEntry = GetTTFTableEntry("head");

            if (headEntry == null)
            {
                return;
            }

            ttfReader.Position = headEntry.OffsetPos;

            head.MajorVersion = ttfReader.ReadUInt16();
            head.MinorVersion = ttfReader.ReadUInt16();
            head.FontRevisionMaj = ttfReader.ReadUInt16();
            head.FontRevisionMin = ttfReader.ReadUInt16();
            head.CheckSumAdjustment = ttfReader.ReadUInt32();
            head.MagicNumber = ttfReader.ReadUInt32();
            head.Flags = ttfReader.ReadUInt16();
            head.UnitsPerEm = ttfReader.ReadUInt16();
            head.CreatedDate = ttfReader.ReadInt64();
            head.ModifiedDate = ttfReader.ReadInt64();
            head.XMin = ttfReader.ReadInt16();
            head.YMin = ttfReader.ReadInt16();
            head.XMax = ttfReader.ReadInt16();
            head.YMax = ttfReader.ReadInt16();
            head.MacStyle = ttfReader.ReadUInt16();
            head.LowestRecPPEM = ttfReader.ReadUInt16();
            head.FontDirectionHint = ttfReader.ReadInt16();
            head.IndexToLocFormat = ttfReader.ReadInt16();
            head.GlyphDataFormat = ttfReader.ReadInt16();

            FontData.UnitsPerEm = head.UnitsPerEm;
            FontData.LowestRecPPEM = head.LowestRecPPEM;
        }

        private void ReadTTFMaximumProfileTable()
        {
            maxp = new TTFMaximumProfileTable();
            var maxpEntry = GetTTFTableEntry("maxp");

            if (maxpEntry == null)
            {
                return;
            }

            ttfReader.Position = maxpEntry.OffsetPos;

            maxp.MajorVersion = ttfReader.ReadUInt16();
            maxp.MinorVersion = ttfReader.ReadUInt16();
            maxp.NumGlyphs = ttfReader.ReadUInt16();
            maxp.MaxPoints = ttfReader.ReadUInt16();
            maxp.MaxContours = ttfReader.ReadUInt16();
            maxp.MaxCompositePoints = ttfReader.ReadUInt16();
            maxp.MaxCompositeContours = ttfReader.ReadUInt16();
            maxp.MaxZones = ttfReader.ReadUInt16();
            maxp.MaxTwilightPoints = ttfReader.ReadUInt16();
            maxp.MaxStorage = ttfReader.ReadUInt16();
            maxp.MaxFunctionDefs = ttfReader.ReadUInt16();
            maxp.MaxInstructionDefs = ttfReader.ReadUInt16();
            maxp.MaxStackElements = ttfReader.ReadUInt16();
            maxp.MaxSizeOfInstructions = ttfReader.ReadUInt16();
            maxp.MaxComponentElements = ttfReader.ReadUInt16();
            maxp.MaxComponentDepth = ttfReader.ReadUInt16();

            FontData.GlyphData = new GlyphData[maxp.NumGlyphs];

            for (ushort i = 0; i < maxp.NumGlyphs; ++i)
            {
                FontData.GlyphData[i] = new GlyphData();
            }
        }

        private void ReadTTFNameTable()
        {
            String fontFullName = String.Empty;
            name = new TTFNameTable();
            var nameEntry = GetTTFTableEntry("name");

            if (nameEntry == null)
            {
                return;
            }

            ttfReader.Position = nameEntry.OffsetPos;

            name.Format = ttfReader.ReadUInt16();
            name.Count = ttfReader.ReadUInt16();
            name.StringOffset = ttfReader.ReadUInt16();

            name.NameRecords = new TTFNameRecord[name.Count];

            for (int i = 0; i < name.Count; ++i)
            {
                name.NameRecords[i] = ReadTTFNameRecord();

                if (name.NameRecords[i].NameId == 4)
                {
                    var preservedCurrentOffset = ttfReader.Position;
                    ttfReader.Position = nameEntry.OffsetPos + name.StringOffset + name.NameRecords[i].Offset;
                    fontFullName = ttfReader.ReadString(name.NameRecords[i].Length);
                    ttfReader.Position = preservedCurrentOffset;

                    if (String.IsNullOrEmpty(fontFullName))
                    {
                        continue;
                    }

                    break;
                }
            }

            FontData.FontFullName = fontFullName.Replace("\0", "");
        }

        private TTFNameRecord ReadTTFNameRecord()
        {
            var record = new TTFNameRecord();

            record.PlatformId = ttfReader.ReadUInt16();
            record.EncodingId = ttfReader.ReadUInt16();
            record.LanguageId = ttfReader.ReadUInt16();
            record.NameId = ttfReader.ReadUInt16();
            record.Length = ttfReader.ReadUInt16();
            record.Offset = ttfReader.ReadUInt16();

            return record;
        }

        private void ReadTTFGlyphIndexToLocationTable()
        {
            loca = new TTFIndexToLocationTable();
            var locaEntry = GetTTFTableEntry("loca");

            if (locaEntry == null)
            {
                return;
            }

            ttfReader.Position = locaEntry.OffsetPos;

            loca.GlyphOffsets = new UInt32[maxp.NumGlyphs + 1]; // + 'end-of-glyphs' entry

            if (head.IndexToLocFormat == 0)
            {
                for (var i = 0; i < maxp.NumGlyphs + 1; ++i)
                {
                    var glyphIndex = ttfReader.ReadUInt16();
                    loca.GlyphOffsets[i] = glyphIndex * 2u;
                }
            }
            else
            {
                for (var i = 0; i < maxp.NumGlyphs + 1; ++i)
                {
                    loca.GlyphOffsets[i] = ttfReader.ReadUInt32();
                }
            }
        }

        private void ReadTTFCharToGlyphIndexTable()
        {
            cmap = new TTFCharToGlyphTable();
            var cmapEntry = GetTTFTableEntry("cmap");

            if (cmapEntry == null)
            {
                return;
            }

            ttfReader.Position = cmapEntry.OffsetPos;

            cmap.Version = ttfReader.ReadUInt16();
            cmap.NumTables = ttfReader.ReadUInt16();

            bool isValidCmapTableFound = false;

            cmap.EncodingRecords = new TTFEncodingRecord[cmap.NumTables];
            for (var i = 0; i < cmap.NumTables; ++i)
            {
                cmap.EncodingRecords[i] = ReadTTFEncodingRecord();

                if (!(cmap.EncodingRecords[i].PlatformId == 3 && cmap.EncodingRecords[i].EncodingId == 1)) // it is Windows Unicode, for Macintosh Unicode use platformId = 0, encodingId = 3
                {
                    continue;
                }

                ttfReader.Position = cmapEntry.OffsetPos + cmap.EncodingRecords[i].SubtableOffset;

                if (ReadTTFEncodingSubtable(cmap.EncodingRecords[i].Subtable))
                {
                    isValidCmapTableFound = true;
                    break;
                }
            }

            if (!isValidCmapTableFound)
            {
                throw new ParserException("no valid 'cmap' table found");
            }
        }

        private TTFEncodingRecord ReadTTFEncodingRecord()
        {
            var record = new TTFEncodingRecord();

            record.PlatformId = ttfReader.ReadUInt16();
            record.EncodingId = ttfReader.ReadUInt16();
            record.SubtableOffset = ttfReader.ReadUInt32();

            return record;
        }

        private bool ReadTTFEncodingSubtable(TTFEncodingSubtable subtable)
        {
            subtable.Format = ttfReader.ReadUInt16();

            if (subtable.Format != 4)
            {
                return false;
            }

            subtable.Length = ttfReader.ReadUInt16();
            subtable.Language = ttfReader.ReadUInt16();
            subtable.SegCountX2 = ttfReader.ReadUInt16();
            subtable.SearchRange = ttfReader.ReadUInt16();
            subtable.EntrySelector = ttfReader.ReadUInt16();
            subtable.RangeShift = ttfReader.ReadUInt16();

            var segCount = subtable.SegCountX2 / 2;

            subtable.EndCodes = new ushort[segCount];
            for (var i = 0; i < segCount; ++i)
            {

                subtable.EndCodes[i] = ttfReader.ReadUInt16();
            }

            // skip reservedPad field
            ttfReader.Position += sizeof(UInt16);

            subtable.StartCodes = new ushort[segCount];
            for (var i = 0; i < segCount; ++i)
            {
                subtable.StartCodes[i] = ttfReader.ReadUInt16();
            }

            subtable.IdDeltas = new ushort[segCount];
            for (var i = 0; i < segCount; ++i)
            {
                subtable.IdDeltas[i] = ttfReader.ReadUInt16();
            }

            subtable.IdRangeOffsets = new ushort[segCount];
            for (var i = 0; i < segCount; ++i)
            {
                subtable.IdRangeOffsets[i] = ttfReader.ReadUInt16();

                if (subtable.IdRangeOffsets[i] == 0)
                {
                    for (uint ch = subtable.StartCodes[i]; ch <= subtable.EndCodes[i]; ++ch)
                    {
                        FontData.CharToGlyphIndexMapWindowsUnicode.Add((ushort)ch, (ushort)(ch + subtable.IdDeltas[i]));
                    }
                }
                else
                {
                    for (uint ch = subtable.StartCodes[i]; ch <= subtable.EndCodes[i]; ++ch)
                    {
                        var preservedCurrentOffset = ttfReader.Position;

                        ttfReader.Position -= Marshal.SizeOf(subtable.IdRangeOffsets[i]); // return to offset of the current idRangeOffsets[i] (StreamReader increases the offset to the next entry)
                        ttfReader.Position += subtable.IdRangeOffsets[i] + 2 * (ch - subtable.StartCodes[i]);
                        FontData.CharToGlyphIndexMapWindowsUnicode.Add((ushort)ch, ttfReader.ReadUInt16());

                        if (FontData.CharToGlyphIndexMapWindowsUnicode[(ushort)ch] != 0)
                        {
                            FontData.CharToGlyphIndexMapWindowsUnicode[(ushort)ch] += subtable.IdDeltas[i];
                        }

                        ttfReader.Position = preservedCurrentOffset;
                    }
                }
            }

            return true;
        }

        private void ReadTTFHorizontalHeaderTable()
        {
            hhea = new TTFHorizontalHeaderTable();
            var hheaEntry = GetTTFTableEntry("hhea");

            if (hheaEntry == null)
            {
                return;
            }

            ttfReader.Position = hheaEntry.OffsetPos;

            hhea.MajorVersion = ttfReader.ReadUInt16();
            hhea.MinorVersion = ttfReader.ReadUInt16();
            hhea.Ascender = ttfReader.ReadInt16();
            hhea.Descender = ttfReader.ReadInt16();
            hhea.LineGap = ttfReader.ReadInt16();
            hhea.AdvanceWidthMax = ttfReader.ReadUInt16();
            hhea.MinLeftSideBearing = ttfReader.ReadInt16();
            hhea.MinRightSideBearing = ttfReader.ReadInt16();
            hhea.XMaxExtent = ttfReader.ReadInt16();
            hhea.CaretSlopeRise = ttfReader.ReadInt16();
            hhea.CaretSlopeRun = ttfReader.ReadInt16();
            hhea.CaretOffset = ttfReader.ReadInt16();

            // skip 4 reserved int16 fields
            ttfReader.Position += sizeof(Int16) * 4;

            hhea.MetricDataFormat = ttfReader.ReadInt16();
            hhea.NumberOfHMetrics = ttfReader.ReadUInt16();

            FontData.LineSpace = hhea.Ascender - hhea.Descender + hhea.LineGap;
        }

        private void ReadTTFHorizontalMetricsTable()
        {
            hmtx = new TTFHorizontalMetricsTable();
            var hmtxEntry = GetTTFTableEntry("hmtx");

            if (hmtxEntry == null)
            {
                return;
            }

            ttfReader.Position = hmtxEntry.OffsetPos;

            ushort lastAdvanceWidth = 0;

            hmtx.AdvanceWidths = new ushort[maxp.NumGlyphs];
            hmtx.LeftSideBearings = new short[maxp.NumGlyphs];

            for (var i = 0; i < maxp.NumGlyphs; ++i)
            {
                if (i < hhea.NumberOfHMetrics)
                {
                    var advanceWidth = ttfReader.ReadUInt16();
                    hmtx.AdvanceWidths[i] = advanceWidth;

                    lastAdvanceWidth = advanceWidth; // last advanceWidth entry is propagated for all glyph indexes beyond 'numberOfHMetrics' count
                }
                else
                {
                    hmtx.AdvanceWidths[i] = lastAdvanceWidth;
                }

                var leftSideBearing = ttfReader.ReadInt16();
                hmtx.LeftSideBearings[i] = leftSideBearing;
                FontData.GlyphData[i].AdvanceWidth = lastAdvanceWidth;
                FontData.GlyphData[i].LeftSideBearing = leftSideBearing;
            }
        }

        private void ReadTTFAllGlyphs()
        {
            var glyfEntry = GetTTFTableEntry("glyf");

            if (glyfEntry == null)
            {
                return;
            }

            // set correct encoding table
            var windowsUnicodeEncodingRecordIndex = GetTTFEncodingRecordIndex(3, 1, 4);

            if (windowsUnicodeEncodingRecordIndex < 0)
            {
                throw new ParserException("Windows Unicode encoding record has not been found");
            }

            // process all glyphs
            glyphs = new Glyph[maxp.NumGlyphs];

            for (ushort i = 0; i < maxp.NumGlyphs; ++i)
            {
                glyphs[i] = new Glyph();
            }

            for (ushort i = 0; i < maxp.NumGlyphs; ++i)
            {
                ReadTTFGlyphComponentData(glyfEntry.OffsetPos, i);
            }
        }

        private Int32 GetTTFEncodingRecordIndex(UInt16 platformId, UInt16 encodingId, UInt16 format)
        {
            for (var i = 0; i < cmap.EncodingRecords.Length; ++i)
            {
                if (cmap.EncodingRecords[i].PlatformId == platformId &&
                    cmap.EncodingRecords[i].EncodingId == encodingId &&
                    cmap.EncodingRecords[i].Subtable.Format == format)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ReadTTFGlyphComponentData(UInt32 glyfTableOffset, UInt16 glyphIndex)
        {
            // if offset for current glyph is equal to offset for the next glyph, then current glyph has no outline, skip all other steps, but the glyph considers as loaded
            if (loca.GlyphOffsets[glyphIndex] == loca.GlyphOffsets[glyphIndex + 1])
            {
                FontData.ErrorMessages.Add($"[WARN] Offsets for indeces {glyphIndex} and {glyphIndex + 1} are equal - current glyph has no outline");
                return;
            }

            // if offset for current glyph is equal to "end of table" - this is invalid, skip all other steps, glyph considers as not loaded
            if (loca.GlyphOffsets[glyphIndex] == loca.GlyphOffsets[maxp.NumGlyphs])
            {
                FontData.ErrorMessages.Add($"[ERR] Offset for index {glyphIndex} is equal to 'end-of-table'");
                FontData.GlyphData[glyphIndex].IsInvalid = true;
                return;
            }

            ttfReader.Position = glyfTableOffset + loca.GlyphOffsets[glyphIndex];

            glyphs[glyphIndex].Header = ReadTTFGlyphHeader();
            glyphs[glyphIndex].Index = glyphIndex;

            if (glyphs[glyphIndex].Header.NumberOfContours >= 0) // simple glyph
            {
                glyphs[glyphIndex].IsSimple = true;
                ReadTTFSimpleGlyphComponentData(glyphIndex);
            }
            else // composite glyph
            {
                glyphs[glyphIndex].IsSimple = false;
                ReadTTFCompositeGlyphComponentData(glyfTableOffset, glyphIndex);
            }
        }

        private void ReadTTFSimpleGlyphComponentData(UInt16 glyphIndex)
        {
            glyphs[glyphIndex].SegmentedContours = new Contour[glyphs[glyphIndex].Header.NumberOfContours];

            // get the 'contour ends' array
            UInt16[] endPtsOfContours = new ushort[glyphs[glyphIndex].Header.NumberOfContours];

            for (var j = 0; j < glyphs[glyphIndex].Header.NumberOfContours; ++j)
            {
                endPtsOfContours[j] = ttfReader.ReadUInt16();
                var contour = new Contour();
                // calc num of points for current contour
                contour.NumberOfPoints = (ushort)(endPtsOfContours[j] - (j != 0 ? endPtsOfContours[j - 1] : -1));
                glyphs[glyphIndex].SegmentedContours[j] = contour;
            }

            // skip instructions
            var instructionLength = ttfReader.ReadUInt16();
            ttfReader.Position += instructionLength;

            // calc number of points for entire glyph component
            glyphs[glyphIndex].NumberOfPoints = (ushort)(endPtsOfContours[glyphs[glyphIndex].Header.NumberOfContours - 1] + 1);

            // read all flags
            TTFGlyphPointFlag[] flags = ReadTTFGlyphComponentFlags(glyphs[glyphIndex].NumberOfPoints);

            // read all 'x' coordinates
            Int16[] xCoords = new short[glyphs[glyphIndex].NumberOfPoints];
            for (var j = 0; j < glyphs[glyphIndex].NumberOfPoints; ++j)
            {
                if (flags[j].XShort)
                {
                    byte shortXCoord = (byte)ttfReader.ReadByte();
                    xCoords[j] = shortXCoord;

                    if (!flags[j].XMultipurpose)
                    {
                        xCoords[j] *= -1;
                    }

                    // apply delta to obtain absolute coordinates(coordinates of current point stored as deltas to previous point, or to(0, 0) if this is the first point)
                    if (j > 0)
                    {
                        xCoords[j] += xCoords[j - 1];
                    }
                }
                else
                {
                    if (flags[j].XMultipurpose)
                    {
                        // current absolute coordinate is the same as previously calculated one, so we do not add delta here, instead we just store the same value
                        xCoords[j] = (short)(j != 0 ? xCoords[j - 1] : 0);
                    }
                    else
                    {
                        xCoords[j] = ttfReader.ReadInt16();

                        // apply delta to obtain absolute coordinates(coordinates of current point stored as deltas to previous point, or to(0, 0) if this is the first point)
                        if (j > 0)
                        {
                            xCoords[j] += xCoords[j - 1];
                        }
                    }
                }
            }

            // read all 'y' coordinates
            Int16[] yCoords = new short[glyphs[glyphIndex].NumberOfPoints];
            for (var j = 0; j < glyphs[glyphIndex].NumberOfPoints; ++j)
            {
                if (flags[j].YShort)
                {
                    byte shortYCoord = (byte)ttfReader.ReadByte();
                    yCoords[j] = shortYCoord;

                    if (!flags[j].YMultipurpose)
                    {
                        yCoords[j] *= -1;
                    }

                    // apply delta to obtain absolute coordinates(coordinates of current point stored as deltas to previous point, or to(0, 0) if this is the first point)
                    if (j > 0)
                    {
                        yCoords[j] += yCoords[j - 1];
                    }
                }
                else
                {
                    if (flags[j].YMultipurpose)
                    {
                        // current absolute coordinate is the same as previously calculated one, so we do not add delta here, instead we just store the same value
                        yCoords[j] = (short)(j != 0 ? yCoords[j - 1] : 0);
                    }
                    else
                    {
                        yCoords[j] = ttfReader.ReadInt16();

                        // apply delta to obtain absolute coordinates(coordinates of current point stored as deltas to previous point, or to(0, 0) if this is the first point)
                        if (j > 0)
                        {
                            yCoords[j] += yCoords[j - 1];
                        }
                    }
                }
            }

            // process all points for every contour
            for (ushort c = 0; c < glyphs[glyphIndex].Header.NumberOfContours; ++c)
            {
                ushort startOfContour = c != 0 ? (ushort)(endPtsOfContours[c - 1] + 1) : (ushort)0;
                ushort endOfContour = endPtsOfContours[c];

                // start from first "on curve" point and cycle within current contour untill all its points processed
                ushort numberOfPointsProcessed = 0;
                bool onCurveFound = false;
                ContourSegment currentSegment = new ContourSegment();
                Point currentPoint;
                ushort firstOnCurvePointIndex = 0;

                for (ushort p = startOfContour; numberOfPointsProcessed < glyphs[glyphIndex].SegmentedContours[c].NumberOfPoints; ++p)
                {

                    if (p > endOfContour)
                    {
                        p = startOfContour;
                    }

                    if (!onCurveFound)
                    {
                        if (flags[p].OnCurve)
                        {
                            onCurveFound = true;
                            firstOnCurvePointIndex = p;
                        }
                    }

                    if (onCurveFound)
                    {
                        currentPoint = new Point();
                        currentPoint.X = xCoords[p];
                        currentPoint.Y = yCoords[p];

                        if (flags[p].OnCurve)
                        {

                            // if point is on curve - add it immediately to current segment 
                            currentSegment.Points.Add(currentPoint);

                            // if there are more than one point in current segment - it is complete, add it to current contour
                            if (currentSegment.Points.Count > 1)
                            {
                                glyphs[glyphIndex].SegmentedContours[c].Segments.Add(currentSegment);

                                // also immediately add the same point as a start of new segment, because all segments share start points with end points of prevoius segments
                                currentSegment = new ContourSegment();
                                var nextPoint = new Point();
                                nextPoint.X = currentPoint.X;
                                nextPoint.Y = currentPoint.Y;
                                currentSegment.Points.Add(nextPoint);
                            }
                        }
                        else
                        {
                            var prevPoint = p != startOfContour ? p - 1 : endOfContour;

                            // if previous point was on curve - current point is control point of current segment, add it to segment immediately
                            if (flags[prevPoint].OnCurve)
                            {
                                currentSegment.Points.Add(currentPoint);
                            }
                            // else - current point is control point of the next segment, close current segment by creating "on curve" end point
                            else
                            {
                                Point newOnCurvePoint = new Point();

                                // left point + half the lenght between left and right points = middle point - no variable overload this way
                                newOnCurvePoint.X = xCoords[prevPoint] + (xCoords[p] - xCoords[prevPoint]) / 2.0f;
                                newOnCurvePoint.Y = yCoords[prevPoint] + (yCoords[p] - yCoords[prevPoint]) / 2.0f;

                                currentSegment.Points.Add(newOnCurvePoint);
                                glyphs[glyphIndex].SegmentedContours[c].Segments.Add(currentSegment);

                                // immediately add newly created point as a start of new segment, because all segments share start points with end points of prevoius segments
                                currentSegment = new ContourSegment();
                                var nextPoint = new Point();
                                nextPoint.X = newOnCurvePoint.X;
                                nextPoint.Y = newOnCurvePoint.Y;
                                currentSegment.Points.Add(nextPoint);

                                // finally - add current processed point as control point of new segment
                                currentSegment.Points.Add(currentPoint);
                            }
                        }

                        ++numberOfPointsProcessed;
                    }
                }

                // add the first processed point as a last point of last segment - the contour is closed now
                currentPoint = new Point();
                currentPoint.X = xCoords[firstOnCurvePointIndex];
                currentPoint.Y = yCoords[firstOnCurvePointIndex];
                currentSegment.Points.Add(currentPoint);
                glyphs[glyphIndex].SegmentedContours[c].Segments.Add(currentSegment);
            }
        }

        private void ReadTTFCompositeGlyphComponentData(UInt32 glyfTableOffset, UInt16 glyphIndex)
        {
            TTFGlyphCompositeFlag compositeFlag;
            ushort glyphIndexInComposite;

            do
            {
                CompositeGlyphComponent compositeGlyphComponent = new CompositeGlyphComponent();

                compositeFlag = ReadTTFGlyphCompositeFlag();
                glyphIndexInComposite = ttfReader.ReadUInt16();

                compositeGlyphComponent.SimpleGlyphIndex = glyphIndexInComposite;

                Int16 arg1Word = 0;
                Int16 arg2Word = 0;
                sbyte arg1Byte = 0;
                sbyte arg2Byte = 0;

                if (compositeFlag.Arg1And2AreWords)
                {
                    arg1Word = ttfReader.ReadInt16();
                    arg2Word = ttfReader.ReadInt16();
                }
                else
                {
                    arg1Byte = (sbyte)ttfReader.ReadByte();
                    arg2Byte = (sbyte)ttfReader.ReadByte();
                }

                if (compositeFlag.WeHaveAScale)
                {
                    Int16 xyScale = ttfReader.ReadInt16();

                    compositeGlyphComponent.TransformationMatrix[0] = To2Point14Float(xyScale);
                    compositeGlyphComponent.TransformationMatrix[3] = To2Point14Float(xyScale);
                }
                else if (compositeFlag.WeHaveAnXAndYScale)
                {
                    Int16 xScale = ttfReader.ReadInt16();
                    Int16 yScale = ttfReader.ReadInt16();

                    compositeGlyphComponent.TransformationMatrix[0] = To2Point14Float(xScale);
                    compositeGlyphComponent.TransformationMatrix[3] = To2Point14Float(yScale);
                }
                else if (compositeFlag.WeHaveATwoByTwo)
                {
                    Int16 xScale = ttfReader.ReadInt16();
                    Int16 scale01 = ttfReader.ReadInt16();
                    Int16 scale10 = ttfReader.ReadInt16();
                    Int16 yScale = ttfReader.ReadInt16();

                    compositeGlyphComponent.TransformationMatrix[0] = To2Point14Float(xScale);
                    compositeGlyphComponent.TransformationMatrix[1] = To2Point14Float(scale01);
                    compositeGlyphComponent.TransformationMatrix[2] = To2Point14Float(scale10);
                    compositeGlyphComponent.TransformationMatrix[3] = To2Point14Float(yScale);
                }

                if (compositeFlag.ArgsAreXYValues) // matched points == false
                {
                    compositeGlyphComponent.TransformationMatrix[4] = compositeFlag.Arg1And2AreWords ? arg1Word : arg1Byte;
                    compositeGlyphComponent.TransformationMatrix[5] = compositeFlag.Arg1And2AreWords ? arg2Word : arg2Byte;

                    if (compositeFlag.ScaledComponentOffset) // x and y offset values are considered to be in the component glyph’s coordinate system, and the scale transformation is applied to both values
                    {
                        compositeGlyphComponent.TransformationMatrix[4] *= compositeGlyphComponent.TransformationMatrix[0];
                        compositeGlyphComponent.TransformationMatrix[5] *= compositeGlyphComponent.TransformationMatrix[3];
                    }
                }

                // skip instructions
                if (compositeFlag.WeHaveInstructions)
                {
                    ushort numberOfInstructions = ttfReader.ReadUInt16();
                    ttfReader.Position += numberOfInstructions;
                }

                if (!compositeFlag.ArgsAreXYValues) // matched points == true, unsupported
                {
                    FontData.ErrorMessages.Add($"[ERR] Unsupported matched points in composite glyph {glyphIndex}");
                    FontData.GlyphData[glyphIndex].IsInvalid = true;
                }

                glyphs[glyphIndex].CompositeGlyphComponents.Add(compositeGlyphComponent);
            } while (compositeFlag.MoreComponents);
        }

        private GlyphContour TransformContour(float[] transformationMatrix, GlyphContour contour)
        {
            GlyphContour transformedContour = new GlyphContour();

            foreach (var point in contour.Points)
            {
                Point transformedPoint = new Point();
                transformedPoint.X = point.X * transformationMatrix[0] + point.Y * transformationMatrix[1] + transformationMatrix[4];
                transformedPoint.Y = point.X * transformationMatrix[2] + point.Y * transformationMatrix[3] + transformationMatrix[5];
                transformedContour.Points.Add(transformedPoint);
            }

            return transformedContour;
        }

        private float To2Point14Float(short value)
        {
            return (value / 16384.0f);
        }

        private TTFGlyphCompositeFlag ReadTTFGlyphCompositeFlag()
        {
            ushort rawFlag = ttfReader.ReadUInt16();
            return ParseTTFRawGlyphCompositeFlag(rawFlag);
        }

        private TTFGlyphCompositeFlag ParseTTFRawGlyphCompositeFlag(ushort rawFlag)
        {
            TTFGlyphCompositeFlag flag = new TTFGlyphCompositeFlag();

            flag.Arg1And2AreWords = Convert.ToBoolean(rawFlag & 0x0001);
            flag.ArgsAreXYValues = Convert.ToBoolean(rawFlag & 0x0002);
            flag.WeHaveAScale = Convert.ToBoolean(rawFlag & 0x0008);
            flag.MoreComponents = Convert.ToBoolean(rawFlag & 0x0020);
            flag.WeHaveAnXAndYScale = Convert.ToBoolean(rawFlag & 0x0040);
            flag.WeHaveATwoByTwo = Convert.ToBoolean(rawFlag & 0x0080);
            flag.WeHaveInstructions = Convert.ToBoolean(rawFlag & 0x0100);
            flag.ScaledComponentOffset = Convert.ToBoolean(rawFlag & 0x0800);

            return flag;
        }

        private TTFGlyphPointFlag[] ReadTTFGlyphComponentFlags(ushort numberOfPoints)
        {
            byte rawFlag;
            byte numberOfRepeats = 0;

            var flags = new TTFGlyphPointFlag[numberOfPoints];

            for (ushort i = 0; i < numberOfPoints; ++i)
            {
                if (numberOfRepeats == 0)
                {
                    rawFlag = (byte)ttfReader.ReadByte();
                    flags[i] = ParseTTFRawGlyphPointFlag(rawFlag);

                    if (flags[i].Repeat)
                    {
                        numberOfRepeats = (byte)ttfReader.ReadByte();
                    }
                }
                else
                {
                    flags[i] = flags[i - 1];
                    --numberOfRepeats;
                }
            }

            return flags;
        }

        private TTFGlyphPointFlag ParseTTFRawGlyphPointFlag(byte rawFlag)
        {
            var flag = new TTFGlyphPointFlag();
            flag.OnCurve = Convert.ToBoolean(rawFlag & 0x01);
            flag.XShort = Convert.ToBoolean(rawFlag & 0x02);
            flag.YShort = Convert.ToBoolean(rawFlag & 0x04);
            flag.Repeat = Convert.ToBoolean(rawFlag & 0x08);
            flag.XMultipurpose = Convert.ToBoolean(rawFlag & 0x10);
            flag.YMultipurpose = Convert.ToBoolean(rawFlag & 0x20);

            return flag;
        }

        private GlyphHeader ReadTTFGlyphHeader()
        {
            GlyphHeader glyphHeader = new GlyphHeader();

            glyphHeader.NumberOfContours = ttfReader.ReadInt16();
            glyphHeader.XMin = ttfReader.ReadInt16();
            glyphHeader.YMin = ttfReader.ReadInt16();
            glyphHeader.XMax = ttfReader.ReadInt16();
            glyphHeader.YMax = ttfReader.ReadInt16();

            return glyphHeader;
        }

        private void ReadTTFKerningTable()
        {
            kern = new TTFKerningTable();
            var kernEntry = GetTTFTableEntry("kern");

            if (kernEntry == null)
            {
                return;
            }

            ttfReader.Position = kernEntry.OffsetPos;

            kern.Version = ttfReader.ReadUInt16();
            kern.NumTables = ttfReader.ReadUInt16();

            kern.Subtables = new TTFKerningSubtable[kern.NumTables];

            long nextSubtableOffset = ttfReader.Position;

            for (ushort i = 0; i < kern.NumTables; ++i)
            {
                if (i > 0)
                {
                    // advance to next subtable
                    nextSubtableOffset += kern.Subtables[i - 1].Length;
                    ttfReader.Position = nextSubtableOffset;
                }

                var kernSubtable = new TTFKerningSubtable();

                kernSubtable.Version = ttfReader.ReadUInt16();
                kernSubtable.Length = ttfReader.ReadUInt16();
                ushort rawCoverage = ttfReader.ReadUInt16();

                ParseTTFCoverage(rawCoverage, kernSubtable);

                if (kernSubtable.Format != 0) // Windows supports only format 0
                {
                    kern.Subtables[i] = kernSubtable;
                    continue;
                }

                kernSubtable.NumberOfPairs = ttfReader.ReadUInt16();
                kernSubtable.SearchRange = ttfReader.ReadUInt16();
                kernSubtable.EntrySelector = ttfReader.ReadUInt16();
                kernSubtable.RangeShift = ttfReader.ReadUInt16();

                for (ushort j = 0; j < kernSubtable.NumberOfPairs; ++j)
                {
                    ushort left = ttfReader.ReadUInt16();
                    ushort right = ttfReader.ReadUInt16();

                    UInt32 key = GenerateKerningKey(left, right);
                    kernSubtable.KerningValues[key] = ttfReader.ReadInt16();
                }

                kern.Subtables[i] = kernSubtable;
                FontData.KerningData = kernSubtable;
            }
        }

        private void ParseTTFCoverage(ushort rawCoverage, TTFKerningSubtable kerningSubtable)
        {
            kerningSubtable.Horizontal = Convert.ToBoolean(rawCoverage & 0x0001);
            kerningSubtable.Minimum = Convert.ToBoolean(rawCoverage & 0x0002);
            kerningSubtable.CrossStream = Convert.ToBoolean(rawCoverage & 0x0004);
            kerningSubtable.Override = Convert.ToBoolean(rawCoverage & 0x0008);
            ushort format = (ushort)(rawCoverage & 0xFF00);
            format >>= 8; // fit 8-15 bits of ushort into byte
            kerningSubtable.Format = (byte)format;
        }

        static int count = 0;
        private void GenerateAllGeometry()
        {
            Dictionary<UInt16, Glyph> simpleGlyphs = new Dictionary<ushort, Glyph>();
            Dictionary<UInt16, Glyph> compositeGlyphs = new Dictionary<ushort, Glyph>();

            for (UInt16 i = 0; i < glyphs.Length; ++i)
            {
                if (!FontData.GlyphData[i].IsInvalid)
                {
                    if (glyphs[i].IsSimple)
                    {
                        simpleGlyphs.Add(i, glyphs[i]);
                    }
                    else
                    {
                        compositeGlyphs.Add(i, glyphs[i]);
                    }
                }
            }

            var perfTimer = Stopwatch.StartNew();

            Parallel.ForEach(simpleGlyphs, simpleGlyph =>
            {
                GenerateSimpleGlyphContours(simpleGlyph.Value, FontData.GlyphData[simpleGlyph.Key]);
                //Interlocked.Increment(ref count);
                //Debug.WriteLine($"processed {(count / (float)glyphs.Length) * 100} %");
            });

            Parallel.ForEach(compositeGlyphs, compositeGlyph =>
            {
                FillCompositeGlyphGeometry(compositeGlyph.Value, FontData.GlyphData[compositeGlyph.Key]);
                //Interlocked.Increment(ref count);
                //Debug.WriteLine($"processed {(count / (float)glyphs.Length) * 100} %");
            });

            perfTimer.Stop();

            Debug.WriteLine($"time elapsed: {perfTimer.ElapsedMilliseconds} ms");
        }

        private void GenerateSimpleGlyphContours(Glyph glyph, GlyphData glyphData)
        {
            foreach (var contour in glyph.SegmentedContours)
            {
                glyphData.BezierContours.Add(GeometryGenerator.GenerateContour(contour, 1.0f / bezierResolution));
            }
        }

        public void GenerateGlyphTriangles(GlyphData glyphData)
        {
            var polygon = new Polygon();
            
            foreach (var contour in glyphData.BezierContours)
            {
                List<Vector2D> pointList2D = new List<Vector2D>();
            
                foreach (var point in contour.Points)
                {
                    var point2D = new Vector2D(point.X, point.Y);
                    pointList2D.Add(point2D);
                }
            
                var polygonItem = new PolygonItem(pointList2D);
                polygon.Polygons.Add(polygonItem);
            }
            
            glyphData.Vertices = polygon.Fill();
        }

        private void FillCompositeGlyphGeometry(Glyph glyph, GlyphData glyphData)
        {
            foreach (var component in glyph.CompositeGlyphComponents)
            {
                foreach (var bezierContour in FontData.GlyphData[component.SimpleGlyphIndex].BezierContours)
                {
                    glyphData.BezierContours.Add(TransformContour(component.TransformationMatrix, bezierContour));
                }
            }
        }

        static public UInt32 GenerateKerningKey(ushort leftIndex, ushort rightIndex)
        {
            UInt32 left = leftIndex;
            UInt32 right = rightIndex;

            UInt32 key = left;
            key <<= 16;
            key |= right; 
            return key;
        }
    };
}
