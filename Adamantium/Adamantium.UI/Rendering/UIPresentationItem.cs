using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering
{
    internal class UIPresentationItem : DisposableObject
    {
        public UIComponentRenderer GeometryRenderer { get; set; }
        
        public UIComponentRenderer StrokeRenderer { get; set; }
        
        public Matrix4x4F Transform { get; set; }
        
        public Texture Texture { get; set; }
    }
}