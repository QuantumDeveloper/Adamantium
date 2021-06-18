using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Exceptions;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Parsers.CFF;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;
using Adamantium.Fonts.Tables.GPOS;
using Adamantium.Fonts.Tables.GSUB;

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
                case TableNames.GPOS:
                    ReadGlyphPositioningTable(entry);
                    break;
                case TableNames.GSUB:
                    ReadGlyphSubstitutionTable(entry);
                    break;
                case TableNames.fvar:
                    ReadFvarTable(entry);
                    break;
                case TableNames.CFF:
                case TableNames.CFF2:
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
            CurrentFont.VariationData = cffFont.VariationStore;
        }
        
        protected virtual void ReadFvarTable(TableEntry entry)
        {
            FontReader.Position = entry.Offset;

            var majorVersion = FontReader.ReadUInt16();
            var minorVersion = FontReader.ReadUInt16();
            var axesArrayOffset = FontReader.ReadUInt16();
            var reserved = FontReader.ReadUInt16();
            var axisCount = FontReader.ReadUInt16();
            var axisSize = FontReader.ReadUInt16();
            var instanceCount = FontReader.ReadUInt16();
            var instanceSize = FontReader.ReadUInt16();

            FontReader.Position = entry.Offset + axesArrayOffset;
            var currentOffset = FontReader.Position;
            
            var axes = new List<VariationAxisRecord>(); 
            
            for (var i = 0; i < axisCount; ++i)
            {
                var axis = new VariationAxisRecord();

                axis.AxisTag = FontReader.ReadString(4);
                axis.MinValue = FontReader.ReadInt32().FromF16Dot16();
                axis.DefaultValue = FontReader.ReadInt32().FromF16Dot16();
                axis.MaxValue = FontReader.ReadInt32().FromF16Dot16();
                axis.Flags = FontReader.ReadUInt16();
                axis.AxisNameID = FontReader.ReadUInt16();

                axes.Add(axis);
                
                currentOffset += axisSize;
                FontReader.Position = currentOffset;
            }

            var instances = new List<InstanceRecord>();
            
            for (var j = 0; j < instanceCount; ++j)
            {
                var instance = new InstanceRecord();

                instance.SubfamilyNameID = FontReader.ReadUInt16();
                instance.Flags = FontReader.ReadUInt16();
                instance.Coordinates = new List<double>();
                
                for (var k = 0; k < axisCount; ++k)
                {
                    instance.Coordinates.Add(FontReader.ReadInt32().FromF16Dot16());
                }

                var nameTableOffset = CurrentTableDirectory.TablesOffsets[TableNames.name];
                var nameRecord = Name.NameRecords.FirstOrDefault(x => x.NameId == instance.SubfamilyNameID);

                if (nameRecord != null)
                {
                    FontReader.Position = nameTableOffset + Name.StorageOffset + nameRecord.StringOffset;
                    var encoding = nameRecord.EncodingId is 3 or 1 ? Encoding.BigEndianUnicode : Encoding.UTF8;
                    var str = FontReader.ReadString(nameRecord.Length, encoding);
                    instance.InstanceSubfamilyName = str;
                }

                instances.Add(instance);
                
                currentOffset += instanceSize;
                FontReader.Position = currentOffset;
            }

            CurrentFont.InstanceData = instances;
        }

        protected virtual void ReadGlyphPositioningTable(TableEntry entry)
        {
            FontReader.Position = entry.Offset;
            
            var gpos = new GlyphPositioningTable();
            gpos.MajorVersion = FontReader.ReadUInt16();
            gpos.MinorVersion = FontReader.ReadUInt16();

            var scriptListOffset = FontReader.ReadUInt16() + entry.Offset;
            var featureListOffset = FontReader.ReadUInt16() + entry.Offset;
            var lookupListOffset = FontReader.ReadUInt16() + entry.Offset;

            if (gpos.MinorVersion == 1)
            {
                gpos.FeatureVariationsOffset = FontReader.ReadUInt16();
            }

            gpos.ScriptList = FontReader.ReadScriptList(scriptListOffset);

            gpos.FeatureList = FontReader.ReadFeatureList(featureListOffset);
            
            gpos.LookupList = FontReader.ReadGPOSLookupListTable(lookupListOffset);
        }

        protected virtual void ReadGlyphSubstitutionTable(TableEntry entry)
        {
            FontReader.Position = entry.Offset;

            var gsub = new GlyphSubstitutionTable();
            gsub.MajorVersion = FontReader.ReadUInt16();
            gsub.MinorVersion = FontReader.ReadUInt16();
            
            var scriptListOffset = FontReader.ReadUInt16() + entry.Offset;
            var featureListOffset = FontReader.ReadUInt16() + entry.Offset;
            var lookupListOffset = FontReader.ReadUInt16() + entry.Offset;

            long featureVariationsOffset = 0;
            if (gsub.MinorVersion == 1)
            {
                featureVariationsOffset = FontReader.ReadUInt16();
            }
            
            gsub.ScriptList = FontReader.ReadScriptList(scriptListOffset);

            gsub.FeatureList = FontReader.ReadFeatureList(featureListOffset);

            gsub.LookupList = FontReader.ReadGSUBLookupListTable(lookupListOffset);

        }

    }
}
