using System;
using System.IO;
using System.Text;
using Adamantium.Engine.Compiler.Effects;
using Adamantium.Engine.Core.Effects;
using MessagePack;
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

        [Test]
        public void EffectSerializationTest()
        {
            var path = Path.Combine("EffectsData", "UIEffect.fx");
            if (File.Exists(path))
            {
                var result = EffectCompiler.CompileFromFile(path);

                var memoryStream = new MemoryStream();
                MessagePackSerializer.DefaultOptions = MessagePack.Resolvers.ContractlessStandardResolver.Options;
                MessagePackSerializer.Serialize(memoryStream, result.EffectData);
                memoryStream.Position = 0;
                var results2 = MessagePackSerializer.Deserialize<EffectData>(memoryStream);
                Assert.NotNull(results2);
            }
        }
        
        [Test]
        public void EffectDeserializationTest()
        {
            var path = Path.Combine("EffectsData", "BasicEffect.fx");
            if (File.Exists(path))
            {
                var result = EffectCompiler.CompileFromFile(path);
                result.EffectData.Save("BasicEffect1");
                var restored = EffectData.Load("BasicEffect1");
            }
        }
    }
}