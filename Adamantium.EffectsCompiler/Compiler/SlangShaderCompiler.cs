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
        /// <summary>The SPIR-V versions a pass may ask for by <c>Profile</c>, newest last. The last is what a pass that
        /// names none is compiled at.</summary>
        public static readonly IReadOnlyList<string> SpirvProfiles =
            ["spirv_1_0", "spirv_1_1", "spirv_1_2", "spirv_1_3", "spirv_1_4", "spirv_1_5", "spirv_1_6"];

        /// <summary>The newest of <see cref="SpirvProfiles"/> - the default target.</summary>
        public static string LatestSpirvProfile => SpirvProfiles[SpirvProfiles.Count - 1];

        // ONE SESSION PER PROFILE, built on demand. The profile is a property of the Slang SESSION, while Profile is
        // declared per PASS, so an effect whose passes disagree needs one session each - and an effect whose passes all
        // take the default (which is all of them) still builds exactly one.
        private readonly Dictionary<string, SlangCompiler> compilers = new();

        private SlangCompiler CompilerFor(string profile)
        {
            if (compilers.TryGetValue(profile, out var existing)) return existing;

            var created = new SlangCompiler(
                profile,
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
                // Legacy HLSL spellings the engine's .fx files still use but Slang's stricter front end does not define;
                // applied as session-wide preprocessor macros.
                defines: new Dictionary<string, string> { ["sampler"] = "SamplerState" });

            compilers[profile] = created;
            return created;
        }

        /// <param name="profile">The SPIR-V target profile, one of <see cref="SpirvProfiles"/>.</param>
        /// <param name="resolveInclude">Maps an <c>#include</c> path to file contents (null = not found).</param>
        public ShaderCompilationResult Compile(string source, string entryPoint, EffectShaderType stage, string profile,
            Func<string, string> resolveInclude)
        {
            var compiler = CompilerFor(profile);
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

        public void Dispose()
        {
            foreach (var compiler in compilers.Values) compiler.Dispose();
            compilers.Clear();
        }
    }
}
