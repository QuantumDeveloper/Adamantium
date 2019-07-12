﻿using System;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Imaging.PaletteQuantizer.Helpers.Pixels;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.PaletteQuantizer.Helpers
{
    /// <summary>
    /// This is a pixel format independent pixel.
    /// </summary>
    public class Pixel
    {
        #region | Constants |

        internal const Byte Zero = 0;
        internal const Byte One = 1;
        internal const Byte Two = 2;
        internal const Byte Four = 4;
        internal const Byte Eight = 8;

        internal const Byte NibbleMask = 0xF;
        internal const Byte ByteMask = 0xFF;

        internal const Int32 RedShift = 0;
        internal const Int32 GreenShift = 8;
        internal const Int32 BlueShift = 16;
        internal const Int32 AlphaShift = 24;

        internal const Int32 AlphaMask = ByteMask << AlphaShift;
        internal const Int32 RedGreenBlueMask = 0xFFFFFF;

        #endregion

        #region | Fields |

        private Int32 bitOffset;
        private IGenericPixel pixelData;

        #endregion

        #region | Properties |

        /// <summary>
        /// Gets the X.
        /// </summary>
        public Int32 X { get; private set; }

        /// <summary>
        /// Gets the Y.
        /// </summary>
        public Int32 Y { get; private set; }

        /// <summary>
        /// Gets the parent buffer.
        /// </summary>
        public ImageBuffer Parent { get; private set; }

        #endregion

        #region | Calculated properties |

        /// <summary>
        /// Gets or sets the index.
        /// </summary>
        /// <value>The index.</value>
        public Byte Index
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the color.
        /// </summary>
        /// <value>The color.</value>
        public Color Color
        {
            get => pixelData.GetColor();
            set => pixelData.SetColor(value);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is indexed.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is indexed; otherwise, <c>false</c>.
        /// </value>
        public Boolean UsePalette => Parent.UsePalette;

        #endregion

        #region | Constructors |

        /// <summary>
        /// Initializes a new instance of the <see cref="Pixel"/> struct.
        /// </summary>
        public Pixel(ImageBuffer parent)
        {
            Parent = parent;

            Initialize();
        }

        private void Initialize()
        {
            // creates pixel data
            pixelData = GetPixelType(Parent.PixelFormat);
        }

        #endregion

        #region | Methods |

        /// <summary>
        /// Gets the type of the pixel format.
        /// </summary>
        internal IGenericPixel GetPixelType(Format pixelFormat)
        {
            switch (pixelFormat)
            {
                case Format.R5G5B5A1_UNORM_PACK16: return new PixelDataArgb1555();
                case Format.R5G6B5_UNORM_PACK16: return new PixelDataRgb565();
                case Format.R8G8B8_UNORM:
                    return new PixelDataRgb888();
                case Format.B8G8R8_UNORM:
                    return new PixelDataBgr888();
                case Format.R8G8B8A8_UNORM:
                    return new PixelDataRgb8888();
                case Format.B8G8R8A8_UNORM:
                    return new PixelDataBgr8888();
                case Format.R16G16B16_UNORM:
                    return new PixelDataRgb48();
                case Format.R16G16B16A16_UNORM:
                    return new PixelDataArgb64();
                default:
                    String message = $"This pixel format '{pixelFormat}' is either indexed, or not supported.";
                    throw new NotSupportedException(message);
            }
        }

        private static IntPtr MarshalToPointer(Object data)
        {
            Int32 size = Marshal.SizeOf(data);
            IntPtr pointer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(data, pointer, false);
            return pointer;
        }

        #endregion

        #region | Update methods |

        /// <param name="x">The X coordinate.</param>
        /// <param name="y">The Y coordinate.</param>
        public void Update(Int32 x, Int32 y)
        {
            X = x; 
            Y = y; 
            bitOffset = Parent.GetBitOffset(x);
        }

        /// <summary>
        /// Reads the raw data.
        /// </summary>
        /// <param name="imagePointer">The image pointer.</param>
        public void ReadRawData(IntPtr imagePointer)
        {
            //pixelData = Marshal.PtrToStructure(imagePointer, pixelType);
            var data = new byte[Parent.BytesPerPixel];
            Utilities.Read(imagePointer, data, 0, Parent.BytesPerPixel);
            Color color = new Color();
            for (int i = 0; i<data.Length; ++i)
            {
                color[i] = data[i];
            }
            pixelData.SetColor(color);
        }

        /// <summary>
        /// Reads the data.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void ReadData(Byte[] buffer, Int32 offset)
        {
            Color color = new Color();
            int index = 0;
            int end = offset + Parent.BytesPerPixel;
            for (int i = offset; i < end; ++i)
            {
                color[index++] = buffer[i];
            }
            pixelData.SetColor(color);
            //Marshal.Copy(buffer, offset, pixelDataPointer, Parent.BytesPerPixel);
            //pixelData = Marshal.PtrToStructure(pixelDataPointer, pixelType);
        }

        /// <summary>
        /// Writes the raw data.
        /// </summary>
        /// <param name="imagePointer">The image pointer.</param>
        public void WriteRawData(IntPtr imagePointer)
        {
            Utilities.Write(imagePointer, pixelData.GetColor().ToArray(), 0, Parent.BytesPerPixel);
            //Marshal.StructureToPtr(pixelData, imagePointer, false);
        }

        /// <summary>
        /// Writes the data.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void WriteData(Byte[] buffer, Int32 offset)
        {
            //Color color = new Color();
            //var color = new Color(pixelData.Red, pixelData.Green, pixelData.Blue);
            int index = 0;
            var color = pixelData.GetColor();
            int end = offset + Parent.BytesPerPixel;
            //Marshal.Copy(pixelDataPointer, buffer, offset, Parent.BytesPerPixel);
            for (int i = offset; i< end; ++i)
            {
                buffer[i] = color[index++];
            }
        }

        #endregion
    }
}