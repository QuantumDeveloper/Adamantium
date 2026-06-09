using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AdamantiumVulkan.Slang;

namespace Adamantium.EffectsCompiler.Compiler
{
    /// <summary>
    /// Compiles HLSL/Slang source to SPIR-V via the Slang runtime (AdamantiumVulkan.Slang).
    /// Unlike the DXC path, the source is handed to Slang with its <c>#include</c> directives intact and
    /// resolved by Slang itself through a VFS callback wired to the engine's include collection — Slang has
    /// proper include processing, so no flattening is needed. One instance per effect compile; the Slang
    /// session is reused across that effect's stages (each gets a unique module name).
    /// </summary>
    internal sealed class SlangShaderCompiler : IDisposable
    {
        // Matches the DXC path's target (CompilerArguments.SpvTargetEnvVulkan1_3 -> SPIR-V 1.6) so both
        // backends emit for the same Vulkan environment.
        private const string SpirvProfile = "spirv_1_6";

        // Legacy HLSL spellings the engine's .fx files use but Slang's stricter front-end doesn't define.
        // Applied as session-wide preprocessor macros (cleaner than prepending source, and leaves the
        // source untouched so diagnostics line numbers stay correct). DXC tolerates these as-is.
        private static readonly string[] CompatibilityDefineNames = { "sampler" };
        private static readonly string[] CompatibilityDefineValues = { "SamplerState" };

        // Slang compiler options, configurable here without touching the native shim. VulkanUseEntryPointName
        // keeps the real entry-point name in the SPIR-V (instead of "main") so Vulkan can create pipelines by
        // the shader's name. Add more options to this array as needed.
        private static readonly SlangcCompilerOption[] CompilerOptions =
        {
            new SlangcCompilerOption
            {
                Name = (int)SlangcCompilerOptionName.VulkanUseEntryPointName,
                ValueKind = 0, // int / bool
                IntValue0 = 1
            }
        };

        // Native VFS callback: int(void* userData, const char* path, const unsigned char** outData, size_t* outSize).
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SlangcLoadFile(IntPtr userData, IntPtr path, out IntPtr outData, out UIntPtr outSize);

        private readonly SlangcSession session;
        private readonly SlangcLoadFile loadFileDelegate; // kept alive for the session's lifetime
        private readonly List<IntPtr> pendingBuffers = new List<IntPtr>();

        private Func<string, string> includeResolver;
        private int moduleCounter;

        public SlangShaderCompiler()
        {
            loadFileDelegate = LoadFile;
            var loadFilePtr = unchecked((nuint)Marshal.GetFunctionPointerForDelegate(loadFileDelegate).ToInt64());

            nuint userData = 0;
            session = SlangNative.SessionCreate(
                null, 0,
                CompatibilityDefineNames, CompatibilityDefineValues, CompatibilityDefineNames.Length,
                SpirvProfile, CompilerOptions, CompilerOptions.Length, loadFilePtr, ref userData);
        }

        /// <param name="resolveInclude">Maps an <c>#include</c> path to file contents (null = not found).</param>
        public ShaderCompilationResult Compile(string source, string entryPoint, EffectShaderType stage, Func<string, string> resolveInclude)
        {
            includeResolver = resolveInclude;

            // Unique module name per compile so Slang's per-session module cache never collides.
            var moduleName = $"m{moduleCounter++}_{entryPoint}";

            var result = session.Compile(moduleName, source, entryPoint, MapStage(stage));
            try
            {
                var ok = result.ResultOk() != 0;
                var diagnostics = result.ResultDiagnostics();

                byte[] spirv = null;
                if (ok)
                {
                    var ptr = result.ResultSpirv(out var size);
                    if (ptr != 0 && size > 0)
                    {
                        spirv = new byte[size];
                        Marshal.Copy(new IntPtr((long)(ulong)ptr), spirv, 0, (int)size);
                    }
                }

                return new ShaderCompilationResult
                {
                    Bytecode = spirv,
                    HasErrors = !ok || spirv == null,
                    Errors = diagnostics
                };
            }
            finally
            {
                result.ResultRelease();
                // The shim copies callback buffers synchronously during compilation, so they are safe to free now.
                FreePendingBuffers();
                includeResolver = null;
            }
        }

        // Called by Slang (native) for every #include it can't already satisfy. The returned buffer must stay
        // valid until the call returns; the shim copies it immediately, so we free after the compile completes.
        private int LoadFile(IntPtr userData, IntPtr path, out IntPtr outData, out UIntPtr outSize)
        {
            outData = IntPtr.Zero;
            outSize = UIntPtr.Zero;

            var requested = Marshal.PtrToStringAnsi(path);
            var content = string.IsNullOrEmpty(requested) ? null : includeResolver?.Invoke(requested);
            if (content == null)
                return 0;

            var bytes = Encoding.UTF8.GetBytes(content);
            var buffer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            pendingBuffers.Add(buffer);

            outData = buffer;
            outSize = (UIntPtr)bytes.Length;
            return 1;
        }

        private void FreePendingBuffers()
        {
            foreach (var buffer in pendingBuffers)
                Marshal.FreeHGlobal(buffer);
            pendingBuffers.Clear();
        }

        private static SlangcStage MapStage(EffectShaderType type)
        {
            switch (type)
            {
                case EffectShaderType.Vertex: return SlangcStage.Vertex;
                case EffectShaderType.Hull: return SlangcStage.Hull;
                case EffectShaderType.Domain: return SlangcStage.Domain;
                case EffectShaderType.Geometry: return SlangcStage.Geometry;
                case EffectShaderType.Fragment: return SlangcStage.Fragment;
                case EffectShaderType.Compute: return SlangcStage.Compute;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported shader stage for Slang.");
            }
        }

        public void Dispose()
        {
            FreePendingBuffers();
            session?.SessionRelease();
        }
    }
}
