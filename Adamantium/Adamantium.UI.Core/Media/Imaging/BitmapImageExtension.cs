using Adamantium.Imaging;

namespace Adamantium.UI.Core.Media.Imaging;

public static class BitmapImageExtension
{
    public static BitmapImage ToBimapImage(this IRawBitmap bitmap)
    {
        return new BitmapImage(bitmap);
    }
}