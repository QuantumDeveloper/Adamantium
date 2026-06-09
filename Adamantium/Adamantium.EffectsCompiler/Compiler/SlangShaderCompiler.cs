using System;
using System.Runtime.InteropServices;
using AdamantiumVulkan.Slang;

namespace Adamantium.EffectsCompiler.Compiler
{
    /// <summary>
    /// Compiles HLSL/Slang source to SPIR-V via the Slang runtime (AdamantiumVulkan.Slang).
    /// Includes are expected to be pre-resolved by the effect preprocessor (same as the DXC path),
    /// so no file-system callback is wired here. One instance per effect compile; the underlying
    /// Slang session is reused across that effect's stages (each gets a unique module name).
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

        private readonly SlangcSession session;
        private int moduleCounter;

        public SlangShaderCompiler()
        {
            nuint userData = 0;
            session = SlangNative.SessionCreate(
                null, 0,
                CompatibilityDefineNames, CompatibilityDefineValues, CompatibilityDefineNames.Length,
                SpirvProfile, 0, ref userData);
        }

        public ShaderCompilationResult Compile(string source, string entryPoint, EffectShaderType stage)
        {
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
            }
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
            session?.SessionRelease();
        }
    }
}
