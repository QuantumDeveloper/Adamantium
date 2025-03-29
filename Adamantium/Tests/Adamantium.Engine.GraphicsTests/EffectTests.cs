using NUnit.Framework;
using System.IO;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;

namespace Adamantium.Engine.GraphicsTests
{
    [TestFixture]
    public class EffectTests
    {
        [Test]
        public void EffectLoadingTest()
        {
            var main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "TestApp", true);
            var device = main.CreateRenderDevice();
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "FontEffect.fx"), device);
        }
    }
}
