using System;
using System.Collections.Immutable;
using System.Linq;
using Adamantium.EffectsCompiler;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    /// <summary>
    /// A pass per KIND - the shape the pattern family already has - needs one shared shading function and a thin entry
    /// point per pass that calls it with a literal. Adding that shape to an effect took the compiler down with an
    /// access violation and no diagnostic, so this pins the smallest source that does it, away from any real effect.
    /// <para>Compiling a STRING rather than editing a real .fx and rebuilding: the question is about one construct, and
    /// the answer came back in seconds instead of three minutes a guess.</para>
    /// </summary>
    [TestFixture]
    public class HelperCallCompileTests
    {
        private const string Prologue = @"
struct ProbeIn
{
    float4 Position : SV_Position;
    float2 Local    : TEXCOORD0;
};

[shader(""vertex"")]
ProbeIn ProbeVS(uint vertexId : SV_VertexID)
{
    ProbeIn o;
    o.Position = float4(0.0, 0.0, 0.0, 1.0);
    o.Local = float2(0.0, 0.0);
    return o;
}
";

        private const string Technique = @"
technique Probe
{
    pass Only
    {
        VertexShader = ProbeVS;
        PixelShader = ProbePS;
    }
}
";

        // Through a real FILE: the compiler resolves includes against the source's own directory, so it needs a path
        // that exists - a bare name leaves it enumerating "".
        private static string Diagnose(string body)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"probe_{Guid.NewGuid():N}.fx");
            System.IO.File.WriteAllText(path, Prologue + body + Technique);
            try
            {
                var result = EffectCompiler.CompileFromFile(path);
                return result.HasErrors
                    ? string.Join(Environment.NewLine, result.Logger.Messages.Select(m => m.ToString()))
                    : null;
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>Compile whatever ADAM_FX names. A shader question answered by editing a real effect and rebuilding
        /// costs three minutes and reports nothing; through here it costs a second and reports the compiler's own
        /// message. Ignored when the variable is unset, so it never runs in an ordinary pass.</summary>
        [Test]
        public void CompilesTheFileNamedByEnvironment()
        {
            var path = Environment.GetEnvironmentVariable("ADAM_FX");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("ADAM_FX not set");

            // The generator hands the includes in as CONTENT, not as a search path - so this does the same: every
            // .fxh beside the effect, keyed by name.
            // ADAM_FX_INCLUDES lets the effect under test live anywhere - a scratch copy in a temp folder, say, while
            // its .fxh files stay where they are.
            var dir = Environment.GetEnvironmentVariable("ADAM_FX_INCLUDES") is { Length: > 0 } d
                ? d
                : System.IO.Path.GetDirectoryName(path);
            var includes = System.IO.Directory
                .GetFiles(dir, "*.fxh", System.IO.SearchOption.AllDirectories)
                .Select(f => new ShaderFileInfo
                {
                    FileName = System.IO.Path.GetFileName(f),
                    Path = f,
                    Content = System.IO.File.ReadAllText(f)
                })
                .ToImmutableArray();

            var result = EffectCompiler.Compile(System.IO.File.ReadAllText(path), path, includes);
            var messages = string.Join(Environment.NewLine, result.Logger.Messages.Select(m => m.ToString()));
            TestContext.WriteLine(messages);
            Assert.That(result.HasErrors, Is.False, messages);
        }

        // The control: an entry point that computes its own answer compiles, so anything below that fails differs from
        // this by the CALL alone.
        [Test]
        public void AnEntryPointOnItsOwnCompiles()
        {
            var errors = Diagnose(@"
[shader(""fragment"")] float4 ProbePS(ProbeIn i) : SV_Target { return float4(1.0, i.Local.x, 0.0, 1.0); }
");
            Assert.That(errors, Is.Null, errors);
        }

        // ...and the same answer reached through a shared function, which is what a pass per kind is made of.
        [Test]
        public void AnEntryPointMayCallASharedShadingFunction()
        {
            var errors = Diagnose(@"
float4 ProbeShade(ProbeIn input, int k) { return float4(float(k), input.Local.x, 0.0, 1.0); }

[shader(""fragment"")] float4 ProbePS(ProbeIn i) : SV_Target { return ProbeShade(i, 1); }
");
            Assert.That(errors, Is.Null, errors);
        }
    }
}
