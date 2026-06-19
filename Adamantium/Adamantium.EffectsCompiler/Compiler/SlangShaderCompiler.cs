using System;
using System.Collections.Generic;
using Adamantium.Vulkan.Slang;

namespace Adamantium.EffectsCompiler.Compiler
{
    /// <summary>
    /// Engine-side adapter over the shared <see cref="Adamantium.Vulkan.Slang.SlangCompiler"/>: the native Slang
    /// session, <c>#include</c> callback and SPIR-V extraction live in that one class (reused by the Vulkan demo too),
    /// while this wrapper supplies the engine's configuration and translates between engine and Slang types - it maps
    /// <see cref="EffectShaderType"/> to <see cref="SlangcStage"/>, routes the engine's include collection through the
    /// compiler's resolver, and wraps the output as a <see cref="ShaderCompilationResult"/>. One instance per effect
    /// compile; the session is reused across that effect's stages.
    /// </summary>
    internal sealed class SlangShaderCompiler : IDisposable
    {
        // Matches the DXC path's target (SPIR-V 1.6) so both backends emit for the same Vulkan environment.
        private const string SpirvProfile = "spirv_1_6";

        private readonly SlangCompiler compiler;

        public SlangShaderCompiler()
        {
            compiler = new SlangCompiler(
                SpirvProfile,
                // VulkanUseEntryPointName keeps the real entry-point name in the SPIR-V (instead of "main") so Vulkan
                // can create pipelines by the shader's name.
                options:
                [
                    new SlangcCompilerOption
                    {
                        Name = (int)SlangcCompilerOptionName.VulkanUseEntryPointName,
                        ValueKind = 0,
                        IntValue0 = 1
                    }
                ],
                // Legacy HLSL spellings the engine's .fx files use but Slang's stricter front-end doesn't define
                // (DXC tolerated these as-is); applied as session-wide preprocessor macros.
                defines: new Dictionary<string, string> { ["sampler"] = "SamplerState" });
        }

        /// <param name="resolveInclude">Maps an <c>#include</c> path to file contents (null = not found).</param>
        public ShaderCompilationResult Compile(string source, string entryPoint, EffectShaderType stage, Func<string, string> resolveInclude)
        {
            compiler.IncludeResolver = resolveInclude;
            try
            {
                var result = compiler.Compile(source, entryPoint, MapStage(stage));
                return new ShaderCompilationResult
                {
                    Bytecode = result.Spirv,
                    HasErrors = !result.Success,
                    Errors = result.Diagnostics
                };
            }
            finally
            {
                compiler.IncludeResolver = null;
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

        public void Dispose() => compiler.Dispose();
    }
}
