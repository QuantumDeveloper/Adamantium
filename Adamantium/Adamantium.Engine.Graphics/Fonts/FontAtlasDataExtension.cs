using Adamantium.Fonts.TextureGeneration;
using Adamantium.Imaging;

namespace Adamantium.Engine.Graphics.Fonts;

public static class FontAtlasDataExtension
{
    public static void SaveAtlas(this FontAtlasData data, string path)
    {
        var img = Image.New2D((uint)data.AtlasSize.Width, (uint)data.AtlasSize.Height, SurfaceFormat.R8G8B8A8.UNorm);
        var pixels = img.GetPixelBuffer(0, 0);
        pixels.SetPixels(data.ImageData);
        img.Save(path, ImageFileType.Png);
    }
}