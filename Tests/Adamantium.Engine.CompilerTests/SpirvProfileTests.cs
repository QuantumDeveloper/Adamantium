using System;
using System.Linq;
using Adamantium.EffectsCompiler;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    /// <summary>
    /// A pass's <c>Profile</c> is the SPIR-V version its shaders are compiled to. It used to be parsed and dropped -
    /// 163 declarations across 21 files that reached nothing but a field nobody read, with neighbouring passes carrying
    /// different numbers as though it mattered. These tests hold it to being real: the number in the source has to come
    /// back out of the SPIR-V header, and a number this compiler cannot target has to be an ERROR rather than a
    /// silently ignored one.
    /// </summary>
    [TestFixture]
    public class SpirvProfileTests
    {
        private const string Source = """
            float4 Tint;

            float4 VS(in float4 position : POSITION) : SV_POSITION
            {
                return position;
            }

            float4 PS(in float4 position : SV_POSITION) : SV_Target0
            {
                return Tint;
            }

            technique Only
            {
                pass P0
                {
            {0}        VertexShader = VS;
                    PixelShader = PS;
                }
            }
            """;

        // A real directory, not a bare name: the compiler enumerates the source's folder looking for includes.
        private static string SourcePath =>
            System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "ProfileTest.fx");

        private static string WithProfile(string profileLine) => Source.Replace("{0}", profileLine);

        // Word 1 of a SPIR-V module is its version: major in the third byte, minor in the second.
        private static (int Major, int Minor) SpirvVersion(byte[] bytecode)
        {
            Assert.That(bytecode, Is.Not.Null.And.Length.GreaterThan(8), "no SPIR-V came back");
            Assert.That(BitConverter.ToUInt32(bytecode, 0), Is.EqualTo(0x07230203u), "not a SPIR-V module");

            var version = BitConverter.ToUInt32(bytecode, 4);
            return ((int)((version >> 16) & 0xFF), (int)((version >> 8) & 0xFF));
        }

        private static EffectData CompileOrFail(string source)
        {
            var result = EffectCompiler.Compile(source, SourcePath);
            var messages = string.Join(Environment.NewLine, result.Logger.Messages);
            Assert.That(result.HasErrors, Is.False, $"failed to compile:{Environment.NewLine}{messages}");
            return result.EffectData;
        }

        private static byte[] FirstShader(EffectData data) =>
            data.Shaders.First(s => s.Bytecode is { Length: > 0 }).Bytecode;

        /// <summary>A pass that names no profile takes the newest the compiler has.</summary>
        [Test]
        public void APassWithNoProfile_TakesTheNewest()
        {
            var data = CompileOrFail(WithProfile(string.Empty));

            Assert.That(SpirvVersion(FirstShader(data)), Is.EqualTo((1, 6)),
                "the default has to be the newest SPIR-V this compiler targets");
        }

        /// <summary>...and one that names a profile is compiled to THAT version, which is the whole point.</summary>
        [Test]
        public void APassThatNamesAProfile_IsCompiledToIt()
        {
            var data = CompileOrFail(WithProfile("            Profile = 1.3;" + Environment.NewLine));

            Assert.That(SpirvVersion(FirstShader(data)), Is.EqualTo((1, 3)),
                "the version in the source never reached the SPIR-V - the attribute is decoration again");
        }

        /// <summary>EVERY version the compiler offers, not one sample: the claim is that the number in the source is the
        /// one applied, and a single case cannot tell that from a coincidence.</summary>
        [TestCase(1, 0)]
        [TestCase(1, 1)]
        [TestCase(1, 2)]
        [TestCase(1, 3)]
        [TestCase(1, 4)]
        [TestCase(1, 5)]
        [TestCase(1, 6)]
        public void EveryOfferedProfile_IsTheOneApplied(int major, int minor)
        {
            var data = CompileOrFail(WithProfile($"            Profile = {major}.{minor};" + Environment.NewLine));

            Assert.That(SpirvVersion(FirstShader(data)), Is.EqualTo((major, minor)));
        }

        /// <summary>...and the profile reaches the CODE GENERATOR, not just a header field the compiler stamps: the
        /// oldest and the newest have to produce different modules.</summary>
        [Test]
        public void TheOldestAndNewestProfiles_ProduceDifferentModules()
        {
            var oldest = FirstShader(CompileOrFail(WithProfile("            Profile = 1.0;" + Environment.NewLine)));
            var newest = FirstShader(CompileOrFail(WithProfile("            Profile = 1.6;" + Environment.NewLine)));

            Assert.That(oldest, Is.Not.EqualTo(newest), "the profile changed nothing but the version word");
        }

        /// <summary>The record says which one it was compiled at, rather than a shader model nobody reads.</summary>
        [Test]
        public void TheProfileIsRecordedOnTheShader()
        {
            var data = CompileOrFail(WithProfile("            Profile = 1.3;" + Environment.NewLine));

            Assert.That(data.Shaders.First(s => s.Bytecode is { Length: > 0 }).Level, Is.EqualTo("spirv_1_3"));
        }

        /// <summary>A value this compiler cannot target is an ERROR, in each of the shapes a wrong one comes in: an old
        /// SHADER MODEL that reads like a version (5.1, 6.6 - what all 163 removed declarations said), a SPIR-V version
        /// that does not exist (1.7, the edge above the newest), and something that is not a number at all. Falling back
        /// on any of them would put us back where we started: a number in the source that means nothing.</summary>
        [TestCase("5.1", TestName = "AWrongProfile_IsAnError(a shader model)")]
        [TestCase("6.6", TestName = "AWrongProfile_IsAnError(the other shader model)")]
        [TestCase("1.7", TestName = "AWrongProfile_IsAnError(past the newest)")]
        [TestCase("0.9", TestName = "AWrongProfile_IsAnError(below the oldest)")]
        [TestCase("fx_5_0", TestName = "AWrongProfile_IsAnError(not a number)")]
        public void AWrongProfile_IsAnError(string value)
        {
            var result = EffectCompiler.Compile(WithProfile($"            Profile = {value};" + Environment.NewLine),
                SourcePath);

            Assert.That(result.HasErrors, Is.True, $"[{value}] is not a SPIR-V version this compiler targets");
            Assert.That(string.Join(Environment.NewLine, result.Logger.Messages), Does.Contain("Profile"),
                "the error has to name the attribute it is about");
        }
    }
}
