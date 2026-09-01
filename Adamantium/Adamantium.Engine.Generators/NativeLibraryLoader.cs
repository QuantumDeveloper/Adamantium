using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Generators;

/// <summary>
/// Loads the native libraries the effect compiler needs into the generator (analyzer) process.
/// The libraries are shipped as embedded resources and extracted next to the (possibly shadow-copied)
/// generator assembly before loading — analyzers receive neither NuGet runtime/native assets nor a
/// search-path entry, and netstandard2.0 has no <c>NativeLibrary</c> API, so this is the robust route.
/// </summary>
public class NativeLibraryLoader
{
    private static readonly object LoadingLock = new();

    private static volatile bool IsLibraryLoaded;

    // Loaded in order; a shim must come after the runtime it imports
    // (slang-c-shared.dll imports slang.dll), so list dependencies first.
    private static readonly string[] NativeLibraries =
    {
        "spirv-cross-c-shared.dll",
        "slang.dll",
        "slang-c-shared.dll",
    };

    public static void LoadNativeLibraries()
    {
        static string ExtractLibrary(string @namespace, string dstPath, string dllName)
        {
            using Stream sourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{@namespace}.{dllName}");

            string finalPath = Path.Combine(dstPath, dllName);

            try
            {
                using Stream destinationStream = File.Open(finalPath, FileMode.OpenOrCreate, FileAccess.Write);

                sourceStream.CopyTo(destinationStream);

                sourceStream.Close();
                sourceStream.Dispose();
                destinationStream.Dispose();

            }
            catch (IOException)
            {
            }

            return finalPath;
        }

        static unsafe void LoadLibrary(string filename)
        {
            [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
            static extern void* LoadLibraryW(ushort* lpLibFileName);

            fixed (char* p = filename)
            {
                if (LoadLibraryW((ushort*)p) is null)
                {
                    int hresult = Marshal.GetLastWin32Error();

                    throw new Win32Exception(hresult, $"Failed to load {Path.GetFileName(filename)}.");
                }
            }
        }

        if (IsLibraryLoaded)
        {
            return;
        }

        lock (LoadingLock)
        {
            if (IsLibraryLoaded)
            {
                return;
            }

            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            foreach (var library in NativeLibraries)
            {
                var path = ExtractLibrary("Adamantium.Engine.Generators", directory, library);
                LoadLibrary(path);
            }

            IsLibraryLoaded = true;
        }
    }
}
