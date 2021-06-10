using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CMAP;
using Adamantium.Fonts.Tables.Layout;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.Parsers
{
    internal class TTFParser : IFontParser
    {
        // TTF font data - main output of parser class
        //public TTFFont FontData { get; }
        
        public TypeFace TypeFace { get; protected set; }

        // mandatory tables
        private static List<string> mandatoryTables;

        // "table name-table entry" mapping
        //private Dictionary<string, TableEntry> tableMap;

        // tables
        private TTFFileHeader header;
        private HeadTable head;
        private MaximumProfileTable maxp;
        private TTFIndexToLocationTable loca;
        private HorizontalHeaderTable hhea;
        private HorizontalMetricsTable hmtx;
        private KerningTable kern;
        private NameTable name;

        protected List<TableDirectory> TableDirectories { get; set; }

        // TTF font file path (all data in Big-Endian)
        protected string FilePath { get; set; }

        // TTF byte FontReader
        protected FontStreamReader FontReader { get; set; }
        
        protected Font CurrentFont { get; private set; }
        
        protected TableDirectory CurrentTableDirectory { get; private set; }

        protected static List<string> StandardGlyphNames { get; }

        protected static Dictionary<string, int> SortingTable { get; }
        
        // resolution of bezier curves
        protected byte Resolution { get; set; }
        
        protected HashSet<long> ReadTables { get; private set; }

        static TTFParser()
        {
            mandatoryTables = new List<string>();
            mandatoryTables.Add("head");
            mandatoryTables.Add("maxp");
            mandatoryTables.Add("loca");
            mandatoryTables.Add("cmap");
            mandatoryTables.Add("glyf");
            
            StandardGlyphNames = new List<string>()
            {
                ".notdef",
                ".null",
                "nonmarkingreturn",
                "space",
                "exclam",
                "quotedbl",
                "numbersign",
                "dollar",
                "percent",
                "ampersand",
                "quotesingle",
                "parenleft",
                "parenright",
                "asterisk",
                "plus",
                "comma",
                "hyphen",
                "period",
                "slash",
                "zero",
                "one",
                "two",
                "three",
                "four",
                "five",
                "six",
                "seven",
                "eight",
                "nine",
                "colon",
                "semicolon",
                "less",
                "equal",
                "greater",
                "question",
                "at",
                "A",
                "B",
                "C",
                "D",
                "E",
                "F",
                "G",
                "H",
                "I",
                "J",
                "K",
                "L",
                "M",
                "N",
                "O",
                "P",
                "Q",
                "R",
                "S",
                "T",
                "U",
                "V",
                "W",
                "X",
                "Y",
                "Z",
                "bracketleft",
                "backslash",
                "bracketright",
                "asciicircum",
                "underscore",
                "grave",
                "a",
                "b",
                "c",
                "d",
                "e",
                "f",
                "g",
                "h",
                "i",
                "j",
                "k",
                "l",
                "m",
                "n",
                "o",
                "p",
                "q",
                "r",
                "s",
                "t",
                "u",
                "v",
                "w",
                "x",
                "y",
                "z",
                "braceleft",
                "bar",
                "braceright",
                "asciitilde",
                "Adieresis",
                "Aring",
                "Ccedilla",
                "Eacute",
                "Ntilde",
                "Odieresis",
                "Udieresis",
                "aacute",
                "agrave",
                "acircumflex",
                "adieresis",
                "atilde",
                "aring",
                "ccedilla",
                "eacute",
                "egrave",
                "ecircumflex",
                "edieresis",
                "iacute",
                "igrave",
                "icircumflex",
                "idieresis",
                "ntilde",
                "oacute",
                "ograve",
                "ocircumflex",
                "odieresis",
                "otilde",
                "uacute",
                "ugrave",
                "ucircumflex",
                "udieresis",
                "dagger",
                "degree",
                "cent",
                "sterling",
                "section",
                "bullet",
                "paragraph",
                "germandbls",
                "registered",
                "copyright",
                "trademark",
                "acute",
                "dieresis",
                "notequal",
                "AE",
                "Oslash",
                "infinity",
                "plusminus",
                "lessequal",
                "greaterequal",
                "yen",
                "mu",
                "partialdiff",
                "summation",
                "product",
                "pi",
                "integral",
                "ordfeminine",
                "ordmasculine",
                "Omega",
                "ae",
                "oslash",
                "questiondown",
                "exclamdown",
                "logicalnot",
                "radical",
                "florin",
                "approxequal",
                "Delta",
                "guillemotleft",
                "guillemotright",
                "ellipsis",
                "nonbreakingspace",
                "Agrave",
                "Atilde",
                "Otilde",
                "OE",
                "oe",
                "endash",
                "emdash",
                "quotedblleft",
                "quotedblright",
                "quoteleft",
                "quoteright",
                "divide",
                "lozenge",
                "ydieresis",
                "Ydieresis",
                "fraction",
                "currency",
                "guilsinglleft",
                "guilsinglright",
                "fi",
                "fl",
                "daggerdbl",
                "periodcentered",
                "quotesinglbase",
                "quotedblbase",
                "perthousand",
                "Acircumflex",
                "Ecircumflex",
                "Aacute",
                "Edieresis",
                "Egrave",
                "Iacute",
                "Icircumflex",
                "Idieresis",
                "Igrave",
                "Oacute",
                "Ocircumflex",
                "apple",
                "Ograve",
                "Uacute",
                "Ucircumflex",
                "Ugrave",
                "dotlessi",
                "circumflex",
                "tilde",
                "macron",
                "breve",
                "dotaccent",
                "ring",
                "cedilla",
                "hungarumlaut",
                "ogonek",
                "caron",
                "Lslash",
                "lslash",
                "Scaron",
                "scaron",
                "Zcaron",
                "zcaron",
                "brokenbar",
                "Eth",
                "eth",
                "Yacute",
                "yacute",
                "Thorn",
                "thorn",
                "minus",
                "multiply",
                "onesuperior",
                "twosuperior",
                "threesuperior",
                "onehalf",
                "onequarter",
                "threequarters",
                "franc",
                "Gbreve",
                "gbreve",
                "Idotaccent",
                "Scedilla",
                "scedilla",
                "Cacute",
                "cacute",
                "Ccaron",
                "ccaron",
                "dcroat",
            };
            
            SortingTable = new Dictionary<string, int>()
            {
                { "head",    0      },
                { "maxp",    10     },
                { "name",    20     },
                { "loca",    30     },
                { "glyf",    40     },
                { "CFF ",    50     },
                { "CFF2",    60     },
                { "cmap",    70     },
                { "hhea",    80     },
                { "hmtx",    90     },
                { "fvar",    91	    },
                { "vhea",    100    },
                { "vmtx",    110    },
                { "OS/2",    120    },
                { "post",    130    },
                { "BASE",    140    },
                { "GDEF",    150	},
                { "GPOS",    160	},
                { "GSUB",    170	},
                { "EBSC",    180	},
                { "JSTF",    190	},
                { "MATH",    200	},
                { "CBDT",    210	},
                { "CBLC",    220	},
                { "COLR",    230	},
                { "CPAL",    240	},
                { "SVG ",    250	},
                { "cvt ",    260    },    
                { "fpgm",    270    },
                { "prep",    280    },
                { "VORG",    290    },
                { "EBDT",    300    },
                { "EBLC",    310    },
                { "gasp",    320    },
                { "hdmx",    330    },
                { "kern",    340    },
                { "LTSH",    350	},
                { "PCLT",    360	},
                { "VDMX",    370    },
                { "sbix",    380	},
                { "acnt",    390	},
                { "avar",    400	},
                { "bdat",    410	},
                { "bloc",    420	},
                { "bsln",    430	},
                { "cvar",    440	},
                { "fdsc",    450	},
                { "feat",    460	},
                { "fmtx",    470	},
                { "gvar",    490	},
                { "hsty",    500	},
                { "just",    510	},
                { "lcar",    520	},
                { "mort",    530	},
                { "morx",    540	},
                { "opbd",    550	},
                { "prop",    560	},
                { "trak",    570	},
                { "Zapf",    580	},
                { "Silf",    590	},
                { "Glat",    600	},
                { "Gloc",    610	},
                { "Feat",    620	},
                { "Sill",    630	},
                { "DSIG",    640	},
            };
        }

        public static TypeFace Parse(string filePath, byte resolution)
        {
            var parser = new TTFParser(filePath, resolution);
            return parser.TypeFace;
        }
        
        public static TypeFace Parse(FontStreamReader fontReader, byte resolution, params TableDirectory[] tableDirectory)
        {
            var parser = new TTFParser(fontReader, resolution, tableDirectory);
            return parser.TypeFace;
        }

        protected TTFParser()
        {
            
        }

        protected TTFParser(string filePath, byte resolution)
        {
            Initialize(filePath, resolution);

            Parse();
        }
        
        protected TTFParser(FontStreamReader fontReader, byte resolution, params TableDirectory[] tableDirectories)
        {
            Initialize(fontReader, resolution, tableDirectories);
            
            ReadFontCollection();
        }

        protected void InitializeBase(string filePath, byte resolution)
        {
            TypeFace = new TypeFace();
            FilePath = filePath;
            Resolution = resolution > 0 ? resolution : (byte)1;
            ReadTables = new HashSet<long>();
        }

        protected virtual void Initialize(string filePath, byte resolution)
        {
            InitializeBase(filePath, resolution);
            TableDirectories = new List<TableDirectory>();
            FontReader = FilePath.LoadIntoStream();
        }

        protected virtual void Initialize(FontStreamReader fontReader, byte resolution, params TableDirectory[] tableDirectories)
        {
            InitializeBase(fontReader.FilePath, resolution);
            TableDirectories = new List<TableDirectory>(tableDirectories);
            FontReader = fontReader;
        }

        protected virtual void Parse()
        {
            // 1st step: read ttf file header, we need number of tables from here
            ReadTTFHeader();

            // 2nd step: make "name - table" mapping for all tables, we need name, offset and length from here
            MapTableDirectories();
            
            ReadFontCollection();
        }

        private void SortTables()
        {
            foreach (var tableDirectory in TableDirectories)
            {
                var dict = new Dictionary<int, TableEntry>();
                foreach (var table in tableDirectory.Tables)
                {
                    if (!SortingTable.ContainsKey(table.Name)) continue;

                    dict[SortingTable[table.Name]] = table;
                }
                var sortedTables = dict.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
                tableDirectory.Tables = sortedTables;
            }
        }

        protected virtual void ReadFontCollection()
        {
            SortTables();
            
            foreach (var tableDirectory in TableDirectories)
            {
                CurrentTableDirectory = tableDirectory;
                ReadTableDirectory(tableDirectory);
            }
            
            TypeFace.UpdateGlyphNames();
        }

        protected virtual void ReadTableDirectory(TableDirectory tableDirectory)
        {
            var font = new Font(TypeFace);
            TypeFace.AddFont(font);
            CurrentFont = font;

            foreach (var tableEntry in tableDirectory.Tables)
            {
                if (ReadTables.Contains(tableEntry.Offset))
                    continue;
                
                ReadTables.Add(tableEntry.Offset);
                
                switch (tableEntry.Name)
                {
                    case TableNames.head:
                        ReadHeadTable(tableEntry);
                        break;
                    case TableNames.maxp:
                        ReadMaximumProfileTable(tableEntry);
                        break;
                    case TableNames.name:
                        ReadNameTable(tableEntry);
                        break;
                    case TableNames.loca:
                        ReadGlyphIndexToLocationTable(tableEntry);
                        break;
                    case TableNames.cmap:
                        ReadCharToGlyphIndexTable(tableEntry);
                        break;
                    case TableNames.hhea:
                        ReadHorizontalHeaderTable(tableEntry);
                        break;
                    case TableNames.hmtx:
                        ReadHorizontalMetricsTable(tableEntry);
                        break;
                    case TableNames.glyf:
                        ReadTTFGlyphs(tableEntry);
                        break;
                    case TableNames.post:
                        ReadPostTable(tableEntry);
                        break;
                    case TableNames.kern:
                        ReadKerningTable(tableEntry);
                        break;
                    case TableNames.GPOS:
                        ReadGlyphPositioningTable(tableEntry);
                        break;
                    case TableNames.fvar:
                        ReadFvarTable(tableEntry);
                        break;
                    default:
                        ReadTable(tableEntry);
                        break;
                }
            }
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
                var nameRecord = name.NameRecords.FirstOrDefault(x => x.NameId == instance.SubfamilyNameID);

                if (nameRecord != null)
                {
                    FontReader.Position = nameTableOffset + name.StorageOffset + nameRecord.StringOffset;
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

        protected virtual void ReadTable(TableEntry entry)
        {
            
        }
        
        protected virtual void ReadPostTable(TableEntry entry)
        {
            FontReader.Position = entry.Offset;

            var version = FontReader.ReadUInt32();
            var italicAngle = FontReader.ReadUInt32();
            var underlinePosition = FontReader.ReadInt16();
            var underlineThickness = FontReader.ReadInt16();
            var isFixedPitch = FontReader.ReadUInt32(); // 0 is proportionally spaced, non-zero - font is monospaced

            // skip next 4 fields
            FontReader.Position += 16;

            var post = new PostTable();

            switch (version)
            {
                case 0x00010000:
                    post.Version = 1;
                    break;
                case 0x00030000:
                    post.Version = 3;
                    break;
                case 0x00020000:
                    post.Version = 2;
                    CurrentFont.isGlyphNamesProvided = true;
                    var glyphsNumber = FontReader.ReadUInt16();
                    var glyphNameIndices = FontReader.ReadUInt16Array(glyphsNumber);

                    var glyphNames = post.GlyphNames;
                    for (uint i = 0; i < glyphsNumber; ++i)
                    {
                        var glyphNameIndex = glyphNameIndices[i];
                        if (glyphNameIndex < 258)
                        {
                            glyphNames[i] = StandardGlyphNames[glyphNameIndex];
                        }
                        else
                        {
                            var length = FontReader.ReadByte();
                            var glyphName = FontReader.ReadString(length, Encoding.UTF8);
                            glyphNames[i] = glyphName;
                        }

                        var glyph = TypeFace.GetGlyphByIndex(i);
                        if (glyph != null)
                        {
                            glyph.Name = glyphNames[i];
                        }
                    }
                    break;
            }
        }

        private void ReadTTFHeader()
        {
            header = new TTFFileHeader();

            header.MajorVersion = FontReader.ReadUInt16();
            header.MinorVersion = FontReader.ReadUInt16();
            header.NumTables = FontReader.ReadUInt16();
            header.SearchRange = FontReader.ReadUInt16();
            header.EntrySelector = FontReader.ReadUInt16();
            header.RangeShift = FontReader.ReadUInt16();
        }

        private void MapTableDirectories()
        {
            var tableDirectory = new TableDirectory();
            TableDirectories.Add(tableDirectory);

            tableDirectory.Tables = new TableEntry[header.NumTables];
            // make "name - table" mapping for all tables, we need name, offset and length from here
            for (var i = 0; i < header.NumTables; ++i)
            {
                var entry = ReadTableEntry();
                tableDirectory.Tables[i] = entry;
                tableDirectory.TablesOffsets[entry.Name] = entry.Offset;
            }
        }

        private TableEntry ReadTableEntry()
        {
            TableEntry entry = new TableEntry();

            entry.Name = FontReader.ReadString(4);
            FontReader.Position -= 4;
            entry.Tag = FontReader.ReadUInt32();
            entry.CheckSum = FontReader.ReadUInt32();
            entry.Offset = FontReader.ReadUInt32();
            entry.Length = FontReader.ReadUInt32();

            return entry;
        }

        protected virtual void ReadHeadTable(TableEntry entry)
        {
            head = new HeadTable();
            
            FontReader.Position = entry.Offset;

            head.MajorVersion = FontReader.ReadUInt16();
            head.MinorVersion = FontReader.ReadUInt16();
            head.FontRevisionMaj = FontReader.ReadUInt16();
            head.FontRevisionMin = FontReader.ReadUInt16();
            head.CheckSumAdjustment = FontReader.ReadUInt32();
            head.MagicNumber = FontReader.ReadUInt32();
            head.Flags = FontReader.ReadUInt16();
            head.UnitsPerEm = FontReader.ReadUInt16();
            head.CreatedDate = FontReader.ReadInt64();
            head.ModifiedDate = FontReader.ReadInt64();
            head.XMin = FontReader.ReadInt16();
            head.YMin = FontReader.ReadInt16();
            head.XMax = FontReader.ReadInt16();
            head.YMax = FontReader.ReadInt16();
            head.MacStyle = FontReader.ReadUInt16();
            head.LowestRecPPEM = FontReader.ReadUInt16();
            head.FontDirectionHint = FontReader.ReadInt16();
            head.IndexToLocFormat = FontReader.ReadInt16();
            head.GlyphDataFormat = FontReader.ReadInt16();

            CurrentFont.UnitsPerEm = head.UnitsPerEm;
            CurrentFont.LowestRecPPEM = head.LowestRecPPEM;
            var originalDate = new DateTime(1904, 1, 1);
            var delta = new TimeSpan(head.CreatedDate);
            CurrentFont.Created = originalDate + delta;
            delta = new TimeSpan(head.ModifiedDate);
            CurrentFont.Modified = originalDate + delta;
        }

        protected virtual void ReadMaximumProfileTable(TableEntry entry)
        {
            maxp = new MaximumProfileTable();
            
            FontReader.Position = entry.Offset;

            maxp.MajorVersion = FontReader.ReadUInt16();
            maxp.MinorVersion = FontReader.ReadUInt16();
            maxp.NumGlyphs = FontReader.ReadUInt16();
            maxp.MaxPoints = FontReader.ReadUInt16();
            maxp.MaxContours = FontReader.ReadUInt16();
            maxp.MaxCompositePoints = FontReader.ReadUInt16();
            maxp.MaxCompositeContours = FontReader.ReadUInt16();
            maxp.MaxZones = FontReader.ReadUInt16();
            maxp.MaxTwilightPoints = FontReader.ReadUInt16();
            maxp.MaxStorage = FontReader.ReadUInt16();
            maxp.MaxFunctionDefs = FontReader.ReadUInt16();
            maxp.MaxInstructionDefs = FontReader.ReadUInt16();
            maxp.MaxStackElements = FontReader.ReadUInt16();
            maxp.MaxSizeOfInstructions = FontReader.ReadUInt16();
            maxp.MaxComponentElements = FontReader.ReadUInt16();
            maxp.MaxComponentDepth = FontReader.ReadUInt16();
        }
        
        protected virtual void ReadNameTable(TableEntry entry)
        {
            FontReader.Position = entry.Offset;
            var nameTable = new NameTable();
            nameTable.Version = FontReader.ReadUInt16();
            nameTable.Count = FontReader.ReadUInt16();
            nameTable.StorageOffset = FontReader.ReadUInt16();
            nameTable.NameRecords = new NameRecord[nameTable.Count];
            
            for (int i = 0; i < nameTable.Count; ++i)
            {
                var record = new NameRecord();
                record.PlatformId = FontReader.ReadUInt16();
                record.EncodingId = FontReader.ReadUInt16();
                record.LanguageId = FontReader.ReadUInt16();
                record.NameId = FontReader.ReadUInt16();
                record.Length = FontReader.ReadUInt16();
                record.StringOffset = FontReader.ReadUInt16();
                nameTable.NameRecords[i] = record;
            }

            if (nameTable.Version == 1)
            {
                nameTable.LangTagCount = FontReader.ReadUInt16();
                nameTable.LangTagRecords = new LangTagRecord[nameTable.LangTagCount];

                for (int i = 0; i < nameTable.LangTagCount; ++i)
                {
                    var langRecord = new LangTagRecord();
                    langRecord.Length = FontReader.ReadUInt16();
                    langRecord.LangTagOffset = FontReader.ReadUInt16();
                }
            }

            foreach (var nameRecord in nameTable.NameRecords)
            {
                FontReader.Position = entry.Offset + nameTable.StorageOffset + nameRecord.StringOffset;
                var encoding = nameRecord.EncodingId is 3 or 1 ? Encoding.BigEndianUnicode : Encoding.UTF8;

                var str = FontReader.ReadString(nameRecord.Length, encoding);

                switch ((NameIdKind)nameRecord.NameId)
                {
                    case NameIdKind.Copyright:
                        CurrentFont.Copyright = str;
                        break;
                    case NameIdKind.FontFamily:
                        CurrentFont.FontFamily = str;
                        break;
                    case NameIdKind.FontSubfamily:
                        CurrentFont.FontSubfamily = str;
                        break;
                    case NameIdKind.UniqueFontId:
                        CurrentFont.UniqueId = str;
                        break;
                    case NameIdKind.FullFontName:
                        CurrentFont.FullName = str;
                        break;
                    case NameIdKind.VersionString:
                        CurrentFont.Version = str;
                        break;
                    case NameIdKind.Trademark:
                        CurrentFont.Trademark = str;
                        break;
                    case NameIdKind.Manufacturer:
                        CurrentFont.Manufacturer = str;
                        break;
                    case NameIdKind.Designer:
                        CurrentFont.Designer = str;
                        break;
                    case NameIdKind.Description:
                        CurrentFont.Description = str;
                        break;
                    case NameIdKind.VendorUrl:
                        CurrentFont.VendorUrl = str;
                        break;
                    case NameIdKind.DesignerUrl:
                        CurrentFont.DesignerUrl = str;
                        break;
                    case NameIdKind.LicenseDescription:
                        CurrentFont.LicenseDescription = str;
                        break;
                    case NameIdKind.LicenseInfoUrl:
                        CurrentFont.LicenseInfoUrl = str;
                        break;
                    case NameIdKind.TypographicFamilyName:
                        CurrentFont.TypographicFamilyName = str;
                        break;
                    case NameIdKind.TypographicSubfamilyName:
                        CurrentFont.TypographicSubfamilyName = str;
                        break;
                    case NameIdKind.WwsFamilyName:
                        CurrentFont.WwsFamilyName = str;
                        break;
                    case NameIdKind.WwsSubfamilyName:
                        CurrentFont.WwsSubfamilyName = str;
                        break;
                    case NameIdKind.LightBackgroundPalette:
                        CurrentFont.LightBackgroundPalette = str;
                        break;
                    case NameIdKind.DarkBackgroundPalette:
                        CurrentFont.DarkBackgroundPalette = str;
                        break;
                }
            }

            name = nameTable;
        }

        protected virtual void ReadGlyphIndexToLocationTable(TableEntry entry)
        {
            loca = new TTFIndexToLocationTable();
            
            FontReader.Position = entry.Offset;

            loca.GlyphOffsets = new UInt32[maxp.NumGlyphs + 1]; // + 'end-of-glyphs' entry

            if (head.IndexToLocFormat == 0)
            {
                for (var i = 0; i < maxp.NumGlyphs + 1; ++i)
                {
                    var glyphIndex = FontReader.ReadUInt16();
                    loca.GlyphOffsets[i] = glyphIndex * 2u;
                }
            }
            else
            {
                for (var i = 0; i < maxp.NumGlyphs + 1; ++i)
                {
                    loca.GlyphOffsets[i] = FontReader.ReadUInt32();
                }
            }
        }

        // Cmap
        protected virtual void ReadCharToGlyphIndexTable(TableEntry entry)
        {
            CharacterToGlyphTable cmap = new CharacterToGlyphTable();
            FontReader.Position = entry.Offset;
            cmap.Version = FontReader.ReadUInt16();
            var numTables = FontReader.ReadUInt16();
            var encodings = new EncodingRecord[numTables];
            for (var index = 0; index < encodings.Length; index++)
            {
                encodings[index] = ReadEncodingRecord();
            }

            cmap.CharacterMaps = new CharacterMap[numTables];

            for (var index = 0; index < encodings.Length; index++)
            {
                var encoding = encodings[index];
                FontReader.Position = entry.Offset + encoding.SubtableOffset;
                var format = FontReader.ReadUInt16();
                CharacterMap map = ReadCharacterMap(format);
                map.PlatformId = encoding.PlatformId;
                map.EncodingId = encoding.EncodingId;

                cmap.CharacterMaps[index] = map;
            }
            
            cmap.CollectUnicodeToGlyphMappings();

            var glyphsList = new List<Glyph>();

            foreach (var glyphPair in cmap.GlyphToUnicode)
            {
                var glyph = TypeFace.GetGlyphByIndex(glyphPair.Key);
                glyph.SetUnicodes(glyphPair.Value);
                glyphsList.Add(glyph);
            }
            
            CurrentFont.SetGlyphs(glyphsList);
            var font = CurrentFont as IFont;
            font.SetGlyphUnicodes(cmap.GlyphToUnicode);
        }

        private CharacterMap ReadCharacterMap(ushort format)
        {
            switch (format)
            {
                case 0:
                    return FontReader.ReadCharacterMapFormat0();
                case 2:
                    return FontReader.ReadCharacterMapFormat2();
                case 4:
                    return FontReader.ReadCharacterMapFormat4();
                case 6:
                    return FontReader.ReadCharacterMapFormat6();
                case 8:
                    return FontReader.ReadCharacterMapFormat8();
                case 10:
                    return FontReader.ReadCharacterMapFormat10();
                case 12:
                    return FontReader.ReadCharacterMapFormat12();
                case 13:
                    return FontReader.ReadCharacterMapFormat13();
                case 14:
                    return FontReader.ReadCharacterMapFormat14();
                default:
                    throw new NotSupportedException($"Format {format} is not supported");
            }
        }

        private EncodingRecord ReadEncodingRecord()
        {
            var record = new EncodingRecord();
            record.PlatformId = FontReader.ReadUInt16();
            record.EncodingId = FontReader.ReadUInt16();
            record.SubtableOffset = FontReader.ReadUInt32();
            return record;
        }

        protected virtual void ReadHorizontalHeaderTable(TableEntry entry)
        {
            hhea = new HorizontalHeaderTable();

            FontReader.Position = entry.Offset;

            hhea.MajorVersion = FontReader.ReadUInt16();
            hhea.MinorVersion = FontReader.ReadUInt16();
            hhea.Ascender = FontReader.ReadInt16();
            hhea.Descender = FontReader.ReadInt16();
            hhea.LineGap = FontReader.ReadInt16();
            hhea.AdvanceWidthMax = FontReader.ReadUInt16();
            hhea.MinLeftSideBearing = FontReader.ReadInt16();
            hhea.MinRightSideBearing = FontReader.ReadInt16();
            hhea.XMaxExtent = FontReader.ReadInt16();
            hhea.CaretSlopeRise = FontReader.ReadInt16();
            hhea.CaretSlopeRun = FontReader.ReadInt16();
            hhea.CaretOffset = FontReader.ReadInt16();

            // skip 4 reserved int16 fields
            FontReader.Position += sizeof(Int16) * 4;

            hhea.MetricDataFormat = FontReader.ReadInt16();
            hhea.NumberOfHMetrics = FontReader.ReadUInt16();

            CurrentFont.LineSpace = hhea.Ascender - hhea.Descender + hhea.LineGap;
        }

        protected virtual void ReadHorizontalMetricsTable(TableEntry entry)
        {
            hmtx = new HorizontalMetricsTable();
            
            FontReader.Position = entry.Offset;

            ushort lastAdvanceWidth = 0;

            hmtx.AdvanceWidths = new ushort[maxp.NumGlyphs];
            hmtx.LeftSideBearings = new short[maxp.NumGlyphs];

            for (var i = 0; i < maxp.NumGlyphs; ++i)
            {
                if (i < hhea.NumberOfHMetrics)
                {
                    var advanceWidth = FontReader.ReadUInt16();
                    hmtx.AdvanceWidths[i] = advanceWidth;

                    lastAdvanceWidth = advanceWidth; // last advanceWidth entry is propagated for all glyph indexes beyond 'numberOfHMetrics' count
                }
                else
                {
                    hmtx.AdvanceWidths[i] = lastAdvanceWidth;
                }

                var leftSideBearing = FontReader.ReadInt16();
                hmtx.LeftSideBearings[i] = leftSideBearing;
                var glyph = TypeFace.GetGlyphByIndex((uint)i);
                glyph.AdvanceWidth = lastAdvanceWidth;
                glyph.LeftSideBearing = leftSideBearing;
            }
        }

        protected virtual void ReadTTFGlyphs(TableEntry entry)
        {
            var glyphs = new Glyph[maxp.NumGlyphs];
            for (uint i = 0; i < glyphs.Length; ++i)
            {
                var glyph = new Glyph(i) {OutlineType = OutlineType.TrueType};
                glyphs[i] = glyph;
            }
            
            TypeFace.SetGlyphs(glyphs);

            for (ushort i = 0; i < maxp.NumGlyphs; ++i)
            {
                ReadGlyphComponentData(entry.Offset, i);
            }

            var compositeGlyphs = TypeFace.Glyphs.Where(x => x.IsComposite).ToArray();
            Parallel.ForEach(compositeGlyphs, FillCompositeGlyphGeometry);
        }

        private void ReadGlyphComponentData(Int64 glyfTableOffset, UInt16 glyphIndex)
        {
            var glyph = TypeFace.GetGlyphByIndex(glyphIndex);
            // if offset for current glyph is equal to offset for the next glyph, then current glyph has no outline, skip all other steps, but the glyph considers as loaded
            if (loca.GlyphOffsets[glyphIndex] == loca.GlyphOffsets[glyphIndex + 1])
            {
                TypeFace.AddErrorMessage($"[WARN] Offsets for indices {glyphIndex} and {glyphIndex + 1} are equal - current glyph has no outline");
                return;
            }

            // if offset for current glyph is equal to "end of table" - this is invalid, skip all other steps, glyph considers as not loaded
            if (loca.GlyphOffsets[glyphIndex] == loca.GlyphOffsets[maxp.NumGlyphs])
            {
                TypeFace.AddErrorMessage($"[ERR] Offset for index {glyphIndex} is equal to 'end-of-table'");
                glyph.IsInvalid = true;
                return;
            }

            FontReader.Position = glyfTableOffset + loca.GlyphOffsets[glyphIndex];

            var glyphHeader = ReadTTFGlyphHeader();
            glyph.BoundingRectangle =
                Rectangle.FromCorners(glyphHeader.XMin, glyphHeader.YMin, glyphHeader.XMax, glyphHeader.YMax);

            if (glyphHeader.NumberOfContours >= 0) // simple glyph
            {
                ReadSimpleGlyphComponentData(glyph, glyphHeader.NumberOfContours);
            }
            else // composite glyph
            {
                ReadTTFCompositeGlyphComponentData(glyph);
            }
        }

        private void ReadSimpleGlyphComponentData(Glyph glyph, Int32 numberOfContours)
        {
            // get the 'contour ends' array
            UInt16[] endPtsOfContours = new ushort[numberOfContours];

            for (var j = 0; j < numberOfContours; ++j)
            {
                endPtsOfContours[j] = FontReader.ReadUInt16();
                // calc num of points for current contour
                var numberOfPoints = (ushort)(endPtsOfContours[j] - (j != 0 ? endPtsOfContours[j - 1] : -1));
                glyph.AddOutline(new Outline() {NumberOfPoints = numberOfPoints});
            }

            // skip instructions
            var instructionLength = FontReader.ReadUInt16();
            glyph.SetInstructions(FontReader.ReadBytes(instructionLength));

            // calc number of points for entire glyph component
            var numberOfPointsInGlyph = (ushort)(endPtsOfContours[numberOfContours - 1] + 1);

            // read all flags
            TTFGlyphPointFlag[] flags = ReadGlyphComponentFlags(numberOfPointsInGlyph);

            // read all 'x' coordinates
            Int16[] xCoords = new short[numberOfPointsInGlyph];
            for (var j = 0; j < numberOfPointsInGlyph; ++j)
            {
                if (flags[j].XShort)
                {
                    byte shortXCoord = FontReader.ReadByte();
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
                        xCoords[j] = FontReader.ReadInt16();

                        // apply delta to obtain absolute coordinates(coordinates of current point stored as deltas to previous point, or to(0, 0) if this is the first point)
                        if (j > 0)
                        {
                            xCoords[j] += xCoords[j - 1];
                        }
                    }
                }
            }

            // read all 'y' coordinates
            Int16[] yCoords = new short[numberOfPointsInGlyph];
            for (var j = 0; j < numberOfPointsInGlyph; ++j)
            {
                if (flags[j].YShort)
                {
                    byte shortYCoord = FontReader.ReadByte();
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
                        yCoords[j] = FontReader.ReadInt16();

                        // apply delta to obtain absolute coordinates(coordinates of current point stored as deltas to previous point, or to(0, 0) if this is the first point)
                        if (j > 0)
                        {
                            yCoords[j] += yCoords[j - 1];
                        }
                    }
                }
            }

            int currentPointIndex = 0;
            foreach (var outline in glyph.Outlines)
            {
                for (int i = 0; i < outline.NumberOfPoints; ++i)
                {
                    var outlinePoint = new OutlinePoint(xCoords[currentPointIndex], yCoords[currentPointIndex],
                        !flags[currentPointIndex].OnCurve);
                    outline.Points.Add(outlinePoint);
                    currentPointIndex++;
                }
            }
            
            glyph.Sample(Resolution);

        }

        protected bool ReadTTFCompositeGlyphComponentData(Glyph glyph, bool readInstructions = true)
        {
            TTFGlyphCompositeFlag compositeFlag;
            glyph.IsComposite = true;

            do
            {
                var compositeGlyphComponent = new CompositeGlyphComponent();
                
                compositeFlag = ReadGlyphCompositeFlag();
                var glyphIndexInComposite = FontReader.ReadUInt16();

                compositeGlyphComponent.SimpleGlyphIndex = glyphIndexInComposite;
                var matrix = compositeGlyphComponent.TransformMatrix;

                Int16 arg1Word = 0;
                Int16 arg2Word = 0;
                sbyte arg1Byte = 0;
                sbyte arg2Byte = 0;

                if (compositeFlag.Arg1And2AreWords)
                {
                    arg1Word = FontReader.ReadInt16();
                    arg2Word = FontReader.ReadInt16();
                }
                else
                {
                    arg1Byte = (sbyte)FontReader.ReadByte();
                    arg2Byte = (sbyte)FontReader.ReadByte();
                }

                if (compositeFlag.WeHaveAScale)
                {
                    Int16 xyScale = FontReader.ReadInt16();

                    matrix.M11 = xyScale.FromF2Dot14();
                    matrix.M22 = xyScale.FromF2Dot14();
                }
                else if (compositeFlag.WeHaveAnXAndYScale)
                {
                    Int16 xScale = FontReader.ReadInt16();
                    Int16 yScale = FontReader.ReadInt16();

                    matrix.M11 = xScale.FromF2Dot14();
                    matrix.M22 = yScale.FromF2Dot14();
                }
                else if (compositeFlag.WeHaveATwoByTwo)
                {
                    Int16 xScale = FontReader.ReadInt16();
                    Int16 scale01 = FontReader.ReadInt16();
                    Int16 scale10 = FontReader.ReadInt16();
                    Int16 yScale = FontReader.ReadInt16();

                    matrix.M11 = xScale.FromF2Dot14();
                    matrix.M12 = scale01.FromF2Dot14();
                    matrix.M21 = scale10.FromF2Dot14();
                    matrix.M22 = yScale.FromF2Dot14();
                }

                if (compositeFlag.ArgsAreXYValues) // matched points == false
                {
                    matrix.M31 = compositeFlag.Arg1And2AreWords ? arg1Word : arg1Byte;
                    matrix.M32 = compositeFlag.Arg1And2AreWords ? arg2Word : arg2Byte;

                    if (compositeFlag.ScaledComponentOffset) // x and y offset values are considered to be in the component glyph’s coordinate system, and the scale transformation is applied to both values
                    {
                        matrix.M31 = compositeFlag.Arg1And2AreWords ? arg1Word : arg1Byte;
                        matrix.M32 = compositeFlag.Arg1And2AreWords ? arg2Word : arg2Byte;
                    }
                }

                compositeGlyphComponent.TransformMatrix = matrix;

                // skip instructions in case we reconstructing glyph table from WOFF2 format
                if (compositeFlag.WeHaveInstructions && readInstructions)
                {
                    ushort numberOfInstructions = FontReader.ReadUInt16();
                    glyph.SetInstructions(FontReader.ReadBytes(numberOfInstructions));
                }

                if (!compositeFlag.ArgsAreXYValues) // matched points == true, unsupported
                {
                    TypeFace.AddErrorMessage($"[ERR] Unsupported matched points in composite glyph {glyph.Index}");
                    glyph.IsInvalid = true;
                }

                glyph.CompositeGlyphComponents.Add(compositeGlyphComponent);
            } while (compositeFlag.MoreComponents);

            return compositeFlag.WeHaveInstructions;
        }

        protected TTFGlyphCompositeFlag ReadGlyphCompositeFlag()
        {
            ushort rawFlag = FontReader.ReadUInt16();
            return ParseRawGlyphCompositeFlag(rawFlag);
        }

        private TTFGlyphCompositeFlag ParseRawGlyphCompositeFlag(ushort rawFlag)
        {
            TTFGlyphCompositeFlag flag = new TTFGlyphCompositeFlag
            {
                Arg1And2AreWords = Convert.ToBoolean(rawFlag & 0x0001),
                ArgsAreXYValues = Convert.ToBoolean(rawFlag & 0x0002),
                WeHaveAScale = Convert.ToBoolean(rawFlag & 0x0008),
                MoreComponents = Convert.ToBoolean(rawFlag & 0x0020),
                WeHaveAnXAndYScale = Convert.ToBoolean(rawFlag & 0x0040),
                WeHaveATwoByTwo = Convert.ToBoolean(rawFlag & 0x0080),
                WeHaveInstructions = Convert.ToBoolean(rawFlag & 0x0100),
                ScaledComponentOffset = Convert.ToBoolean(rawFlag & 0x0800)
            };


            return flag;
        }

        private TTFGlyphPointFlag[] ReadGlyphComponentFlags(ushort numberOfPoints)
        {
            byte numberOfRepeats = 0;

            var flags = new TTFGlyphPointFlag[numberOfPoints];

            for (ushort i = 0; i < numberOfPoints; ++i)
            {
                if (numberOfRepeats == 0)
                {
                    var rawFlag = FontReader.ReadByte();
                    flags[i] = ParseRawGlyphPointFlag(rawFlag);

                    if (flags[i].Repeat)
                    {
                        numberOfRepeats = FontReader.ReadByte();
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

        private TTFGlyphPointFlag ParseRawGlyphPointFlag(byte rawFlag)
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

            glyphHeader.NumberOfContours = FontReader.ReadInt16();
            glyphHeader.XMin = FontReader.ReadInt16();
            glyphHeader.YMin = FontReader.ReadInt16();
            glyphHeader.XMax = FontReader.ReadInt16();
            glyphHeader.YMax = FontReader.ReadInt16();

            return glyphHeader;
        }

        protected virtual void ReadKerningTable(TableEntry entry)
        {
            FontReader.Position = entry.Offset;
            
            kern = new KerningTable();
            kern.Version = FontReader.ReadUInt16();
            kern.NumTables = FontReader.ReadUInt16();

            kern.Subtables = new KerningSubtable[kern.NumTables];

            long nextSubtableOffset = FontReader.Position;

            for (ushort i = 0; i < kern.NumTables; ++i)
            {
                if (i > 0)
                {
                    // advance to next subtable
                    nextSubtableOffset += kern.Subtables[i - 1].Length;
                    FontReader.Position = nextSubtableOffset;
                }

                var kernSubtable = new KerningSubtable();

                kernSubtable.Version = FontReader.ReadUInt16();
                kernSubtable.Length = FontReader.ReadUInt16();
                ushort rawCoverage = FontReader.ReadUInt16();

                ParseTTFCoverage(rawCoverage, kernSubtable);

                if (kernSubtable.Format != 0) // Windows supports only format 0
                {
                    kern.Subtables[i] = kernSubtable;
                    continue;
                }

                kernSubtable.NumberOfPairs = FontReader.ReadUInt16();
                kernSubtable.SearchRange = FontReader.ReadUInt16();
                kernSubtable.EntrySelector = FontReader.ReadUInt16();
                kernSubtable.RangeShift = FontReader.ReadUInt16();

                for (ushort j = 0; j < kernSubtable.NumberOfPairs; ++j)
                {
                    ushort left = FontReader.ReadUInt16();
                    ushort right = FontReader.ReadUInt16();

                    UInt32 key = GenerateKerningKey(left, right);
                    kernSubtable.KerningValues[key] = FontReader.ReadInt16();
                }

                kern.Subtables[i] = kernSubtable;
                
                // TODO: define how to process kern data
                //FontData.KerningData = kernSubtable;
            }
        }

        protected virtual void ReadGlyphPositioningTable(TableEntry entry)
        {
            FontReader.Position = entry.Offset;
            
            var gpos = new GlyphPositioningTable();
            gpos.MajorVersion = FontReader.ReadUInt16();
            gpos.MinorVersion = FontReader.ReadUInt16();
            gpos.ScriptListOffset = FontReader.ReadUInt16();
            gpos.FeatureListOffset = FontReader.ReadUInt16();
            gpos.LookupListOffset = FontReader.ReadUInt16();

            if (gpos.MinorVersion == 1)
            {
                gpos.FeatureVariationsOffset = FontReader.ReadUInt16();
            }
            
            var scriptListOffset = entry.Offset + gpos.ScriptListOffset;
            var featureListOffset = entry.Offset + gpos.FeatureListOffset;
            var lookupListOffset = entry.Offset + gpos.LookupListOffset;

            var scriptList = FontReader.ReadScriptList(scriptListOffset);

            var featureList = FontReader.ReadFeatureList(featureListOffset);

            var lookupTable = FontReader.ReadLookupListTable(lookupListOffset);
        }

        
        private void ParseTTFCoverage(ushort rawCoverage, KerningSubtable kerningSubtable)
        {
            kerningSubtable.Horizontal = Convert.ToBoolean(rawCoverage & 0x0001);
            kerningSubtable.Minimum = Convert.ToBoolean(rawCoverage & 0x0002);
            kerningSubtable.CrossStream = Convert.ToBoolean(rawCoverage & 0x0004);
            kerningSubtable.Override = Convert.ToBoolean(rawCoverage & 0x0008);
            ushort format = (ushort)(rawCoverage & 0xFF00);
            format >>= 8; // fit 8-15 bits of ushort into byte
            kerningSubtable.Format = (byte)format;
        }

        private void FillCompositeGlyphGeometry(Glyph compositeGlyph)
        {
            var sampledOutlinesList = new List<SampledOutline>();
            foreach (var component in compositeGlyph.CompositeGlyphComponents)
            {
                var sampleOutlines = TypeFace.GetGlyphByIndex(component.SimpleGlyphIndex)
                    .TransformOutlines(component.TransformMatrix, Resolution);
                sampledOutlinesList.AddRange(sampleOutlines);
            }
            compositeGlyph.SetOutlinesForRate(Resolution, sampledOutlinesList.ToArray());
        }

        public static UInt32 GenerateKerningKey(ushort leftIndex, ushort rightIndex)
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
