using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Effects;

namespace Adamantium.Engine.GraphicsTests
{
    [TestFixture]
    public class EffectTests
    {
        [Test]
        public void EffectLoadingTest()
        {
            var main = MainGraphicsDevice.Create("TestApp", true);
            var device = main.CreateRenderDevice(new PresentationParameters(PresenterType.RenderTarget, 100, 100, IntPtr.Zero));
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "UIEffect.fx"), device);
        }
    }
}
