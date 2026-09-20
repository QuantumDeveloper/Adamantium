using System;
using System.Collections.Generic;
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

    /// <summary>Null when it worked, the reason when it did not: throwing would only be a warning to Roslyn, and the
    /// build would "succeed" with an empty assembly.</summary>
    public static string LoadNativeLibraries(string slangPackagePath = null)
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
            return null;
        }

        lock (LoadingLock)
        {
            if (IsLibraryLoaded)
            {
                return null;
            }

            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            try
            {
                LoadSlangRuntime(slangPackagePath);

                foreach (var library in NativeLibraries)
                {
                    var path = ExtractLibrary("Adamantium.Engine.Generators", directory, library);
                    LoadLibrary(path);
                }
            }
            catch (Exception exception)
            {
                return exception.Message;
            }

            IsLibraryLoaded = true;
            return null;
        }
    }

    /// <summary>slang.dll is a 75 KB front for slang-compiler.dll, which it finds by NAME - so on the PATH, which an
    /// IDE started before an SDK upgrade no longer has. Loaded here by full path instead; a later load by name then
    /// finds the module already in the process. The PACKAGE comes first, so a machine without the Vulkan SDK still
    /// builds shaders.</summary>
    private static void LoadSlangRuntime(string slangPackagePath)
    {
        foreach (var directory in RuntimeDirectories(slangPackagePath))
        {
            var loaded = false;

            foreach (var companion in SlangRuntime)
            {
                var path = Path.Combine(directory, companion);

                if (!File.Exists(path)) continue;

                try
                {
                    LoadLibraryByPath(path);
                    loaded = true;
                }
                catch (Win32Exception)
                {
                    // Not every layout needs every piece; slang.dll says so itself if something it needs is missing.
                }
            }

            if (loaded) return;
        }
    }

    private static IEnumerable<string> RuntimeDirectories(string slangPackagePath)
    {
        if (!string.IsNullOrEmpty(slangPackagePath))
        {
            yield return Path.Combine(slangPackagePath, "contentFiles", "any", "netstandard2.0");
            yield return Path.Combine(slangPackagePath, "content");
        }

        var sdk = Environment.GetEnvironmentVariable("VULKAN_SDK");

        if (string.IsNullOrEmpty(sdk) || !Directory.Exists(sdk))
        {
            sdk = Environment.GetEnvironmentVariable("VULKAN_SDK", EnvironmentVariableTarget.Machine);
        }

        if (!string.IsNullOrEmpty(sdk) && Directory.Exists(sdk)) yield return Path.Combine(sdk, "Bin");
    }

    // Measured: only the compiler is needed for HLSL to SPIR-V. The others load when present and cost nothing when not.
    private static readonly string[] SlangRuntime =
    {
        "slang-rt.dll",
        "slang-glslang.dll",
        "slang-glsl-module.dll",
        "slang-compiler.dll",
    };

    private static unsafe void LoadLibraryByPath(string filename)
    {
        [DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
        static extern void* LoadLibraryW(ushort* lpLibFileName);

        fixed (char* p = filename)
        {
            if (LoadLibraryW((ushort*)p) is null)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to load {Path.GetFileName(filename)}.");
            }
        }
    }
}
