using System;
using System.IO;
using System.Security.Cryptography;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics;

/// <summary>
/// On-disk cache of DRIVER-compiled shader-object binaries (VK_EXT_shader_object). Its whole purpose is to dodge the
/// Turing/Quadro <c>vkCreateShadersEXT</c> flake: creating a shader from SPIR-V runs the driver's NVVM compiler in-process
/// and intermittently access-violates (a corrupted-state exception that can't be caught). A shader created ONCE from
/// SPIR-V can hand back its compiled binary (<c>vkGetShaderBinaryDataEXT</c>); persisting that lets every LATER launch
/// create the shader from the BINARY instead - the driver just loads it, no NVVM, no flake.
/// </summary>
/// <remarks>
/// Binaries are device + driver specific: the cache is keyed by the physical device's pipeline-cache UUID + driver
/// version, so a driver update or a different GPU simply misses (and recompiles once). The FIRST cold compile of a shader
/// still runs NVVM (still flaky) - that launch warms the cache; a respawn-until-success wrapper turns "warm the cache"
/// into "reliable startup". Not our bug to fix (buggy driver); this is the durable workaround. Single-threaded (shader
/// objects are created serially at startup). All IO is best-effort: any failure falls back to the SPIR-V path.
/// </remarks>
public static class ShaderBinaryCache
{
    /// <summary>Master switch (off = always compile from SPIR-V, the pre-cache behaviour).</summary>
    public static bool Enabled = true;

    private static string _deviceDir;   // per-device cache folder, resolved once

    // The per-device cache folder: %LOCALAPPDATA%/Adamantium/ShaderCache/<deviceHash>/. Keyed by the STABLE identity of
    // the GPU + driver (device name + vendor/device id + driver version) - which changes exactly when a cached binary
    // would become incompatible. NB pipelineCacheUUID is deliberately NOT used: its marshalled bytes were not stable
    // across runs here, so it spawned a fresh folder every launch and the cache never hit (the whole point defeated).
    private static string DeviceDir(GraphicsDevice device)
    {
        if (_deviceDir != null) return _deviceDir;
        var props = device.MainDevice.GraphicsAdapter.AdapterProperties;
        var key = $"{props.DeviceName}|{props.VendorID:X}|{props.DeviceID:X}|{props.DriverVersion:X}";
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key))).Substring(0, 16);
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _deviceDir = Path.Combine(root, "Adamantium", "ShaderCache", hash);
        return _deviceDir;
    }

    // The cache file for one shader: "<effect>.<technique>.<pass>.<stage>_<hash>.shaderbin". The NAME is what makes the
    // folder readable - which shader is which, and how many actually compiled - and the HASH is what keeps it correct:
    // it covers the SPIR-V bytes + stage + entry-point name, so editing a shader lands on a new file instead of loading
    // a binary that no longer matches the code (the name alone cannot tell two versions apart).
    private static string FileFor(GraphicsDevice device, ShaderCreateInfoEXT info, string name)
    {
        using var sha = SHA256.Create();
        var buf = new MemoryStream();
        if (!info.PCode.IsEmpty) buf.Write(info.PCode.Span);
        buf.Write(BitConverter.GetBytes((uint)info.Stage));
        // Hash the entry-point NAME's bytes, not String.GetHashCode - the latter is RANDOMISED per process, so it made
        // the file name differ every run and the cache never hit.
        if (!string.IsNullOrEmpty(info.PName)) buf.Write(System.Text.Encoding.UTF8.GetBytes(info.PName));
        var hash = Convert.ToHexString(sha.ComputeHash(buf.ToArray())).Substring(0, 16);
        var readable = Sanitize(name) ?? info.Stage.ToString();
        return Path.Combine(DeviceDir(device), $"{readable}_{hash}.shaderbin");
    }

    // File names come from shader/technique/pass names, so keep only what a file name may hold.
    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]) || chars[i] is '.' or '-' or '_') continue;
            chars[i] = '-';
        }
        return new string(chars);
    }

    /// <summary>Try to read a cached driver binary for this shader. False = miss (compile from SPIR-V and then Save).</summary>
    public static bool TryLoad(GraphicsDevice device, ShaderCreateInfoEXT info, string name, out byte[] binary)
    {
        binary = null;
        if (!Enabled) return false;
        try
        {
            var file = FileFor(device, info, name);
            if (!File.Exists(file)) return false;
            binary = File.ReadAllBytes(file);
            return binary.Length > 0;
        }
        catch { return false; }
    }

    /// <summary>Persist the driver-compiled binary of a freshly created shader object, so later launches skip NVVM.</summary>
    public static void Save(GraphicsDevice device, ShaderCreateInfoEXT info, string name, ShaderEXT shader)
    {
        if (!Enabled) return;
        try
        {
            nuint size = 0;
            if (device.LogicalDevice.GetShaderBinaryDataEXT(shader, ref size, default) != Result.Success || size == 0) return;
            var bytes = new byte[(int)size];
            if (device.LogicalDevice.GetShaderBinaryDataEXT(shader, ref size, bytes) != Result.Success) return;

            var file = FileFor(device, info, name);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var tmp = file + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, file, overwrite: true);   // atomic-ish: a partial write never leaves a half file as the cache
        }
        catch { /* best effort - a failed cache write just means we recompile next time */ }
    }

    /// <summary>A copy of <paramref name="info"/> that creates from a driver BINARY instead of SPIR-V (cache-hit path).</summary>
    public static ShaderCreateInfoEXT AsBinary(ShaderCreateInfoEXT info, byte[] binary) => new()
    {
        Flags = info.Flags,
        Stage = info.Stage,
        NextStage = info.NextStage,
        CodeType = ShaderCodeTypeEXT.BinaryExt,
        CodeSize = (nuint)binary.Length,
        PCode = binary,
        PName = info.PName,
        SetLayoutCount = info.SetLayoutCount,
        PSetLayouts = info.PSetLayouts,
        PushConstantRangeCount = info.PushConstantRangeCount,
        PushConstantRanges = info.PushConstantRanges,
        PSpecializationInfo = info.PSpecializationInfo
    };
}
