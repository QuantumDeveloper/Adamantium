using System;
using System.IO;

namespace Adamantium.UI.Input;

/// <summary>
/// The file a dragged picture is offered as when <see cref="DragDropOptions.OfferImagesAsFiles"/> is on. Everything the
/// policy consists of lives here: WHERE the copy goes, WHAT it is called, and WHEN it is swept away - so an application
/// turning the option on can read the whole story in one place instead of discovering files in its temp folder.
/// </summary>
internal static class DragImageFile
{
    /// <summary>Write the picture and hand back the path, or null if it could not be written - a fallback that fails
    /// must degrade to "no file offered", never to an exception in the middle of someone else's drop.</summary>
    public static string[] Write(byte[] picture)
    {
        if (picture is not { Length: > 0 }) return null;
        // A file nobody can open is not a fallback: a TGA or a DDS is converted, while the encodings the world already
        // reads are written verbatim - no needless re-encode, and the extension tells the truth about the bytes.
        // Converted to BMP rather than PNG on purpose: it is the same picture to every application that opens it, and
        // our PNG encoder costs SECONDS where BMP costs milliseconds (see DragPicture.Convert).
        var extension = DragPicture.Extension(picture);
        if (extension == null)
        {
            picture = DragPicture.Convert(picture);
            extension = ".bmp";
            if (picture == null) return null;
        }

        try
        {
            var directory = DragDropOptions.ImageFileDirectory;
            Directory.CreateDirectory(directory);
            Sweep(directory);

            // Named after the CONTENT, so dragging the same picture twice reuses one file instead of littering, and so
            // the name carries no guessable sequence.
            var path = Path.Combine(directory, $"image-{Fingerprint(picture):x8}{extension}");
            if (!File.Exists(path) || new FileInfo(path).Length != picture.Length) File.WriteAllBytes(path, picture);
            return [path];
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Delete OUR own leftovers past their retention. Only inside our own directory, and only files we named -
    /// a sweep that reached anywhere else would be a footgun aimed at whoever set the directory.</summary>
    private static void Sweep(string directory)
    {
        var retention = DragDropOptions.ImageFileRetention;
        if (retention <= TimeSpan.Zero) return;
        var cutoff = DateTime.UtcNow - retention;
        foreach (var file in Directory.EnumerateFiles(directory, "image-*.*"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
            }
            catch (IOException)
            {
                // Still open in the application it was dropped into - it will be swept next time.
            }
        }
    }

    // FNV-1a: a few lines, no dependency, and plenty for telling one dragged picture from another.
    private static uint Fingerprint(byte[] bytes)
    {
        var hash = 2166136261u;
        foreach (var b in bytes)
        {
            hash = (hash ^ b) * 16777619u;
        }
        return hash;
    }
}
