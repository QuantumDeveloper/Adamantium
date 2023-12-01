using Adamantium.Fonts.TextureGeneration;
using Adamantium.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Engine.Graphics.Fonts
{
    public class FontAtlas : GraphicsResource
    {
        protected FontAtlasData AtlasData { get; }

        public FontAtlas(GraphicsDevice device, byte[] atlasDataBytes) : base(device)
        {

        }

        public Size MeasureString(string text)
        {
            return Size.Zero;
        }

        public void DrawString(string text, FontRenderingParameters parameters, RenderTarget renderTarget)
        {

        }
    }
}
