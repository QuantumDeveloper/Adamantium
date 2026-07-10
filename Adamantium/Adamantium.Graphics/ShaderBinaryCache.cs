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

    // The per-device cache folder: %LOCALAPPDATA%/Adamantium/ShaderCache/<deviceHash>/. The hash folds the pipeline-cache
    // UUID + driver version + device/vendor id, so it changes exactly when a cached binary would become incompatible.
    private static string DeviceDir(GraphicsDevice device)
    {
        if (_deviceDir != null) return _deviceDir;
        var props = device.MainDevice.GraphicsAdapter.AdapterProperties;
        using var sha = SHA256.Create();
        var seed = new MemoryStream();
        var uuid = props.PipelineCacheUUID;
        if (!uuid.IsEmpty) seed.Write(uuid.Span);
        var scalars = new byte[12];
        BitConverter.TryWriteBytes(scalars.AsSpan(0), props.DriverVersion);
        BitConverter.TryWriteBytes(scalars.AsSpan(4), props.VendorID);
        BitConverter.TryWriteBytes(scalars.AsSpan(8), props.DeviceID);
        seed.Write(scalars);
        var hash = Convert.ToHexString(sha.ComputeHash(seed.ToArray())).Substring(0, 16);
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _deviceDir = Path.Combine(root, "Adamantium", "ShaderCache", hash);
        return _deviceDir;
    }

    // The cache file for one shader = a hash of its SPIR-V bytes + stage + entry-point name (the SPIR-V already implies
    // the set layouts / push constants, which are reflected from it).
    private static string FileFor(GraphicsDevice device, ShaderCreateInfoEXT info)
    {
        using var sha = SHA256.Create();
        var buf = new MemoryStream();
        if (!info.PCode.IsEmpty) buf.Write(info.PCode.Span);
        var tail = new byte[8];
        BitConverter.TryWriteBytes(tail.AsSpan(0), (uint)info.Stage);
        BitConverter.TryWriteBytes(tail.AsSpan(4), (uint)(info.PName?.GetHashCode() ?? 0));
        buf.Write(tail);
        var name = Convert.ToHexString(sha.ComputeHash(buf.ToArray()));
        return Path.Combine(DeviceDir(device), name + ".shaderbin");
    }

    /// <summary>Try to read a cached driver binary for this shader. False = miss (compile from SPIR-V and then Save).</summary>
    public static bool TryLoad(GraphicsDevice device, ShaderCreateInfoEXT info, out byte[] binary)
    {
        binary = null;
        if (!Enabled) return false;
        try
        {
            var file = FileFor(device, info);
            if (!File.Exists(file)) return false;
            binary = File.ReadAllBytes(file);
            return binary.Length > 0;
        }
        catch { return false; }
    }

    /// <summary>Persist the driver-compiled binary of a freshly created shader object, so later launches skip NVVM.</summary>
    public static void Save(GraphicsDevice device, ShaderCreateInfoEXT info, ShaderEXT shader)
    {
        if (!Enabled) return;
        try
        {
            nuint size = 0;
            if (device.LogicalDevice.GetShaderBinaryDataEXT(shader, ref size, default) != Result.Success || size == 0) return;
            var bytes = new byte[(int)size];
            if (device.LogicalDevice.GetShaderBinaryDataEXT(shader, ref size, bytes) != Result.Success) return;

            var file = FileFor(device, info);
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
