using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Extensions;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.WOFF;
using ICSharpCode.SharpZipLib.Zip.Compression;

namespace Adamantium.Fonts.Parsers
{
    internal class WoffParser : IFontParser
    {
        private string filePath;
        private FontStreamReader reader;
        private byte resolution;

        private WoffHeader header;
        private WoffTableDirectory tableDirectory;
        private List<WoffTable> tables;
        
        private WoffParser(string filePath, byte resolution = 1)
        {
            this.filePath = filePath;
            this.resolution = resolution;
            reader = filePath.LoadIntoStream();
            
            Parse();
        }

        internal static TypeFace Parse(string filePath, byte resolution = 1)
        {
            var parser = new WoffParser(filePath, resolution);
            return parser.TypeFace;
        }

        public TypeFace TypeFace { get; private set; }
        
        private void Parse()
        {
            ReadWoffHeader();
            ReadTableDirectory();
            DecompressTables();
        }

        private void ReadWoffHeader()
        {
            header = new WoffHeader();
            header.Signature = reader.ReadUInt32();
            header.Flavor = reader.ReadUInt32();
            header.Length = reader.ReadUInt32();
            header.NumTables = reader.ReadUInt16();
            header.Reserved = reader.ReadUInt16();
            header.TotalSfntSize = reader.ReadUInt32();
            header.MajorVersion = reader.ReadUInt16();
            header.MinorVersion = reader.ReadUInt16();
            header.MetaOffset = reader.ReadUInt32();
            header.MetaLength = reader.ReadUInt32();
            header.MetaOrigLength = reader.ReadUInt32();
            header.PrivOffset = reader.ReadUInt32();
            header.PrivLength = reader.ReadUInt32();
        }

        private void ReadTableDirectory()
        {
            long expectedOffset = 0;
            tableDirectory = new WoffTableDirectory();
            tables = new List<WoffTable>();
            
            // table order matters. According to spec, tables are is ascending order,
            // so we can easily calculate offset of each table in decompressed state
            for (int i = 0; i < header.NumTables; ++i)
            {
                var table = new WoffTable
                {
                    Tag = reader.ReadString(4, Encoding.UTF8),
                    Offset = reader.ReadUInt32(),
                    CompLength = reader.ReadUInt32(),
                    OrigLength = reader.ReadUInt32(),
                    OrigChecksum = reader.ReadUInt32(),
                    ExpectedOffset = expectedOffset
                };

                tableDirectory.Tables[table.Tag] = table;

                expectedOffset += table.OrigLength;
                
                tables.Add(table);
            }
        }

        private void DecompressTables()
        {
            using (var decompressedStream = new FontStreamReader())
            {
                foreach (var table in tables)
                {
                    reader.Position = table.Offset;
                    var compressedBuffer = reader.ReadBytes(table.CompLength, true);
                    if (compressedBuffer.Length == table.OrigLength)
                    {
                        decompressedStream.Write(compressedBuffer);
                    }
                    else
                    {
                        var decompressedBuffer = new byte[table.OrigLength];
                        DecompressWoff(compressedBuffer, decompressedBuffer);
                        decompressedStream.Write(decompressedBuffer);
                    }
                }
                
                decompressedStream.Position = 0;

                var otfTableDirectory = new TableDirectory();
                if (header.Flavor == 0x00010000)
                {
                    otfTableDirectory.OutlineType = OutlineType.TrueType;
                }
                else if (header.Flavor == 0x4F54544F)
                {
                    otfTableDirectory.OutlineType = OutlineType.CompactFontFormat;
                }

                otfTableDirectory.NumTables = header.NumTables;
                otfTableDirectory.Tables = new TableEntry[header.NumTables];
                for (int i = 0; i < header.NumTables; ++i)
                {
                    var table = new TableEntry();
                    table.Name = tables[i].Tag;
                    table.Offset = tables[i].ExpectedOffset;
                    
                    otfTableDirectory.Tables[i] = table;
                }

                otfTableDirectory.CreateTableEntriesMap();
                
                foreach (var (key, value) in tableDirectory.Tables)
                {
                    otfTableDirectory.TablesOffsets[key] = value.ExpectedOffset;
                }
                
                TypeFace = OTFParser.Parse(decompressedStream, resolution, otfTableDirectory);
            }

        }

        private void DecompressWoff(byte[] compressed, byte[] decompressed)
        {
            Inflater inflater = new Inflater();
            inflater.SetInput(compressed);
            inflater.Inflate(decompressed);
        }

    }
}