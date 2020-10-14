using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Imaging.Bmp;
using Adamantium.Imaging.Dds;
using Adamantium.Imaging.Gif;
using Adamantium.Imaging.Ico;
using Adamantium.Imaging.Jpeg;
using Adamantium.Imaging.Png;
using Adamantium.Imaging.Tga;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging
{
    /// <summary>
    /// Provides method to instantiate an image 1D/2D/3D supporting TextureArray and mipmaps on the CPU or to load/save an image from/to the disk.
    /// </summary>
    //[ContentReader(typeof(ImageContentReader))]
    public sealed class Image : DisposableObject
    {
        public delegate Image ImageLoadDelegate(IntPtr dataPointer, int dataSize, bool makeACopy, GCHandle? handle);
        public delegate void ImageSaveDelegate(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream);

        /// <summary>
        /// Pixel buffers.
        /// </summary>
        internal PixelBuffer[] pixelBuffers;
        private DataBox[] dataBoxArray;
        private List<int> mipMapToZIndex;
        private int zBufferCountPerArraySlice;
        private MipMapDescription[] mipmapDescriptions;
        private static List<LoadSaveDelegate> loadSaveDelegates = new List<LoadSaveDelegate>();
        private bool isAnimated;
        private uint numberOfReplays;

        /// <summary>
        /// Provides access to all pixel buffers.
        /// </summary>
        /// <remarks>
        /// For Texture3D, each z slice of the Texture3D has a pixelBufferArray * by the number of mipmaps.
        /// For other textures, there is Description.MipLevels * Description.ArraySize pixel buffers.
        /// </remarks>
        private PixelBufferArray pixelBufferArray;

        /// <summary>
        /// Gets the total number of bytes occupied by this image in memory.
        /// </summary>
        private int totalSizeInBytes;

        /// <summary>
        /// Pointer to the buffer.
        /// </summary>
        private IntPtr buffer;

        /// <summary>
        /// True if the buffer must be disposed.
        /// </summary>
        private bool bufferIsDisposable;

        /// <summary>
        /// Handle != null if the buffer is a pinned managed object on the LOH (Large Object Heap).
        /// </summary>
        private GCHandle? handle;

        /// <summary>
        /// Description of this image.
        /// </summary>
        public ImageDescription Description;

        public bool IsAnimated => isAnimated;

        public uint NumberOfReplays => numberOfReplays;

        /// <summary>
        /// Gets a pointer to the image buffer in memory.
        /// </summary>
        /// <value>A pointer to the image buffer in memory.</value>
        public IntPtr DataPointer => this.buffer;

        /// <summary>
        /// Provides access to all pixel buffers.
        /// </summary>
        /// <remarks>
        /// For Texture3D, each z slice of the Texture3D has a pixelBufferArray * by the number of mipmaps.
        /// For other textures, there is Description.MipLevels * Description.ArraySize pixel buffers.
        /// </remarks>
        public PixelBufferArray PixelBuffer => pixelBufferArray;

        /// <summary>
        /// Gets or sets default image. Actual for APNG if you want to exclude some <see cref="PixelBuffer"/> from animation sequence
        /// </summary>
        public PixelBuffer DefaultImage { get; set; }

        /// <summary>
        /// Gets the total number of bytes occupied by this image in memory.
        /// </summary>
        public int TotalSizeInBytes => totalSizeInBytes;

        private Image()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image" /> class.
        /// </summary>
        /// <param name="description">The image description.</param>
        /// <param name="dataPointer">The pointer to the data buffer.</param>
        /// <param name="offset">The offset from the beginning of the data buffer.</param>
        /// <param name="handle">The handle (optional).</param>
        /// <param name="bufferIsDisposable">if set to <c>true</c> [buffer is disposable].</param>
        /// <exception cref="System.InvalidOperationException">If the format is invalid, or width/height/depth/arraysize is invalid with respect to the dimension.</exception>
        internal Image(ImageDescription description, IntPtr dataPointer, int offset, GCHandle? handle, bool bufferIsDisposable, PitchFlags pitchFlags = PitchFlags.None)
        {
            Initialize(description, dataPointer, offset, handle, bufferIsDisposable, pitchFlags);
        }

        internal Image(ImageDescription mainDescription, uint numberOfReplays, params AnimatedImageDescription[] animatedDescriptions)
        {
            InitializeAnimated(mainDescription, numberOfReplays, animatedDescriptions);
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            if (handle.HasValue)
            {
                handle.Value.Free();
            }

            if (bufferIsDisposable)
            {
                Utilities.FreeMemory(buffer);
            }

            base.Dispose(disposeManagedResources);
        }

        /// <summary>
        /// Gets the mipmap description of this instance for the specified mipmap level.
        /// </summary>
        /// <param name="mipmap">The mipmap.</param>
        /// <returns>A description of a particular mipmap for this texture.</returns>
        public MipMapDescription GetMipMapDescription(int mipmap)
        {
            return mipmapDescriptions[mipmap];
        }

        /// <summary>
        /// Gets the pixel buffer for the specified array/z slice and mipmap level.
        /// </summary>
        /// <param name="arrayOrZSliceIndex">For 3D image, the parameter is the Z slice, otherwise it is an index into the texture array.</param>
        /// <param name="mipmap">The mipmap.</param>
        /// <returns>A <see cref="pixelBufferArray"/>.</returns>
        /// <exception cref="System.ArgumentException">If arrayOrZSliceIndex or mipmap are out of range.</exception>
        public PixelBuffer GetPixelBuffer(int arrayOrZSliceIndex, int mipmap)
        {
            // Check for parameters, as it is easy to mess up things...
            if (mipmap > Description.MipLevels)
                throw new ArgumentException("Invalid mipmap level", "mipmap");

            if (Description.Dimension == TextureDimension.Texture3D)
            {
                if (arrayOrZSliceIndex > Description.Depth)
                    throw new ArgumentException("Invalid z slice index", "arrayOrZSliceIndex");

                // For 3D textures
                return GetPixelBufferUnsafe(0, arrayOrZSliceIndex, mipmap);
            }

            if (arrayOrZSliceIndex > Description.ArraySize)
            {
                throw new ArgumentException("Invalid array slice index", "arrayOrZSliceIndex");
            }

            // For 1D, 2D textures
            return GetPixelBufferUnsafe(arrayOrZSliceIndex, 0, mipmap);
        }

        /// <summary>
        /// Gets the pixel buffer for the specified array/z slice and mipmap level.
        /// </summary>
        /// <param name="arrayIndex">Index into the texture array. Must be set to 0 for 3D images.</param>
        /// <param name="zIndex">Z index for 3D image. Must be set to 0 for all 1D/2D images.</param>
        /// <param name="mipmap">The mipmap.</param>
        /// <returns>A <see cref="pixelBufferArray"/>.</returns>
        /// <exception cref="System.ArgumentException">If arrayIndex, zIndex or mipmap are out of range.</exception>
        public PixelBuffer GetPixelBuffer(int arrayIndex, int zIndex, int mipmap)
        {
            // Check for parameters, as it is easy to mess up things...
            if (mipmap > Description.MipLevels)
                throw new ArgumentException("Invalid mipmap level", "mipmap");

            if (arrayIndex > Description.ArraySize)
                throw new ArgumentException("Invalid array slice index", "arrayIndex");

            if (zIndex > Description.Depth)
                throw new ArgumentException("Invalid z slice index", "zIndex");

            return this.GetPixelBufferUnsafe(arrayIndex, zIndex, mipmap);
        }


        /// <summary>
        /// Registers a loader/saver for a specified image file type.
        /// </summary>
        /// <param name="type">The file type (use integer and explicit casting to <see cref="ImageFileType"/> to register other file format.</param>
        /// <param name="loader">The loader delegate (can be null).</param>
        /// <param name="saver">The saver delegate (can be null).</param>
        /// <exception cref="System.ArgumentException"></exception>
        public static void Register(ImageFileType type, ImageLoadDelegate loader, ImageSaveDelegate saver)
        {
            // If reference equals, then it is null
            if (ReferenceEquals(loader, saver))
                throw new ArgumentNullException("loader/saver", "Can set both loader and saver to null");

            var newDelegate = new LoadSaveDelegate(type, loader, saver);
            for (int i = 0; i < loadSaveDelegates.Count; i++)
            {
                var loadSaveDelegate = loadSaveDelegates[i];
                if (loadSaveDelegate.FileType == type)
                {
                    loadSaveDelegates[i] = newDelegate;
                    return;
                }
            }
            loadSaveDelegates.Add(newDelegate);
        }


        /// <summary>
        /// Gets the databox from this image.
        /// </summary>
        /// <returns>The databox of this image.</returns>
        public DataBox[] ToDataBox()
        {
            return (DataBox[])dataBoxArray.Clone();
        }

        /// <summary>
        /// Gets the databox from this image.
        /// </summary>
        /// <returns>The databox of this image.</returns>
        private DataBox[] ComputeDataBox()
        {
            dataBoxArray = new DataBox[Description.ArraySize * Description.MipLevels];
            int i = 0;
            for (int arrayIndex = 0; arrayIndex < Description.ArraySize; arrayIndex++)
            {
                for (int mipIndex = 0; mipIndex < Description.MipLevels; mipIndex++)
                {
                    // Get the first z-slice (A DataBox for a Texture3D is pointing to the whole texture).
                    var pixelBuffer = this.GetPixelBufferUnsafe(arrayIndex, 0, mipIndex);

                    dataBoxArray[i].DataPointer = pixelBuffer.DataPointer;
                    dataBoxArray[i].RowPitch = pixelBuffer.RowStride;
                    dataBoxArray[i].SlicePitch = pixelBuffer.BufferStride;
                    i++;
                }
            }
            return dataBoxArray;
        }

        /// <summary>
        /// Gets the databox from this image.
        /// </summary>
        /// <returns>The databox of this image.</returns>
        private DataBox[] ComputeAnimatedDataBox()
        {
            dataBoxArray = new DataBox[Description.ArraySize * Description.MipLevels];
            int i = 0;
            for (int arrayIndex = 0; arrayIndex < PixelBuffer.Count; arrayIndex++)
            {
                dataBoxArray[i].DataPointer = PixelBuffer[i].DataPointer;
                dataBoxArray[i].RowPitch = PixelBuffer[i].RowStride;
                dataBoxArray[i].SlicePitch = PixelBuffer[i].BufferStride;
            }
            return dataBoxArray;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Image"/> from an image description.
        /// </summary>
        /// <param name="description">The image description.</param>
        /// <returns>A new image.</returns>
        public static Image New(ImageDescription description)
        {
            return New(description, IntPtr.Zero);
        }

        /// <summary>
        /// Creates a new instance of animated <see cref="Image"/> from an image description.
        /// </summary>
        /// <param name="description">The image description.</param>
        /// <returns>A new image.</returns>
        public static Image New(ImageDescription mainDescription, uint numberOfReplays, params AnimatedImageDescription[] descriptions)
        {
            return new Image(mainDescription, numberOfReplays, descriptions);
        }

        /// <summary>
        /// Creates a new instance of a 1D <see cref="Image"/>.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="mipMapCount">The mip map count.</param>
        /// <param name="format">The format.</param>
        /// <param name="arraySize">Size of the array.</param>
        /// <returns>A new image.</returns>
        public static Image New1D(uint width, MipMapCount mipMapCount, SurfaceFormat format, uint arraySize = 1)
        {
            return New1D(width, mipMapCount, format, arraySize, IntPtr.Zero);
        }

        /// <summary>
        /// Creates a new instance of a 2D <see cref="Image"/>.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="mipMapCount">The mip map count.</param>
        /// <param name="format">The format.</param>
        /// <param name="arraySize">Size of the array.</param>
        /// <returns>A new image.</returns>
        public static Image New2D(uint width, uint height, MipMapCount mipMapCount, SurfaceFormat format, uint arraySize = 1)
        {
            return New2D(width, height, mipMapCount, format, arraySize, IntPtr.Zero);
        }

        /// <summary>
        /// Creates a new instance of a Cube <see cref="Image"/>.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="mipMapCount">The mip map count.</param>
        /// <param name="format">The format.</param>
        /// <returns>A new image.</returns>
        public static Image NewCube(uint width, MipMapCount mipMapCount, SurfaceFormat format)
        {
            return NewCube(width, mipMapCount, format, IntPtr.Zero);
        }

        /// <summary>
        /// Creates a new instance of a 3D <see cref="Image"/>.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="depth">The depth.</param>
        /// <param name="mipMapCount">The mip map count.</param>
        /// <param name="format">The format.</param>
        /// <returns>A new image.</returns>
        public static Image New3D(uint width, uint height, uint depth, MipMapCount mipMapCount, SurfaceFormat format)
        {
            return New3D(width, height, depth, mipMapCount, format, IntPtr.Zero);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Image"/> from an image description.
        /// </summary>
        /// <param name="description">The image description.</param>
        /// <param name="dataPointer">Pointer to an existing buffer.</param>
        /// <returns>A new image.</returns>
        public static Image New(ImageDescription description, IntPtr dataPointer)
        {
            return new Image(description, dataPointer, 0, null, false);
        }

        /// <summary>
        /// Creates a new instance of a 1D <see cref="Image"/>.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="mipMapCount">The mip map count.</param>
        /// <param name="format">The format.</param>
        /// <param name="arraySize">Size of the array.</param>
        /// <param name="dataPointer">Pointer to an existing buffer.</param>
        /// <returns>A new image.</returns>
        public static Image New1D(uint width, MipMapCount mipMapCount, SurfaceFormat format, uint arraySize, IntPtr dataPointer)
        {
            return new Image(CreateDescription(TextureDimension.Texture1D, width, 1, 1, mipMapCount, format, arraySize), dataPointer, 0, null, false);
        }

        /// <summary>
        /// Creates a new instance of a 2D <see cref="Image"/>.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="mipMapCount">The mip map count.</param>
        /// <param name="format">The format.</param>
        /// <param name="arraySize">Size of the array.</param>
        /// <param name="dataPointer">Pointer to an existing buffer.</param>
        /// <returns>A new image.</returns>
        public static Image New2D(uint width, uint height, MipMapCount mipMapCount, SurfaceFormat format, uint arraySize, IntPtr dataPointer)
        {
            return new Image(CreateDescription(TextureDimension.Texture2D, width, height, 1, mipMapCount, format, arraySize), dataPointer, 0, null, false);
        }

        /// <summary>
        /// Creates a new instance of a Cube <see cref="Image"/>.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="mipMapCount">The mip map count.</param>
        /// <param name="format">The format.</param>
        /// <param name="dataPointer">Pointer to an existing buffer.</param>
        /// <returns>A new image.</returns>
        public static Image NewCube(uint width, MipMapCount mipMapCount, SurfaceFormat format, IntPtr dataPointer)
        {
            return new Image(CreateDescription(TextureDimension.TextureCube, width, width, 1, mipMapCount, format, 6), dataPointer, 0, null, false);
        }

        /// <summary>
        /// Creates a new instance of a 3D <see cref="Image"/>.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="depth">The depth.</param>
        /// <param name="mipMapCount">The mip map count.</param>
        /// <param name="format">The format.</param>
        /// <param name="dataPointer">Pointer to an existing buffer.</param>
        /// <returns>A new image.</returns>
        public static Image New3D(uint width, uint height, uint depth, MipMapCount mipMapCount, SurfaceFormat format, IntPtr dataPointer)
        {
            return new Image(CreateDescription(TextureDimension.Texture3D, width, height, depth, mipMapCount, format, 1), dataPointer, 0, null, false);
        }

        /// <summary>
        /// Loads an image from an unmanaged memory pointer.
        /// </summary>
        /// <param name="dataPointer">Pointer to an unmanaged memory. If <paramref name="makeACopy"/> is false, this buffer must be allocated with <see cref="Utilities.AllocateMemory"/>.</param>
        /// <param name="dataSize">Size of the unmanaged buffer.</param>
        /// <param name="expectedType">Expected file type to improve image loading performance</param>
        /// <param name="makeACopy">True to copy the content of the buffer to a new allocated buffer, false otherwise.</param>
        /// <returns>An new image.</returns>
        /// <remarks>If <paramref name="makeACopy"/> is set to false, the returned image is now the holder of the unmanaged pointer and will release it on Dispose. </remarks>
        public static Image Load(IntPtr dataPointer, int dataSize, ImageFileType expectedType = ImageFileType.Unknown, bool makeACopy = false)
        {
            //Find delegate which will try to load image of concrete type
            foreach (var loadSaveDelegate in loadSaveDelegates)
            {
                if (loadSaveDelegate.FileType != expectedType)
                    continue;

                var image = loadSaveDelegate.Load?.Invoke(dataPointer, dataSize, makeACopy, null);
                if (image != null)
                {
                    return image;
                }
                break;
            }

            //If we couldnt load an image (for ex. image file extention is not of a correct format,
            //then we will try to load image with other register delegates excluding that delegate, which we previously used
            foreach (var loadSaveDelegate in loadSaveDelegates)
            {
                if (loadSaveDelegate.FileType == expectedType)
                    continue;

                var image = loadSaveDelegate.Load?.Invoke(dataPointer, dataSize, makeACopy, null);
                if (image != null)
                {
                    return image;
                }
            }

            return null;
        }

        /// <summary>
        /// Loads an image from an unmanaged memory pointer.
        /// </summary>
        /// <param name="dataBuffer">Pointer to an unmanaged memory. If <paramref name="makeACopy"/> is false, this buffer must be allocated with <see cref="Utilities.AllocateMemory"/>.</param>
        /// <param name="makeACopy">True to copy the content of the buffer to a new allocated buffer, false otherwhise.</param>
        /// <returns>An new image.</returns>
        /// <remarks>If <paramref name="makeACopy"/> is set to false, the returned image is now the holder of the unmanaged pointer and will release it on Dispose. </remarks>
        public static Image Load(DataPointer dataBuffer, bool makeACopy = false)
        {
            return Load(dataBuffer.Pointer, dataBuffer.Size, makeACopy);
        }

        /// <summary>
        /// Loads an image from an unmanaged memory pointer.
        /// </summary>
        /// <param name="dataPointer">Pointer to an unmanaged memory. If <paramref name="makeACopy"/> is false, this buffer must be allocated with <see cref="Utilities.AllocateMemory"/>.</param>
        /// <param name="dataSize">Size of the unmanaged buffer.</param>
        /// <param name="makeACopy">True to copy the content of the buffer to a new allocated buffer, false otherwise.</param>
        /// <returns>An new image.</returns>
        /// <remarks>If <paramref name="makeACopy"/> is set to false, the returned image is now the holder of the unmanaged pointer and will release it on Dispose. </remarks>
        public static Image Load(IntPtr dataPointer, int dataSize, bool makeACopy = false)
        {
            return Load(dataPointer, dataSize, makeACopy, null);
        }

        /// <summary>
        /// Loads an image from a managed buffer.
        /// </summary>
        /// <param name="buffer">Reference to a managed buffer.</param>
        /// <returns>An new image.</returns>
        /// <remarks>This method support the following format: <c>dds, bmp, jpg, png, gif, tiff, wmp, tga</c>.</remarks>
        public static unsafe Image Load(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            int size = buffer.Length;

            // If buffer is allocated on Large Object Heap, then we are going to pin it instead of making a copy.
            if (size > (85 * 1024))
            {
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                return Load(handle.AddrOfPinnedObject(), size, false, handle);
            }

            fixed (void* pbuffer = buffer)
            {
                return Load((IntPtr)pbuffer, size, true);
            }
        }

        /// <summary>
        /// Loads the specified image from a stream.
        /// </summary>
        /// <param name="imageStream">The image stream.</param>
        /// <param name="fileType"></param>
        /// <returns>An new image.</returns>
        /// <remarks>This method support the following format: <c>dds, bmp, jpg, png, gif, tiff, wmp, tga</c>.</remarks>
        public static Image Load(Stream imageStream, ImageFileType fileType = ImageFileType.Unknown)
        {
            // Use fast path using FileStream
            // TODO: THIS IS NOT OPTIMIZED IN THE CASE THE STREAM IS NOT AN IMAGE. FIND A WAY TO OPTIMIZE THIS CASE.
            var nativeImageStream = imageStream as FileStream;
            if (nativeImageStream != null)
            {
                var imageBuffer = IntPtr.Zero;
                Image image = null;
                try
                {
                    unsafe
                    {
                        var imageSize = (int)nativeImageStream.Length;
                        imageBuffer = Utilities.AllocateMemory(imageSize);
                        Span<byte> bytes = new Span<byte>(imageBuffer.ToPointer(), imageSize);
                        nativeImageStream.Read(bytes);
                        image = Load(imageBuffer, imageSize, fileType);
                    }
                }
                finally
                {
                    if (image == null)
                    {
                        Utilities.FreeMemory(imageBuffer);
                    }
                }
                return image;
            }

            // Else Read the whole stream into memory.
            return Load(Utilities.ReadStream(imageStream));
        }

        /// <summary>
        /// Loads the specified image from a file.
        /// </summary>
        /// <param name="fileName">The filename.</param>
        /// <returns>An new image.</returns>
        /// <remarks>This method support the following format: <c>dds, bmp, jpg, png, gif, tiff, wmp, tga</c>.</remarks>
        public static Image Load(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            FileStream stream = null;
            IntPtr memoryPtr = IntPtr.Zero;
            int size;
            try
            {
                unsafe
                {
                    stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    size = (int)stream.Length;
                    memoryPtr = Utilities.AllocateMemory(size);
                    Span<byte> bytes = new Span<byte>(memoryPtr.ToPointer(), size);
                    stream.Read(bytes);
                }
            }
            catch (Exception)
            {
                if (memoryPtr != IntPtr.Zero)
                    Utilities.FreeMemory(memoryPtr);
                throw;
            }
            finally
            {
                try
                {
                    stream?.Dispose();
                }
                catch { }
            }

            var type = GetImageTypeFromFileName(fileName);
            // If everything was fine, load the image from memory
            return Load(memoryPtr, size, type);
        }

        /// <summary>
        /// Returns <see cref="ImageFileType"/> from file name based on its extension to predict its type
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static ImageFileType GetImageTypeFromFileName(string fileName)
        {
            ImageFileType fileType;
            var extension = Path.GetExtension(fileName);
            extension ??= string.Empty;
            extension = extension.TrimStart('.').ToLower();
            switch (extension)
            {
                case "jpg":
                    fileType = ImageFileType.Jpg;
                    break;
                case "dds":
                    fileType = ImageFileType.Dds;
                    break;
                case "gif":
                    fileType = ImageFileType.Gif;
                    break;
                case "bmp":
                    fileType = ImageFileType.Bmp;
                    break;
                case "png":
                    fileType = ImageFileType.Png;
                    break;
                case "tga":
                    fileType = ImageFileType.Tga;
                    break;
                //case "tiff":
                //    fileType = ImageFileType.Tiff;
                //    break;
                default:
                    fileType = ImageFileType.Unknown;
                    break;
            }
            return fileType;
        }

        /// <summary>
        /// Saves this instance to a file.
        /// </summary>
        /// <param name="fileName">The destination file.</param>
        /// <param name="fileType">Specify the output format.</param>
        /// <remarks>This method support the following format: <c>dds, bmp, jpg, png, gif, tiff, wmp, tga</c>.</remarks>
        public void Save(string fileName, ImageFileType fileType)
        {
            using (var imageStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                Save(imageStream, fileType);
            }
        }

        /// <summary>
        /// Saves this instance to a stream.
        /// </summary>
        /// <param name="imageStream">The destination stream.</param>
        /// <param name="fileType">Specify the output format.</param>
        /// <remarks>This method support the following format: <c>dds, bmp, jpg, png, gif, tiff, wmp, tga</c>.</remarks>
        public void Save(Stream imageStream, ImageFileType fileType)
        {
            Save(this, pixelBuffers, pixelBuffers.Length, Description, imageStream, fileType);
        }

        public void ApplyPixelBuffer(PixelBuffer pixelBuffer, int index, bool freeBuffer)
        {
            var destinationBuffer = PixelBuffer[index];
            if (destinationBuffer == null)
            {
                throw new ArgumentOutOfRangeException($"Parameter index is outside pixel array bounds. Maximum numbers of elemnts in array: {PixelBuffer.Count}");
            }

            if (destinationBuffer.BufferStride != pixelBuffer.BufferStride)
            {
                throw new ArgumentOutOfRangeException($"Source pixel buffer stride is not the same as destination buffer stride. Source is {pixelBuffer.BufferStride}, destination - is {destinationBuffer.BufferStride}. Buffer size shuld be the same");
            }

            Utilities.CopyMemory(destinationBuffer.DataPointer, pixelBuffer.DataPointer, pixelBuffer.BufferStride);

            destinationBuffer.Format = pixelBuffer.Format;
            destinationBuffer.Width = pixelBuffer.Width;
            destinationBuffer.Height = pixelBuffer.Height;

            if (freeBuffer)
            {
                Utilities.FreeMemory(pixelBuffer.DataPointer);
            }
        }


        /// <summary>
        /// Loads an image from the specified pointer.
        /// </summary>
        /// <param name="dataPointer">The data pointer.</param>
        /// <param name="dataSize">Size of the data.</param>
        /// <param name="makeACopy">if set to <c>true</c> [make A copy].</param>
        /// <param name="handle">The handle.</param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException"></exception>
        private static Image Load(IntPtr dataPointer, int dataSize, bool makeACopy, GCHandle? handle)
        {
            foreach (var loadSaveDelegate in loadSaveDelegates)
            {
                var image = loadSaveDelegate.Load?.Invoke(dataPointer, dataSize, makeACopy, handle);
                if (image != null)
                {
                    return image;
                }
            }
            return null;
        }

        /// <summary>
        /// Saves this instance to a stream.
        /// </summary>
        /// <param name="img">Image to save</param>
        /// <param name="pixelBuffers">The buffers to save.</param>
        /// <param name="count">The number of buffers to save.</param>
        /// <param name="description">Global description of the buffer.</param>
        /// <param name="imageStream">The destination stream.</param>
        /// <param name="fileType">Specify the output format.</param>
        /// <remarks>This method support the following format: <c>dds, bmp, jpg, png, gif, tiff, wmp, tga</c>.</remarks>
        internal static void Save(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream, ImageFileType fileType)
        {
            foreach (var loadSaveDelegate in loadSaveDelegates)
            {
                if (loadSaveDelegate.FileType == fileType)
                {
                    loadSaveDelegate.Save(img, pixelBuffers, count, description, imageStream);
                    return;
                }

            }
            throw new NotSupportedException("This file format is not implemented.");
        }

        static Image()
        {
            Register(ImageFileType.Dds, DDSHelper.LoadFromMemory, DDSHelper.SaveToStream);
            Register(ImageFileType.Ico, ICOHelper.LoadFromMemory, ICOHelper.SaveToStream);
            Register(ImageFileType.Gif, GIFHelper.LoadFromMemory, GIFHelper.SaveToStream);
            Register(ImageFileType.Bmp, BitmapHelper.LoadFromMemory, BitmapHelper.SaveToStream);
            Register(ImageFileType.Jpg, JpegHelper.LoadFromMemory, JpegHelper.SaveToStream);
            Register(ImageFileType.Png, PNGHelper.LoadFromMemory, PNGHelper.SaveToStream);
            Register(ImageFileType.Tga, TGAHelper.LoadFromMemory, TGAHelper.SaveToStream);
            //Register(ImageFileType.Tiff, WICHelper.LoadFromWICMemory, WICHelper.SaveTiffToWICMemory);
        }


        internal unsafe void Initialize(ImageDescription description, IntPtr dataPointer, int offset, GCHandle? handle, bool bufferIsDisposable, PitchFlags pitchFlags = PitchFlags.None)
        {
            if (!FormatHelper.IsValid(description.Format))
                throw new InvalidOperationException("Unsupported Image Format");

            this.handle = handle;

            switch (description.Dimension)
            {
                case TextureDimension.Texture1D:
                    if (description.Width <= 0 || description.Height != 1 || description.Depth != 1 || description.ArraySize == 0)
                        throw new InvalidOperationException("Invalid Width/Height/Depth/ArraySize for Image 1D");

                    // Check that miplevels are fine
                    description.MipLevels = CalculateMipLevels(description.Width, 1, description.MipLevels);
                    break;

                case TextureDimension.Texture2D:
                case TextureDimension.TextureCube:
                    if (description.Width <= 0 || description.Height <= 0 || description.Depth != 1 || description.ArraySize == 0)
                        throw new InvalidOperationException("Invalid Width/Height/Depth/ArraySize for Image 2D");

                    if (description.Dimension == TextureDimension.TextureCube)
                    {
                        if ((description.ArraySize % 6) != 0)
                            throw new InvalidOperationException("TextureCube must have an arraysize = 6");
                    }

                    // Check that miplevels are fine
                    description.MipLevels = CalculateMipLevels(description.Width, description.Height, description.MipLevels);
                    break;

                case TextureDimension.Texture3D:
                    if (description.Width <= 0 || description.Height <= 0 || description.Depth <= 0 || description.ArraySize != 1)
                        throw new InvalidOperationException("Invalid Width/Height/Depth/ArraySize for Image 3D");

                    // Check that miplevels are fine
                    description.MipLevels = CalculateMipLevels(description.Width, description.Height, description.Depth, description.MipLevels);
                    break;
            }

            // Calculate mipmaps
            int pixelBufferCount;
            mipMapToZIndex = CalculateImageArray(description, pitchFlags, out pixelBufferCount, out totalSizeInBytes);
            mipmapDescriptions = CalculateMipMapDescription(description, pitchFlags);
            zBufferCountPerArraySlice = mipMapToZIndex[^1];

            // Allocate all pixel buffers
            pixelBuffers = new PixelBuffer[pixelBufferCount];
            pixelBufferArray = new PixelBufferArray(this);

            // Setup all pointers
            // only release buffer that is not pinned and is asked to be disposed.
            this.bufferIsDisposable = !handle.HasValue && bufferIsDisposable;
            this.buffer = dataPointer;

            if (dataPointer == IntPtr.Zero)
            {
                buffer = Utilities.AllocateMemory(totalSizeInBytes);
                offset = 0;
                this.bufferIsDisposable = true;
            }

            SetupImageArray((IntPtr)((byte*)buffer + offset), description, pitchFlags, pixelBuffers);

            Description = description;

            // PreCompute databoxes
            dataBoxArray = ComputeDataBox();
        }


        internal unsafe void InitializeAnimated(ImageDescription mainDescription, uint numberOfReplays, params AnimatedImageDescription[] animatedDescriptions)
        {
            if (!FormatHelper.IsValid(mainDescription.Format))
                throw new InvalidOperationException("Unsupported Image Format");

            if (animatedDescriptions == null || animatedDescriptions.Length < 2)
                throw new InvalidOperationException("Animated descriptions count must be more than 1");

            isAnimated = true;

            this.numberOfReplays = numberOfReplays;

            // Calculate mipmaps
            int pixelBufferCount = animatedDescriptions.Length;
            totalSizeInBytes = CalculateAnimatedImageArray(animatedDescriptions);

            // Allocate all pixel buffers
            pixelBuffers = new PixelBuffer[pixelBufferCount];
            pixelBufferArray = new PixelBufferArray(this);

            buffer = Utilities.AllocateMemory(totalSizeInBytes);
            this.bufferIsDisposable = true;

            SetupImageArray((IntPtr)((byte*)buffer), mainDescription.Format, animatedDescriptions, pixelBuffers);

            Description = mainDescription;

            // PreCompute databoxes
            dataBoxArray = ComputeAnimatedDataBox();
        }

        private PixelBuffer GetPixelBufferUnsafe(int arrayIndex, int zIndex, int mipmap)
        {
            var depthIndex = this.mipMapToZIndex[mipmap];
            var pixelBufferIndex = arrayIndex * this.zBufferCountPerArraySlice + depthIndex + zIndex;
            return pixelBuffers[pixelBufferIndex];
        }

        private static ImageDescription CreateDescription(TextureDimension dimension, uint width, uint height, uint depth, MipMapCount mipMapCount, SurfaceFormat format, uint arraySize)
        {
            return new ImageDescription()
            {
                Width = width,
                Height = height,
                Depth = depth,
                ArraySize = arraySize,
                Dimension = dimension,
                Format = format,
                MipLevels = mipMapCount,
            };
        }

        [Flags]
        internal enum PitchFlags
        {
            None = 0x0,         // Normal operation
            LegacyDword = 0x1,  // Assume pitch is DWORD aligned instead of BYTE aligned
            Bpp24 = 0x10000,    // Override with a legacy 24 bits-per-pixel format size
            Bpp16 = 0x20000,    // Override with a legacy 16 bits-per-pixel format size
            Bpp8 = 0x40000,     // Override with a legacy 8 bits-per-pixel format size
        };

        internal static void ComputePitch(Format format, int width, int height, out int rowPitch, out int slicePitch, out int widthCount, out int heightCount, PitchFlags flags = PitchFlags.None)
        {
            widthCount = width;
            heightCount = height;

            if (format.IsCompressed())
            {
                //int bpb = (format == Format.BC1_Typeless
                //            || format == Format.BC1_UNorm
                //            || format == Format.BC1_UNorm_SRgb
                //            || format == Format.BC4_Typeless
                //            || format == Format.BC4_UNorm
                //            || format == Format.BC4_SNorm) ? 8 : 16;
                int bpb = (format == Format.BC1_RGBA_SRGB_BLOCK
                            || format == Format.BC1_RGBA_UNORM_BLOCK
                            || format == Format.BC4_UNORM_BLOCK
                            || format == Format.BC4_SNORM_BLOCK) ? 8 : 16;
                widthCount = Math.Max(1, (width + 3) / 4);
                heightCount = Math.Max(1, (height + 3) / 4);
                rowPitch = widthCount * bpb;

                slicePitch = rowPitch * heightCount;
            }
            else if (format.IsPacked())
            {
                rowPitch = ((width + 1) >> 1) * 4;

                slicePitch = rowPitch * height;
            }
            else
            {
                int bpp;

                if (flags.HasFlag(PitchFlags.Bpp24))
                    bpp = 24;
                else if (flags.HasFlag(PitchFlags.Bpp16))
                    bpp = 16;
                else if (flags.HasFlag(PitchFlags.Bpp8))
                    bpp = 8;
                else
                    bpp = format.SizeOfInBits();

                if ((flags & PitchFlags.LegacyDword) != 0)
                {
                    // Special computation for some incorrectly created DDS files based on
                    // legacy DirectDraw assumptions about pitch alignment
                    rowPitch = ((width * bpp + 31) / 32) * sizeof(int);
                    slicePitch = rowPitch * height;
                }
                else
                {
                    rowPitch = (width * bpp + 7) / 8;
                    slicePitch = rowPitch * height;
                }
            }
        }

        internal static MipMapDescription[] CalculateMipMapDescription(ImageDescription metadata, PitchFlags cpFlags = PitchFlags.None)
        {
            int nImages;
            int pixelSize;
            return CalculateMipMapDescription(metadata, cpFlags, out nImages, out pixelSize);
        }

        internal static MipMapDescription[] CalculateMipMapDescription(ImageDescription metadata, PitchFlags cpFlags, out int nImages, out int pixelSize)
        {
            pixelSize = 0;
            nImages = 0;

            var w = metadata.Width;
            var h = metadata.Height;
            var d = metadata.Depth;

            var mipmaps = new MipMapDescription[metadata.MipLevels];

            for (int level = 0; level < metadata.MipLevels; ++level)
            {
                int rowPitch, slicePitch;
                int widthPacked;
                int heightPacked;
                ComputePitch(metadata.Format, (int)w, (int)h, out rowPitch, out slicePitch, out widthPacked, out heightPacked, PitchFlags.None);

                mipmaps[level] = new MipMapDescription(
                    w,
                    h,
                    d,
                    rowPitch,
                    slicePitch,
                    widthPacked,
                    heightPacked
                    );

                pixelSize += (int)d * slicePitch;
                nImages += (int)d;

                if (h > 1)
                    h >>= 1;

                if (w > 1)
                    w >>= 1;

                if (d > 1)
                    d >>= 1;
            }
            return mipmaps;
        }

        private static int CalculateAnimatedImageArray(params AnimatedImageDescription[] descriptions)
        {
            if (descriptions == null)
                return 0;

            long allocateSize = 0;
            foreach (var desc in descriptions)
            {
                allocateSize += desc.Width * desc.Height * desc.BytesPerPixel;
            }

            return (int)allocateSize;
        }

        /// <summary>
        /// Determines number of image array entries and pixel size.
        /// </summary>
        /// <param name="imageDesc">Description of the image to create.</param>
        /// <param name="pitchFlags">Pitch flags.</param>
        /// <param name="bufferCount">Output number of mipmap.</param>
        /// <param name="pixelSizeInBytes">Output total size to allocate pixel buffers for all images.</param>
        private static List<int> CalculateImageArray(ImageDescription imageDesc, PitchFlags pitchFlags, out int bufferCount, out int pixelSizeInBytes)
        {
            pixelSizeInBytes = 0;
            bufferCount = 0;

            var mipmapToZIndex = new List<int>();

            for (int j = 0; j < imageDesc.ArraySize; j++)
            {
                var w = imageDesc.Width;
                var h = imageDesc.Height;
                var d = imageDesc.Depth;

                for (int i = 0; i < imageDesc.MipLevels; i++)
                {
                    int rowPitch, slicePitch;
                    int widthPacked;
                    int heightPacked;
                    ComputePitch(imageDesc.Format, (int)w, (int)h, out rowPitch, out slicePitch, out widthPacked, out heightPacked, pitchFlags);

                    // Store the number of z-slices per miplevel
                    if (j == 0)
                        mipmapToZIndex.Add(bufferCount);

                    // Keep a trace of indices for the 1st array size, for each mip levels
                    pixelSizeInBytes += (int)d * slicePitch;
                    bufferCount += (int)d;

                    if (h > 1)
                        h >>= 1;

                    if (w > 1)
                        w >>= 1;

                    if (d > 1)
                        d >>= 1;
                }

                // For the last mipmaps, store just the number of zbuffers in total
                if (j == 0)
                    mipmapToZIndex.Add(bufferCount);
            }
            return mipmapToZIndex;
        }


        /// <summary>
        /// Allocates PixelBuffers for animated image
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="imageDesc"></param>
        /// <param name="pitchFlags"></param>
        /// <param name="output"></param>
        private static unsafe void SetupImageArray(IntPtr buffer, Format format, AnimatedImageDescription[] imageDesc, PixelBuffer[] output)
        {
            int index = 0;
            var pixels = (byte*)buffer;
            foreach (var desc in imageDesc)
            {
                int rowPitch, slicePitch;
                int widthPacked;
                int heightPacked;
                ComputePitch(format, (int)desc.Width, (int)desc.Height, out rowPitch, out slicePitch, out widthPacked, out heightPacked);

                // We use the same memory organization that Direct3D 11 needs for D3D11_SUBRESOURCE_DATA
                // with all slices of a given miplevel being continuous in memory
                var pxBuffer = new PixelBuffer(desc.Width, desc.Height, format, rowPitch, slicePitch, (IntPtr)pixels);
                pxBuffer.XOffset = desc.XOffset;
                pxBuffer.YOffset = desc.YOffset;
                pxBuffer.DelayNumerator = desc.DelayNumerator;
                pxBuffer.DelayDenominator = desc.DelayDenominator;
                output[index] = pxBuffer;
                ++index;

                pixels += slicePitch;
            }
        }

        /// <summary>
        /// Allocates PixelBuffers 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="imageDesc"></param>
        /// <param name="pitchFlags"></param>
        /// <param name="output"></param>
        private static unsafe void SetupImageArray(IntPtr buffer, ImageDescription imageDesc, PitchFlags pitchFlags, PixelBuffer[] output)
        {
            int index = 0;
            var pixels = (byte*)buffer;
            for (uint item = 0; item < imageDesc.ArraySize; ++item)
            {
                var w = imageDesc.Width;
                var h = imageDesc.Height;
                var d = imageDesc.Depth;

                for (uint level = 0; level < imageDesc.MipLevels; ++level)
                {
                    int rowPitch, slicePitch;
                    int widthPacked;
                    int heightPacked;
                    ComputePitch(imageDesc.Format, (int)w, (int)h, out rowPitch, out slicePitch, out widthPacked, out heightPacked, pitchFlags);

                    for (uint zSlice = 0; zSlice < d; ++zSlice)
                    {
                        // We use the same memory organization that Direct3D 11 needs for D3D11_SUBRESOURCE_DATA
                        // with all slices of a given miplevel being continuous in memory
                        output[index] = new PixelBuffer(w, h, imageDesc.Format, rowPitch, slicePitch, (IntPtr)pixels);
                        ++index;

                        pixels += slicePitch;
                    }

                    if (h > 1)
                        h >>= 1;

                    if (w > 1)
                        w >>= 1;

                    if (d > 1)
                        d >>= 1;
                }
            }
        }

        public static uint CalculateMipLevels(uint width, uint height, MipMapCount mipLevels)
        {
            return CalculateMipLevels(width, height, 1, mipLevels);
        }


        public static uint CalculateMipLevels(uint width, uint height, uint depth, MipMapCount mipLevels)
        {
            var maxMipLevels = CountMipLevels(width, height, depth);
            if (mipLevels > 1 && maxMipLevels > mipLevels)
            {
                throw new InvalidOperationException($"MipLevels must be <= {maxMipLevels}");
            }
            return mipLevels;
        }

        private static int CountMipLevels(uint width, uint height, uint depth)
        {
            /*
             * Math.Max function selects the largest dimension. 
             * Math.Log2 function calculates how many times that dimension can be divided by 2.
             * Math.Floor function handles cases where the largest dimension is not a power of 2.
             * 1 is added so that the original image has a mip level.
             */
            var max = Math.Max(Math.Max(width, height), depth);
            var levels = (int)(Math.Floor(Math.Log2(max)) + 1);
            return levels;
        }

        private class LoadSaveDelegate
        {
            public LoadSaveDelegate(ImageFileType fileType, ImageLoadDelegate load, ImageSaveDelegate save)
            {
                FileType = fileType;
                Load = load;
                Save = save;
            }

            public ImageFileType FileType;

            public ImageLoadDelegate Load;

            public ImageSaveDelegate Save;
        }

    }
}