﻿namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Image file format used by <see cref="Image.Save(string, ImageFileType)"/>
    /// </summary>
    public enum ImageFileType
    {
        /// <summary>
        /// An Unknown file format.
        /// </summary>
        Unknown,

        /// <summary>
        /// A DDS file.
        /// </summary>
        Dds,

        /// <summary>
        /// A PNG file.
        /// </summary>
        Png,

        /// <summary>
        /// A GIF file.
        /// </summary>
        Gif,

        /// <summary>
        /// A JPG file.
        /// </summary>
        Jpg,

        /// <summary>
        /// A BMP file.
        /// </summary>
        Bmp,

        /// <summary>
        /// A TIFF file.
        /// </summary>
        Tiff,

        /// <summary>
        /// A WMP file.
        /// </summary>
        Wmp,

        /// <summary>
        /// A TGA File.
        /// </summary>
        Tga,

    }
}
