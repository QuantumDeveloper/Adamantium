namespace Adamantium.UI.Controls;

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
            return;
        
        VerifyAccess();
        if (Renderer is not { FirstFrameProcessed: true })
        {
            WindowWorkerService.SetTitle(Title);
            ShouldDisplayWindow = true;
        }
        else
        {
            ShouldDisplayWindow = false;
            WindowWorkerService.ShowWindow(State);
        }
    }
        
    public override void Close()
    {
        IsClosed = true;
        OnClosed();
    }

    public override void Hide()
    {
        WindowWorkerService.HideWindow();
    }
}