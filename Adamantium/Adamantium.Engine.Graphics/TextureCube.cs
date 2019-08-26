using System;
using System.Collections.Generic;
using System.Text;
using VulkanImage = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
    public class TextureCube : Texture
    {
        public static TextureCube New(GraphicsDevice device, VulkanImage image)
        {
            return new TextureCube();
        }

        protected override void Dispose(bool disposeManaged)
        {
            throw new NotImplementedException();
        }
    }
}
