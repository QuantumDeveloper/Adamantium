using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Adamantium.EffectsCompiler;
using MessagePack;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    [TestFixture]
    public class EffectsCompilerTests
    {
        /// <summary>A compile that FAILS must say so with the compiler's own messages. Dereferencing EffectData without
        /// asking whether the compile succeeded turned every shader error into a bare NullReferenceException with the
        /// reason thrown away - which is why this sat red without anyone being able to tell what it was complaining about.
        /// </summary>
        private static EffectData CompileOrFail(string path)
        {
            var result = EffectCompiler.CompileFromFile(path);
            var messages = string.Join(Environment.NewLine, result.Logger.Messages);
            Assert.That(result.HasErrors, Is.False, $"{path} failed to compile:{Environment.NewLine}{messages}");
            Assert.That(result.EffectData, Is.Not.Null, $"{path} compiled without errors but produced no EffectData");
            return result.EffectData;
        }

        [Test]
        public void UIEffectParsingTest()
        {
            var path = Path.Combine("CompilerEffects", "UIEffect.fx");
            if (!File.Exists(path)) Assert.Ignore($"missing test asset: {path}");

            var effectData = CompileOrFail(path);
            effectData.Save("UIEffect.fx.compiled");
            Assert.That(EffectData.Load("UIEffect.fx.compiled"), Is.Not.Null, "the saved effect did not load back");
        }
        
        [Test]
        public void BasicEffectParsingTest()
        {
            try
            {
                var path = Path.Combine("CompilerEffects", "BasicEffect.fx");
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path);
                    var result = EffectCompiler.CompileFromFile(path);
                    
                    var effectParameters = new List<string>();
                    var parametersIdentity = new HashSet<string>();
                    foreach (var shader in result.EffectData.Shaders)
                    {
                        foreach (var constantBuffer in shader.ConstantBuffers)
                        {
                            foreach (var parameter in constantBuffer.Parameters)
                            {
                                if (parametersIdentity.Contains(parameter.Name)) continue;

                                parametersIdentity.Add(parameter.Name);
                                var name = char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1);
                                effectParameters.Add(name);
                                //textGenerator.WriteLine($"{name} = Parameters[\"{parameter.Name}\"];");
                            }
                        }

                        foreach (var resource in shader.ResourceParameters)
                        {
                            if (parametersIdentity.Contains(resource.Name) || resource.Name == "type.$Globals") continue;

                            parametersIdentity.Add(resource.Name);
                            var name = char.ToUpper(resource.Name[0]) + resource.Name.Substring(1);
                            effectParameters.Add(name);
                            //textGenerator.WriteLine($"{name} = Parameters[\"{resource.Name}\"];");
                        }
                    }
            
                    //result.EffectData.Save("BasicEffect.fx.compiled");
                    //var restored = EffectData.Load("BasicEffect.fx.compiled");
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
            var path = Path.Combine("CompilerEffects", "UIEffect.fx");
            if (File.Exists(path))
            {
                var result = EffectCompiler.CompileFromFile(path);

                var memoryStream = new MemoryStream();
                //MessagePackSerializer.DefaultOptions = MessagePack.Resolvers.ContractlessStandardResolver.Options;
                MessagePackSerializer.Serialize(memoryStream, result.EffectData);
                memoryStream.Position = 0;
                var results2 = MessagePackSerializer.Deserialize<EffectData>(memoryStream);
                Assert.NotNull(results2);
            }
        }
        
        [Test]
        public void EffectDeserializationTest()
        {
            var path = Path.Combine("CompilerEffects", "BasicEffect.fx");
            if (File.Exists(path))
            {
                var result = EffectCompiler.CompileFromFile(path);
                result.EffectData.Save("BasicEffect1");
                var restored = EffectData.Load("BasicEffect1");
            }
        }

        // Two headers. Tint.fxh is what the effect calls; it pulls in Bee.fxh, and THAT is what makes the compiler parse
        // Tint.fxh at all - an include's content is only handed to the include parser when it literally contains
        // "#include" (EffectCompilerInternal), and unlike the main source its comments are never stripped. So a header
        // that includes anything is exactly where a directive hiding in a comment gets read as code.
        private static ImmutableArray<ShaderFileInfo> TintInclude(string commentedLine = "") => ImmutableArray.Create(
            new ShaderFileInfo
            {
                FileName = "Tint.fxh",
                Path = Path.Combine("Includes", "Tint.fxh"),
                Content = "#include \"Includes/Bee.fxh\"\n" + commentedLine + "float4 Tint(float4 c) { return c * Bee(); }\n"
            },
            new ShaderFileInfo
            {
                FileName = "Bee.fxh",
                Path = Path.Combine("Includes", "Bee.fxh"),
                Content = "float Bee() { return 1; }\n"
            });

        private static string EffectCalling(string includeLine) =>
            includeLine + @"
float4 VS(float4 p : POSITION) : SV_Position { return p; }
float4 PS(float4 p : SV_Position) : SV_Target0 { return Tint(float4(1, 1, 1, 1)); }
technique T { pass P { Profile = 6.6; VertexShader = VS; PixelShader = PS; } }
";

        /// <summary>The baseline for the test below: a REAL #include is expanded, so the function it defines resolves.</summary>
        [Test]
        public void IncludeIsExpanded()
        {
            var result = EffectCompiler.Compile(EffectCalling("#include \"Includes/Tint.fxh\"\n"), "Probe.fx", TintInclude());

            var messages = string.Join(Environment.NewLine, result.Logger.Messages);
            Assert.That(result.HasErrors, Is.False, $"a real include did not resolve:{Environment.NewLine}{messages}");
        }

        /// <summary>A preprocessor directive written inside a COMMENT is not a directive. The tokenizer handed `/` to the
        /// divide rule before the comment rule could claim `//`, so comments were tokenised as ordinary code and every
        /// `#` in one was obeyed - this shape reports "Unsupported preprocessor token".
        /// <para>It guards more than it used to: the source reaching the parser was stripped of comments by regex first,
        /// which hid this everywhere except inside a header. That stripping was there for the DXC backend and went with
        /// it, so comments now reach the parser exactly as written.</para></summary>
        [Test]
        public void DirectiveInsideACommentIsNotObeyed()
        {
            var source = "// #banana - a directive that only a parser reading comments as code would ever see\n"
                       + EffectCalling("#include \"Includes/Tint.fxh\"\n");

            var result = EffectCompiler.Compile(source, "Probe.fx", TintInclude());

            var messages = string.Join(Environment.NewLine, result.Logger.Messages);
            Assert.That(messages, Does.Not.Contain("Unsupported preprocessor token"),
                $"a `#` inside a comment was taken for a directive:{Environment.NewLine}{messages}");
            Assert.That(result.HasErrors, Is.False, $"the effect did not compile:{Environment.NewLine}{messages}");
        }
    }
}