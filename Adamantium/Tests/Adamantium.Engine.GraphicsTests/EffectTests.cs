using NUnit.Framework;
using System;
using System.IO;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Presentation;

namespace Adamantium.Engine.GraphicsTests
{
    [TestFixture]
    public class EffectTests
    {
        [Test]
        public void EffectLoadingTest()
        {
            var main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(),"TestApp", true);
            var device = main.CreateRenderDevice(new PresentationParameters(PresenterType.RenderTarget, 100, 100, IntPtr.Zero));
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "FontEffect.fx"), device);
        }
    }
}
