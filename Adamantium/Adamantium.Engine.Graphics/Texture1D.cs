using System;
using System.Collections.Generic;
using System.Text;
using VulkanImage = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
    public class Texture1D : Texture
    {
        public static Texture1D New(GraphicsDevice device, VulkanImage image)
        {
            return new Texture1D();
        }

        protected override void Dispose(bool disposeManaged)
        {
            throw new NotImplementedException();
        }
    }
}
