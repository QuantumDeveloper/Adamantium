using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Adamantium.Core;
using Adamantium.Imaging.Dds;
using Adamantium.Imaging.Gif;
using Adamantium.Imaging.Ico;
using Adamantium.Imaging.Jpeg;
using Adamantium.Imaging.Png;
using Adamantium.Imaging.Png.IO;
using Adamantium.Imaging.Tga;

namespace Adamantium.Imaging;

public static class BitmapLoader
{
    public delegate IRawBitmap ImageLoadDelegate(IntPtr dataPointer, long dataSize);

    public delegate void ImageSaveDelegate(IRawBitmap image, Stream imageStream);
    
    private static readonly List<LoadSaveDelegates> _loadSaveDelegates;
    
    static BitmapLoader()
    {
        _loadSaveDelegates = new List<LoadSaveDelegates>();
        Register(ImageFileType.Gif, LoadGif, SaveGif);
        Register(ImageFileType.Png, LoadPng, null);
        Register(ImageFileType.Bmp, LoadBmp, null);
        Register(ImageFileType.Dds, LoadDds, null);
        Register(ImageFileType.Ico, LoadIco, null);
        Register(ImageFileType.Jpg, LoadJpg, null);
        Register(ImageFileType.Tga, LoadTga, null);
        Register(ImageFileType.Tiff, LoadTiff, null);
    }

    public static void Register(ImageFileType imageType, ImageLoadDelegate loadDelegate, ImageSaveDelegate saveDelegate)
    {
        var loader = new LoadSaveDelegates() { FileType = imageType, Loader = loadDelegate, Saver = saveDelegate };
        _loadSaveDelegates.Add(loader);
    }
    
    public static unsafe IRawBitmap LoadGif(IntPtr dataPointer, long dataSize)
    {
        var stream = new UnmanagedMemoryStream((byte*)dataPointer, dataSize);
        var decoder = new GifDecoder();
        var img = decoder.Decode(stream);
        return img;
    }

    public static void SaveGif(IRawBitmap image, Stream imageStream)
    {
        GIFHelper.SaveToStream((GifImage)image, imageStream);
    }
    
    public static IRawBitmap LoadPng(IntPtr dataPointer, long dataSize)
    {
        var stream = new PNGStreamReader(dataPointer, dataSize);
        var decoder = new PngDecoder(stream);
        return decoder.Decode();
    }
    
    public static IRawBitmap LoadDds(IntPtr dataPointer, long dataSize)
    {
        return DdsHelper.LoadFromMemory(dataPointer, dataSize, false, null);
    }
    
    public static IRawBitmap LoadJpg(IntPtr dataPointer, long dataSize)
    {
        return JpegHelper.LoadFromMemory(dataPointer, dataSize);
    }
    
    public static IRawBitmap LoadBmp(IntPtr dataPointer, long dataSize)
    {
        return Bmp.BitmapHelper.LoadFromMemory(dataPointer, (ulong)dataSize, false, null);
    }
    
    public static IRawBitmap LoadIco(IntPtr dataPointer, long dataSize)
    {
        return IcoHelper.LoadFromMemory(dataPointer, dataSize);
    }
    
    public static IRawBitmap LoadTga(IntPtr dataPointer, long dataSize)
    {
        return TgaHelper.LoadFromMemory(dataPointer, dataSize);
    }
    
    public static IRawBitmap LoadTiff(IntPtr dataPointer, long dataSize)
    {
        return null;
    }
    
    public static unsafe IRawBitmap Load(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        FileStream stream = null;
        IntPtr memoryPtr = IntPtr.Zero;
        long size = 0;
        try
        {
            stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            
            size = stream.Length;
            memoryPtr = Utilities.AllocateMemory((int)size);
            var bytes = new Span<byte>(memoryPtr.ToPointer(), (int)size);
            stream.Read(bytes);
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

        return Load(memoryPtr, size);
    }

    public static IRawBitmap Load(Stream stream, ImageFileType fileType)
    {
        return null;
    }
    
    private static IRawBitmap Load(IntPtr dataPointer, long dataSize)
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
            stream.Dispose();
        }
    }
    
    
    private class LoadSaveDelegates
    {
        public ImageFileType FileType { get; set; }
        
        public ImageLoadDelegate Loader { get; set; }
        
        public ImageSaveDelegate Saver { get; set; }
    }
}