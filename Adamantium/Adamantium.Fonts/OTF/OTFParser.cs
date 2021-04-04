using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.OTF
{
    internal class OTFParser : IFontParser
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

        private CFFFont cffFont;

        private readonly HashSet<UInt32> uniqueOffsets;
        
        public TypeFace TypeFace { get; }
        
        private static List<string> standardGlyphNames;

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

            standardGlyphNames = new List<string>()
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
        }

        public OTFParser(string filePath, UInt32 resolution = 0)
        {
            tableDirectories = new List<TableDirectory>();
            this.filePath = filePath;
            this.resolution = resolution;

            uniqueOffsets = new HashSet<uint>();

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
                var font = new Font();
                TypeFace.AddFont(font);
                switch (tableDirectory.OutlineType)
                {
                    case OutlineType.CompactFontFormat:
                        ParseCFF(tableDirectory);
                        break;
                    default:
                        ParseTTF(tableDirectory);
                        break;
                }

                ReadNameTable(tableDirectory, font);
                ReadPostTable(tableDirectory);
                ReadCmapTable(tableDirectory, font);
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

        private void ParseTTF(TableDirectory tableDirectory)
        {
            
        }

        private void ParseCFF(TableDirectory tableDirectory)
        {
            DetermineCFFVersion(tableDirectory);
            bool shouldParse = true; 

            if (CFFVersion == OTF.CFFVersion.CFF)
            {
                var offset = tableDirectory.TablesOffsets[cffMandatoryTables["CFF"]];
                shouldParse = uniqueOffsets.Add(offset);
                cffParser = new CFFParser(offset, otfReader);
            }
            else if (CFFVersion == OTF.CFFVersion.CFF2)
            {
                var offset = tableDirectory.TablesOffsets[cffMandatoryTables["CFF2"]];
                shouldParse = uniqueOffsets.Add(offset);
                cffParser = new CFFParser(offset, otfReader);
            }
            
            if (!shouldParse)
                return;

            cffFont = cffParser.Parse();
        }

        private void ReadNameTable(TableDirectory tableDirectory, Font font)
        {
            var offset = tableDirectory.TablesOffsets["name"];
            otfReader.Position = offset;
            var nameTable = new NameTable();
            nameTable.Version = otfReader.ReadUInt16();
            nameTable.Count = otfReader.ReadUInt16();
            nameTable.StorageOffset = otfReader.ReadUInt16();
            nameTable.NameRecords = new NameRecord[nameTable.Count];
            
            for (int i = 0; i < nameTable.Count; ++i)
            {
                var record = new NameRecord();
                record.PlatformId = otfReader.ReadUInt16();
                record.EncodingId = otfReader.ReadUInt16();
                record.LanguageId = otfReader.ReadUInt16();
                record.NameId = otfReader.ReadUInt16();
                record.Length = otfReader.ReadUInt16();
                record.StringOffset = otfReader.ReadUInt16();
                nameTable.NameRecords[i] = record;
            }

            if (nameTable.Version == 1)
            {
                nameTable.LangTagCount = otfReader.ReadUInt16();
                nameTable.LangTagRecords = new LangTagRecord[nameTable.LangTagCount];

                for (int i = 0; i < nameTable.LangTagCount; ++i)
                {
                    var langRecord = new LangTagRecord();
                    langRecord.Length = otfReader.ReadUInt16();
                    langRecord.LangTagOffset = otfReader.ReadUInt16();
                }
            }

            foreach (var nameRecord in nameTable.NameRecords)
            {
                otfReader.Position = offset + nameTable.StorageOffset + nameRecord.StringOffset;
                Encoding encoding;
                if (nameRecord.EncodingId == 3 || nameRecord.EncodingId == 1)
                {

                    encoding = Encoding.BigEndianUnicode;
                }
                else
                {
                    encoding = Encoding.UTF8;
                }

                var str = otfReader.ReadString(nameRecord.Length, encoding);

                switch ((NameIdKind)nameRecord.NameId)
                {
                    case NameIdKind.Copyright:
                        font.Copyright = str;
                        break;
                    case NameIdKind.FontFamily:
                        font.FontFamily = str;
                        break;
                    case NameIdKind.FontSubfamily:
                        font.FontSubfamily = str;
                        break;
                    case NameIdKind.UniqueFontId:
                        font.UniqueId = str;
                        break;
                    case NameIdKind.FullFontName:
                        font.FullName = str;
                        break;
                    case NameIdKind.VersionString:
                        font.Version = str;
                        break;
                    case NameIdKind.Trademark:
                        font.Trademark = str;
                        break;
                    case NameIdKind.Manufacturer:
                        font.Manufacturer = str;
                        break;
                    case NameIdKind.Designer:
                        font.Designer = str;
                        break;
                    case NameIdKind.Description:
                        font.Description = str;
                        break;
                    case NameIdKind.VendorUrl:
                        font.VendorUrl = str;
                        break;
                    case NameIdKind.DesignerUrl:
                        font.DesignerUrl = str;
                        break;
                    case NameIdKind.LicenseDescription:
                        font.LicenseDescription = str;
                        break;
                    case NameIdKind.LicenseInfoUrl:
                        font.LicenseInfoUrl = str;
                        break;
                    case NameIdKind.TypographicFamilyName:
                        font.TypographicFamilyName = str;
                        break;
                    case NameIdKind.TypographicSubfamilyName:
                        font.TypographicSubfamilyName = str;
                        break;
                    case NameIdKind.WwsFamilyName:
                        font.WwsFamilyName = str;
                        break;
                    case NameIdKind.WwsSubfamilyName:
                        font.WwsSubfamilyName = str;
                        break;
                    case NameIdKind.LightBackgroundPalette:
                        font.LightBackgroundPalette = str;
                        break;
                    case NameIdKind.DarkBackgroundPalette:
                        font.DarkBackgroundPalette = str;
                        break;
                }
            }
        }

        private void ReadPostTable(TableDirectory tableDirectory)
        {
            var offset = tableDirectory.TablesOffsets["post"];
            if (uniqueOffsets.Contains(offset))
                return;

            uniqueOffsets.Add(offset);
            otfReader.Position = offset;

            var version = otfReader.ReadUInt32();
            var italicAngle = otfReader.ReadUInt32();
            var underlinePosition = otfReader.ReadInt16();
            var underlineThickness = otfReader.ReadInt16();
            var isFixedPitch = otfReader.ReadUInt32(); // 0 is proportionally spaced, non-zero - font is monospaced

            // skip next 4 fields
            otfReader.Position += 16;

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

                    var glyphsNumber = otfReader.ReadUInt16();
                    var glyphNameIndices = otfReader.ReadUInt16Array(glyphsNumber);

                    var glyphNames = new Dictionary<UInt32, string>();
                    for (uint i = 0; i < glyphsNumber; ++i)
                    {
                        var glyphNameIndex = glyphNameIndices[i];
                        if (glyphNameIndex < 258)
                        {
                            glyphNames[i] = standardGlyphNames[glyphNameIndex];
                        }
                        else
                        {
                            var length = otfReader.ReadByte();
                            var glyphName = otfReader.ReadString(length, Encoding.UTF8);
                            glyphNames[i] = glyphName;
                        }
                    }
                    break;
            }
        }

        private void ReadCmapTable(TableDirectory tableDirectory, Font font)
        {
            var offset = tableDirectory.TablesOffsets["cmap"];

            uniqueOffsets.Add(offset);
            
            CMapTable cmap = new CMapTable();
            otfReader.Position = offset;
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
                otfReader.Position = offset + encoding.SubtableOffset;
                var format = otfReader.ReadUInt16();
                CharacterMap map = ReadCharacterMap(format);
                map.PlatformId = encoding.PlatformId;
                map.EncodingId = encoding.EncodingId;

                cmap.CharacterMaps[index] = map;
            }
            
            cmap.CollectUnicodeToGlyphMappings();

            var glyphList = new List<Glyph>();
            foreach (var glyphPair in cmap.GlyphToUnicode)
            {
                var glyph = cffFont.GetGlyphByIndex(glyphPair.Key);
                glyph.SetUnicodes(glyphPair.Value);
                glyphList.Add(glyph);
            }
            
            font.SetGlyphs(glyphList);

            // foreach (var glyph in font.Glyphs)
            // {
            //     glyph.Unicodes.AddRange(cmap.GetUnicodesByGlyphId(glyph.Index));
            // }
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
