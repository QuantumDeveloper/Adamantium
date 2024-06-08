using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Generators;

public class DxcLibraryLoader
{
    private static readonly object LoadingLock = new();

    private static volatile bool IsLibraryLoaded;

    public static void LoadNativeDxLibrary()
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

            var path = ExtractLibrary("Adamantium.Engine.Generators", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "dxcompiler.dll");
            LoadLibrary(path);
            path = ExtractLibrary("Adamantium.Engine.Generators", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "spirv-cross-c-shared.dll");
            LoadLibrary(path);

            IsLibraryLoaded = true;
        }
    }
}