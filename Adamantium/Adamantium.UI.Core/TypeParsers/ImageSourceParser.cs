using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.TypeParsers;

public class ImageSourceParser : ITypeParser<BitmapSource>
{
    public BitmapSource Parse(string value)
    {
        // Resolve a relative asset path against the APPLICATION base directory (where the exe + its copied content live),
        // NOT the process working directory. BitmapImage.Load ultimately calls BitmapLoader.Load(path) with a relative
        // path, which the OS resolves against CWD - so launching from anywhere but the output dir (e.g. an IDE that sets
        // CWD to the project folder) made every image silently fail to decode: the background load Task faults, nobody
        // awaits it, and IsLoaded stays false forever (the control just shows its no-image fallback, no error logged).
        // Rooting the path here makes image loading independent of how the app was launched.
        var full = Path.IsPathRooted(value) ? value : Path.Combine(AppContext.BaseDirectory, value);
        // Through the cache: the same file shown twice is one decode and one texture, not two (see BitmapImageCache).
        return BitmapImageCache.GetOrCreate(full);
    }
}
