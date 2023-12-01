using Adamantium.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Engine.Graphics.Fonts
{
    public class FontRenderingParameters
    {
        public Rectangle TextArea { get; set; }

        public double FontSize { get; set; }

        public TextWrapping TextWrapping { get; set; }

        public TextAlignment TextAlignment { get; set; }
    }
}
