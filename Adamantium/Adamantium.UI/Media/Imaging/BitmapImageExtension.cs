using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Imaging;
using Adamantium.Imaging.Gif;

namespace Adamantium.UI.Media.Imaging;

public static class BitmapImageExtension
{
    public static BitmapImage ToBimapImage(this IRawBitmap bitmap)
    {
        return new BitmapImage(bitmap);
    }
}