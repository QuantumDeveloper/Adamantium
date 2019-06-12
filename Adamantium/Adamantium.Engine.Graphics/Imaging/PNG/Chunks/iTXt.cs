﻿using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    /*international text chunk (iTXt)*/
    internal class iTXt : Chunk
    {
        public iTXt()
        {
            Name = "iTXt";
        }

        public string Key { get; set; }

        public string LangTag { get; set; }

        public string TransKey { get; set; }

        public string Text { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();

            var keyWord = Encoding.ASCII.GetBytes(Key);
            if (keyWord.Length < 1 || keyWord.Length > 79)
            {
                throw new PNGEncoderException("Keyword should have from 1 to 79 chars");
            }
            var langTagBytes = Encoding.ASCII.GetBytes(LangTag);
            var transKeyBytes = Encoding.ASCII.GetBytes(TransKey);

            uint numBytes = (uint)(keyWord.Length + langTagBytes.Length + transKeyBytes.Length + 5);

            var textBytes = Encoding.ASCII.GetBytes(Text);

            var compressedData = new List<byte>();
            if (state.EncoderSettings.TextCompression)
            {
                PNGCompressor compressor = new PNGCompressor();
                var error = compressor.Compress(textBytes, state.EncoderSettings, compressedData);
                if (error > 0)
                {
                    throw new PNGEncoderException(error.ToString());
                }
                numBytes += (uint)compressedData.Count;
            }
            else /*not compressed*/
            {
                numBytes += (uint)textBytes.Length;
            }

            bytes.AddRange(Utilities.GetBytesWithReversedEndian(numBytes));
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(keyWord);
            bytes.Add(0); // Null terminator
            bytes.Add(state.EncoderSettings.TextCompression ? (byte)1 : (byte)0); /*compression flag*/
            bytes.Add(0); // Compression method
            
            bytes.AddRange(langTagBytes);
            bytes.Add(0); // Null terminator
            
            bytes.AddRange(transKeyBytes);
            bytes.Add(0); // Null terminator

            if (state.EncoderSettings.TextCompression)
            {
                bytes.AddRange(compressedData);
            }
            else /*not compressed*/
            {
                bytes.AddRange(textBytes);
            }

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static iTXt FromTextItem(ITextItem item)
        {
            var itxt = new iTXt();
            itxt.Key = item.Key;
            itxt.LangTag = item.LangTag;
            itxt.TransKey = item.TransKey;
            itxt.Text = item.Text;

            return itxt;
        }
    }
}
