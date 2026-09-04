using System;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Drawings;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Content of the OverlayWindow demo, shown VM-first via IOverlayService (no UI is created in the view model).
/// The title is a settable, INPC property (via [Bindable]), so "Rename" changes it and the window's title bar updates
/// live. Left/Top are two-way bound to the window: they open it (StartupLocation.Manual), a drag updates them live, and
/// "Move (VM)" changes them from the view model to move the window - proving the position is bindable both ways.</summary>
[ViewModel]
public partial class OverlayDemoViewModel : AdamantiumViewModel, IOverlayAware
{
    private static int _cascade;   // successive windows step down-right from the view model instead of stacking
    private int _renames;

    [Bindable] private string title = "Overlay window";
    [Bindable] private string message = "Drag the title bar (X/Y update live), or move it from the view model.";
    [Bindable] private double left;
    [Bindable] private double top;

    public OverlayDemoViewModel()
    {
        // Set the backing fields BEFORE the window binds Left/Top, so a Manual window opens at this position (the binding's
        // initial source->target push, at bind time, is synchronous; later INPC changes are batched to the next frame).
        var n = _cascade++ % 6;
        left = 80 + n * 32;
        top = 80 + n * 32;
    }

    /// <summary>The card's title-bar icon: a small floating panel - a rounded frame with its own caption strip and a
    /// second sheet peeking out behind it, which is what this window IS. Drawn, not typed: it used to be the literal
    /// character "□", and a character is a box in every font that lacks it, so it read as a rendering fault.</summary>
    public object Icon => new Image
    {
        Width = 12,
        Height = 12,
        Source = new DrawingImage
        {
            Drawing = new GeometryDrawing
            {
                Geometry = new SVGParser().Parse(
                    "M4,1 L11,1 L11,8 L4,8 Z " +          // the sheet behind
                    "M1,4 L8,4 L8,11 L1,11 Z " +          // the card in front
                    "M1,4 L8,4 L8,6 L1,6 Z"),             // its caption strip
                Brush = Brushes.Gray
            }
        }
    };

    // Open at Left/Top (below) rather than centred, to show explicit positioning.
    public OverlayStartupLocation StartupLocation => OverlayStartupLocation.Manual;

    public void OnOverlayOpened(NavigationParameters parameters)
    {
        if (parameters != null && parameters.TryGetValue<string>("title", out var t)) Title = t;
    }

    public event Action<object> RequestClose;

    // Changes the title at runtime; the hosting OverlayWindow's bar reflects it live (Title is INPC).
    [Command] private void Rename() => Title = $"Renamed {++_renames}";

    // Moves the window FROM the view model - the two-way Left/Top binding drives the window.
    [Command] private void MoveFromVm() { Left += 40; Top += 30; }

    [Command] private void CloseSelf() => RequestClose?.Invoke("closed from view model");
}
