using System;
using System.IO;
using Adamantium.UI.Controls;
using Adamantium.UI.Markup;
using Adamantium.UI.Rendering;

namespace Adamantium.UI;

public class Window : WindowBase
{
    public override IntPtr SurfaceHandle { get; internal set; }
    public override IntPtr Handle { get; internal set; }

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
            WindowWorkerService.SetTitle(Title);
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