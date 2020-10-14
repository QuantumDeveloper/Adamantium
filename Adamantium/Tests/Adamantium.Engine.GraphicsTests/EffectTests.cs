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
            var instance = VulkanInstance.Create("TestApp", true);
            var device = GraphicsDevice.Create(instance, instance.CurrentDevice);
            var effect = Effect.CompileFromFile(Path.Combine("EffectsData", "UIEffect.fx"), device);
        }
    }
}
