using System;
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
        public void UIEffectParsingTest()
        {
            try
            {
                var path = Path.Combine("EffectsData", "UIEffect.fx");
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path);
                    var result = EffectCompiler.CompileFromFile(path);
            
                    result.EffectData.Save("UIEffect.fx.compiled");
                    var restored = EffectData.Load("UIEffect.fx.compiled");
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }
        
        [Test]
        public void BasicEffectParsingTest()
        {
            try
            {
                var path = Path.Combine("EffectsData", "BasicEffect.fx");
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path);
                    var result = EffectCompiler.CompileFromFile(path);
            
                    result.EffectData.Save("BasicEffect.fx.compiled");
                    var restored = EffectData.Load("BasicEffect.fx.compiled");
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }
    }
}