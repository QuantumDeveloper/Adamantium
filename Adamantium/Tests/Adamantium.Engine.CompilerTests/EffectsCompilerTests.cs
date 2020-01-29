using System.IO;
using Adamantium.Engine.Compiler.Effects;
using Adamantium.Engine.Core.Effects;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    [TestFixture]
    public class EffectsCompilerTests
    {
        [Test]
        public void EffectParsingTest()
        {
            var path = Path.Combine("EffectsData", "UIEffect.fx");
            var result = EffectCompiler.CompileFromFile(path);
            foreach (var tech in result.EffectData.Description.Techniques)
            {
                foreach(var pass in tech.Passes)
                {
                    for (int i = 0; i< pass.Pipeline.Links.Length; ++i)
                    {
                        if (pass.Pipeline.Links[i] != null)
                        {
                            pass.Pipeline.Links2.Add(pass.Pipeline.Links[i]);
                        }
                        else
                        {
                            pass.Pipeline.Links[i] = new EffectData.ShaderLink();
                        }
                    }
                }
            }
            result.EffectData.Save("UIEffect");
            var restored = EffectData.Load("UIEffect");
        }
    }
}