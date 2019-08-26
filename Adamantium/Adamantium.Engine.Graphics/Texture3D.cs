using System;
using System.Collections.Generic;
using System.Text;
using VulkanImage = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
    public class Texture3D : Texture
    {
        public static Texture3D New(GraphicsDevice device, VulkanImage image)
        {
            return new Texture3D();
        }

        protected override void Dispose(bool disposeManaged)
        {
            throw new NotImplementedException();
        }
    }
}
