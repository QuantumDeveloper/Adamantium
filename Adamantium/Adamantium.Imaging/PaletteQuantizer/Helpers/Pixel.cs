using System;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Adamantium.Imaging.PaletteQuantizer.Helpers.Pixels;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.PaletteQuantizer.Helpers
{
    /// <summary>
    /// This is a pixel format independent pixel.
    /// </summary>
    public class Pixel : IDisposable
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

        private Type pixelType;
        private Int32 bitOffset;
        private Object pixelData;
        private IntPtr pixelDataPointer;

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
            get => ((IGenericPixel) pixelData).GetColor();
            set => ((IGenericPixel) pixelData).SetColor(value);
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
            pixelType = GetPixelType(Parent.PixelFormat);
            NewExpression newType = Expression.New(pixelType);
            UnaryExpression convertNewType = Expression.Convert(newType, typeof (Object));
            Expression<Func<Object>> indexedExpression = Expression.Lambda<Func<Object>>(convertNewType);
            pixelData = indexedExpression.Compile().Invoke();
            pixelDataPointer = MarshalToPointer(pixelData);
        }

        #endregion

        #region | Methods |

        /// <summary>
        /// Gets the type of the pixel format.
        /// </summary>
        internal Type GetPixelType(Format pixelFormat)
        {
            switch (pixelFormat)
            {
                case Format.R5G5B5A1_UNORM_PACK16: return typeof(PixelDataArgb1555);
                case Format.R5G6B5_UNORM_PACK16: return typeof(PixelDataRgb565);
                case Format.R8G8B8_UNORM:
                    return typeof(PixelDataRgb888);
                case Format.B8G8R8_UNORM:
                    return typeof(PixelDataBgr888);
                case Format.R8G8B8A8_UNORM:
                    return typeof(PixelDataRgb8888);
                case Format.B8G8R8A8_UNORM:
                    return typeof(PixelDataBgr8888);
                case Format.R16G16B16_UNORM:
                    return typeof(PixelDataRgb48);
                case Format.R16G16B16A16_UNORM:
                    return typeof(PixelDataArgb64);
                default:
                    String message = String.Format("This pixel format '{0}' is either indexed, or not supported.", pixelFormat);
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
            pixelData = Marshal.PtrToStructure(imagePointer, pixelType);
        }

        /// <summary>
        /// Reads the data.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void ReadData(Byte[] buffer, Int32 offset)
        {
            Marshal.Copy(buffer, offset, pixelDataPointer, Parent.BytesPerPixel);
            pixelData = Marshal.PtrToStructure(pixelDataPointer, pixelType);
        }

        /// <summary>
        /// Writes the raw data.
        /// </summary>
        /// <param name="imagePointer">The image pointer.</param>
        public void WriteRawData(IntPtr imagePointer)
        {
            Marshal.StructureToPtr(pixelData, imagePointer, false);
        }

        /// <summary>
        /// Writes the data.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void WriteData(Byte[] buffer, Int32 offset)
        {
            Marshal.Copy(pixelDataPointer, buffer, offset, Parent.BytesPerPixel);
        }

        #endregion

        #region << IDisposable >>

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Marshal.FreeHGlobal(pixelDataPointer);
        }

        #endregion
    }
}