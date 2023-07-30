using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adamantium.Core;
using Adamantium.Imaging.Bmp;
using Adamantium.Imaging.Dds;
using Adamantium.Imaging.Gif;
using Adamantium.Imaging.Ico;
using Adamantium.Imaging.Jpeg;
using Adamantium.Imaging.Png;
using Adamantium.Imaging.Tga;
using Adamantium.Imaging.Tiff;

namespace Adamantium.Imaging;

public static class BitmapLoader
{
    public delegate IRawBitmap ImageLoadDelegate(IntPtr dataPointer, long dataSize);
    public delegate void ImageSaveDelegate(IRawBitmap image, Stream imageStream);
    
    private static readonly List<LoadSaveDelegates> _loadSaveDelegates;
    
    static BitmapLoader()
    {
        _loadSaveDelegates = new List<LoadSaveDelegates>();
        Register(ImageFileType.Gif, GIFHelper.LoadFromMemory, GIFHelper.SaveToStream);
        Register(ImageFileType.Png, PngHelper.LoadFromMemory, PngHelper.SaveToStream);
        Register(ImageFileType.Bmp, BmpHelper.LoadFromMemory, BmpHelper.SaveToStream);
        Register(ImageFileType.Dds, DdsHelper.LoadFromMemory, DdsHelper.SaveToStream);
        Register(ImageFileType.Ico, IcoHelper.LoadFromMemory, IcoHelper.SaveToStream);
        Register(ImageFileType.Jpg, JpegHelper.LoadFromMemory, JpegHelper.SaveToStream);
        Register(ImageFileType.Tga, TgaHelper.LoadFromMemory, TgaHelper.SaveToStream);
        Register(ImageFileType.Tiff, TiffHelper.LoadFromMemory, TiffHelper.SaveToStream);
    }

    public static void Register(ImageFileType imageType, ImageLoadDelegate loadDelegate, ImageSaveDelegate saveDelegate)
    {
        var loader = new LoadSaveDelegates() { FileType = imageType, Loader = loadDelegate, Saver = saveDelegate };
        _loadSaveDelegates.Add(loader);
    }
    
    public static IRawBitmap Load(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
            return Load(stream);
        }
    }

    public static unsafe IRawBitmap Load(Stream stream)
    {
        IntPtr memoryPtr = IntPtr.Zero;
        try
        {
            var size = stream.Length;
            memoryPtr = Utilities.AllocateMemory((int)size);
            var bytes = new Span<byte>(memoryPtr.ToPointer(), (int)size);
            stream.Read(bytes);
            return Load(memoryPtr, size);
        }
        finally
        {
            try
            {
                stream?.Dispose();
                if (memoryPtr != IntPtr.Zero)
                    Utilities.FreeMemory(memoryPtr);
            }
            catch { }
        }
    }
    
    public static IRawBitmap Load(IntPtr dataPointer, long dataSize)
    {
        foreach (var loader in _loadSaveDelegates)
        {
            try
            {
                var img = loader.Loader?.Invoke(dataPointer, dataSize);
                return img;
            }
            catch (Exception e)
            {
                // ignore
            }
        }

        return null;
    }

    public static void Save(IRawBitmap bitmap, string path, ImageFileType fileType)
    {
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        try
        {
            var saveDelegate = _loadSaveDelegates.FirstOrDefault(x => x.FileType == fileType);
            saveDelegate?.Saver?.Invoke(bitmap, stream);
        }
        finally
        {
            stream.Flush();
            stream.Dispose();
        }
    }
    
    public static void Save(IRawBitmap bitmap, Stream stream, ImageFileType fileType)
    {
        var saveDelegate = _loadSaveDelegates.FirstOrDefault(x => x.FileType == fileType);
        saveDelegate?.Saver?.Invoke(bitmap, stream);
    }
    
    private class LoadSaveDelegates
    {
        public ImageFileType FileType { get; set; }
        
        public ImageLoadDelegate Loader { get; set; }
        
        public ImageSaveDelegate Saver { get; set; }
    }
}