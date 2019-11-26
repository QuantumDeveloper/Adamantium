using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Engine.Effects;
using System.IO;
using Adamantium.Engine.Graphics;

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
