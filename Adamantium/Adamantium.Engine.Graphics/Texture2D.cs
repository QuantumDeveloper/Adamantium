using System;
using System.Collections.Generic;
using System.Text;
using VulkanImage = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
    public class Texture2D : Texture
    {
        public Texture2D(GraphicsDevice device) : base(device)
        {
        }

        public static Texture2D New(GraphicsDevice device, TextureDescription description)
        {
            return new Texture2D(device);
        }

        protected override void Dispose(bool disposeManaged)
        {
            throw new NotImplementedException();
        }
    }
}
