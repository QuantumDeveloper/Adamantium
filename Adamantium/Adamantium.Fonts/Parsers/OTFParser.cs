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

        protected OTFParser()
        {
            
        }

        protected OTFParser(string filePath, byte resolution = 1) : base(filePath, resolution)
        {
        }

        protected OTFParser(FontStreamReader fontStreamReader, byte resolution = 0, params TableDirectory[] tableDirectories) 
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
