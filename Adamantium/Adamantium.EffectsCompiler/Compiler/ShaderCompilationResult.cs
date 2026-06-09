namespace Adamantium.EffectsCompiler.Compiler
{
    /// <summary>
    /// Backend-neutral result of compiling a single shader stage to SPIR-V.
    /// Produced by either the Slang or the DXC backend so the rest of the pipeline
    /// (reflection, parameter building) does not care who emitted the bytecode.
    /// </summary>
    internal sealed class ShaderCompilationResult
    {
        public byte[] Bytecode { get; set; }

        public bool HasErrors { get; set; }

        public string Errors { get; set; }

        public static implicit operator byte[](ShaderCompilationResult result) => result?.Bytecode;
    }
}
