using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Imaging;
using Adamantium.Core;

namespace Adamantium.Engine.Graphics
{
    public abstract class Texture : DisposableBase
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public SurfaceFormat Format { get; set; }

        public IntPtr NativePointer { get; }

        public void Save(string path, ImageFileType fileType)
        {

        }

        //public static int CalculateMipLevels(int width, int height, MipMapCount mipLevels)
        //{
        //    return CalculateMipLevels(width, height, 1, mipLevels);
        //}


        //public static int CalculateMipLevels(int width, int height, int depth, MipMapCount mipLevels)
        //{
        //    var maxMipLevels = CountMipLevels(width, height, depth);
        //    if (mipLevels > 1 && maxMipLevels > mipLevels)
        //    {
        //        throw new InvalidOperationException($"MipLevels must be <= {maxMipLevels}");
        //    }
        //    return mipLevels;
        //}

        //private static int CountMipLevels(int width, int height, int depth)
        //{
        //    /*
        //     * Math.Max function selects the largest dimension. 
        //     * Math.Log2 function calculates how many times that dimension can be divided by 2.
        //     * Math.Floor function handles cases where the largest dimension is not a power of 2.
        //     * 1 is added so that the original image has a mip level.
        //     */
        //    var max = Math.Max(Math.Max(width, height), depth);
        //    var levels = (int)(Math.Floor(Math.Log2(max)) + 1);
        //    return levels;
        //}
    }
}
