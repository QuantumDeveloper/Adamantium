﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public class DepthBuffer : Texture
    {
        public DepthBuffer(GraphicsDevice device, TextureDescription description) : base(device, description)
        {
        }
    }
}