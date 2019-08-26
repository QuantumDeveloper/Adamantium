using System;
using System.Collections.Generic;
using System.Text;
using VulkanImage = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
    public class Texture2D : Texture
    {
        public static Texture2D New(GraphicsDevice device, VulkanImage image)
        {
            return new Texture2D();
        }

        protected override void Dispose(bool disposeManaged)
        {
            throw new NotImplementedException();
        }
    }
}
