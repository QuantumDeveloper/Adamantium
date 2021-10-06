using System;
using Adamantium.Mathematics;

namespace Adamantium.UI
{
    public class Window : WindowBase
    {
        public override IntPtr SurfaceHandle { get; internal set; }
        public override IntPtr Handle { get; internal set; }

        public override Point PointToClient(Point point)
        {
            return ScreenToClient(point);
        }

        public override Point PointToScreen(Point point)
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
        
        public override void Render()
        {
            
        }

    }
}