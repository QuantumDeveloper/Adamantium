using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;

namespace Adamantium.UI.Rendering
{
    internal class UIPresentationItem : DisposableObject
    {
        private UIComponentRenderer geometryRenderer;
        private UIComponentRenderer strokeRenderer;

        public UIComponentRenderer GeometryRenderer
        {
            get => geometryRenderer;
            set => geometryRenderer = ToDispose(value);
        }

        public UIComponentRenderer StrokeRenderer
        {
            get => strokeRenderer;
            set => strokeRenderer = ToDispose(value);
        }

        public Matrix4x4F Transform { get; set; }
        
        public Texture Texture { get; set; }

        protected override void Dispose(bool disposeManagedResources)
        {
            base.Dispose(disposeManagedResources);
            
            GeometryRenderer?.Dispose();
            StrokeRenderer?.Dispose();
        }
    }
}