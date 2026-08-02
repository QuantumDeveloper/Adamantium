using System.Collections.Generic;

namespace Adamantium.UI.Core.Media.Imaging;

/// <summary>
/// One decoded image per FILE, however many places show it. Without this every <c>Source="…"</c> built its own
/// <see cref="BitmapImage"/>: the same file was read and decoded again for each one (measured at 1.4 s apiece for a
/// 4984x3858 TGA), each kept its own copy of the pixels, and each uploaded its own texture - two views of one picture
/// cost twice everything. Returning to a view that shows it paid the whole price a second time.
/// </summary>
/// <remarks>
/// Entries are WEAK: the cache never keeps an image alive by itself, it only stops a second copy being made while one is
/// still in use. When the last control showing a picture is gone, the entry dies with it and the file will be decoded
/// again if it is ever shown again - which is the honest trade while nothing else tracks how long a picture is needed.
/// Playback state does NOT live in the image (the control owns its own cursor and range), so sharing one source between
/// images with different speeds or frame ranges is safe.
/// </remarks>
public static class BitmapImageCache
{
    private static readonly Dictionary<string, WeakReference<BitmapImage>> Entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Master switch - off makes every request build its own image, as before.</summary>
    public static bool Enabled { get; set; } = true;

    public static BitmapImage GetOrCreate(string fullPath)
    {
        if (!Enabled) return new BitmapImage(new Uri(fullPath));

        lock (Entries)
        {
            if (Entries.TryGetValue(fullPath, out var entry) && entry.TryGetTarget(out var cached) && !cached.IsDisposed)
            {
                return cached;
            }

            var image = new BitmapImage(new Uri(fullPath));
            Entries[fullPath] = new WeakReference<BitmapImage>(image);
            return image;
        }
    }
}
