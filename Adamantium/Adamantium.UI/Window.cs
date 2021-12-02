using System;
using Adamantium.Mathematics;
using Adamantium.UI.Rendering;

namespace Adamantium.UI
{
    public class Window : WindowBase
    {
        private IWindowRenderer renderer;
        
        public override IntPtr SurfaceHandle { get; internal set; }
        public override IntPtr Handle { get; internal set; }

        public Window()
        {
            
        }

        public override Vector2 PointToClient(Vector2 point)
        {
            return ScreenToClient(point);
        }

        public override Vector2 PointToScreen(Vector2 point)
        {
            return ClientToScreen(point);
        }
        
        public override void Show()
        {
            if (Handle == IntPtr.Zero)
            {
                VerifyAccess();
                WindowWorkerService.SetWindow(this);
            }
        }
        
        public override void Close()
        {
            IsClosed = true;
            OnClosed();
        }

        public override void Hide()
        {
            
        }

        public override bool IsActive { get; internal set; }

        internal void SetRenderer(IWindowRenderer renderer)
        {
            this.renderer = renderer;
            renderer.SetWindow(this);
        }
        
        public override void Render()
        {
            
        }

    }
}