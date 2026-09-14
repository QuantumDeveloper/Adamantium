using System;
using System.Collections.Generic;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>A button per tool of an <see cref="InfiniteCanvas"/>, built from the canvas's own
/// <see cref="InfiniteCanvas.Tools"/>: the picture, the name and the key all come off the tool, and the button that is
/// marked is the one holding <see cref="InfiniteCanvas.Tool"/>.
/// <para>Built here rather than written out in markup, and that is the whole saving. Every fact a button needs is a
/// fact about the TOOL, so a hand-written row of buttons is a second copy of the tool list that has to be kept in step
/// by hand - which is how adding one tool turned into six edits, five of them about showing it.</para>
/// <para>An application that wants something else puts its own content in the pane: this is what a rail does when
/// nobody says otherwise, not the only thing a rail may be.</para></summary>
/// <para>A WRAPPING panel and not a stack: how many tools there are is the application's business, and a rail taller
/// than the viewport is one whose last tools cannot be reached. Too many for one column become a second, which is what
/// the hand-written panel did by splitting the buttons into rows - except that this one does it at whatever size the
/// canvas happens to be.</para>
public class CanvasToolRail : WrapPanel
{
    private readonly List<ToggleButton> _buttons = new();
    private readonly Dictionary<ToggleButton, ICanvasTool> _of = new();

    private InfiniteCanvas _canvas;

    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasToolRail), new PropertyMetadata(null, OnCanvasChanged));

    // TWO numbers and not one Size: a Size has no type parser, so a theme stating one as "30,28" throws while the
    // template is being built - and a template that throws is a theme that does not apply, which shows up as the
    // control having no panel at all rather than as a wrong number.
    public static readonly AdamantiumProperty ButtonWidthProperty = AdamantiumProperty.Register(nameof(ButtonWidth),
        typeof(Double), typeof(CanvasToolRail),
        new PropertyMetadata(30.0, PropertyMetadataOptions.AffectsMeasure, OnLookChanged));

    public static readonly AdamantiumProperty ButtonHeightProperty = AdamantiumProperty.Register(nameof(ButtonHeight),
        typeof(Double), typeof(CanvasToolRail),
        new PropertyMetadata(28.0, PropertyMetadataOptions.AffectsMeasure, OnLookChanged));

    public static readonly AdamantiumProperty IconSizeProperty = AdamantiumProperty.Register(nameof(IconSize),
        typeof(Double), typeof(CanvasToolRail),
        new PropertyMetadata(13.0, PropertyMetadataOptions.AffectsMeasure, OnLookChanged));

    public static readonly AdamantiumProperty ButtonGapProperty = AdamantiumProperty.Register(nameof(ButtonGap),
        typeof(Double), typeof(CanvasToolRail),
        new PropertyMetadata(4.0, PropertyMetadataOptions.AffectsMeasure, OnLookChanged));

    /// <summary>The canvas whose tools these are. Set by the pane the rail sits in, which got it from the layer.
    /// </summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    /// <summary>How wide one tool button is.</summary>
    public Double ButtonWidth
    {
        get => GetValue<Double>(ButtonWidthProperty);
        set => SetValue(ButtonWidthProperty, value);
    }

    /// <summary>How tall one tool button is.</summary>
    public Double ButtonHeight
    {
        get => GetValue<Double>(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }

    /// <summary>How big the picture inside it is.</summary>
    public Double IconSize
    {
        get => GetValue<Double>(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>The space between two buttons.</summary>
    public Double ButtonGap
    {
        get => GetValue<Double>(ButtonGapProperty);
        set => SetValue(ButtonGapProperty, value);
    }

    private static void OnCanvasChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasToolRail rail) return;

        // Unhooked from the OLD one and not from whatever is current: the canvas is not the rail's to own, and a rail
        // that let go of nothing would go on rebuilding itself for a canvas that has moved on.
        if (rail._canvas != null)
        {
            rail._canvas.ToolsChanged -= rail.OnToolsChanged;
            rail._canvas.ToolChanged -= rail.OnToolChanged;
        }

        rail._canvas = e.NewValue as InfiniteCanvas;

        if (rail._canvas != null)
        {
            rail._canvas.ToolsChanged += rail.OnToolsChanged;
            rail._canvas.ToolChanged += rail.OnToolChanged;
        }

        rail.Rebuild();
    }

    private static void OnLookChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as CanvasToolRail)?.Rebuild();

    private void OnToolsChanged(object sender, EventArgs e) => Rebuild();

    private void OnToolChanged(object sender, EventArgs e) => MarkCurrent();

    private void Rebuild()
    {
        foreach (var button in _buttons) button.Click -= OnToolPicked;

        _buttons.Clear();
        _of.Clear();
        Children.Clear();

        if (_canvas?.Tools is not { } tools) return;

        var gap = ButtonGap;
        foreach (var tool in tools)
        {
            if (tool == null) continue;

            var iconic = !string.IsNullOrEmpty(tool.Icon);

            var button = new ToggleButton
            {
                // A tool with a PICTURE gets the stated square; one without shows its name and has to be as wide as
                // that name is - forced to the square, "Button" came out cut in half.
                Width = iconic ? ButtonWidth : Double.NaN,
                Height = ButtonHeight,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(iconic ? 0 : 8, 0, iconic ? 0 : 8, 0),
                ToolTip = Tip(tool)
            };

            // On the TRAILING sides only. Two neighbours still make exactly one gap between them, whichever way the
            // rail runs and wherever it wraps - but the first button's left and top edges stay flush, so the column of
            // tools lines up with the pane's own handles above it. Half a gap on every side put the tools two pixels
            // right of the handles, which reads as crooked and is.
            button.Margin = new Thickness(0, 0, gap, gap);

            if (iconic)
            {
                var image = new Image { Width = IconSize, Height = IconSize };

                // LIVE and not a one-shot lookup: a theme swap has to reach the rail's pictures like it reaches
                // everything else, and the tool names a key precisely so the theme can answer differently.
                new ObservableResource(tool.Icon).Apply(image, nameof(Image.Source));
                button.Content = image;
            }
            else
            {
                button.Content = tool.Name;
            }

            button.Click += OnToolPicked;

            _buttons.Add(button);
            _of[button] = tool;
            Children.Add(button);
        }

        MarkCurrent();
    }

    private void MarkCurrent()
    {
        var current = _canvas?.Tool;

        foreach (var button in _buttons)
        {
            var wanted = _of.TryGetValue(button, out var tool) && ReferenceEquals(tool, current);
            if (button.IsChecked != wanted) button.IsChecked = wanted;
        }
    }

    private void OnToolPicked(object sender, RoutedEventArgs e)
    {
        if (_canvas == null || sender is not ToggleButton button || !_of.TryGetValue(button, out var tool)) return;

        _canvas.SetCurrentValue(InfiniteCanvas.ToolProperty, tool);

        // Marked from the CANVAS's answer and not from the press: the tool that ends up in hand is the canvas's to say,
        // and a button that ticked itself would lie the moment anything refused.
        MarkCurrent();
        e.Handled = true;
    }

    private static string Tip(ICanvasTool tool)
    {
        var name = string.IsNullOrEmpty(tool.Name) ? tool.GetType().Name : tool.Name;
        var key = tool.Shortcut == Key.None ? string.Empty : $" ({tool.Shortcut})";
        var about = string.IsNullOrEmpty(tool.Description) ? string.Empty : $" - {tool.Description}";

        return name + key + about;
    }
}
