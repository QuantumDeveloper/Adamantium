using Adamantium.Core.Commands;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.Rendering;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Input;

/// <summary>
/// The in-window drag-drop engine (level 1) + its attached-property facade. Retrofit onto ANY element without touching it:
/// set <c>DragDrop.AllowDrag="True"</c> + <c>DragDrop.DragData</c> on a source, <c>DragDrop.AllowDrop="True"</c> +
/// <c>DragDrop.DropCommand</c> on a target. On press+threshold the source is baked to a ghost that follows the cursor (a
/// real layered OS window, <see cref="IDragGhost"/>); on release the target under the cursor gets the payload through its
/// <c>DropCommand</c> - MVVM-first, no UI types in the VM (docs/DRAG_DROP_PLAN.md). App-global, one drag at a time.
/// </summary>
public static class DragDrop
{
    // Pixels the pointer must travel before a press becomes a drag - a click is not a drag.
    private const double DragThreshold = 4.0;
    // Ghost sits down-right of the cursor so it doesn't hide it.
    private const int GhostCursorOffset = 12;

    // ------------------------------------------------------------------ attached properties
    public static readonly AdamantiumProperty AllowDragProperty = AdamantiumProperty.RegisterAttached(
        "AllowDrag", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false, OnAllowDragChanged));

    public static readonly AdamantiumProperty DragDataProperty = AdamantiumProperty.RegisterAttached(
        "DragData", typeof(object), typeof(AdamantiumComponent), new PropertyMetadata(null));

    public static readonly AdamantiumProperty AllowDropProperty = AdamantiumProperty.RegisterAttached(
        "AllowDrop", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false));

    public static readonly AdamantiumProperty DropCommandProperty = AdamantiumProperty.RegisterAttached(
        "DropCommand", typeof(ICommand), typeof(AdamantiumComponent), new PropertyMetadata(null));

    public static bool GetAllowDrag(AdamantiumComponent e) => e.GetValue<bool>(AllowDragProperty);
    public static void SetAllowDrag(AdamantiumComponent e, bool value) => e.SetValue(AllowDragProperty, value);

    public static object GetDragData(AdamantiumComponent e) => e.GetValue(DragDataProperty);
    public static void SetDragData(AdamantiumComponent e, object value) => e.SetValue(DragDataProperty, value);

    public static bool GetAllowDrop(AdamantiumComponent e) => e.GetValue<bool>(AllowDropProperty);
    public static void SetAllowDrop(AdamantiumComponent e, bool value) => e.SetValue(AllowDropProperty, value);

    public static ICommand GetDropCommand(AdamantiumComponent e) => e.GetValue(DropCommandProperty) as ICommand;
    public static void SetDropCommand(AdamantiumComponent e, ICommand value) => e.SetValue(DropCommandProperty, value);

    // ------------------------------------------------------------------ session state (one drag at a time, app-global)
    private static IInputComponent _source;      // the pressed source; armed until the threshold, then dragging
    private static Vector2 _startScreen;
    private static bool _dragging;
    private static IDataPackage _data;
    private static byte[] _ghostBgra;
    private static int _ghostW, _ghostH;
    private static bool _ghostShown;

    private static IVisualRenderer _renderer;
    private static IDragGhost _ghost;
    private static IVisualRenderer Renderer => _renderer ??= UIApplication.Current?.Container.Resolve<IVisualRenderer>();
    private static IDragGhost Ghost => _ghost ??= UIApplication.Current?.Container.Resolve<IDragGhost>();

    private static void OnAllowDragChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not IInputComponent input) return;
        input.PreviewMouseLeftButtonDown -= OnSourceDown;   // idempotent
        if (e.NewValue is true) input.PreviewMouseLeftButtonDown += OnSourceDown;
    }

    private static void OnSourceDown(object sender, MouseButtonEventArgs e)
    {
        if (_dragging || sender is not IInputComponent input) return;
        _source = input;
        _startScreen = Mouse.ScreenCoordinates;
        // Track move/up only while a press is live - added here, removed on up. Move keeps arriving after capture.
        input.MouseMove += OnMove;
        input.MouseLeftButtonUp += OnUp;
    }

    private static void OnMove(object sender, MouseEventArgs e)
    {
        if (_source == null) return;
        if (!_dragging)
        {
            if ((Mouse.ScreenCoordinates - _startScreen).Length() < DragThreshold) return;
            BeginDrag();
        }
        ShowOrMoveGhost();
    }

    private static void BeginDrag()
    {
        _dragging = true;
        _data = ResolveData((AdamantiumComponent)_source);
        Mouse.Capture(_source);   // so move/up keep coming once the cursor leaves the source
        // Bake the source to a bitmap OFF the render thread (queued); the ghost appears when it arrives (a frame later).
        if (_source is IUIComponent src) Renderer?.RequestSnapshot(src, OnGhostReady);
    }

    private static void OnGhostReady(ImageSource img)
    {
        if (!_dragging || img is not BitmapSource bs) return;
        _ghostBgra = DragGhostPixels.ToPremultipliedBgra(bs);
        _ghostW = (int)bs.PixelWidth;
        _ghostH = (int)bs.PixelHeight;
        ShowOrMoveGhost();
    }

    private static void ShowOrMoveGhost()
    {
        if (_ghostBgra == null) return;
        var s = Mouse.ScreenCoordinates;
        int x = (int)s.X + GhostCursorOffset, y = (int)s.Y + GhostCursorOffset;
        if (!_ghostShown)
        {
            Ghost?.Show(_ghostBgra, _ghostW, _ghostH, x, y);
            _ghostShown = true;
        }
        else
        {
            Ghost?.Move(x, y);
        }
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        var source = _source;
        var wasDragging = _dragging;
        if (source != null)
        {
            source.MouseMove -= OnMove;
            source.MouseLeftButtonUp -= OnUp;
        }
        if (wasDragging) CompleteDrop();
        Reset();
    }

    private static void CompleteDrop()
    {
        Ghost?.Hide();
        var target = HitTestDropTarget(out var positionInTarget);
        if (target == null) return;

        var command = GetDropCommand((AdamantiumComponent)target);
        if (command == null) return;

        var args = new DragDropEventArgs(_data, _source, positionInTarget);
        if (command.CanExecute(args)) command.Execute(args);
    }

    // The nearest AllowDrop ancestor of whatever is under the cursor (in the source's window). HitTest ignores capture, so
    // it finds the real element under the pointer even though the source holds mouse capture during the drag.
    private static IUIComponent HitTestDropTarget(out Vector2 positionInTarget)
    {
        positionInTarget = default;
        if (_source is not IUIComponent src || src.RootVisual is not IUIComponent root) return null;

        var pointInRoot = Mouse.GetPosition((IInputComponent)root);
        var hit = root.HitTest(pointInRoot) as IUIComponent;
        for (var c = hit; c != null; c = c.VisualParent)
        {
            if (GetAllowDrop((AdamantiumComponent)c))
            {
                positionInTarget = Mouse.GetPosition((IInputComponent)c);
                return c;
            }
        }
        return null;
    }

    private static void Reset()
    {
        if (_dragging && ReferenceEquals(Mouse.Captured, _source)) Mouse.Capture(null);
        _source = null;
        _data = null;
        _dragging = false;
        _ghostBgra = null;
        _ghostShown = false;
    }

    private static IDataPackage ResolveData(AdamantiumComponent source)
    {
        var d = GetDragData(source);
        return d as IDataPackage ?? new DataPackage(d);
    }
}
